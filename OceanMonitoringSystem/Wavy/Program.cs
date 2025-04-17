using System;
using System.Net.Sockets;
using System.Text;
using OceanMonitoringSystem.Common;
using System.Text.Json;
using Models;

/**
 * @class Wavy
 * @description Simulates an ocean monitoring sensor device that collects environmental data
 * and sends it to an aggregator server. This represents the data collection tier of the
 * distributed IoT monitoring system architecture.
 */
class Wavy
{
    // Storage for sensor data that hasn't been successfully transmitted to the aggregator
    private static DataWavy[] unsentData = Array.Empty<DataWavy>();
    // Random generator for simulating sensor readings
    private static readonly Random random = new Random();
    // Flag to control the application's execution
    private static bool isRunning = true;
    // Thread synchronization object to protect concurrent access to unsentData
    private static readonly object unsentDataLock = new object();
    // Flag to indicate if the device is in maintenance mode (data generation paused)
    private static bool manutencao = false;
    // Flag to track if maintenance mode has been communicated to the aggregator
    private static bool manutencaoSent = false;

    /**
     * @method ClearUnsentData
     * @description Thread-safe method to clear all unsent data after successful transmission
     */
    public static void ClearUnsentData()
    {
        lock (unsentDataLock)
        {
            unsentData = Array.Empty<DataWavy>();
        }
    }

    /**
     * @method AddToUnsentData
     * @description Thread-safe method to add new sensor readings to the unsent data queue
     * @param newData Array of sensor readings to be added to the queue
     */
    public static void AddToUnsentData(DataWavy[] newData)
    {
        lock (unsentDataLock)
        {
            unsentData = unsentData.Concat(newData).ToArray();
        }
    }

    /**
     * @method GetUnsentData
     * @description Thread-safe method to retrieve all pending sensor readings
     * @return Copy of the array containing all unsent data
     */
    public static DataWavy[] GetUnsentData()
    {
        lock (unsentDataLock)
        {
            return unsentData.ToArray();
        }
    }

    /**
     * @method Main
     * @description Entry point for the Wavy sensor application. Sets up configuration, 
     * initializes data generation, and establishes connection with the aggregator.
     * @param args Command-line arguments (not used)
     */
    public static async Task Main(string[] args)
    {
        // Default configuration values
        string aggregatorIp = "127.0.0.1";
        int aggregatorPort = 9000;
        string wavyId = "Wavy1";

        // Interactive configuration setup
        Console.WriteLine($"Using default settings: IP={aggregatorIp}, Port={aggregatorPort}, ID={wavyId}");
        Console.WriteLine("Press Enter to accept or input new values.");

        // Allow user to customize connection parameters
        Console.Write("Enter aggregator IP (or press Enter for default): ");
        string input = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(input))
            aggregatorIp = input;

