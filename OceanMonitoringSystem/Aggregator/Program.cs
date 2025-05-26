using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using OceanMonitoringSystem.Common;
using System.Text.Json;
using Models;
using Grpc.Net.Client;
using GrpcDataParser;
using OceanMonitoringSystem.Common.Services;

/**
 * @class Aggregator
 * @description Middle-tier component that collects data from multiple Wavy sensors, 
 * temporarily stores it, and forwards it to the central server. Acts as both a client
 * (to the server) and a server (to the Wavy devices).
 */
class Aggregator
{
    // Path for storing collected data before forwarding to server
    private static readonly string WavyDataFilePath = "WavyData.json";
    // Mutex for thread-safe file access (with timeout to prevent deadlocks)
    private static readonly Mutex WavyDataMutex = new();
    private static readonly int MutexTimeout = 5000; // 5 seconds timeout

    /**
     * @method SaveWavyDataToFile
     * @description Thread-safe method to save sensor data to a JSON file
     * @param data The sensor data object to be saved
     */
    private static void SaveWavyDataToFile(AggregatorSensorData data)
    {
        try
        {
            bool mutexAcquired = false;
            try
            {
                // Try to acquire mutex with timeout to prevent deadlocks
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

                // Serialize and save back to the file with pretty-printing
                string updatedJsonData = JsonSerializer.Serialize(existingData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(WavyDataFilePath, updatedJsonData);
            }
            finally
            {
                // Always release mutex if acquired to prevent resource leaks
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

    /**
     * @method PeriodicDataSendAsync
     * @description Background task that periodically sends accumulated data to the central server
     * @param interval Time interval in milliseconds between sending attempts
     */
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
                        // Send data to server and wait for acknowledgment
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
                // Always release mutex if acquired
                if (mutexAcquired)
                {
                    WavyDataMutex.ReleaseMutex();
                }
            }
        }
    }
    
    // Connection objects for the central server
    private static NetworkStream? serverStream;
    private static TcpClient? serverClient;
    private static string aggregatorId = string.Empty;
    // RabbitMQ service instance
    private static RabbitMQService? rabbitmqService;

    /**
     * @method Main
     * @description Entry point for the Aggregator application. Sets up RabbitMQ message consumption 
     * from Wavy sensors and forwarding to the central server.
     */
    public static async Task Main()
    {
        // Interactive configuration
        Console.Write("Enter Aggregator ID: ");
        aggregatorId = Console.ReadLine() ?? "Agg1";

        // RabbitMQ configuration from environment variables or defaults
        string rabbitmqHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        int rabbitmqPort = int.Parse(Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? "5672");
        string rabbitmqUser = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_USER") ?? "oceanguest";
        string rabbitmqPassword = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_PASS") ?? "oceanpass";
        string queueName = Environment.GetEnvironmentVariable("AGGREGATOR_QUEUE") ?? $"{aggregatorId.ToLower()}_queue";

        // Default server connection settings
        string serverIp = "127.0.0.1";
        int serverPort = 8080;

        Console.WriteLine($"Aggregator {aggregatorId} Configuration:");
        Console.WriteLine($"  RabbitMQ Host: {rabbitmqHost}:{rabbitmqPort}");
        Console.WriteLine($"  Listening Queue: {queueName}");
        Console.WriteLine($"  Server: {serverIp}:{serverPort}");

        // Data limit settings to prevent memory issues
        int maxBytes = 2048;
        int currentBytes = 0;
        bool mutexAcquired = false;
        
        try
        {
            // Check current data size to ensure it's within limits
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

        // Safety check to prevent processing excessive data
        if (currentBytes > maxBytes)
        {
            Console.WriteLine("Data size exceeds maximum limit. Exiting...");
            return;
        }
        
        // Configure data forwarding interval
        int sendInterval = 10000; // 10 seconds between sending data to server

        // Establish connection to the central server
        bool connected = await ConnectToServerAsync(serverIp, serverPort);

        if (!connected)
        {
            Console.WriteLine("Failed to connect to server. Exiting...");
            return;
        }

        // Initialize RabbitMQ service
        var config = new RabbitMQConfig
        {
            HostName = rabbitmqHost,
            Port = rabbitmqPort,
            UserName = rabbitmqUser,
            Password = rabbitmqPassword
        };

        try
        {
            rabbitmqService = new RabbitMQService(config);
            Console.WriteLine("Connected to RabbitMQ successfully.");

            // Declare the queue for this aggregator
            rabbitmqService.DeclareQueue(queueName, queueName);
            Console.WriteLine($"Queue '{queueName}' declared successfully.");

            // Start background task for sending data to server
            _ = Task.Run(() => PeriodicDataSendAsync(sendInterval));

            // Start consuming messages from RabbitMQ
            Console.WriteLine($"Aggregator {aggregatorId} starting to consume messages from queue '{queueName}'...");
            
            rabbitmqService.StartConsumer(queueName, (message) => {
                return HandleRabbitMQMessage(message);
            });

            Console.WriteLine("Press [Enter] to exit...");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize RabbitMQ: {ex.Message}");
            Console.WriteLine("Please ensure RabbitMQ service is running and accessible.");
        }
        finally
        {
            // Clean up resources
            rabbitmqService?.Dispose();
            serverClient?.Close();
            Console.WriteLine("Aggregator shutting down.");
        }
    }

    /**
     * @method HandleRabbitMQMessage
     * @description Processes incoming RabbitMQ messages from Wavy sensors
     * @param message The RabbitMQ message containing sensor data
     * @return Boolean indicating if message was processed successfully
     */
    private static bool HandleRabbitMQMessage(RabbitMQMessage message)
    {
        try
        {
            Console.WriteLine($"Processing message from {message.WavyId}: {message.MessageType}");

            switch (message.MessageType)
            {
                case "SENSOR_DATA":
                    // Process sensor data
                    foreach (var dataPoint in message.SensorData)
                    {
                        var aggregatorData = new AggregatorSensorData
                        {
                            AggregatorId = aggregatorId,
                            WavyId = message.WavyId,
                            DataType = dataPoint.dataType,
                            RawValue = dataPoint.value,
                            Timestamp = message.Timestamp
                        };

                        SaveWavyDataToFile(aggregatorData);
                        Console.WriteLine($"Saved {dataPoint.dataType}={dataPoint.value} from {message.WavyId}");
                    }
                    break;

                case "MAINTENANCE_UP":
                    Console.WriteLine($"Wavy {message.WavyId} entered maintenance mode");
                    break;

                case "MAINTENANCE_DOWN":
                    Console.WriteLine($"Wavy {message.WavyId} exited maintenance mode");
                    break;

                default:
                    Console.WriteLine($"Unknown message type: {message.MessageType}");
                    break;
            }

            return true; // Message processed successfully
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message from {message.WavyId}: {ex.Message}");
            return false; // Message processing failed
        }
    }

    /**
     * @method ConnectToServerAsync
     * @description Establishes connection to the central server with retry mechanism
     * @param serverIp IP address of the server
     * @param serverPort Port number of the server
     * @return Boolean indicating if connection was successful
     */
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

                // Send connection request with aggregator ID
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

    /**
     * @method HandleWavyAsync
     * @description Handles communication with a connected Wavy device
     * @param wavyClient The TCP client representing the connected Wavy
     */
    private static async Task HandleWavyAsync(TcpClient wavyClient)
    {
        string wavyId = string.Empty;
        using NetworkStream stream = wavyClient.GetStream();
        byte[] buffer = new byte[1024];
        using var channel = GrpcChannel.ForAddress("http://localhost:50051");
        var parserClient = new DataParser.DataParserClient(channel);

        try
        {
            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var (messageType, payload) = Protocol.ParseMessage(message);
                Console.WriteLine("Received data from Wavy " + message);

                switch (messageType)
                {
                    case Protocol.CONN_REQ:
                        wavyId = payload;
                        await SendMessageToWavy(stream, Protocol.CONN_ACK);
                        Console.WriteLine($"Wavy {wavyId} connected.");
                        break;

                    case Protocol.DATA_SEND:
                        try
                        {
                            string[] parts = payload.Split('|');
                            if (parts.Length >= 2)
                            {
                                string dataFormat = parts[0];
                                string dataPayload = parts[1];

                                string jsonPayload = string.Empty;
                                switch (dataFormat)
                                {
                                    case "CSV":
                                        // Parse CSV in gRPC
                                        var request = new ParseRequest { Data = dataPayload, From = dataFormat, To = "JSON" };
                                        var response = parserClient.Parse(request);
                                        jsonPayload = response.Result;
                                        break;
                                    case "JSON":
                                        jsonPayload = dataPayload;
                                        break;
                                }

                                Console.WriteLine("Data points: " + jsonPayload);
                                var dataPoints = JsonSerializer.Deserialize<List<SensorDataPayload>>(jsonPayload);
                                foreach (var dataPoint in dataPoints)
                                {
                                    string dataType = dataPoint.type;
                                    string processedValue = dataPoint.value.ToString();

                                    var aggregatorData = new AggregatorSensorData
                                    {
                                        WavyId = wavyId,
                                        AggregatorId = aggregatorId,
                                        DataType = dataType,
                                        Timestamp = DateTime.UtcNow,
                                        RawValue = processedValue
                                    };

                                    SaveWavyDataToFile(aggregatorData);
                                }
                                await SendMessageToWavy(stream, Protocol.DATA_ACK);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing data from Wavy {wavyId}: {ex.Message}");
                        }
                        break;

                    case Protocol.MAINTENANCE_STATE_UP:
                        Console.WriteLine($"Wavy {wavyId} entered maintenance mode.");
                        await SendMessageToWavy(stream, Protocol.STATUS_ACK);
                        break;

                    case Protocol.MAINTENANCE_STATE_DOWN:
                        Console.WriteLine($"Wavy {wavyId} exited maintenance mode.");
                        await SendMessageToWavy(stream, Protocol.STATUS_ACK);
                        break;

                    case Protocol.DISC_REQ:
                        await SendMessageToWavy(stream, Protocol.DISC_ACK);
                        Console.WriteLine($"Wavy {wavyId} disconnected.");
                        return;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling Wavy {wavyId}: {ex.Message}");
        }
        finally
        {
            wavyClient.Close();
        }
    }

    /**
     * @method SendAggregatedDataToServer
     * @description Sends collected sensor data to the central server with retry mechanism
     * @param aggregatedPayload JSON string containing the data to send
     */
    private static async Task SendAggregatedDataToServer(string aggregatedPayload)
    {
        while (true) // Keep trying to send data until successful
        {
            try
            {
                // Check server connection status
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

                // Format and send data according to protocol
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

    /**
     * @method SendMessageToWavy
     * @description Helper method to send protocol messages to a Wavy device
     * @param stream The NetworkStream to send data through
     * @param messageType The type of protocol message
     * @param payload Optional data payload
     */
    private static async Task SendMessageToWavy(NetworkStream stream, string messageType, string payload = "")
    {
        string message = Protocol.CreateMessage(messageType, payload);
        byte[] messageBytes = Encoding.ASCII.GetBytes(message);
        await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
        Console.WriteLine($"Sent to wavy: {messageType} - {payload}");
    }
    
    /**
     * @method GetWavyDataFromFile
     * @description Reads accumulated sensor data from the JSON file
     * @return Array of sensor data objects
     */
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

    /**
     * @method CleanWavyData
     * @description Deletes the temporary data file after successful transmission to server
     * Note: Only call when having the mutex locked
     */
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

    /**
     * @method ReconnectToServerAsync
     * @description Attempts to reestablish connection with the central server
     * @return Boolean indicating if reconnection was successful
     */
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

    public class SensorDataPayload
    {
        public string type { get; set; }
        public double value { get; set; }
    }
}