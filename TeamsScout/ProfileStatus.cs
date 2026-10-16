namespace TeamsScout;

internal static class ProfileStatus
{
    // Only read an explicit own-profile label, never a contact's presence text.
    public static bool TryRead(string name, out string status)
    {
        status = "";
        string[] prefixes = ["Your profile, status ", "Ihr Profil, Status ", "Dein Profil, Status "];
        foreach (string prefix in prefixes)
        {
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            string value = name[prefix.Length..].Trim();
            if (value.Length == 0) return false;
            status = value.ToLowerInvariant() switch
            {
                "available" or "verfügbar" => "Verfügbar",
                "busy" or "beschäftigt" => "Beschäftigt",
                "do not disturb" or "nicht stören" => "Nicht stören",
                "be right back" or "bin gleich zurück" or "gleich zurück" => "Bin gleich zurück",
                "away" or "abwesend" => "Abwesend",
                "appear away" or "als abwesend anzeigen" => "Als abwesend anzeigen",
                "offline" => "Offline",
                "appear offline" or "als offline anzeigen" => "Als offline anzeigen",
                _ => "Unbekannte Statusangabe: " + System.Text.Json.JsonSerializer.Serialize(
                    value.Length <= 140 ? value : value[..140] + "…")
            };
            return true;
        }
        return false;
    }
}