        Console.Write("Enter aggregator port (or press Enter for default): ");
        input = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input, out int port))
            aggregatorPort = port;

        Console.Write("Enter Wavy ID (or press Enter for default): ");
        input = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(input))
            wavyId = input;

        Console.Write("Enter data generation interval in seconds (default 5): ");
        input = Console.ReadLine() ?? string.Empty;
        int dataGenerationInterval = 5;
        if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input, out int interval))
            dataGenerationInterval = interval;

        // Start background tasks for data generation and command handling
        _ = Task.Run(() => GenerateDataPeriodically(dataGenerationInterval * 1000));
        _ = Task.Run(() => HandleConsoleCommands(wavyId));

        // Main connection loop - keeps trying to connect to the aggregator
        while (isRunning)
        {
            try
            {
                // Establish TCP connection to the aggregator
                using TcpClient client = new TcpClient();
                Console.WriteLine($"Connecting to aggregator at {aggregatorIp}:{aggregatorPort}...");
                await client.ConnectAsync(aggregatorIp, aggregatorPort);
                using NetworkStream stream = client.GetStream();

                Console.WriteLine($"Connected to aggregator at {aggregatorIp}:{aggregatorPort}");

                // Send connection request with Wavy ID for identification
                string connReq = Protocol.CreateMessage(Protocol.CONN_REQ, wavyId);
                await SendAsync(stream, connReq);

                // Wait for connection acknowledgment from aggregator
                string response = await ReadAsync(stream);
                var (connAckMessage, _) = Protocol.ParseMessage(response);

                // Verify correct response type
                if (connAckMessage != Protocol.CONN_ACK)
                {
                    Console.WriteLine("Unexpected response. Retrying connection...");
                    await Task.Delay(3000);
                    continue;
                }
                Console.WriteLine("Connection acknowledged by aggregator.");

                // Main data transmission loop
                while (isRunning)
                {
                    // Handle maintenance mode
                    while(manutencao)
                    {
                        // Send maintenance notification once when entering maintenance
                        if(!manutencaoSent)
                        {
                            await SendAsync(stream, Protocol.CreateMessage(Protocol.MAINTENANCE_STATE_UP, wavyId));
                            manutencaoSent = true;
                        }

                        Console.WriteLine("In maintenance mode. Waiting...");
                        await Task.Delay(1000);
                    }

                    // Send notification when exiting maintenance mode
                    if(!manutencao && manutencaoSent)
                    {
                        await SendAsync(stream, Protocol.CreateMessage(Protocol.MAINTENANCE_STATE_DOWN, wavyId));
                        manutencaoSent = false;
                    }

                    // Check for data that needs to be sent
                    DataWavy[] currentUnsentData = GetUnsentData();
                    if (currentUnsentData.Length > 0)
                    {
                        Console.WriteLine($"Sending {currentUnsentData.Length} data points to aggregator...");
                        // Serialize and send data according to protocol
                        await SendAsync(stream, Protocol.CreateMessage(Protocol.DATA_SEND, JsonSerializer.Serialize(currentUnsentData)));

                        try
                        {
                            // Wait for acknowledgment from aggregator
                            string dataReply = await ReadAsync(stream);
                            var (messageType, _) = Protocol.ParseMessage(dataReply);

                            // Clear data only if properly acknowledged
                            if (messageType == Protocol.DATA_ACK)
                            {
                                Console.WriteLine("Data acknowledged by aggregator.");
                                ClearUnsentData();
                            }
                            else
                            {
                                Console.WriteLine("Unexpected response. Keeping data in unsentData.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error receiving acknowledgment: {ex.Message}");
                            break;
                        }
                    }

                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection error: {ex.Message}");
            }
            finally
            {
                // Reconnection policy with exponential backoff could be implemented here
                Console.WriteLine("Reconnecting in 3 seconds...");
                await Task.Delay(3000);
            }
        }

        Console.WriteLine("Wavy shutting down.");
    }

    /**
     * @method GenerateDataPeriodically
     * @description Background task that simulates sensor readings at regular intervals
     * @param intervalMs Time interval in milliseconds between data generation cycles
     */
    private static async Task GenerateDataPeriodically(int intervalMs)
    {
        while (isRunning)
        {
            // Skip data generation during maintenance
            if (manutencao)
            {
                await Task.Delay(intervalMs);
                continue;
            }

            // Generate simulated data for various ocean measurements
            DataWavy[] dataPoints = new[]
            {
                new DataWavy { dataType = "temperature", value = GenerateRandomTemperature().ToString() },
                new DataWavy { dataType = "humidity", value = GenerateRandomHumidity().ToString() },
                new DataWavy { dataType = "windSpeed", value = GenerateRandomWindSpeed().ToString() },
                new DataWavy { dataType = "waterLevel", value = GenerateRandomWaterLevel().ToString() }
            };

            AddToUnsentData(dataPoints);

            Console.WriteLine($"Generated data: Temperature={dataPoints[0].value}°C, Humidity={dataPoints[1].value}%, " +
                              $"Wind Speed={dataPoints[2].value}km/h, Water Level={dataPoints[3].value}m");

            await Task.Delay(intervalMs);
        }
    }

    /**
     * @method HandleConsoleCommands
     * @description Interactive command handler for manual data generation, maintenance mode toggle, and shutdown
     * @param wavyId The ID of this Wavy instance for maintenance notifications
     */
    private static async Task HandleConsoleCommands(string wavyId)
    {
        while (isRunning)
        {
            Console.WriteLine("\nCommands: [g]enerate data manually, [m]aintenance toggle, [q]uit");
            Console.Write("> ");
            string command = Console.ReadLine()?.ToLower() ?? "";

            switch (command)
            {
                case "g":
                    // Manual data generation for testing
                    DataWavy[] manualData = new[]
                    {
                        new DataWavy { dataType = "temperature", value = GenerateRandomTemperature().ToString() },
                        new DataWavy { dataType = "windSpeed", value = GenerateRandomWindSpeed().ToString() },
                    };
                    AddToUnsentData(manualData);
                    Console.WriteLine("Manual data generated and added to queue.");
                    break;

                case "m":
                    // Toggle maintenance mode
                    manutencao = !manutencao;
                    Console.WriteLine(manutencao ? "Maintenance mode enabled." : "Maintenance mode disabled.");
                    break;

                case "q":
                    // Graceful shutdown
                    isRunning = false;
                    Console.WriteLine("Shutting down...");
                    break;

                default:
                    if (!string.IsNullOrWhiteSpace(command))
                    {
                        Console.WriteLine("Unknown command.");
                    }
                    break;
            }

            await Task.Delay(100);
        }
    }

    /**
     * @method SendAsync
     * @description Helper method to send messages over the network stream
     * @param stream The NetworkStream to send data through
     * @param message The string message to send
     */
    private static async Task SendAsync(NetworkStream stream, string message)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(message);
        await stream.WriteAsync(bytes, 0, bytes.Length);
    }

    /**
     * @method ReadAsync
     * @description Helper method to read messages from the network stream
     * @param stream The NetworkStream to read data from
     * @return String containing the received message
     */
    private static async Task<string> ReadAsync(NetworkStream stream)
    {
        byte[] buffer = new byte[1024];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        return Encoding.ASCII.GetString(buffer, 0, bytesRead);
    }

    // Sensor data simulation methods

    /**
     * @method GenerateRandomTemperature
     * @description Generates a random temperature value between -10°C and 40°C
     * @return Random temperature value
     */
    private static int GenerateRandomTemperature()
    {
        return random.Next(-10, 40);
    }

    /**
     * @method GenerateRandomWindSpeed
     * @description Generates a random wind speed value between 0 and 100 km/h
     * @return Random wind speed value
     */
    private static int GenerateRandomWindSpeed()
    {
        return random.Next(0, 100);
    }

    /**
     * @method GenerateRandomHumidity
     * @description Generates a random humidity percentage between 20% and 100%
     * @return Random humidity value
     */
    private static int GenerateRandomHumidity()
    {
        return random.Next(20, 100);
    }

    /**
     * @method GenerateRandomWaterLevel
     * @description Generates a random water level between 0 and 10 meters with 2 decimal precision
     * @return Random water level value
     */
    private static double GenerateRandomWaterLevel()
    {
        return Math.Round(random.NextDouble() * 10, 2);
    }
}
