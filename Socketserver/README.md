# Socketserver mit TLS verwenden

## Voraussetzungen

- Das .NET 8 SDK ist installiert (`dotnet --version`).
- Im Repository liegen die Zertifikate standardmäßig unter
  `PK12_SSL/ssl-projekt/`:
  - `certificate.p12` wird nur vom Server benötigt und muss den privaten
    Schlüssel enthalten.
  - `certificate.crt` wird von jedem Client benötigt. Es muss zum Zertifikat
    in der `.p12`-Datei passen.

Die `.p12`-Datei und den privaten Schlüssel niemals an Clients weitergeben.
Verteile nur die `.crt`-Datei über einen vertrauenswürdigen Weg.

## Server starten

Im Stammverzeichnis des Repositorys ausführen:

```sh
dotnet run --project Socketserver/Server.cs/Server.cs.csproj
```

Bei der Frage `Möchtest du den Server oder Client starten? (s/c):` `s`
eingeben, danach den gewünschten Server-Benutzernamen. Anschließend das
Passwort für die `.p12`-Datei eingeben. Ist die Datei nicht passwortgeschützt,
kann die Eingabe leer bleiben. Während der Server läuft, können Nachrichten
direkt in diesem Terminal eingegeben werden.

Der Server lauscht standardmäßig auf Port `5000` auf allen
Netzwerkschnittstellen. Falls der Port bereits belegt ist (auf macOS kann
beispielsweise `ControlCenter` Port 5000 verwenden), kann für Server und
Clients derselbe alternative Port gesetzt werden. Beispiel für macOS/Linux:

```sh
export SOCKETSERVER_PORT=5001
```

Danach den Server und alle Clients in Terminals starten, in denen diese
Variable gesetzt ist. Für Verbindungen von anderen Geräten muss der gewählte
Port in der Firewall freigegeben sein.

## Client verbinden

Auf jedem Client ebenfalls im Repository-Stammverzeichnis den Startbefehl
ausführen:

```sh
dotnet run --project Socketserver/Server.cs/Server.cs.csproj
```

Bei der Auswahl `c` eingeben, anschließend die IP-Adresse oder den Hostnamen
des Server-Rechners und einen Benutzernamen. Nachrichten können danach im
Terminal gesendet werden. Der Client prüft das Serverzertifikat gegen
`certificate.crt`; bei einem nicht passenden oder abgelaufenen Zertifikat wird
die TLS-Verbindung abgelehnt.

## Andere Zertifikatspfade verwenden

Standardmäßig sucht das Programm nach Zertifikaten in
`PK12_SSL/ssl-projekt/`. Falls die Dateien woanders liegen, lassen sich die
Pfade über Umgebungsvariablen setzen. Für macOS/Linux beispielsweise:

```sh
export SOCKETSERVER_P12_PATH="/pfad/zum/certificate.p12"
export SOCKETSERVER_CERT_PATH="/pfad/zum/certificate.crt"
```

Der Server verwendet `SOCKETSERVER_P12_PATH`, der Client
`SOCKETSERVER_CERT_PATH`. Die Variablen müssen im jeweiligen Terminal gesetzt
sein, bevor das Programm gestartet wird.

Das Passwort wird bei der Eingabe nicht angezeigt; nach der Eingabe mit
`Enter` bestätigen. Für das mitgelieferte Zertifikat ist das Passwort
`1234`.
