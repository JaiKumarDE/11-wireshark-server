using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

class Program
{
    private static readonly List<SslStream> clients = new();
    private static readonly object lockObject = new();
    private static string serverUsername = "Server";
    private const int DefaultPort = 5050;

    static void Main(string[] args)
    {
        Console.Write("Möchtest du den Server oder Client starten? (s/c): ");
        string? choice = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (choice == "s")
        {
            Console.Write("Wähle einen Benutzernamen für den Server: ");
            serverUsername = Console.ReadLine()?.Trim() ?? "Server";
            if (string.IsNullOrWhiteSpace(serverUsername))
                serverUsername = "Server";

            StartServer();
        }
        else if (choice == "c")
        {
            StartClient();
        }
        else
        {
            Console.WriteLine("Ungültige Eingabe.");
        }
    }

    private static void StartServer()
    {
        try
        {
            int port = ResolvePort();
            string certificatePath = ResolveCertificatePath(
                "SOCKETSERVER_P12_PATH",
                "certificate.p12");
            string password = ReadPassword("Passwort für das PKCS#12-Zertifikat (leer, falls keines gesetzt): ");

            using var serverCertificate = new X509Certificate2(
                certificatePath,
                password);

            if (!serverCertificate.HasPrivateKey)
                throw new InvalidOperationException("Das PKCS#12-Zertifikat enthält keinen privaten Schlüssel.");

            var server = new TcpListener(IPAddress.Any, port);
            server.Start();
            Console.WriteLine($"[{serverUsername}] TLS-Server auf Port {port} gestartet.");
            Console.WriteLine($"Zertifikat: {certificatePath}");

            var sendThread = new Thread(ServerSendMessages) { IsBackground = true };
            sendThread.Start();

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                var clientThread = new Thread(() => HandleClient(client, serverCertificate)) { IsBackground = true };
                clientThread.Start();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("[Server Fehler]: " + e.Message);
        }
    }

