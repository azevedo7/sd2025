using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Models;
using OceanMonitoringSystem.Common.Services;

/**
 * @class Wavy
 * @description Simulates an ocean monitoring sensor device that collects environmental data
 * and sends it to aggregators via RabbitMQ messaging. This represents the data collection tier 
 * of the distributed IoT monitoring system architecture.
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
    // Flag to track if maintenance notification has been sent
    private static bool manutencaoSent = false;
    // RabbitMQ service instance  
    private static RabbitMQService? rabbitmqService;

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
     * initializes data generation, and establishes RabbitMQ connection with the aggregator.
     * @param args Command-line arguments (not used)
     */
    public static async Task Main(string[] args)
    {
        // Default configuration values from environment variables or defaults
        string rabbitmqHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        int rabbitmqPort = int.Parse(Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? "5672");
        string rabbitmqUser = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_USER") ?? "oceanguest";
        string rabbitmqPassword = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_PASS") ?? "oceanpass";
        string aggregatorQueue = Environment.GetEnvironmentVariable("AGGREGATOR_QUEUE") ?? "agg1_queue";
        string wavyId = Environment.GetEnvironmentVariable("WAVY_ID") ?? "Wavy1";

        // Interactive configuration setup
        Console.WriteLine($"Default settings:");
        Console.WriteLine($"  RabbitMQ Host: {rabbitmqHost}:{rabbitmqPort}");
        Console.WriteLine($"  Target Queue: {aggregatorQueue}");
        Console.WriteLine($"  Wavy ID: {wavyId}");
        Console.WriteLine("Press Enter to accept or input new values.");

        // Allow user to customize connection parameters
        Console.Write("Enter RabbitMQ host (or press Enter for default): ");
        string input = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(input))
            rabbitmqHost = input;

        Console.Write("Enter RabbitMQ port (or press Enter for default): ");
        input = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input, out int port))
            rabbitmqPort = port;

        Console.Write("Enter target aggregator queue (or press Enter for default): ");
        input = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(input))
            aggregatorQueue = input;

        Console.Write("Enter Wavy ID (or press Enter for default): ");
        input = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(input))
            wavyId = input;

        Console.Write("Enter data generation interval in seconds (default 5): ");
        input = Console.ReadLine() ?? string.Empty;
        int dataGenerationInterval = 5;
        if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input, out int interval))
            dataGenerationInterval = interval;

        // Initialize RabbitMQ service
        var config = new RabbitMQConfig
        {
            HostName = rabbitmqHost,
            Port = rabbitmqPort,
            UserName = rabbitmqUser,
            Password = rabbitmqPassword
        };

        rabbitmqService = new RabbitMQService(config);

        try
        {
            // Initialize RabbitMQ connection (connection is established in constructor)
            Console.WriteLine("Connected to RabbitMQ successfully.");

            // Declare the target queue
            rabbitmqService.DeclareQueue(aggregatorQueue, aggregatorQueue);
            Console.WriteLine($"Queue '{aggregatorQueue}' declared successfully.");

            // Start background tasks for data generation and command handling
            _ = Task.Run(() => GenerateDataPeriodically(dataGenerationInterval * 1000));
            _ = Task.Run(() => HandleConsoleCommands(wavyId));

            // Main message sending loop
            while (isRunning)
            {
                try
                {
                    // Handle maintenance mode
                    while (manutencao)
                    {
                        // Send maintenance notification once when entering maintenance
                        if (!manutencaoSent)
                        {
                            var maintenanceMessage = new RabbitMQMessage
                            {
                                MessageId = Guid.NewGuid().ToString(),
                                WavyId = wavyId,
                                MessageType = "MAINTENANCE_UP",
                                SensorData = Array.Empty<DataWavy>(),
                                DataFormat = "JSON",
                                Priority = 1,
                                TargetQueue = aggregatorQueue,
                                Timestamp = DateTime.UtcNow
                            };

                            // Publish maintenance notification to all sensor type topics
                            string[] sensorTypes = { "temperature", "humidity", "windSpeed", "waterLevel" };
                            foreach (string sensorType in sensorTypes)
                            {
                                string topic = RabbitMQService.GenerateTopic(sensorType, wavyId, "MAINTENANCE_UP");
                                rabbitmqService.PublishToTopic(topic, maintenanceMessage);
                            }
                            Console.WriteLine("Maintenance mode notification sent.");
                            manutencaoSent = true;
                        }

                        Console.WriteLine("In maintenance mode. Waiting...");
                        await Task.Delay(1000);
                    }

                    // Send notification when exiting maintenance mode
                    if (!manutencao && manutencaoSent)
                    {
                        var maintenanceDownMessage = new RabbitMQMessage
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            WavyId = wavyId,
                            MessageType = "MAINTENANCE_DOWN",
                            SensorData = Array.Empty<DataWavy>(),
                            DataFormat = "JSON",
                            Priority = 1,
                            TargetQueue = aggregatorQueue,
                            Timestamp = DateTime.UtcNow
                        };

                        // Publish maintenance down notification to all sensor type topics
                        string[] sensorTypes = { "temperature", "humidity", "windSpeed", "waterLevel" };
                        foreach (string sensorType in sensorTypes)
                        {
                            string topic = RabbitMQService.GenerateTopic(sensorType, wavyId, "MAINTENANCE_DOWN");
                            rabbitmqService.PublishToTopic(topic, maintenanceDownMessage);
                        }
                        Console.WriteLine("Maintenance mode ended notification sent.");
                        manutencaoSent = false;
                    }

                    // Check for data that needs to be sent
                    DataWavy[] currentUnsentData = GetUnsentData();
                    if (currentUnsentData.Length > 0)
                    {
                        Console.WriteLine($"Sending {currentUnsentData.Length} data points via RabbitMQ...");

                        // Determine data format (20% chance of CSV, 80% JSON)
                        string dataFormat = random.Next(100) < 20 ? "CSV" : "JSON";
                        
                        // Publish each sensor data point to its appropriate topic
                        foreach (var dataPoint in currentUnsentData)
                        {
                            var message = new RabbitMQMessage
                            {
                                MessageId = Guid.NewGuid().ToString(),
                                WavyId = wavyId,
                                MessageType = "SENSOR_DATA",
                                SensorData = new[] { dataPoint }, // Single data point per message
                                DataFormat = dataFormat,
                                Priority = 5,
                                TargetQueue = aggregatorQueue, // Keep for backwards compatibility
                                Timestamp = DateTime.UtcNow
                            };

                            // Generate topic based on sensor type
                            string topic = RabbitMQService.GenerateTopic(dataPoint.dataType, wavyId, "SENSOR_DATA");
                            
                            // Publish to topic-based exchange
                            rabbitmqService.PublishToTopic(topic, message);
                            Console.WriteLine($"Published {dataPoint.dataType} data to topic: {topic}");
                        }
                        
                        Console.WriteLine($"Data sent successfully via RabbitMQ (Format: {dataFormat}, {currentUnsentData.Length} data points)");
                        
                        // Clear sent data
                        ClearUnsentData();
                    }

                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending data via RabbitMQ: {ex.Message}");
                    // In case of error, keep data in unsent queue for retry
                    await Task.Delay(3000);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to RabbitMQ: {ex.Message}");
            Console.WriteLine("Please ensure RabbitMQ service is running and accessible.");
        }
        finally
        {
            // Clean up RabbitMQ connection
            rabbitmqService?.Dispose();
            Console.WriteLine("Wavy shutting down.");
        }
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
