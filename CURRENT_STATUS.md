# TeamsScout – Stand für die Fortsetzung

Stand: 16.10.2026, Europe/Berlin.

## Auftrag und Arbeitsstand

C#-Konsolenprogramm namens TeamsScout für Windows und Visual Studio 2022. Ausschließlich lesender Zugriff über Windows UI Automation: keine Graph-API, automatischen Klicks, Tastatureingaben oder Fokuswechsel. Keine Pushes; der Benutzer pusht selbst.

Arbeitsbranch: `feat/teamsscout-uia-diagnostics`. Die neu erstellten Projektdateien sind derzeit noch nicht committed. Keine Commits oder Pushes wurden durch den Assistenten erstellt.

Solution: `TeamsScout.sln`. Standardziel: `net9.0-windows`, da hier SDK 9.0.319 installiert ist und .NET 10 als Ziel nicht von Visual Studio 2022 unterstützt wird. Optionaler .NET-10-CLI-Build ist im README beschrieben, aber nicht geprüft.

## Tatsächlich gewünschte Ausgabe

Der Benutzer benötigt insbesondere:

- Eigener Teams-Präsenzstatus.
- Ob ungelesene Nachrichten vorhanden sind.
- Ob unquittierte Aktivitäten vorhanden sind.

Die kompakte Ausgabe soll kurz sein und am Ende diese Ergebnisse liefern. Allgemeine Bedienelementlisten oder der Zustand der Schaltfläche „Unread“ beantworten die Frage nicht. Der vollständige Diagnosebaum bleibt optional über `--raw` verfügbar.

## Implementiert

- Erkennung von Teams-Desktopfenstern über die Prozesse `ms-teams` und `Teams`, alternativ `--pid`.
- Traversierung des Raw-UIA-Baums über Prozessgrenzen hinweg, mit Tiefen- und Knotenlimit. Standard: Tiefe 35, maximal 3000 Knoten je Fenster.
- Rohdarstellung mit Name, AutomationId, ControlType, unterstützten Patterns sowie Prozess-/RuntimeId.
- Kompakte Zusammenfassung mit Untersuchungsumfang und Suchgrenzen.
- `watch`: Eigenschafts- und Strukturereignisse, begrenzter Puffer, zusammengefasste Ereigniszahlen je Fünfsekundenintervall; Einzelereignisse mit `--raw`. Abbruch über Strg+C, Handlerbereinigung im finally-Block.
- Eigener Status aus dem expliziten Profilbutton, Implementierung in `TeamsScout/ProfileStatus.cs`. Beobachteter englischer Name: `Your profile, status Available `. Daraus wird `Eigener Teams-Status: Verfügbar (laut UIA-Profilbutton).` Deutsche Präfixvarianten sind implementiert, aber nicht praktisch verifiziert. Fehlende/widersprüchliche Angaben werden ausdrücklich gemeldet.
- Vorläufige Dreiwertlogik für Nachrichten/Aktivitäten in `TeamsScout/NotificationSummary.cs`: Ja / Nein / Nicht ermittelbar anhand expliziter UIA-Texte. Keine negative Aussage allein wegen fehlender Badges oder fehlender positiver Treffer. Globale Negativtexte werden nur an der jeweiligen Navigationsschaltfläche akzeptiert. Fehler/Suchlimits verhindern ein negatives Gesamtergebnis.
- `TeamsScout.Checks`: kleines ausführbares Prüfprojekt ohne externe Testpakete für die Benachrichtigungsregeln.
- README mit Build, Verwendung, Regeln und Grenzen; `.gitignore` schließt Builddateien und `diagnostics/` aus.

## Beobachtete UIA-Daten

- Teams verwendet eingebetteten Webinhalt (`RootWebArea`), in dieser Sitzung in einem anderen Prozess als das Hauptfenster. Der Baum ist erreichbar; die ursprüngliche Standardtiefe 12 war zu gering.
- Profilbutton-AutomationId: `idna-me-control-avatar-trigger`.
- Chat-Navigationsbutton-AutomationId: `86fcd49b-61a2-4701-b771-54728cd291fb`.
- Activity-Navigationsbutton-AutomationId: `14d6962d-6eeb-4f48-8890-de55454bb136`.
- Diese IDs wurden an dieser Teams-Instanz beobachtet; Versions-/Sprachunabhängigkeit ist nicht belegt.
- Sichtbare Chatzeilen sind TreeItems mit Namen wie `Chat …` oder `Group chat …`. Sie können Kontaktstatus und Nachrichtenvorschauen enthalten. Der Teil ab ` Last message` wird vor der Benachrichtigungsauswertung entfernt.
- Der eigene Profilstatus war `Available`. Ein anderer Chateintrag desselben Namens meldete `Offline`; Kontaktstatus darf nicht mit dem eigenen Profilstatus verwechselt werden.
- `Unread` ist ein Filterbutton mit TogglePattern, kein Beweis für ungelesene Nachrichten. Die Filterzeile wurde aus der kompakten Ausgabe entfernt.
- `Settings and more, new activity` gehört zum Einstellungsmenü und ist kein Beweis für offene Activity-Feed-Einträge.
- Zuletzt war die Activity-Ansicht geöffnet. Der Baum enthielt die generische Anleitung `You will see @mentions, reactions and other notifications here.` Diese wird vorsichtshalber nicht als globale Bestätigung „keine offenen Aktivitäten“ ausgewertet.

