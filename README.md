# TeamsScout

Lesendes C#-Konsolenprogramm zur Diagnose der Windows-UI-Automation-Oberfläche von Microsoft Teams. Es findet Desktop-Hauptfenster anhand der Prozessnamen `ms-teams` und `Teams`. Standardmäßig erscheint eine kompakte Zusammenfassung statt des gesamten Baums. Auch separate Teams-Fenster werden erfasst, sofern ihr Prozess einen dieser Namen hat.

Die kompakte Ausgabe endet mit drei Angaben: eigener Profilstatus, **Ungelesene Nachrichten vorhanden** und **Unquittierte Aktivitäten vorhanden**. Benachrichtigungen werden als **Ja**, **Nein** oder **Nicht ermittelbar** ausgegeben. Ja setzt explizite Ungelesen-Texte in Name, ItemStatus oder HelpText eines Chat-/Activity-Navigationselements oder passenden Eintrags voraus. Nein setzt eine explizite globale Negativangabe an der jeweiligen Navigationsschaltfläche voraus; weder fehlende Badges noch nur gelesene sichtbare Chats beweisen Nein. Fehler oder Suchlimits verhindern eine negative Gesamtaussage. Widersprüchliche Hinweise ergeben Nicht ermittelbar.

Die hier beobachteten Navigation-AutomationIds sind in der Implementierung dokumentiert; andere Teams-Versionen können abweichen. Chatzeilen werden am englischen Präfix erkannt, Aktivitäteneinträge im Activity-Bereich an ListItem/TreeItem. Die Regeln für Ungelesen-Meldungen sind bisher synthetisch geprüft, nicht an einer realen neuen Benachrichtigung. Nachrichtenvorschauen ab ` Last message` und Befehle wie `Mark as unread` werden ausgeschlossen. Die Schaltfläche `Unread`/`Ungelesen` ist nur ein Filter und wird nicht als Nachricht gewertet. `Settings and more, new activity` ist keine offene Activity-Feed-Benachrichtigung. Unquittiert bezeichnet hier eine als ungelesen markierte Aktivität; eine gesonderte fachliche Quittierung kann UIA damit nicht belegen.

Der aktuelle Teams-Baum liefert keine eindeutigen Ungelesen-Merkmale; deshalb meldet das Tool derzeit für beide Benachrichtigungsarten Nicht ermittelbar. Die generische leere Feed-Anleitung wird nicht als globale Negativbestätigung gewertet. Eine verlässliche binäre Aussage für jeden Teams-Zustand ist mit den bisher beobachteten UIA-Daten noch nicht erreicht. Im Watch-Modus werden diese Angaben beim Start gelesen; spätere Intervallberichte enthalten Ereigniszahlen, keine erneute Bestandsprüfung.

Standardtiefe: 35, maximal 3000 untersuchte Knoten je Fenster. Die Traversierung besucht den Raw-UIA-Baum. Sichtbare Chatbeschriftungen erscheinen nur mit `--raw`, maximal zehn und ohne den Teil ab ` Last message`.

Mit `--raw` erscheint zusätzlich der vollständige untersuchte Baum mit Name, AutomationId, ControlType, Prozess-/RuntimeId und vom Provider gemeldeten Patterns, häufigsten ControlTypes und bis zu acht unbestätigten Texttreffern zu Ungelesen/Präsenz. Im kompakten Watch-Modus werden Änderungen höchstens alle fünf Sekunden als Zähler pro Intervall zusammengefasst, bei Ruhe bleibt die Ausgabe still. Beim Beenden erscheint ein letzter Intervallbericht. Einzelereignisse gibt es nur mit `watch --raw`.

