using System;
using System.Net.Sockets;

public class Aggregator
{
    /// <summary>
    /// Entry point for the Aggregator application.
    /// </summary>
    /// <param name="args">
    /// Command line arguments:
    /// args[0] - (Optional) Aggregator ID (int). Default is 0.
    /// args[1] - (Optional) Server IP address (string). Default is "127.0.0.1".
    /// args[2] - (Optional) Server port (int). Default is 8080.
    /// </param>
    public static void Main(string[] args)
    {
        int aggregatorId = 0;
        string serverIp = "127.0.0.1";
        int serverPort = 8080;

        if (args.Length >= 1) aggregatorId = int.Parse(args[0]);
        if (args.Length >= 2) serverIp = args[1];
        if (args.Length >= 3) serverPort = int.Parse(args[2]);

        Connect(serverIp, "Hello from aggregator!");
    }

    private static void Connect(String server, String message)
    {
        try
        {
            // Create a TcpClient.
            Int32 port = 8080;

            // Prefer a using declaration to ensure the instance is Disposed later.
            using TcpClient client = new TcpClient(server, port);

            //// Translate the passed message into ASCII and store it as a Byte array.
            //Byte[] data = System.Text.Encoding.ASCII.GetBytes(message);

            //// Get a client stream for reading and writing.
            //NetworkStream stream = client.GetStream();

            //// Send the message to the connected TcpServer.
            //stream.Write(data, 0, data.Length);

            //Console.WriteLine("Sent: {0}", message);

            //// Receive the server response.

            //// Buffer to store the response bytes.
            //data = new Byte[256];

            //// String to store the response ASCII representation.
            //String responseData = String.Empty;

            //// Read the first batch of the TcpServer response bytes.
            //Int32 bytes = stream.Read(data, 0, data.Length);
            //responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
            //Console.WriteLine("Received: {0}", responseData);

            while (true)
            {
                // Get user input
                string userInput = Console.ReadLine();
                if (userInput == "exit")
                    break;

                // Send the message to the server
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(userInput);
                NetworkStream stream = client.GetStream();

                stream.Write(data, 0, data.Length);
                Console.WriteLine("Sent: {0}", userInput);

            }
        }
        catch (ArgumentNullException e)
        {
            Console.WriteLine("ArgumentNullException: {0}", e);
        }
        catch (SocketException e)
        {
            Console.WriteLine("SocketException: {0}", e);
        }

        Console.WriteLine("\n Press Enter to continue...");
        Console.Read();
    }
}