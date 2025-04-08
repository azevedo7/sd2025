using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using OceanMonitoringSystem.Common;
using System.Text.Json;

class Aggregator
{
    private static readonly ConcurrentBag<string> WavyData = new();
    private static TcpClient serverClient;
    private static NetworkStream serverStream;

    public static async Task Main()
    {
        int wavyPort = 9000;
        string serverIp = "127.0.0.1";
        int serverPort = 8080;

        // Connect to the main server
        serverClient = new TcpClient();
        await serverClient.ConnectAsync(IPAddress.Parse(serverIp), serverPort);
        serverStream = serverClient.GetStream();
        Console.WriteLine("Connected to server.");

        // Start listening for wavys
        TcpListener wavyListener = new TcpListener(IPAddress.Parse("127.0.0.1"), wavyPort);
        wavyListener.Start();
        Console.WriteLine($"Aggregator listening for wavys on port {wavyPort}...");

        while (true)
        {
            TcpClient wavyClient = await wavyListener.AcceptTcpClientAsync();
            Console.WriteLine("Wavy connected.");
            _ = Task.Run(() => HandleWavyAsync(wavyClient));
        }
    }

    private static async Task HandleWavyAsync(TcpClient wavyClient)
    {
        string wavyId = string.Empty; // Initialize wavyId to avoid CS0165 error
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
                            WavyData.Add(payload);

                            // Payload -> DATA as json
                            string dataReceived = payload;
                            Console.WriteLine("Data received: " + dataReceived);

                            string cleanData = dataReceived.Trim('[', ']');
                            string[] dataParts = cleanData.Split("},{").Select(p => "{" + p.Trim('{', '}') + "}").ToArray();
                            Console.WriteLine(dataParts);

                            for (int i = 0; i < dataParts.Length; i++)
                            {
                                Console.WriteLine($"Data part {i}: " + dataParts[i]);
                                // Convert dataReceived to JSON object
                                JsonDocument jsonDocument = JsonDocument.Parse(dataParts[i]);
                                JsonElement jsonObject = jsonDocument.RootElement;

                                // Example of accessing JSON properties
                                string dataType = jsonObject.GetProperty("dataType").GetString() ?? string.Empty;
                                string value = jsonObject.GetProperty("value").GetString() ?? string.Empty;

                                Console.WriteLine($"Data Type: {dataType}, Value: {value}");

                                // Data comes in this JSON format
                                //{
                                //    "dataType": "temperature",
                                //    "value": 25.5,
                                //}

                                // Aggregate data on files based by data type

                                if (String.IsNullOrEmpty(dataType) || String.IsNullOrEmpty(value))
                                {
                                    Console.WriteLine("Data type or value is empty. Not saving to CSV.");
                                    SendMessageToWavy(stream, Protocol.DATA_ACK, "Data type or value is empty").Wait();
                                    break;
                                }
                                else
                                {
                                    // Save data to CSV
                                    var csvHelper = new CsvHelper();
                                    csvHelper.SaveData(wavyId, dataType, value);
                                }

                            }
                            SendMessageToWavy(stream, Protocol.DATA_ACK, "Data received").Wait();
                            break;
                        

                        case Protocol.CONN_REQ:
                            // Save to file wavy.csv WAVY_ID:status:[data_types]:last_sync
                            // Payload is expected to be the WAVY_ID
                            wavyId = payload;
                            string status = WavyStatus.ACTIVE; // Example status
                            string dataTypes = "[]"; // Example data types
                            string lastSync = DateTime.UtcNow.ToString("o"); // ISO 8601 format

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
                                        // Check if the status is already active
                                        if (columns[1] == WavyStatus.ACTIVE)
                                        {
                                            Console.WriteLine("Wavy is already connected.");
                                            SendMessageToWavy(stream, Protocol.CONN_ACK, "Wavy is already connected").Wait();
                                            return;
                                        }

                                        columns[1] = WavyStatus.ACTIVE; // Update status to active
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

                            SendMessageToWavy(stream, Protocol.CONN_ACK, "Connection acknowledged").Wait();
                            Console.WriteLine("Sent connection acknowledgment to wavy.");
                            break;

                        case Protocol.DISC_REQ:
                            // Update the status in the CSV to inactive
                            CsvHelper.UpdateWavyStatus(payload, WavyStatus.INACTIVE);

                            SendMessageToWavy(stream, Protocol.DISC_ACK, "Disconnection acknowledged").Wait();
                            Console.WriteLine("Sent disconnection acknowledgment to wavy.");
                            break; // Exit the loop on disconnection request
                        case Protocol.MAINTENANCE_STATE:
                            // Update the status in the CSV to maintenance
                            CsvHelper.UpdateWavyStatus(payload, WavyStatus.MAINTENANCE);
                            SendMessageToWavy(stream, Protocol.STATUS_ACK, "Maintenance state acknowledged").Wait();
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
            if (wavyId != null)
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
        if (serverStream == null) return;

        string message = Protocol.CreateMessage(Protocol.DATA_SEND, aggregatedPayload);
        byte[] msgBytes = Encoding.ASCII.GetBytes(message);

        try
        {
            await serverStream.WriteAsync(msgBytes, 0, msgBytes.Length);
            Console.WriteLine("Sent to server: " + aggregatedPayload);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to send to server: " + ex.Message);
        }
    }

    private static async Task SendMessageToWavy(NetworkStream stream, string messageType, string payload = "")
    {
        string message = Protocol.CreateMessage(messageType, payload);
        byte[] messageBytes = Encoding.ASCII.GetBytes(message);
        await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
        Console.WriteLine($"Sent to wavy: {payload}");
    }
}
