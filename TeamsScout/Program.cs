using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;

namespace TeamsScout;

internal static class Program
{
    private static readonly ManualResetEventSlim Stop = new(false);
    private static readonly BlockingCollection<string> Events = new(2048);
    private static long dropped;

    // UIA calls and event registration run on a dedicated MTA console thread.
    [MTAThread]
    private static int Main(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            if (options.Help) { Console.WriteLine(Options.Usage); return 0; }
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; Stop.Set(); };
            var windows = FindWindows(options.Pid);
            if (windows.Count == 0)
            {
                Console.Error.WriteLine("Keine Teams-Fenster gefunden. Teams öffnen oder --pid <PID> angeben.");
                return 2;
            }
            Console.WriteLine($"TeamsScout | Fenster={windows.Count} | Ausgabe={(options.Raw ? "Rohbaum" : "Kompakt")} | {DateTimeOffset.Now:O}");
            if (options.Watch) Watch(windows, options);
            else foreach (var window in windows) Dump(window, options);
            return 0;
        }
        catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message + "\n" + Options.Usage); return 1; }
        catch (Exception ex) { Console.Error.WriteLine($"Diagnose fehlgeschlagen: {ex.GetType().Name}: {ex.Message}"); return 3; }
    }

    private static List<AutomationElement> FindWindows(int? pid)
    {
        var processes = new HashSet<int>();
        if (pid.HasValue) processes.Add(pid.Value);
        else
        {
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (process.ProcessName.Equals("ms-teams", StringComparison.OrdinalIgnoreCase) ||
                            process.ProcessName.Equals("Teams", StringComparison.OrdinalIgnoreCase))
                            processes.Add(process.Id);
                    }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception) { }
                }
            }
        }
        var result = new List<AutomationElement>();
        var desktop = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
        foreach (AutomationElement window in desktop)
        {
            try { if (processes.Contains(window.Current.ProcessId)) result.Add(window); }
            catch (Exception ex) when (IsUiaError(ex)) { Warn(ex); }
        }
        return result;
    }

    private static void Dump(AutomationElement root, Options options)
    {
        int count = 0, errors = 0, depthCuts = 0;
        bool nodeLimit = false, webContent = false;
        var types = new Dictionary<string, int>();
        var hints = new HashSet<string>();
        var ownStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var chats = new List<string>();
        var notifications = new NotificationSummary();
        var pending = new Stack<(AutomationElement Element, int Depth, bool ActivityFeed)>();
        pending.Push((root, 0, false));
        while (pending.Count > 0 && !Stop.IsSet)
        {
            var (element, depth, activityFeed) = pending.Pop();
            if (count >= options.MaxNodes) { nodeLimit = true; break; }
            count++;
            try
            {
                var current = element.Current;
                if (current.ControlType == ControlType.Button && ProfileStatus.TryRead(current.Name, out var ownStatus))
                    ownStatuses.Add(ownStatus);
                if (!current.IsOffscreen && current.ControlType == ControlType.TreeItem &&
                    (current.Name.StartsWith("Chat ", StringComparison.Ordinal) || current.Name.StartsWith("Group chat ", StringComparison.Ordinal)))
                {
                    string label = current.Name;
                    int preview = label.IndexOf(" Last message", StringComparison.Ordinal);
                    if (preview >= 0) label = label[..preview];
                    bool selected = element.GetCurrentPropertyValue(SelectionItemPattern.IsSelectedProperty, true) is true;
                    chats.Add($"{Quote(Short(label))}{(selected ? " [ausgewählt]" : "")}");
                }
                activityFeed |= current.ControlType == ControlType.Group && current.Name is "Activity" or "Aktivität" or "Aktivitäten";
                string type = current.ControlType.ProgrammaticName.Replace("ControlType.", "");
                types[type] = types.GetValueOrDefault(type) + 1;
                webContent |= current.ControlType == ControlType.Document;
                if (depth == 0) Console.WriteLine($"Fenster: {Quote(current.Name)} (PID {current.ProcessId})");
                if (options.Raw) Console.WriteLine(new string(' ', depth * 2) + Describe(element, true));
                string status = element.GetCurrentPropertyValue(AutomationElement.ItemStatusProperty, true) as string ?? "";
                string help = element.GetCurrentPropertyValue(AutomationElement.HelpTextProperty, true) as string ?? "";
                bool chatItem = current.ControlType == ControlType.TreeItem &&
                    (current.Name.StartsWith("Chat ", StringComparison.Ordinal) || current.Name.StartsWith("Group chat ", StringComparison.Ordinal));
                bool chatNavigation = current.ControlType == ControlType.Button &&
                    current.AutomationId == "86fcd49b-61a2-4701-b771-54728cd291fb";
                bool activityNavigation = current.ControlType == ControlType.Button &&
                    current.AutomationId == "14d6962d-6eeb-4f48-8890-de55454bb136";
                bool isFilter = current.ControlType == ControlType.Button && current.Name is "Unread" or "Ungelesen";
                if (!current.IsOffscreen && !isFilter)
                    notifications.Observe(current.Name, status, help, chatItem || chatNavigation,
                        activityNavigation || (activityFeed &&
                            (current.ControlType == ControlType.ListItem || current.ControlType == ControlType.TreeItem)),
                        global: chatNavigation || activityNavigation);
                string evidence = $"{current.Name} {status} {help}";
                if (hints.Count < 8 && new[] { "ungelesen", "unread", "verfügbar", "available", "beschäftigt", "busy", "abwesend", "away", "offline", "nicht stören", "do not disturb" }
                    .Any(word => evidence.Contains(word, StringComparison.OrdinalIgnoreCase)))
                    hints.Add($"{type}: Name={Quote(Short(current.Name))}" +
                        (status.Length > 0 ? $" ItemStatus={Quote(Short(status))}" : "") +
                        (help.Length > 0 ? $" HelpText={Quote(Short(help))}" : ""));
                if (depth >= options.Depth)
                {
                    if (TreeWalker.RawViewWalker.GetFirstChild(element) != null) depthCuts++;
                    continue;
                }
                // Push siblings in reverse order without recursive calls or a full subtree query.
                var children = new List<AutomationElement>();
                var child = TreeWalker.RawViewWalker.GetFirstChild(element);
                while (child != null && children.Count < options.MaxNodes && !Stop.IsSet)
                {
                    children.Add(child);
                    child = TreeWalker.RawViewWalker.GetNextSibling(child);
                }
                if (child != null) nodeLimit = true;
                for (int i = children.Count - 1; i >= 0; i--) pending.Push((children[i], depth + 1, activityFeed));
            }
            catch (Exception ex) when (IsUiaError(ex)) { errors++; if (options.Raw) Warn(ex); }
        }
        Console.WriteLine($"Ergebnis: {count} Knoten untersucht, {errors} Lesefehler. Web-Dokument: {(webContent ? "gefunden" : "nicht gefunden")}.");
        if (options.Raw)
        {
            Console.WriteLine("Häufigste Typen: " + string.Join(", ", types.OrderByDescending(pair => pair.Value).Take(6).Select(pair => $"{pair.Key}={pair.Value}")));
        }
        if (options.Raw)
        {
            Console.WriteLine("Texttreffer zu Ungelesen/Präsenz (unbestätigte Hinweise, max. 8):");
            foreach (string hint in hints) Console.WriteLine("  " + hint);
        }
        if (options.Raw)
        {
            Console.WriteLine($"Sichtbare Chateinträge: {chats.Count} (maximal 10 angezeigt)");
            foreach (string chat in chats.Take(10)) Console.WriteLine("  " + chat);
        }
        Console.WriteLine($"Abdeckung: Tiefenlimit an {depthCuts} Knoten; Knotenlimit {(nodeLimit ? "erreicht" : "nicht erreicht")}; Abbruch {(Stop.IsSet ? "ja" : "nein")}.");
        if (depthCuts > 0 || nodeLimit) Console.WriteLine("Hinweis: Untersuchung begrenzt; bei Bedarf --depth oder --max-nodes erhöhen.");
        Console.WriteLine(ownStatuses.Count switch
        {
            0 => "Eigener Teams-Status: Nicht ermittelbar (kein unterstützter Profilbutton gefunden).",
            1 => $"Eigener Teams-Status: {ownStatuses.Single()} (laut UIA-Profilbutton).",
            _ => "Eigener Teams-Status: Nicht eindeutig (abweichende Profilangaben: " + string.Join(", ", ownStatuses.Order()) + ")."
        });
        bool incomplete = errors > 0 || depthCuts > 0 || nodeLimit || Stop.IsSet;
        string Guard(string result) => incomplete && result.StartsWith("Nein", StringComparison.Ordinal)
            ? "Nicht ermittelbar (Untersuchung unvollständig)." : result;
        Console.WriteLine("Ungelesene Nachrichten vorhanden: " + Guard(notifications.Messages));
        Console.WriteLine("Unquittierte Aktivitäten vorhanden: " + Guard(notifications.Activities));
    }

    private static string Describe(AutomationElement element, bool patterns)
    {
        var current = element.Current;
        string supported = patterns
            ? " Patterns=[" + string.Join(", ", element.GetSupportedPatterns().Select(p => p.ProgrammaticName)) + "]"
            : "";
        return $"Name={Quote(current.Name)} AutomationId={Quote(current.AutomationId)} " +
               $"ControlType={current.ControlType.ProgrammaticName} PID={current.ProcessId} " +
               $"RuntimeId={Quote(string.Join('.', element.GetRuntimeId()))}{supported}";
    }

    private static void Watch(List<AutomationElement> windows, Options options)
    {
        AutomationPropertyChangedEventHandler property = (sender, e) => Enqueue(() =>
            $"PROPERTY {Describe((AutomationElement)sender, false)} Property={e.Property.ProgrammaticName} NewValue={Value(e.NewValue)}");
        StructureChangedEventHandler structure = (sender, e) => Enqueue(() =>
            $"STRUCTURE {Describe((AutomationElement)sender, false)} Change={e.StructureChangeType} AffectedRuntimeId={Quote(string.Join('.', e.GetRuntimeId() ?? []))}");
        var registeredProperties = new List<AutomationElement>();
        var registeredStructures = new List<AutomationElement>();
        try
        {
            foreach (var window in windows)
            {
                try
                {
                    Automation.AddAutomationPropertyChangedEventHandler(window, TreeScope.Subtree, property,
                        AutomationElement.NameProperty, AutomationElement.AutomationIdProperty,
                        AutomationElement.ControlTypeProperty, AutomationElement.ItemStatusProperty,
                        AutomationElement.HelpTextProperty, AutomationElement.IsEnabledProperty,
                        AutomationElement.IsOffscreenProperty, AutomationElement.HasKeyboardFocusProperty,
                        ValuePattern.ValueProperty, TogglePattern.ToggleStateProperty,
                        SelectionItemPattern.IsSelectedProperty, ExpandCollapsePattern.ExpandCollapseStateProperty);
                    registeredProperties.Add(window);
                    Automation.AddStructureChangedEventHandler(window, TreeScope.Subtree, structure);
                    registeredStructures.Add(window);
                }
                catch (Exception ex) when (IsUiaError(ex)) { Warn(ex); }
            }
            if (registeredProperties.Count == 0 && registeredStructures.Count == 0)
                throw new InvalidOperationException("Keine UIA-Ereignisregistrierung erfolgreich.");
            Console.WriteLine("Beobachtung aktiv. Abbruch: Strg+C. Neue Hauptfenster erfordern einen Neustart.");
            foreach (var window in windows) Dump(window, options);
            var interval = Stopwatch.StartNew();
            int properties = 0, structures = 0, other = 0;
            long lostTotal = 0;
            void Report()
            {
                Console.WriteLine($"Beobachtung {DateTimeOffset.Now:HH:mm:ss}: {properties} Eigenschafts-, {structures} Strukturmeldungen, {other} Quellenfehler, {lostTotal} verworfen.");
                properties = structures = other = 0;
                lostTotal = 0;
                interval.Restart();
            }
            while (!Stop.IsSet)
            {
                if (Events.TryTake(out var line, 250))
                {
                    if (options.Raw) Console.WriteLine(line);
                    if (line.Contains(" PROPERTY ")) properties++;
                    else if (line.Contains(" STRUCTURE ")) structures++;
                    else other++;
                }
                var lost = Interlocked.Exchange(ref dropped, 0);
                lostTotal += lost;
                if (interval.Elapsed.TotalSeconds >= 5 && (properties + structures + other > 0 || lostTotal > 0)) Report();
            }
            while (Events.TryTake(out var remaining))
            {
                if (options.Raw) Console.WriteLine(remaining);
                if (remaining.Contains(" PROPERTY ")) properties++;
                else if (remaining.Contains(" STRUCTURE ")) structures++;
                else other++;
            }
            lostTotal += Interlocked.Exchange(ref dropped, 0);
            Report();
            Console.WriteLine("Beobachtung beendet. Ereigniszahlen erlauben keine Aussage über ungelesene Chats oder Präsenz.");
        }
        finally
        {
            foreach (var window in registeredProperties)
                try { Automation.RemoveAutomationPropertyChangedEventHandler(window, property); }
                catch (Exception ex) when (IsUiaError(ex)) { Warn(ex); }
            foreach (var window in registeredStructures)
                try { Automation.RemoveStructureChangedEventHandler(window, structure); }
                catch (Exception ex) when (IsUiaError(ex)) { Warn(ex); }
        }
    }

    private static void Enqueue(Func<string> message)
    {
        try
        {
            if (!Stop.IsSet && !Events.TryAdd($"{DateTimeOffset.Now:O} {message()}")) Interlocked.Increment(ref dropped);
        }
        catch (Exception ex) when (IsUiaError(ex))
        {
            if (!Events.TryAdd($"{DateTimeOffset.Now:O} [UIA-Quelle nicht verfügbar: {ex.GetType().Name}]"))
                Interlocked.Increment(ref dropped);
        }
    }

    private static bool IsUiaError(Exception ex) => ex is ElementNotAvailableException or
        ElementNotEnabledException or COMException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException;
    private static void Warn(Exception ex) => Console.Error.WriteLine($"[UIA: {ex.GetType().Name}: {ex.Message}]");
    private static string Quote(string? value) => JsonSerializer.Serialize(value);
    private static string Short(string value) => value.Length <= 140 ? value : value[..140] + "…";
    private static string Value(object? value) => value switch
    {
        null => "null",
        string text => Quote(text),
        Array values => "[" + string.Join(",", values.Cast<object?>().Select(Value)) + "]",
        _ => Quote(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture))
    };
}