Am Ende jeder Fensterdiagnose steht **Eigener Teams-Status: Verfügbar (laut UIA-Profilbutton)** oder der entsprechend erkannte Status. Als Quelle gilt ausschließlich ein Button mit dem expliziten Präfix `Your profile, status `, `Ihr Profil, Status ` oder `Dein Profil, Status `. Das englische Präfix wurde hier am laufenden Teams beobachtet; die deutschen Varianten sind noch nicht praktisch verifiziert. Kontaktstatus wird nicht als eigener Status verwendet. Bekannte englische/deutsche Werte werden auf Deutsch ausgegeben. Fehlende, unbekannte oder widersprüchliche Angaben werden ausdrücklich gekennzeichnet. Dies ist der angezeigte Profilstatus zum Lesezeitpunkt, keine unabhängige Prüfung der Erreichbarkeit. Im Watch-Modus gilt die Statuszeile für die Anfangsdiagnose; spätere Ereignisberichte enthalten weiterhin nur Zähler.

Keine Graph-API, keine Klicks, keine Tastatureingaben, kein Fokuswechsel und keine Ausführung von UIA-Patterns. Das Programm liest ausschließlich Eigenschaften/Pattern-Metadaten und registriert Ereignisbeobachter. Benachrichtigungsregeln bleiben auf explizite UIA-Angaben beschränkt.

## Voraussetzungen und Build

Windows mit interaktiver Desktopsitzung, laufendes Teams, Visual Studio 2022 ab 17.12 mit der Workload **.NET-Desktopentwicklung** und passendem .NET-9-SDK. Für das hier verwendete SDK 9.0.3xx Visual Studio 2022 auf 17.14 aktualisieren. `TeamsScout.sln` öffnen, TeamsScout als Startprojekt wählen und bauen. Die WPF-Frameworkreferenz stellt die Windows-UIA-Assemblies bereit; das Programm bleibt eine Konsole.

```powershell
dotnet build TeamsScout.sln --configuration Release
dotnet run --project TeamsScout --configuration Release -- --help
```

Standardziel ist `net9.0-windows`, da .NET 10 als Target in Visual Studio 2022 nicht unterstützt wird und hier nur SDK 9 installiert ist. Siehe [Microsoft: SDK/MSBuild/Visual-Studio-Versionen](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs). .NET 9 erreicht am 10. November 2026 das Supportende ([Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy)). Für längerfristige Nutzung auf .NET 10 und Visual Studio 2026 wechseln.

Optional mit installiertem .NET-10-SDK über die CLI (hier nicht verifiziert):

```powershell
dotnet build TeamsScout.sln -c Release -p:TargetFramework=net10.0-windows
dotnet .\TeamsScout\bin\Release\net10.0-windows\TeamsScout.dll dump
```

## Verwendung

```powershell
# Kompakte Diagnose; Standard: Tiefe 35, maximal 3000 Knoten je Fenster
dotnet run --project TeamsScout -c Release -- dump

# Tieferer Baum und optional gezielter Prozess aus dem Task-Manager
dotnet run --project TeamsScout -c Release -- dump --pid 12345 --depth 25 --max-nodes 10000

# Anfangsdiagnose plus zusammengefasste Änderungen, Ende mit Strg+C
dotnet run --project TeamsScout -c Release -- watch --depth 15

# Ausführlicher Rohbaum und Einzelereignisse nur bei Bedarf
dotnet run --project TeamsScout -c Release -- dump --raw
dotnet run --project TeamsScout -c Release -- watch --raw

# Direkt nach dem Build, getrennte Diagnose- und Fehlerdateien
New-Item -ItemType Directory -Force diagnostics
.\TeamsScout\bin\Release\net9.0-windows\TeamsScout.exe watch > diagnostics\watch.txt 2> diagnostics\errors.txt
```

`--pid` ersetzt die Prozessnamenerkennung und erfasst Desktop-Fenster genau dieser PID. Damit lassen sich beispielsweise Teams im Browser oder abweichende Hostprozesse ausdrücklich untersuchen. Es erfolgt keine Suche nach Fenstertiteln und keine automatische Interpretation von Browserinhalten. Tiefe 0 zeigt nur das Hauptfenster; zulässig sind 0–100, für das Knotenlimit 1–100000. Strings werden JSON-escaped, sodass Zeilenumbrüche in Elementnamen die Baumdarstellung nicht zerlegen.