    private static void HandleClient(TcpClient client, X509Certificate2 serverCertificate)
    {
        using (client)
        using (var stream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false))
        {
            try
            {
                stream.AuthenticateAsServer(new SslServerAuthenticationOptions
                {
                    ServerCertificate = serverCertificate,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                });

                lock (lockObject)
                    clients.Add(stream);

                Console.WriteLine("[Server] Neuer TLS-Client verbunden.");
                byte[] buffer = new byte[1024];
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine(message);
                    BroadcastMessage(message, stream);
                }
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("[Server] TLS-Verbindung abgelehnt: " + e.Message);
            }
            catch (IOException e)
            {
                Console.WriteLine("[Server] Client-Verbindung beendet: " + e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("[Server] Client-Fehler: " + e.Message);
            }
            finally
            {
                lock (lockObject)
                    clients.Remove(stream);
            }
        }
    }

    private static void ServerSendMessages()
    {
        while (true)
        {
            Console.Write($"[{serverUsername}]: ");
            string? message = Console.ReadLine();
            if (message is null)
                return;

            if (!string.IsNullOrWhiteSpace(message))
                BroadcastMessage($"[{serverUsername}]: {message}", null);
        }
    }

    private static void BroadcastMessage(string message, SslStream? sender)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        lock (lockObject)
        {
            foreach (SslStream client in clients)
            {
                if (client == sender)
                    continue;

                try
                {
                    client.Write(data, 0, data.Length);
                }
                catch (IOException e)
                {
                    Console.WriteLine("[Server] Nachricht konnte nicht gesendet werden: " + e.Message);
                }
                catch (ObjectDisposedException)
                {
                    // The client is already being removed by its handler.
                }
            }
        }
    }

    private static void StartClient()
    {
        try
        {
            Console.Write("Gib die IP-Adresse oder den Hostnamen des Servers ein: ");
            string? serverHost = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(serverHost))
                throw new ArgumentException("Es wurde kein Server angegeben.");

            Console.Write("Wähle einen Benutzernamen: ");
            string username = Console.ReadLine()?.Trim() ?? "Client";
            if (string.IsNullOrWhiteSpace(username))
                username = "Client";

            string certificatePath = ResolveCertificatePath(
                "SOCKETSERVER_CERT_PATH",
                "certificate.crt");
            int port = ResolvePort();
            using var pinnedCertificate = X509Certificate2.CreateFromPemFile(certificatePath);
            var client = new TcpClient(serverHost, port);
            using (client)
            using (var stream = new SslStream(
                client.GetStream(),
                leaveInnerStreamOpen: false,
                (_, certificate, _, _) => IsPinnedCertificateValid(certificate, pinnedCertificate)))
            {
                stream.AuthenticateAsClient(new SslClientAuthenticationOptions
                {
                    TargetHost = serverHost,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                });
                Console.WriteLine("[Client] TLS-Verbindung mit geprüftem Serverzertifikat hergestellt.");

                var receiveThread = new Thread(() =>
                {
                    byte[] buffer = new byte[1024];
                    try
                    {
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            Console.WriteLine("\n" + message);
                            Console.Write("> ");
                        }
                    }
                    catch (IOException e)
                    {
                        Console.WriteLine("[Client] Verbindung beendet: " + e.Message);
                    }
                }) { IsBackground = true };

                receiveThread.Start();
                while (true)
                {
                    Console.Write("> ");
                    string? message = Console.ReadLine();
                    if (message is null)
                        break;
                    if (string.IsNullOrWhiteSpace(message))
                        continue;

                    string fullMessage = $"[{username}]: {message}";
                    byte[] data = Encoding.UTF8.GetBytes(fullMessage);
                    stream.Write(data, 0, data.Length);
                }
            }
        }
        catch (AuthenticationException e)
        {
            Console.WriteLine("[Client] TLS-Prüfung fehlgeschlagen: " + e.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine("[Client Fehler]: " + e.Message);
        }
    }

    private static bool IsPinnedCertificateValid(
        X509Certificate? certificate,
        X509Certificate2 pinnedCertificate)
    {
        if (certificate is null)
            return false;

        using var presentedCertificate = new X509Certificate2(certificate);
        byte[] expectedHash = pinnedCertificate.GetCertHash(HashAlgorithmName.SHA256);
        byte[] actualHash = presentedCertificate.GetCertHash(HashAlgorithmName.SHA256);
        DateTime now = DateTime.UtcNow;

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash)
            && now >= presentedCertificate.NotBefore.ToUniversalTime()
            && now <= presentedCertificate.NotAfter.ToUniversalTime();
    }

    private static string ResolveCertificatePath(string environmentVariable, string fileName)
    {
        string? configuredPath = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            string fullPath = Path.GetFullPath(configuredPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Zertifikatsdatei nicht gefunden: {fullPath}");
            return fullPath;
        }

        string relativePath = Path.Combine("PK12_SSL", "ssl-projekt", fileName);
        string[] startPaths = { Environment.CurrentDirectory, AppContext.BaseDirectory };
        foreach (string startPath in startPaths)
        {
            for (DirectoryInfo? directory = new(startPath); directory is not null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException(
            $"Zertifikat {relativePath} nicht gefunden. Setze bei Bedarf {environmentVariable} auf den Dateipfad.");
    }

    private static int ResolvePort()
    {
        string? configuredPort = Environment.GetEnvironmentVariable("SOCKETSERVER_PORT");
        if (string.IsNullOrWhiteSpace(configuredPort))
            return DefaultPort;

        if (!int.TryParse(configuredPort, out int port)
            || port <= IPEndPoint.MinPort
            || port > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(
                "SOCKETSERVER_PORT",
                "SOCKETSERVER_PORT muss eine Zahl zwischen 1 und 65535 sein.");
        }

        return port;
    }

    private static string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        if (Console.IsInputRedirected)
            return Console.ReadLine() ?? string.Empty;

        var password = new StringBuilder();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                    password.Length--;
                continue;
            }

            if (!char.IsControl(key.KeyChar))
                password.Append(key.KeyChar);
        }
    }
}
