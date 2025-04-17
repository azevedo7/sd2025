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

/**
 * @class Server
 * @description Central repository component that collects, stores, and presents data
 * from all aggregators. Provides persistence using LiteDB and a user interface
 * for data analysis. This is the top tier of the distributed system architecture.
 */
class Server
{
    // Database file path for persistent storage
    private static readonly string DbPath = "oceandata.db";

    // Lock object for thread-safe database access across multiple client connections
    private static readonly object DbLock = new object();

    // Cancellation token to coordinate graceful shutdown of server components
    private static CancellationTokenSource _cts = new CancellationTokenSource();

    /**
     * @method Main
     * @description Entry point for the server application. Initializes the database,
     * starts the TCP listener for client connections, and runs the user interface.
     */
    public static async Task Main()
    {
        // Create or connect to the database and set up required collections
        InitializeDatabase();

        // Start listening for aggregator connections in a separate task
        Task serverTask = StartTcpListenerAsync(_cts.Token);

        // Run the interactive user interface for data querying and management
        await RunUserInterfaceAsync();

        // When user interface exits, initiate graceful shutdown
        _cts.Cancel();

        try
        {
            // Wait for the server to shut down properly
            await serverTask;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Server shut down gracefully.");
        }

        Console.WriteLine("Exiting...");
        Environment.Exit(0);
    }

