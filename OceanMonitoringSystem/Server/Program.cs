using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using LiteDB;
using Models;
using OceanMonitoringSystem.Common;

class Server
{
    // Database file path
    private static readonly string DbPath = "oceandata.db";

    // Mutex object for threads of database access
    private static readonly object DbLock = new object();

    // CancellationTokenSource to signal when to stop the server
    private static CancellationTokenSource _cts = new CancellationTokenSource();


    public static async Task Main()
    {
 
        InitializeDatabase();

        Task serverTask = StartTcpListenerAsync(_cts.Token);

        await RunUserInterfaceAsync();

        // after user stops on the interface
        _cts.Cancel();

        try
        {
            // Safely wait for the server so it can be stopped
            await serverTask;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Server shut down gracefully.");
        }

        Console.WriteLine("Exiting...");
        Environment.Exit(0);
    }

    private static async Task RunUserInterfaceAsync()
    {
        bool exit = false;

        while (!exit)
        {
            Console.WriteLine("\n==== Ocean Monitoring Server ====");
            Console.WriteLine("1. View all sensor data");
            Console.WriteLine("2. View data by WAVY ID");
            Console.WriteLine("3. View data by data type");
            Console.WriteLine("4. View aggregators");
            Console.WriteLine("0. Exit");
            Console.Write("\nEnter option: ");

            string input = Console.ReadLine();

            switch(input)
            {
                case "1":
                    ViewAllSensorData();
                    break;
                case "2":
                    Console.Write("Enter WAVY ID: ");
                    string wavyId = Console.ReadLine();
                    ViewDataByWavyId(wavyId);
                    break;
                case "3":
                    Console.Write("Enter data type: ");
                    string dataType = Console.ReadLine();
                    ViewDataByDataType(dataType);
                    break;
                case "4":
                    ViewAggregators();
                    break;
                case "0":
                    exit = true;
                    break;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }

            if(!exit)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }
    }

    private static void ViewDataByDataType(string? dataType)
    {
        throw new NotImplementedException();
    }

    private static void ViewDataByWavyId(string? wavyId)
    {
        throw new NotImplementedException();
    }

