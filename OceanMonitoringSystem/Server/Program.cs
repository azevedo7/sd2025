using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OceanMonitoringSystem.Common;

class Server
{
    public static async Task Main()
    {
        TcpListener server = null;

        try
        {
            IPAddress ip = IPAddress.Parse("127.0.0.1");
            int port = 8080;

            // Create the tcp listener server and start
            server = new TcpListener(ip, port);
            server.Start();

            while (true)
            {
                Console.WriteLine("Waiting for a connection");

                TcpClient client = await server.AcceptTcpClientAsync();
                Console.WriteLine("Connected!");

                // Get a stream object for reading and writing
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }
        catch (FormatException ex)
        {
            Console.WriteLine("Invalid IP address format: " + ex.Message);
        }
        catch (SocketException ex)
        {
            Console.WriteLine("Socket error: " + ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Unexpected error: " + ex.Message);
        }
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            Byte[] buffer = new Byte[1024];
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
                            // Establish connection

                            response = Protocol.CreateMessage(Protocol.CONN_ACK, "Connection established");
                            break;
                        case Protocol.DATA_SEND:
                            // Process the data payload
                            Console.WriteLine("Data received: {0}", payload);
                            response = Protocol.CreateMessage(Protocol.DATA_ACK, "Data received");
                            break;
                        case Protocol.DISC_REQ:
                            response = Protocol.CreateMessage(Protocol.DISC_ACK, "Disconnecting");
                            break;
                        default:
                            response = Protocol.CreateMessage("ERROR", "Unknown message type");
                            break;
                    }
                    Console.WriteLine($"{messageType} {payload}");
                    // Echo the data back to the client
                    byte[] responseBytes = Encoding.ASCII.GetBytes(response);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                }

            }
        }
        catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx)
        {
            Console.WriteLine($"Client disconnected unexpectedly: {socketEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error handling client: " + ex.Message);
        }
        finally
        {
            client.Close();
        }
    }
}
