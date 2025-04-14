using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using OceanMonitoringSystem.Common;
using System.Text.Json;
using Models;

class Aggregator
{
    private static readonly string WavyDataFilePath = "WavyData.json";
    private static readonly Mutex WavyDataMutex = new();
    // Use a timeout for mutex acquisition to prevent deadlocks
    private static readonly int MutexTimeout = 5000; // 5 seconds timeout

    private static void SaveWavyDataToFile(AggregatorSensorData data)
    {
        try
        {
            bool mutexAcquired = false;
            try
            {
                // Try to acquire mutex with timeout
                mutexAcquired = WavyDataMutex.WaitOne(MutexTimeout);
                if (!mutexAcquired)
                {
                    Console.WriteLine("Failed to acquire mutex while saving WavyData. Skipping this operation.");
                    return;
                }

                List<AggregatorSensorData> existingData = new();

                // Load existing data from the file
                if (File.Exists(WavyDataFilePath))
                {
                    string jsonData = File.ReadAllText(WavyDataFilePath);
                    existingData = JsonSerializer.Deserialize<List<AggregatorSensorData>>(jsonData) ?? new List<AggregatorSensorData>();
                }

                // Add the new data
                existingData.Add(data);

                // Serialize and save back to the file
                string updatedJsonData = JsonSerializer.Serialize(existingData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(WavyDataFilePath, updatedJsonData);
            }
            finally
            {
                if (mutexAcquired)
                {
                    WavyDataMutex.ReleaseMutex();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error saving WavyData to file: " + ex.Message);
        }
    }

    private static async Task PeriodicDataSendAsync(int interval)
    {
        while (true)
        {
            await Task.Delay(interval);

            AggregatorSensorData[] data = null;
            bool mutexAcquired = false;
            
            try
            {
                // Try to acquire mutex with timeout
                mutexAcquired = WavyDataMutex.WaitOne(MutexTimeout);
                if (!mutexAcquired)
                {
                    Console.WriteLine("Failed to acquire mutex during periodic data send. Will try again later.");
                    continue;
                }

                data = GetWavyDataFromFile();

                if (data.Length > 0)
                {
                    string aggregatedPayload = JsonSerializer.Serialize(data);
                    
                    // Release mutex before network operations to prevent long holding times
                    WavyDataMutex.ReleaseMutex();
                    mutexAcquired = false;
                    
                    try
                    {
                        await SendAggregatedDataToServer(aggregatedPayload);
                        
                        // Re-acquire mutex to clean data after successful send
                        mutexAcquired = WavyDataMutex.WaitOne(MutexTimeout);
                        if (mutexAcquired)
                        {
                            CleanWavyData();
                        }
                        else
                        {
                            Console.WriteLine("Failed to acquire mutex while cleaning WavyData. Will try in next cycle.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error sending data to server: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in periodic data send: " + ex.Message);
            }
            finally
            {
                if (mutexAcquired)
                {
                    WavyDataMutex.ReleaseMutex();
                }
            }
        }
    }
    
    private static NetworkStream serverStream;
    private static TcpClient serverClient;
    private static string aggregatorId = string.Empty;

    public static async Task Main()
    {
        Console.Write("Enter Aggregator ID: ");
        aggregatorId = Console.ReadLine();

        Console.Write("Enter Aggregator Port: ");
        if (!int.TryParse(Console.ReadLine(), out int aggregatorPort))
        {
            Console.WriteLine("Invalid port. Exiting...");
            return;
        }

        string serverIp = "127.0.0.1";
        int serverPort = 8080;

        // Aggregator settings
        int maxBytes = 2048;
        
        int currentBytes = 0;
        bool mutexAcquired = false;
        
        try
        {
            // Try to acquire mutex with timeout
            mutexAcquired = WavyDataMutex.WaitOne(MutexTimeout);
            if (mutexAcquired)
            {
                currentBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(GetWavyDataFromFile()));
            }
            else
            {
                Console.WriteLine("Failed to acquire mutex during startup. Using default value for currentBytes.");
            }
        }
        finally
        {
            if (mutexAcquired)
            {
                WavyDataMutex.ReleaseMutex();
            }
        }

        if (currentBytes > maxBytes)
        {
            Console.WriteLine("Data size exceeds maximum limit. Exiting...");
            return;
        }
        
        int sendInterval = 10000; // 1 second - time to send data to server

        // Connect to server
        bool connected = await ConnectToServerAsync(serverIp, serverPort);

        if (!connected)
        {
            Console.WriteLine("Failed to connect to server. Exiting...");
            return;
        }

        // Start periodic data send to server
        _ = Task.Run(() => PeriodicDataSendAsync(sendInterval));

        // Start listening for wavys
        TcpListener wavyListener = new TcpListener(IPAddress.Parse("127.0.0.1"), aggregatorPort);
        wavyListener.Start();
        Console.WriteLine($"Aggregator {aggregatorId} listening for wavys on port {aggregatorPort}...");

        while (true)
        {
            TcpClient wavyClient = await wavyListener.AcceptTcpClientAsync();
            Console.WriteLine("Wavy connected.");
            _ = Task.Run(() => HandleWavyAsync(wavyClient));
        }
    }

    private static async Task<bool> ConnectToServerAsync(string serverIp, int serverPort)
    {
        int maxRetries = 3;
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                // Connect to the main server
                serverClient = new TcpClient();
                await serverClient.ConnectAsync(IPAddress.Parse(serverIp), serverPort);
                serverStream = serverClient.GetStream();
                Console.WriteLine("Connected to server.");

                // Send connection request
                string connReqMessage = Protocol.CreateMessage(Protocol.CONN_REQ, aggregatorId);
                byte[] connReqBytes = Encoding.ASCII.GetBytes(connReqMessage);
                await serverStream.WriteAsync(connReqBytes, 0, connReqBytes.Length);
                Console.WriteLine("Connection request sent to server.");

                // Wait for connection acknowledgment
                byte[] ackBuffer = new byte[1024];
                int bytesRead = await serverStream.ReadAsync(ackBuffer, 0, ackBuffer.Length);
                string ackMessage = Encoding.ASCII.GetString(ackBuffer, 0, bytesRead);

                if (ackMessage.Contains(Protocol.CONN_ACK))
                {
                    Console.WriteLine("Connection acknowledgment received from server.");
                    return true;
                }
                else
                {
                    Console.WriteLine("Connection acknowledgment not received. Retrying...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to server: {ex.Message}. Retrying...");
            }

            retryCount++;
            await Task.Delay(2000); // Wait 2 seconds before retrying
        }

        Console.WriteLine("Failed to connect to server after 3 attempts.");
        return false;
    }

    private static async Task HandleWavyAsync(TcpClient wavyClient)
    {
        string wavyId = string.Empty;

        try
        {
            using NetworkStream stream = wavyClient.GetStream();
            byte[] buffer = new byte[1024];
            string data;

            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Console.WriteLine("Received from wavy: " + data);

                if (Protocol.IsValidMessage(data))
                {
                    var (type, payload) = Protocol.ParseMessage(data);

                    switch (type)
                    {
                        case Protocol.DATA_SEND:
                            // Payload -> DATA as json
                            string dataReceived = payload;
                            Console.WriteLine("Data received: " + dataReceived);

                            // Parse the JSON data
                            var sensorDataList = JsonSerializer.Deserialize<List<DataWavy>>(dataReceived);
                            Console.WriteLine(sensorDataList.Count + " data types received.");

                            bool processingSuccessful = true;
                            string responseMessage = "Data received";
                            
                            if (sensorDataList != null)
                            {
                                foreach (var sensorData in sensorDataList)
                                {
                                    if (string.IsNullOrEmpty(sensorData.dataType) || string.IsNullOrEmpty(sensorData.value))
                                    {
                                        Console.WriteLine("Data type or value is empty. Not saving to file.");
                                        processingSuccessful = false;
                                        responseMessage = "Invalid data received";
                                        continue;
                                    }

                                    try
                                    {
                                        // Save data to CSV - doesn't need mutex lock
                                        var csvHelper = new CsvHelper();
                                        csvHelper.SaveData(wavyId, sensorData.dataType, sensorData.value);

                                        // Create data object before acquiring mutex
                                        var dataWithId = new AggregatorSensorData
                                        {
                                            DataType = sensorData.dataType,
                                            RawValue = sensorData.value,
                                            WavyId = wavyId,
                                            AggregatorId = aggregatorId,
                                        };

                                        // Save data to JSON file - needs mutex lock
                                        SaveWavyDataToFile(dataWithId);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Error processing sensor data: {ex.Message}");
                                        processingSuccessful = false;
                                        responseMessage = "Error while saving data";
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("Failed to parse sensor data.");
                                processingSuccessful = false;
                                responseMessage = "Failed to parse data";
                            }

                            await SendMessageToWavy(stream, Protocol.DATA_ACK, responseMessage);
                            Console.WriteLine($"Sent acknowledgment to wavy: {responseMessage}");
                            break;

                        case Protocol.CONN_REQ:
                            // Save to file wavy.csv WAVY_ID:status:[data_types]:last_sync
                            // Payload is expected to be the WAVY_ID or WAVY_ID|[data_types]
                            wavyId = payload;
                            string status = WavyStatus.ACTIVE; // Example status
                            string dataTypes = ""; // Example data types
                            string lastSync = DateTime.UtcNow.ToString("o"); // ISO 8601 format

                            // Example payload: "WAVY_123|[temperature, humidity]"
                            string[] parts = payload.Split('|');
                            if (parts.Length == 2)
                            {
                                wavyId = parts[0];
                                dataTypes = parts[1];
                                Console.WriteLine($"Wavy ID: {wavyId}, Data Types: {dataTypes}");
                            }
                            else
                            {
                                wavyId = payload;
                                Console.WriteLine($"Wavy ID: {wavyId}");
                            }

                            string csvFilePath = "wavy.csv";
                            bool wavyIdExists = false;
                            List<string> csvLines = new List<string>();

                            if (File.Exists(csvFilePath))
                            {
                                csvLines = File.ReadAllLines(csvFilePath).ToList();
                                for (int i = 0; i < csvLines.Count; i++)
                                {
                                    var columns = csvLines[i].Split(',');
                                    if (columns[0] == wavyId)
                                    {
                                        columns[1] = WavyStatus.ACTIVE; // Update status to active
                                        columns[2] = dataTypes; // Update data types
                                        csvLines[i] = string.Join(",", columns);
                                        wavyIdExists = true;
                                        break;
                                    }
                                }
                            }

                            if (!wavyIdExists)
                            {
                                string newLine = $"{wavyId},{status},{dataTypes},{lastSync}";
                                csvLines.Add(newLine);
                                Console.WriteLine("Added new wavy to CSV: " + newLine);
                            }
                            else
                            {
                                Console.WriteLine("Updated existing wavy in CSV: " + wavyId);
                            }

                            File.WriteAllLines(csvFilePath, csvLines);

                            await SendMessageToWavy(stream, Protocol.CONN_ACK, "Connection acknowledged");
                            Console.WriteLine("Sent connection acknowledgment to wavy.");
                            break;

                        case Protocol.DISC_REQ:
                            // Update the status in the CSV to inactive
                            CsvHelper.UpdateWavyStatus(payload, WavyStatus.INACTIVE);

                            await SendMessageToWavy(stream, Protocol.DISC_ACK, "Disconnection acknowledged");
                            Console.WriteLine("Sent disconnection acknowledgment to wavy.");
                            break; // Exit the loop on disconnection request
                            
                        case Protocol.MAINTENANCE_STATE:
                            // Update the status in the CSV to maintenance
                            CsvHelper.UpdateWavyStatus(payload, WavyStatus.MAINTENANCE);
                            await SendMessageToWavy(stream, Protocol.STATUS_ACK, "Maintenance state acknowledged");
                            Console.WriteLine("Sent maintenance state acknowledgment to wavy.");
                            break;
                            
                        default:
                            Console.WriteLine("Unknown message type received.");
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error handling wavy: " + ex.Message);
            if (!string.IsNullOrEmpty(wavyId))
            {
                CsvHelper.UpdateWavyStatus(wavyId, WavyStatus.INACTIVE);
                Console.WriteLine($"Wavy with id {wavyId} disconnected!");
            }
        }
        finally
        {
            wavyClient.Close();
        }
    }

    private static async Task SendAggregatedDataToServer(string aggregatedPayload)
    {
        while (true) // Keep trying to send data until successful
        {
            try
            {
                if (serverStream == null || !serverClient.Connected)
                {
                    Console.WriteLine("Server connection lost. Attempting to reconnect...");
                    bool reconnected = await ReconnectToServerAsync();
                    if (!reconnected)
                    {
                        Console.WriteLine("Reconnection failed. Retrying in 5 seconds...");
                        await Task.Delay(5000); // Wait before retrying
                        continue;
                    }
                }

                string message = Protocol.CreateMessage(Protocol.AGG_DATA_SEND, aggregatedPayload);
                byte[] msgBytes = Encoding.ASCII.GetBytes(message);

                await serverStream.WriteAsync(msgBytes, 0, msgBytes.Length);
                Console.WriteLine("Sent to server: " + aggregatedPayload);

                // Wait for acknowledgment from the server
                byte[] ackBuffer = new byte[1024];
                int bytesRead = await serverStream.ReadAsync(ackBuffer, 0, ackBuffer.Length);
                string ackMessage = Encoding.ASCII.GetString(ackBuffer, 0, bytesRead);

                if (ackMessage.Contains("ACK"))
                {
                    Console.WriteLine("Acknowledgment received from server. Clearing Data.");
                    break; // Exit the loop after successful send
                }
                else
                {
                    Console.WriteLine("Acknowledgment not received. Retrying...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to send to server: " + ex.Message);
                Console.WriteLine("Retrying in 5 seconds...");
                await Task.Delay(5000); // Wait before retrying
            }
        }
    }

    private static async Task SendMessageToWavy(NetworkStream stream, string messageType, string payload = "")
    {
        string message = Protocol.CreateMessage(messageType, payload);
        byte[] messageBytes = Encoding.ASCII.GetBytes(message);
        await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
        Console.WriteLine($"Sent to wavy: {messageType} - {payload}");
    }
    
    private static AggregatorSensorData[] GetWavyDataFromFile()
    {
        try
        {
            if (File.Exists(WavyDataFilePath))
            {
                string jsonData = File.ReadAllText(WavyDataFilePath);
                return JsonSerializer.Deserialize<AggregatorSensorData[]>(jsonData) ?? Array.Empty<AggregatorSensorData>();
            }
            return Array.Empty<AggregatorSensorData>();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading WavyData file: " + ex.Message);
            return Array.Empty<AggregatorSensorData>();
        }      
    }

    // Only call when having the mutex locked
    private static void CleanWavyData()
    {
        try
        {
            if (File.Exists(WavyDataFilePath))
            {
                File.Delete(WavyDataFilePath);
                Console.WriteLine("WavyData file deleted successfully.");
            }
            else
            {
                Console.WriteLine("WavyData file does not exist.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cleaning WavyData file: {ex.Message}");
        }
    }

    private static async Task<bool> ReconnectToServerAsync()
    {
        int maxRetries = 5;
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                serverClient?.Close(); // Close the previous connection if it exists
                serverClient = new TcpClient();
                await serverClient.ConnectAsync("127.0.0.1", 8080); // Replace with your server IP and port
                serverStream = serverClient.GetStream();
                Console.WriteLine("Reconnected to server.");

                // Send connection request again
                string connReqMessage = Protocol.CreateMessage(Protocol.CONN_REQ, aggregatorId);
                byte[] connReqBytes = Encoding.ASCII.GetBytes(connReqMessage);
                await serverStream.WriteAsync(connReqBytes, 0, connReqBytes.Length);
                Console.WriteLine("Connection request sent to server.");

                // Wait for acknowledgment
                byte[] ackBuffer = new byte[1024];
                int bytesRead = await serverStream.ReadAsync(ackBuffer, 0, ackBuffer.Length);
                string ackMessage = Encoding.ASCII.GetString(ackBuffer, 0, bytesRead);

                if (ackMessage.Contains(Protocol.CONN_ACK))
                {
                    Console.WriteLine("Reconnection acknowledgment received from server.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Reconnection attempt {retryCount + 1} failed: {ex.Message}");
            }

            retryCount++;
            await Task.Delay(5000); // Wait before retrying
        }

        Console.WriteLine("Failed to reconnect to server after multiple attempts.");
        return false;
    }
}