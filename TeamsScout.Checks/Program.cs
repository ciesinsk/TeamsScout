using TeamsScout;

static void Check(string expected, NotificationSummary summary, bool activity = false)
{
    string actual = activity ? summary.Activities : summary.Messages;
    if (!actual.StartsWith(expected, StringComparison.Ordinal))
        throw new Exception($"Erwartet {expected}, erhalten {actual}");
}

var summary = new NotificationSummary();
Check("Nicht ermittelbar", summary);
summary.Observe("Settings and more, new activity", "", "", false, false);
Check("Nicht ermittelbar", summary, true);
summary.Observe("Chat Alice Last message unread messages", "", "", true, false);
Check("Nicht ermittelbar", summary);
summary.Observe("Chat, 2 unread messages", "", "", true, false);
Check("Ja", summary);
Check("Nicht ermittelbar", summary, true);
summary.Observe("Activity, 1 unread notification", "", "", false, true);
Check("Ja", summary, true);
var empty = new NotificationSummary();
empty.Observe("Chat, no unread messages", "", "", true, false, global: true);
Check("Nein", empty);
empty.Observe("Chat Alice, unread", "", "", true, false);
Check("Nicht ermittelbar", empty);
var german = new NotificationSummary();
german.Observe("Aktivität, keine ungelesenen Benachrichtigungen", "", "", false, true, global: true);
Check("Nein", german, true);
german.Observe("Aktivität, ungelesene Benachrichtigung", "", "", false, true);
Check("Nicht ermittelbar", german, true);
var zero = new NotificationSummary();
zero.Observe("Chat, 0 unread messages", "", "", true, false, global: true);
Check("Nein", zero);
var command = new NotificationSummary();
command.Observe("Chat Alice", "", "Mark as unread", true, false);
Check("Nicht ermittelbar", command);
Console.WriteLine("Alle 12 Benachrichtigungsprüfungen erfolgreich.");