    private static void ViewAllSensorData()
    {
        lock (DbLock)
        {
            using (var db = new LiteDatabase(DbPath))
            {
                var collection = db.GetCollection<SensorData>("sensorData");
                var data = collection.FindAll()
                                     .OrderByDescending(item => item.ReceivedAt) // Sort by ReceivedAt in descending order
                                     .ToList();

                if (data.Count == 0)
                {
                    Console.WriteLine("No sensor data found.");
                    return;
                }

                Console.WriteLine("\n{0,-10} {1,-10} {2,-10} {3,-20} {4,-30} {5,-30}",
                   "WAVY ID", "AGG ID", "Data Type", "Timestamp", "Value", "Received At");
                Console.WriteLine(new string('-', 110));

                foreach (var item in data.Take(20)) // Limiting to 20 rows for display  
                {
                    Console.WriteLine("{0,-10} {1,-10} {2,-10} {3,-20} {4,-30} {5,-30}",
                        item.WavyId,
                        item.AggregatorId,
                        item.DataType,
                        item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        item.RawValue,
                        item.ReceivedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                }

                Console.WriteLine($"\nTotal records: {data.Count}");

                if (data.Count > 20)
                {
                    Console.WriteLine($"\n... and {data.Count - 20} more records");
                }

                // Export to CSV
                string csvFilePath = "SensorDataExport.csv";
                using (var writer = new StreamWriter(csvFilePath))
                {
                    writer.WriteLine("WavyId,AggregatorId,DataType,Timestamp,RawValue,ReceivedAt");

                    foreach (var item in data)
                    {
                        writer.WriteLine($"{item.WavyId},{item.AggregatorId},{item.DataType},{item.Timestamp:yyyy-MM-dd HH:mm:ss},{item.RawValue},{item.ReceivedAt:yyyy-MM-dd HH:mm:ss}");
                    }
                }

                Console.WriteLine($"\nSensor data exported to {csvFilePath}");

            }
        }
;
    }

    private static async Task StartTcpListenerAsync(CancellationToken cancellationToken)
    {
        TcpListener server = null;

        try
        {
            IPAddress ip = IPAddress.Parse("127.0.0.1");
            int port = 8080;

            server = new TcpListener(ip, port);
            server.Start();

            

            Console.WriteLine($"Server started on {ip}:{port}");

            while(!cancellationToken.IsCancellationRequested)
            {
                // Set up cancellation for AcceptTcpClientAsync
                var acceptClientTask = server.AcceptTcpClientAsync();

                // Complete as soon as either we get a client or cancellation is requested
                var completedTask = await Task.WhenAny(acceptClientTask, Task.Delay(-1, cancellationToken));

                if (cancellationToken.IsCancellationRequested)
                        break;

                if (completedTask == acceptClientTask)
                {
                    TcpClient client = await acceptClientTask;
                    Console.WriteLine("Client connected!");

                    // Handle client in a separate task
                    _ = Task.Run(() => HandleClientAsync(client));
                }
            }
        } catch(OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server error: {ex.Message}");
        }
        finally
        {
            server?.Stop();
        }
    }

    private static void ProcessAggregatedData(string payload)
    {
        // Parse the JSON payload
        List<SensorData> data = ParseSensorData(payload);
        if (data == null || data.Count == 0)
        {
            Console.WriteLine("No valid sensor data found in the JSON.");
            return;
        }
        else
        {
            StoreData(data);
            Console.WriteLine($"Processed {data.Count}");
        }
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        string clientId = "";

        try
        {
            Byte[] buffer = new Byte[4096]; // Increased buffer size for larger data payloads, maybe have a maximum size for the aggregator and server
            String data = null;

            // Get a stream object for reading and writing
            NetworkStream stream = client.GetStream();

            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    break; // Client disconnected
                }
                data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Console.WriteLine("Received: {0}", data);

                if (Protocol.IsValidMessage(data))
                {
                    // Process the data for the protocol
                    var (messageType, payload) = Protocol.ParseMessage(data);
                    var response = string.Empty;

                    switch (messageType)
                    {
                        case Protocol.CONN_REQ:
                            // Register aggregator connection
                            clientId = payload;
                            RegisterAggregator(clientId);
                            response = Protocol.CreateMessage(Protocol.CONN_ACK, "SUCCESS");
                            break;

                        case Protocol.AGG_DATA_SEND:
                            // Process aggregated data from aggregator
                            if (string.IsNullOrEmpty(clientId)){
                                ProcessAggregatedData(payload);
                                response = Protocol.CreateMessage(Protocol.AGG_DATA_ACK, $"{clientId}:SUCCESS");

                            } else
                            {
                                response = Protocol.CreateMessage(Protocol.AGG_DATA_ACK, $"{clientId}:FAIL");
                            }
                            break;

                        case Protocol.DISC_REQ:
                            //UpdateAggregatorStatus(clientId, "DISCONNECTED");
                            response = Protocol.CreateMessage(Protocol.DISC_ACK, "");
                            break;

                        default:
                            response = Protocol.CreateMessage("ERROR", "Unknown message type");
                            break;
                    }

                    Console.WriteLine($"Processed {messageType} from {clientId}");

                    // Send response back to the client
                    byte[] responseBytes = Encoding.ASCII.GetBytes(response);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                }
            }
        }
        catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx)
        {
            Console.WriteLine($"Client {clientId} disconnected unexpectedly: {socketEx.Message}");
            //UpdateAggregatorStatus(clientId, "DISCONNECTED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client {clientId}: {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine($"Connection with {clientId} closed");
        }
    }

    private static void StoreData(List<SensorData> dataItems)
    {
        lock (DbLock)
        {
            using (var db = new LiteDatabase(DbPath))
            {
                var collection = db.GetCollection<SensorData>("sensorData");

                foreach (var item in dataItems)
                {
                    collection.Insert(item);
                }
            }
        }
    }


    private static void InitializeDatabase()
    {
        lock (DbLock)
        {
            using (var db = new LiteDatabase(DbPath))
            {
                // Create data collections
                var sensorDataCollection = db.GetCollection<SensorData>("sensorData");
                var aggregatorsCollection = db.GetCollection<Aggregator>("aggregators");

                sensorDataCollection.EnsureIndex(x => x.WavyId);
                sensorDataCollection.EnsureIndex(x => x.AggregatorId);
                sensorDataCollection.EnsureIndex(x => x.DataType);
                sensorDataCollection.EnsureIndex(x => x.Timestamp);
            } 

        Console.WriteLine("Database initialized.");
        }
    }
    private static List<SensorData> ParseSensorData(string jsonData)
    {
        try
        {
            var sensorDataList = System.Text.Json.JsonSerializer.Deserialize<List<AggregatorSensorData>>(jsonData);
            if (sensorDataList == null || !sensorDataList.Any())
            {
                Console.WriteLine("No valid sensor data found in the JSON.");
                return new List<SensorData>();
            }

            return sensorDataList.Select(data => new SensorData
            {
                WavyId = data.WavyId,
                AggregatorId = data.AggregatorId,
                DataType = data.DataType,
                Timestamp = data.Timestamp,
                RawValue = data.RawValue,
                ReceivedAt = DateTime.Now
            }).ToList();
        }
        catch (System.Text.Json.JsonException ex)
        {
            Console.WriteLine($"Error parsing JSON data: {ex.Message}");
            return new List<SensorData>();
        }
    }
    private static void RegisterAggregator(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            Console.WriteLine("Invalid client ID. Registration failed.");
            return;
        }

        lock (DbLock)
        {
            using (var db = new LiteDatabase(DbPath))
            {
                var collection = db.GetCollection<Aggregator>("aggregators");

                var existingAggregator = collection.FindOne(a => a.ClientId == clientId);
                if (existingAggregator == null)
                {
                    var newAggregator = new Aggregator
                    {
                        ClientId = clientId,
                        Status = "CONNECTED",
                        RegisteredAt = DateTime.Now
                    };

                    collection.Insert(newAggregator);
                    Console.WriteLine($"Aggregator {clientId} registered successfully.");
                }
                else
                {
                    existingAggregator.Status = "CONNECTED";
                    existingAggregator.LastConnectedAt = DateTime.Now;
                    collection.Update(existingAggregator);
                    Console.WriteLine($"Aggregator {clientId} reconnected successfully.");
                }
            }
        }
    }
    private static void ViewAggregators()
    {
        lock (DbLock)
        {
            using (var db = new LiteDatabase(DbPath))
            {
                var collection = db.GetCollection<Aggregator>("aggregators");
                var aggregators = collection.FindAll().OrderBy(a => a.RegisteredAt).ToList();

                if (aggregators.Count == 0)
                {
                    Console.WriteLine("No aggregators found.");
                    return;
                }

                Console.WriteLine("\n{0,-20} {1,-15} {2,-20} {3,-20}",
                    "Client ID", "Status", "Registered At", "Last Connected At");
                Console.WriteLine(new string('-', 75));

                foreach (var aggregator in aggregators)
                {
                    Console.WriteLine("{0,-20} {1,-15} {2,-20} {3,-20}",
                        aggregator.ClientId,
                        aggregator.Status,
                        aggregator.RegisteredAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        aggregator.LastConnectedAt.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A");
                }

                Console.WriteLine($"\nTotal aggregators: {aggregators.Count}");
            }
        }
    }
}
