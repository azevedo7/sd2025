using System;
using System.Net.Sockets;
using System.Text;
using OceanMonitoringSystem.Common;
using System.Text.Json;


class Wavy
{
    public static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: Wavy <aggregator_ip> <wavy_id> [<data_types]?");
            return;
        }

        string aggregatorIp = args[0];
        string wavyId = args[1];

        string dataTypes = args.Length > 2 ? args[2] : ""; // Default to empty string if not provided
        // [temperature, humidity, pressure] something like this
        //string dataTypes = "[temperature, humidity, pressure]";
        
        string[] dataTypesArray = dataTypes.Trim('[', ']').Split(',');

        int aggregatorPort = 9000;

        try
        {
            using TcpClient client = new TcpClient();
            await client.ConnectAsync(aggregatorIp, aggregatorPort);
            using NetworkStream stream = client.GetStream();

            Console.WriteLine($"Connected to aggregator at {aggregatorIp}:{aggregatorPort}");

            // Send CONN_REQ with ID
            string connReq;
            if (!string.IsNullOrEmpty(dataTypes)){
                connReq = Protocol.CreateMessage(Protocol.CONN_REQ, $"{wavyId}|${dataTypes}");
            } else
            {
                connReq = Protocol.CreateMessage(Protocol.CONN_REQ, wavyId);
            }

            await SendAsync(stream, connReq);

            // Listen for response
            string response = await ReadAsync(stream);
            Console.WriteLine("Aggregator: " + response);

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
                        while (true)
                        {
                            Console.WriteLine("Escolha o formato de saída:");
                            Console.WriteLine("1. JSON");
                            Console.WriteLine("2. CSV");
                            Console.WriteLine("3. XML");
                            Console.Write("Formato: ");
                            string? formatChoice = Console.ReadLine();

                            var dataMessage = new[]
                            {
                                new { dataType = "temperature", value = GenerateRandomTemperature().ToString() },
                                new { dataType = "windSpeed", value = GenerateRandomWindSpeed().ToString() },
                            };

                            string serializedData;
                            switch (formatChoice)
                            {
                                case "1":
                                    Console.WriteLine("Formato JSON selecionado.");
                                    serializedData = JsonSerializer.Serialize(dataMessage);
                                    break;
                                case "2":
                                    Console.WriteLine("Formato CSV selecionado.");
                                    serializedData = SerializeToCsv(dataMessage);
                                    break;
                                case "3":
                                    Console.WriteLine("Formato XML selecionado.");
                                    serializedData = SerializeToXml(dataMessage);
                                    break;
                                default:
                                    Console.WriteLine("Opção inválida. Tente novamente.");
                                    continue;
                            }

                            string dataSend = Protocol.CreateMessage($"{Protocol.DATA_SEND}|{formatChoice}", serializedData);

                            await SendAsync(stream, dataSend);

                            string dataReply = await ReadAsync(stream);
                            Console.WriteLine("Aggregator: " + dataReply);
                            break;
                        }
                        break;
                        //Console.Write("Digite os dados para enviar (envie nada para voltar): ");
                        //string dataInput = Console.ReadLine();

                        //if(dataInput == null || dataInput.Length == 0)
                        //{
                        //    break;
                        //}

                        //string dataSend = Protocol.CreateMessage(Protocol.DATA_SEND, dataInput);
                        //await SendAsync(stream, dataSend);

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
            Console.WriteLine("Error connecting to aggregator: " + ex.Message);
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
        Random random = new Random();
        return random.Next(-10, 40); // Simulate temperature between -10 and 40 degrees Celsius
    }
    private static int GenerateRandomWindSpeed()
    {
        Random random = new Random();
        return random.Next(0, 100); // Simula velocidade do vento entre 0 e 100 km/h
    }
    private static string SerializeToCsv(object[] data)
    {
        var csvBuilder = new StringBuilder();
        foreach (var item in data)
        {
            var properties = item.GetType().GetProperties();
            var values = properties.Select(p => p.GetValue(item)?.ToString() ?? string.Empty);
            csvBuilder.AppendLine(string.Join(",", values));
        }
        return csvBuilder.ToString();
    }
    private static string SerializeToXml(object[] data)
    {
        var serializer = new System.Xml.Serialization.XmlSerializer(data.GetType());
        using var stringWriter = new StringWriter();
        serializer.Serialize(stringWriter, data);
        return stringWriter.ToString();
    }
}
