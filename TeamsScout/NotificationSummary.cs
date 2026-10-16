using System.Text.RegularExpressions;

namespace TeamsScout;

// These rules use explicit notification wording, never absence of a badge.
internal sealed class NotificationSummary
{
    private readonly HashSet<string> messages = [];
    private readonly HashSet<string> activities = [];
    private bool noMessages, noActivities;

    public void Observe(string name, string status, string help, bool chat, bool activity, bool global = false)
    {
        if (!chat && !activity) return;
        // Chat previews can contain arbitrary words supplied by a sender.
        int preview = name.IndexOf(" Last message", StringComparison.OrdinalIgnoreCase);
        if (preview >= 0) name = name[..preview];
        foreach (string text in new[] { name, status, help })
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            bool negative = Regex.IsMatch(text,
                @"\b(no unread (messages|chats|notifications|activities)|0 unread (messages|chats|notifications|activities)|keine ungelesenen (nachrichten|chats|benachrichtigungen|aktivitäten)|0 ungelesene (nachrichten|chats|benachrichtigungen|aktivitäten))\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (negative)
            {
                if (chat && global) noMessages = true;
                if (activity && global) noActivities = true;
                continue;
            }
            if (Regex.IsMatch(text, @"\b(mark as unread|als ungelesen markieren)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) continue;
            // A standalone Unread button is a filter and is deliberately excluded by the caller.
            if (Regex.IsMatch(text, @"\b(unread|ungelesen\w*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                if (chat) messages.Add(text);
                if (activity) activities.Add(text);
            }
        }
    }

    public string Messages => Format(messages, noMessages);
    public string Activities => Format(activities, noActivities);

    private static string Format(HashSet<string> positive, bool negative) => (positive.Count > 0, negative) switch
    {
        (true, true) => "Nicht ermittelbar (widersprüchliche UIA-Angaben).",
        (true, false) => "Ja (explizite Ungelesen-Markierung in UIA).",
        (false, true) => "Nein (explizite UIA-Angabe: keine ungelesenen Einträge).",
        _ => "Nicht ermittelbar (kein eindeutiges UIA-Merkmal)."
    };
}
