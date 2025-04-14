using System;
using System.Net.Sockets;
using System.Text;
using OceanMonitoringSystem.Common;
using System.Text.Json;
using Models;
using System.Security.Cryptography.X509Certificates;

class Wavy
{
    private static DataWavy[] unsentData = Array.Empty<DataWavy>();
    private static readonly Random random = new Random();
    private static bool isRunning = true;
    private static readonly object unsentDataLock = new object();

    public static void ClearUnsentData()
    {
        lock (unsentDataLock)
        {
            unsentData = Array.Empty<DataWavy>();
        }
    }

    public static void AddToUnsentData(DataWavy[] newData)
    {
        lock (unsentDataLock)
        {
            unsentData = unsentData.Concat(newData).ToArray();
        }
    }

    public static DataWavy[] GetUnsentData()
    {
        lock (unsentDataLock)
        {
            return unsentData.ToArray();
        }
    }

    public static async Task Main(string[] args)
    {
        string aggregatorIp = "127.0.0.1";
        int aggregatorPort = 9000;
        string wavyId = "Wavy1";

        // Allow user to override defaults
        Console.WriteLine($"Using default settings: IP={aggregatorIp}, Port={aggregatorPort}, ID={wavyId}");
        Console.WriteLine("Press Enter to accept or input new values.");
        
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

        // Start the automatic data generation in a separate task
        _ = Task.Run(() => GenerateDataPeriodically(dataGenerationInterval * 1000));

        // Handle console commands in a separate task
        _ = Task.Run(() => HandleConsoleCommands(wavyId));

        while (isRunning)
        {
            try
            {
                using TcpClient client = new TcpClient();
                Console.WriteLine($"Connecting to aggregator at {aggregatorIp}:{aggregatorPort}...");
                await client.ConnectAsync(aggregatorIp, aggregatorPort);
                using NetworkStream stream = client.GetStream();

                Console.WriteLine($"Connected to aggregator at {aggregatorIp}:{aggregatorPort}");

                // Send CONN_REQ with ID
                string connReq = Protocol.CreateMessage(Protocol.CONN_REQ, wavyId);
                await SendAsync(stream, connReq);

                // Listen for response
                string response = await ReadAsync(stream);
                var (connAckMessage, _) = Protocol.ParseMessage(response);

                if (connAckMessage != Protocol.CONN_ACK)
                {
                    Console.WriteLine("Unexpected response. Retrying connection...");
                    await Task.Delay(3000);
                    continue;
                }
                Console.WriteLine("Connection acknowledged by aggregator.");

                // Main communication loop
                while (isRunning)
                {
                    // Try to send unsent data if any
                    DataWavy[] currentUnsentData = GetUnsentData();
                    if (currentUnsentData.Length > 0)
                    {
                        Console.WriteLine($"Sending {currentUnsentData.Length} data points to aggregator...");
                        await SendAsync(stream, Protocol.CreateMessage(Protocol.DATA_SEND, JsonSerializer.Serialize(currentUnsentData)));
                        
                        try
                        {
                            string dataReply = await ReadAsync(stream);
                            var (messageType, _) = Protocol.ParseMessage(dataReply);

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
                            break; // Break the inner loop to reconnect
                        }
                    }

                    // Wait a short time before checking for more data to send
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection error: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Reconnecting in 3 seconds...");
                await Task.Delay(3000); // Wait for 3 seconds before reconnecting
            }
        }

        Console.WriteLine("Wavy shutting down.");
    }

    private static async Task GenerateDataPeriodically(int intervalMs)
    {
        while (isRunning)
        {
            // Generate random data
            DataWavy[] dataPoints = new[]
            {
                new DataWavy { dataType = "temperature", value = GenerateRandomTemperature().ToString() },
                new DataWavy { dataType = "humidity", value = GenerateRandomHumidity().ToString() },
                new DataWavy { dataType = "windSpeed", value = GenerateRandomWindSpeed().ToString() },
                new DataWavy { dataType = "waterLevel", value = GenerateRandomWaterLevel().ToString() }
            };

            // Add to unsent data
            AddToUnsentData(dataPoints);
            
            Console.WriteLine($"Generated data: Temperature={dataPoints[0].value}°C, Humidity={dataPoints[1].value}%, " +
                              $"Wind Speed={dataPoints[2].value}km/h, Water Level={dataPoints[3].value}m");

            // Wait for the next interval
            await Task.Delay(intervalMs);
        }
    }

    private static async Task HandleConsoleCommands(string wavyId)
    {
        while (isRunning)
        {
            Console.WriteLine("\nCommands: [g]enerate data manually, [m]aintenance mode, [q]uit");
            Console.Write("> ");
            string command = Console.ReadLine()?.ToLower() ?? "";

            switch (command)
            {
                case "g":
                    // Generate data manually
                    DataWavy[] manualData = new[]
                    {
                        new DataWavy { dataType = "temperature", value = GenerateRandomTemperature().ToString() },
                        new DataWavy { dataType = "windSpeed", value = GenerateRandomWindSpeed().ToString() },
                    };
                    AddToUnsentData(manualData);
                    Console.WriteLine("Manual data generated and added to queue.");
                    break;

                case "m":
                    Console.WriteLine("Maintenance mode not implemented in automatic sending mode.");
                    break;

                case "q":
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

            await Task.Delay(100); // Small delay to prevent CPU spinning
        }
    }

    private static async Task SendAsync(NetworkStream stream, string message)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(message);
        await stream.WriteAsync(bytes, 0, bytes.Length);
    }

    private static async Task<string> ReadAsync(NetworkStream stream)
    {
        byte[] buffer = new byte[1024];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        return Encoding.ASCII.GetString(buffer, 0, bytesRead);
    }

    private static int GenerateRandomTemperature()
    {
        return random.Next(-10, 40); // Temperature between -10 and 40 degrees Celsius
    }

    private static int GenerateRandomWindSpeed()
    {
        return random.Next(0, 100); // Wind speed between 0 and 100 km/h
    }

    private static int GenerateRandomHumidity()
    {
        return random.Next(20, 100); // Humidity between 20% and 100%
    }

    private static double GenerateRandomWaterLevel()
    {
        return Math.Round(random.NextDouble() * 10, 2); // Water level between 0 and 10 meters, with 2 decimal places
    }
}