internal sealed record Options(bool Watch, int Depth, int MaxNodes, int? Pid, bool Help, bool Raw)
{
    public const string Usage = "TeamsScout [dump|watch] [--raw] [--pid <PID>] [--depth <0..100>] [--max-nodes <1..100000>]\n" +
        "Standard: kompakte Diagnose, Tiefe 35, maximal 3000 Knoten. --raw zeigt Baum und Einzelereignisse. --help zeigt diese Hilfe.";

    public static Options Parse(string[] args)
    {
        bool watch = false, help = false, modeSeen = false, raw = false;
        int depth = 35, nodes = 3000;
        int? pid = null;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg is "--help" or "-h") { help = true; continue; }
            if (arg == "--raw") { raw = true; continue; }
            if (arg is "dump" or "watch")
            {
                if (modeSeen) throw new ArgumentException("Nur einen Modus angeben.");
                modeSeen = true; watch = arg == "watch"; continue;
            }
            if (arg is not ("--pid" or "--depth" or "--max-nodes"))
                throw new ArgumentException($"Unbekanntes Argument: {arg}");
            if (++i >= args.Length || !int.TryParse(args[i], out int value))
                throw new ArgumentException($"Ganzzahl für {arg} fehlt oder ist ungültig.");
            int min = arg == "--depth" ? 0 : 1;
            int max = arg == "--depth" ? 100 : arg == "--max-nodes" ? 100000 : int.MaxValue;
            if (value < min || value > max) throw new ArgumentException($"{arg}: erlaubt {min} bis {max}.");
            switch (arg) { case "--pid": pid = value; break; case "--depth": depth = value; break; default: nodes = value; break; }
        }
        return new(watch, depth, nodes, pid, help, raw);
    }
}