`watch` beobachtet den Teilbaum aller beim Start gefundenen Fenster. Erfasst werden Strukturänderungen mit Änderungsart/RuntimeId sowie neue Werte für Name, AutomationId, ControlType, ItemStatus, HelpText, IsEnabled, IsOffscreen, HasKeyboardFocus, Value, ToggleState, IsSelected und ExpandCollapseState. Das sind ausgewählte Diagnoseeigenschaften, keine vollständige Aufzeichnung aller UIA-Eigenschaften. Im Rohmodus enthält jede Ereigniszeile einen Zeitstempel mit Zeitzone. Registrierung erfolgt vor der Anfangsdiagnose, diese ist jedoch kein atomarer Snapshot. Überläufe des begrenzten Ereignispuffers erscheinen im Intervallbericht. Beim Beenden werden die registrierten Handler entfernt.

Neu geöffnete Hauptfenster und ein Teams-Neustart benötigen einen Neustart von TeamsScout. Geschlossene/wechselnde Elemente können UIA-Fehler liefern; diese werden protokolliert. Entfernte Elemente können bereits beim Ereignis unlesbar sein. Einzelne blockierende UIA-Provideraufrufe haben keinen anwendungsseitigen Timeout; Knoten-/Tiefenlimits begrenzen die Traversierung, nicht die Laufzeit einzelner Aufrufe. Strg+C wird zwischen Aufrufen verarbeitet.

## Diagnoseablauf für ungelesene Chats und Präsenz

1. Teams öffnen und `dump` oder `watch` starten.
2. Selbst manuell zwischen einem gelesenen und ungelesenen Chat wechseln beziehungsweise den Präsenzstatus verändern.
3. Namen, ItemStatus/HelpText, unterstützte Patterns und Ereignisse vor/nach der Änderung vergleichen. Bei Bedarf Baumtiefe erhöhen oder einen neuen Snapshot aufnehmen.
4. Erst anhand tatsächlich beobachteter Daten Selektoren und fachliche Regeln entwerfen. AutomationIds, Übersetzungen und Struktur können sich zwischen Teams-Versionen unterscheiden; RuntimeIds gelten nur für die aktuelle Elementlebensdauer.

Teams/WebView kann nur materialisierte Elemente und eventuell keine Präsenzinformationen bereitstellen. Virtualisierte oder eingeklappte Bereiche erscheinen häufig erst nach eigener manueller Navigation. Ausbleibende UIA-Ereignisse beweisen keinen unveränderten Zustand. Ein unterstütztes Pattern allein beweist ebenfalls keine fachliche Bedeutung.

Bei leerer Ausgabe Teams öffnen, die PID prüfen und beide Programme mit gleichem Berechtigungsniveau in derselben Sitzung starten. Das Tool erhöht seine Rechte nicht selbst. Logs können Chattexte und personenbezogene Angaben enthalten; vor Weitergabe prüfen. `diagnostics/` ist deshalb vom Git-Tracking ausgeschlossen.

Exitcodes: `0` erfolgreich/normal beendet, `1` Argumentfehler, `2` kein passendes Fenster, `3` fataler Diagnosefehler. Teilfehler stehen auf stderr und können bei sonst erfolgreichem Lauf mit Exitcode 0 auftreten.

## Lokal geprüft

Mit SDK 9.0.319: Release-Build ohne Warnungen/Fehler, Hilfeausgabe, ungültige Tiefe mit Exitcode 1, nicht vorhandene PID mit Exitcode 2 sowie UIA-Snapshot eines laufenden Teams-Fensters mit Kindknoten und Patterns. Der Watch-Smoke-Test registrierte die Beobachter erfolgreich und gab den Anfangsbaum aus; anschließend wurde er über Strg+C beendet. Reale Eigenschafts-/Strukturänderungen durch Chat- oder Präsenzwechsel und der optionale .NET-10-Build sind noch nicht verifiziert.

Die kompakte Standarddiagnose wurde ebenfalls am laufenden Teams geprüft: 331 Knoten, keine Lesefehler, vollständige Traversierung innerhalb der Limits und etwa zwei Dutzend Ausgabezeilen statt hunderter Baumzeilen. `--raw --depth 0` und die aktualisierte Hilfe wurden geprüft.
