using System;
using System.Net.Sockets;
using System.Text;
using OceanMonitoringSystem.Common;
using System.Text.Json;


class Wavy
{
    public static async Task Main(string[] args)
    {
        // Console.Write("Enter aggregator IP: ");
        // string aggregatorIp = Console.ReadLine() ?? string.Empty;

        // Console.Write("Enter aggregator port: ");
        // if (!int.TryParse(Console.ReadLine(), out int aggregatorPort))
        // {
        //     Console.WriteLine("Invalid port. Exiting...");
        //     return;
        // }

        // Console.Write("Enter Wavy ID: ");
        // string wavyId = Console.ReadLine() ?? string.Empty;

        string aggregatorIp = "127.0.0.1";
        int aggregatorPort = 9000;
        string wavyId = "Wavy1";

        string[] unsentData = Array.Empty<string>();

        while (true)
        {

            try
            {
                using TcpClient client = new TcpClient();
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
                    Console.WriteLine("Unexpected response. Exiting...");
                    return;
                }
                Console.WriteLine("Connection acknowledged by aggregator.");

                if (!await SendUnsentDataAsync(stream, unsentData))
                {
                    Console.WriteLine("Failed to send unsent data. Exiting...");
                    return;
                }
                unsentData = Array.Empty<string>(); // Clear unsent data after sending

                // Simple text input loop
                while (true)
                {
                    Console.WriteLine("Menu:");
                    Console.WriteLine("1. Enviar dados");
                    Console.WriteLine("2. Enviar estado manutenção");
                    Console.WriteLine("3. Sair");
                    Console.Write("Escolha uma opção: ");
                    string? choice = Console.ReadLine();

                    switch (choice)
                    {
                        case "1":
                            var dataMessage = new[]
                            {
                                new { dataType = "temperature", value = GenerateRandomTemperature().ToString() },
                                new { dataType = "windSpeed", value = GenerateRandomWindSpeed().ToString() },
                            };

                            // Add data to unsentData array
                            unsentData = unsentData.Append(JsonSerializer.Serialize(dataMessage)).ToArray();

                            string dataSend = Protocol.CreateMessage(Protocol.DATA_SEND, JsonSerializer.Serialize(unsentData));
                            await SendAsync(stream, dataSend);

                            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                            {
                                try
                                {
                                    string dataReply = await ReadAsync(stream).WaitAsync(cts.Token);
                                    var (messageType, _) = Protocol.ParseMessage(dataReply);
                                    Console.WriteLine("Procotocol ACK: " + messageType);

                                    if (messageType != Protocol.DATA_ACK)
                                    {
                                        Console.WriteLine("Unexpected response. Saving data to unsentData.");
                                        unsentData = unsentData.Append(dataSend).ToArray();
                                        break;
                                    }

                                    unsentData = Array.Empty<string>(); // Clear unsent data after sending
                                }
                                catch (OperationCanceledException)
                                {
                                    Console.WriteLine("No acknowledgment received within 5 seconds. Saving data to unsentData.");
                                    unsentData = unsentData.Append(dataSend).ToArray();
                                    break;
                                }
                            }

                            break;

                        case "2":
                            string maintenanceMessage = Protocol.CreateMessage(Protocol.MAINTENANCE_STATE, wavyId);
                            await SendAsync(stream, maintenanceMessage);
                            string maintenanceReply = await ReadAsync(stream);
                            Console.WriteLine("Aggregator: " + maintenanceReply);
                            break;

                        case "3":
                            string discReq = Protocol.CreateMessage(Protocol.DISC_REQ, wavyId);
                            await SendAsync(stream, discReq);
                            Console.WriteLine("Disconnect request sent.");
                            return;

                        default:
                            Console.WriteLine("Opção inválida. Tente novamente.");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
            }
            finally
            {
                Console.WriteLine("unsentData: " + string.Join(", ", unsentData));

                Console.WriteLine("Reconnecting in 3 seconds...");
                await Task.Delay(3000); // Wait for 3 seconds before reconnecting
            }
        }
    }

    private static async Task<bool> SendUnsentDataAsync(NetworkStream stream, string[] unsentData)
    {
        bool error = false;
        if (unsentData.Length > 0)
                {
                    foreach (var data in unsentData)
                    {
                        string dataSend = Protocol.CreateMessage(Protocol.DATA_SEND, data);
                        await SendAsync(stream, dataSend);
                        string dataReply = await ReadAsync(stream);
                        Console.WriteLine("Data sent: " + dataReply);
                        var (type, _) = Protocol.ParseMessage(dataReply);

                        if (type!= Protocol.DATA_ACK)
                        {
                            error = true;
                            Console.WriteLine("Failed to send unsent data. Exiting...");
                            break;
                        }
                    }
                }
        return !error;

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
        Random random = new Random();
        return random.Next(-10, 40); // Simulate temperature between -10 and 40 degrees Celsius
    }
    private static int GenerateRandomWindSpeed()
    {
        Random random = new Random();
        return random.Next(0, 100); // Simula velocidade do vento entre 0 e 100 km/h
    }
}
