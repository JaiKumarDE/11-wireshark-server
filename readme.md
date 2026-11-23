### UML-Klassendiagramm – Beschreibung

Das Klassendiagramm zeigt den Aufbau unserer verschlüsselten Chat-Anwendung.

- **ChatClient:** Stellt die Verbindung zum Server her und ermöglicht das Senden und Empfangen von Nachrichten.
- **ChatServer:** Wartet auf Verbindungen von Clients und verwaltet die Kommunikation.
- **ClientHandler:** Kümmert sich um die Kommunikation mit einem verbundenen Client.
- **TlsManager:** Verwaltet die TLS-Verschlüsselung und überprüft das Zertifikat.

Die Kommunikation zwischen Client und Server erfolgt über eine verschlüsselte Verbindung. Dadurch können fremde Personen die übertragenen Nachrichten nicht einfach mitlesen.