## Verifikation und aktuelles Ergebnis

- Debug- und Release-Build erfolgreich ohne Warnungen/Fehler.
- Snapshots am laufenden Teams, Hilfe, ungültige Argumente, nicht vorhandene PID, begrenzte Baumtiefe und Rohmodus geprüft.
- Watch-Registrierung, Anfangsausgabe und Abschlussbericht bei Strg+C praktisch geprüft. Reale Benachrichtigungsänderungen sind nicht verifiziert.
- Zwölf synthetische Prüfungen der Benachrichtigungslogik erfolgreich: positive/negative/fehlende/widersprüchliche Angaben, englische/deutsche Texte, Nullzählung, Ausschluss von Nachrichtenvorschauen und „Mark as unread“.
- Der letzte Release-Lauf untersuchte 126 Knoten der Activity-Ansicht ohne Lesefehler oder erreichte Suchlimits und lieferte:

```text
Eigener Teams-Status: Verfügbar (laut UIA-Profilbutton).
Ungelesene Nachrichten vorhanden: Nicht ermittelbar (kein eindeutiges UIA-Merkmal).
Unquittierte Aktivitäten vorhanden: Nicht ermittelbar (kein eindeutiges UIA-Merkmal).
```

**Die zuverlässige Erkennung von ungelesenen Nachrichten und unquittierten Aktivitäten ist noch nicht abgeschlossen.** Die positiven/negativen Benachrichtigungsregeln wurden bisher nicht an realen entsprechenden Zuständen verifiziert. „Build erfolgreich“ und synthetische Tests sind kein Nachweis dafür.

## Offene Punkte für später

Der Benutzer kann derzeit keine ungelesenen Nachrichten erzeugen, da kein Kommunikationspartner verfügbar ist, und möchte später fortsetzen. Jetzt keine weiteren Versuche oder Rückfragen dazu.

Bei der Fortsetzung:

1. Wenn echte ungelesene Nachrichten oder offene Aktivitäten verfügbar sind, UIA-Zustände davor/danach vergleichen. Nur manuelle Interaktionen durch den Benutzer; TeamsScout bleibt lesend.
2. Explizite Badge-/Zähler-/Statusmerkmale der Chat- und Activity-Navigation sowie der Einträge untersuchen, einschließlich Name, ItemStatus, HelpText und Unterelementen.
3. Vorläufige Textregeln gegen diese Daten absichern. Insbesondere verhindern, dass ein Chatname mit dem Wort „unread“ fälschlich als Ungelesen-Markierung gilt. Aktuell sind die Textregeln dafür noch heuristisch.
4. Eine belastbare globale Nein-Aussage muss auch virtualisierte, nicht sichtbare oder eingeklappte Einträge berücksichtigen. Ein vollständig traversierter aktueller UIA-Baum ist nicht zwingend die vollständige Chat-/Activity-Liste.
5. Bedeutung von „unquittiert“ beachten: Derzeit wird dies als „ungelesene Aktivität“ interpretiert. Eine gesonderte fachliche Quittierung wird nicht erkannt.
6. Watch liefert bisher nur beim Start den fachlichen Zustand, anschließend Ereigniszahlen. Eine laufende Aktualisierung der drei Ergebniszeilen ist noch offen.
7. Neue Hauptfenster oder ein Teams-Neustart werden nicht automatisch neu angebunden; Watch muss derzeit neu gestartet werden.

## Befehle

```powershell
dotnet build TeamsScout.sln -c Release
dotnet run --project TeamsScout -c Release -- dump
dotnet run --project TeamsScout -c Release -- dump --raw --depth 45 --max-nodes 3000
dotnet run --project TeamsScout -c Release -- watch
dotnet run --project TeamsScout.Checks
```

Diagnoselogs können personenbezogene Inhalte enthalten. Bei späteren Beispielen Nachrichtentexte und Namen nach Bedarf anonymisieren.