    /**
     * @method RunUserInterfaceAsync
     * @description Provides an interactive menu-driven interface for system administrators
     * to view and analyze the collected sensor data.
     */
    private static async Task RunUserInterfaceAsync()
    {
        bool exit = false;

        while (!exit)
        {
            // Display main menu
            Console.WriteLine("\n==== Ocean Monitoring Server ====");
            Console.WriteLine("1. View all sensor data");
            Console.WriteLine("2. View data by WAVY ID");
            Console.WriteLine("3. View data by data type");
            Console.WriteLine("4. View aggregators");
            Console.WriteLine("0. Exit");
            Console.Write("\nEnter option: ");

            string input = Console.ReadLine();

            // Process user selection
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

    /**
     * @method ViewDataByDataType
     * @description Query and display sensor data filtered by the specified data type
     * @param dataType The type of data to filter by (e.g., "temperature")
     */
    private static void ViewDataByDataType(string? dataType)
    {
        throw new NotImplementedException();
    }

    /**
     * @method ViewDataByWavyId
     * @description Query and display sensor data filtered by the specified Wavy ID
     * @param wavyId The ID of the Wavy device to filter by
     */
    private static void ViewDataByWavyId(string? wavyId)
    {
        throw new NotImplementedException();
    }

    /**
     * @method ViewAllSensorData
     * @description Query and display all sensor data from the database, sorted by timestamp.
     * Also exports the data to CSV for external analysis.
     */
    private static void ViewAllSensorData()
    {
        lock (DbLock)
        {
            using (var db = new LiteDatabase(DbPath))
            {
                // Get the sensor data collection and retrieve all records
                var collection = db.GetCollection<SensorData>("sensorData");
                var data = collection.FindAll()
                                     .OrderByDescending(item => item.ReceivedAt) // Most recent data first
                                     .ToList();

                if (data.Count == 0)
                {
                    Console.WriteLine("No sensor data found.");
                    return;
                }

                // Display data in a tabular format
                Console.WriteLine("\n{0,-10} {1,-10} {2,-10} {3,-20} {4,-30} {5,-30}",
                   "WAVY ID", "AGG ID", "Data Type", "Timestamp", "Value", "Received At");
                Console.WriteLine(new string('-', 110));

                // Limit display to 20 rows for readability
                foreach (var item in data.Take(20))
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

                // Export data to CSV file for external analysis
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
    }

    /**
     * @method StartTcpListenerAsync
     * @description Starts a TCP listener to accept incoming connections from aggregators.
     * Handles client connections in separate tasks for concurrent processing.
     * @param cancellationToken Token to signal when the server should shut down
     */
    private static async Task StartTcpListenerAsync(CancellationToken cancellationToken)
    {
        TcpListener server = null;

        try
        {
            // Configure server endpoint
            IPAddress ip = IPAddress.Parse("127.0.0.1");
            int port = 8080;

            // Start the listener
            server = new TcpListener(ip, port);
            server.Start();

            Console.WriteLine($"Server started on {ip}:{port}");

            // Main server loop - continues until cancellation is requested
            while(!cancellationToken.IsCancellationRequested)
            {
                // Set up cancellation for AcceptTcpClientAsync
                var acceptClientTask = server.AcceptTcpClientAsync();

                // Complete as soon as either we get a client or cancellation is requested
                var completedTask = await Task.WhenAny(acceptClientTask, Task.Delay(-1, cancellationToken));

                if (cancellationToken.IsCancellationRequested)
                        break;

                // Handle new client connection
                if (completedTask == acceptClientTask)
                {
                    TcpClient client = await acceptClientTask;
                    Console.WriteLine("Client connected!");

                    // Process each client in a separate task for concurrency
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
            // Ensure the server is stopped when exiting
            server?.Stop();
        }
    }

    /**
     * @method ProcessAggregatedData
     * @description Processes aggregated sensor data received from an aggregator
     * @param payload JSON string containing the aggregated data
     */
    private static void ProcessAggregatedData(string payload)
    {
        // Parse the JSON payload into sensor data objects
        List<SensorData> data = ParseSensorData(payload);
        if (data == null || data.Count == 0)
        {
            Console.WriteLine("No valid sensor data found in the JSON.");
            return;
        }
        else
        {
            // Store the parsed data in the database
            StoreData(data);
            Console.WriteLine($"Processed {data.Count} data points");
        }
    }

    /**
     * @method HandleClientAsync
     * @description Handles communication with a connected aggregator client
     * @param client The TcpClient representing the connected aggregator
     */
    private static async Task HandleClientAsync(TcpClient client)
    {
        string clientId = "";

        try
        {
            // Buffer for receiving data from the client
            Byte[] buffer = new Byte[4096]; // Increased buffer size for larger data payloads
            String data = null;

            // Get a stream object for reading and writing
            NetworkStream stream = client.GetStream();

            // Client communication loop
            while (true)
            {
                // Read data from the client
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    break; // Client disconnected
                }
                data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Console.WriteLine("Received: {0}", data);

                // Process protocol messages
                if (Protocol.IsValidMessage(data))
                {
                    // Parse the message according to protocol
                    var (messageType, payload) = Protocol.ParseMessage(data);
                    var response = string.Empty;

                    // Handle different message types
                    switch (messageType)
                    {
                        case Protocol.CONN_REQ:
                            // Register new aggregator connection
                            clientId = payload;
                            //RegisterAggregator(clientId);
                            response = Protocol.CreateMessage(Protocol.CONN_ACK, "SUCCESS");
                            break;

                        case Protocol.AGG_DATA_SEND:
                            // Process data sent by an aggregator
                            if (!string.IsNullOrEmpty(clientId)){
                                ProcessAggregatedData(payload);
                                response = Protocol.CreateMessage(Protocol.AGG_DATA_ACK, $"{clientId}:SUCCESS");
                            } else
                            {
                                response = Protocol.CreateMessage(Protocol.AGG_DATA_ACK, $"{clientId}:FAIL");
                            }
                            break;

                        case Protocol.DISC_REQ:
                            // Handle aggregator disconnection
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
                    Console.WriteLine($"Sending response: {response}");
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                }
            }
        }
        catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx)
        {
            // Handle unexpected disconnection
            Console.WriteLine($"Client {clientId} disconnected unexpectedly: {socketEx.Message}");
            //UpdateAggregatorStatus(clientId, "DISCONNECTED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client {clientId}: {ex.Message}");
        }
        finally
        {
            // Clean up resources
            client.Close();
            Console.WriteLine($"Connection with {clientId} closed");
        }
    }

    /**
     * @method StoreData
     * @description Stores sensor data in the LiteDB database with thread safety
     * @param dataItems List of sensor data objects to store
     */
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

    /**
     * @method InitializeDatabase
     * @description Creates or connects to the LiteDB database and sets up required collections and indexes
     */
    private static void InitializeDatabase()
    {
        lock (DbLock)
        {
            using (var db = new LiteDatabase(DbPath))
            {
                // Create or get collections
                var sensorDataCollection = db.GetCollection<SensorData>("sensorData");
                var aggregatorsCollection = db.GetCollection<Aggregator>("aggregators");

                // Create indexes for faster querying
                sensorDataCollection.EnsureIndex(x => x.WavyId);
                sensorDataCollection.EnsureIndex(x => x.AggregatorId);
                sensorDataCollection.EnsureIndex(x => x.DataType);
                sensorDataCollection.EnsureIndex(x => x.Timestamp);
            } 

        Console.WriteLine("Database initialized.");
        }
    }
    
    /**
     * @method ParseSensorData
     * @description Parses JSON-formatted sensor data into a list of SensorData objects
     * @param jsonData JSON string containing the sensor data
     * @return List of parsed SensorData objects
     */
    private static List<SensorData> ParseSensorData(string jsonData)
    {
        try
        {
            // Deserialize JSON into AggregatorSensorData objects
            var sensorDataList = System.Text.Json.JsonSerializer.Deserialize<List<AggregatorSensorData>>(jsonData);
            if (sensorDataList == null || !sensorDataList.Any())
            {
                Console.WriteLine("No valid sensor data found in the JSON.");
                return new List<SensorData>();
            }

            // Transform AggregatorSensorData into SensorData with received timestamp
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
    
    /**
     * @method RegisterAggregator
     * @description Registers a new aggregator or updates an existing one in the database
     * @param clientId Unique identifier for the aggregator
     */
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

                // Check if the aggregator is already registered
                var existingAggregator = collection.FindOne(a => a.ClientId == clientId);
                if (existingAggregator == null)
                {
                    // Register new aggregator
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
                    // Update existing aggregator status
                    existingAggregator.Status = "CONNECTED";
                    existingAggregator.LastConnectedAt = DateTime.Now;
                    collection.Update(existingAggregator);
                    Console.WriteLine($"Aggregator {clientId} reconnected successfully.");
                }
            }
        }
    }
    
    /**
     * @method ViewAggregators
     * @description Displays a list of all registered aggregators with their connection status
     */
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

                // Display aggregator data in tabular format
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
