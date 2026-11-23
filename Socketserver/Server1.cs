using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    static void Main()
    {
        TcpListener server = new TcpListener(IPAddress.Any, 5000);
        server.Start();
        Console.WriteLine("Server gestartet auf Port 5000...");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            Thread clientThread = new Thread(HandleClient);
            clientThread.Start(client);
        }
    }

    static void HandleClient(object obj)
    {
        TcpClient client = (TcpClient)obj;
        NetworkStream stream = client.GetStream();
        byte[] buffer = Encoding.ASCII.GetBytes("Willkommen!\r\n");
        stream.Write(buffer, 0, buffer.Length);

        while (true)
        {
            try
            {
                buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;
                
                string receivedText = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Console.WriteLine("Client: " + receivedText.Trim());
                
                byte[] response = Encoding.ASCII.GetBytes("Empfangen: " + receivedText);
                stream.Write(response, 0, response.Length);
            }
            catch
            {
                break;
            }
        }
        
        client.Close();
    }
}
