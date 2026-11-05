using System.Globalization;

namespace E33ModEditor.Core.SaveGame;

/// <summary>Cheap summary fields shown next to each save in the picker.</summary>
public sealed class SaveSummary
{
    public string StoryPoint { get; init; } = "";
    public string Location { get; init; } = "";
    public int PartyLevel { get; init; }
    public string TimePlayed { get; init; } = "";
    public ulong PlaytimeSeconds { get; init; }
}

/// <summary>Extracts <see cref="SaveSummary"/> data from a parsed save.</summary>
public static class SaveScan
{
    public static SaveSummary Summarize(GvasFile file)
    {
        var story = StoryPoint(file);
        var location = Location(file);
        var (partyLevel, _) = PartyLevels(file);
        var seconds = PlaytimeSeconds(file);
        return new SaveSummary
        {
            StoryPoint = story,
            Location = location,
            PartyLevel = partyLevel,
            PlaytimeSeconds = (ulong)Math.Max(0, seconds),
            TimePlayed = FormatPlaytime(seconds),
        };
    }

    /// <summary>
    /// The current main-story objective. The golden-path quest carries every
    /// story chapter as an objective (1_LumiereBeginning … 17_RenoirDefeated),
    /// so its in-progress objective is far more useful than the quest name —
    /// "Monoco Station", not "Golden Path". Falls back to the last started
    /// Main_* quest for saves that predate the objective map.
    /// </summary>
    public static string StoryPoint(GvasFile file)
    {
        var statuses = SaveGameEditor.FindMap(file, "QuestStatuses");
        if (statuses is null) return "";
        string? active = null, touched = null, golden = null;
        foreach (var e in statuses)
        {
            var key = e.Key.Payload as string ?? "";
            if (!key.StartsWith("Main_", StringComparison.OrdinalIgnoreCase)) continue;
            var status = QuestStatusLabel(e.Value.Payload);
            if (status is null || status.EndsWith("NewEnumerator0", StringComparison.Ordinal)) continue;
            touched = Shorten(key);
            if (status.EndsWith("NewEnumerator1", StringComparison.Ordinal)) active = Shorten(key);

            if (key.Equals("Main_GoldenPath", StringComparison.OrdinalIgnoreCase))
            {
                var (objActive, objLast) = ObjectiveStatus(e.Value.Payload);
                var objective = objActive ?? objLast;
                if (!string.IsNullOrEmpty(objective)) golden = Humanize(StripObjectiveNumber(objective));
            }
        }
        return golden ?? active ?? touched ?? "";
    }

    private static (string? Active, string? LastComplete) ObjectiveStatus(object? questPayload)
    {
        if (questPayload is not GvasPayload.Struct st || st.Fields is null) return (null, null);
        if (!SaveGameEditor.IndexFieldsPublic(st.Fields).TryGetValue("ObjectivesStatus", out var p)
            || p.Value.Payload is not List<MapEntry> map)
            return (null, null);

        string? active = null, last = null;
        foreach (var e in map)
        {
            var key = e.Key.Payload as string ?? "";
            var label = e.Value.Payload is GvasPayload.Byte b ? b.Label : null;
            if (label is null) continue;
            if (label.EndsWith("NewEnumerator1", StringComparison.Ordinal)) active = key;
            else if (label.EndsWith("NewEnumerator2", StringComparison.Ordinal)) last = key;
        }
        return (active, last);
    }

    /// <summary>"9_MonocoStation" → "MonocoStation".</summary>
    private static string StripObjectiveNumber(string key)
    {
        var us = key.IndexOf('_');
        if (us <= 0 || us == key.Length - 1) return key;
        for (var i = 0; i < us; i++)
            if (!char.IsDigit(key[i])) return key;
        return key[(us + 1)..];
    }

    private static string? QuestStatusLabel(object? payload)
    {
        if (payload is not GvasPayload.Struct st || st.Fields is null) return null;
        return SaveGameEditor.IndexFieldsPublic(st.Fields)
            .TryGetValue("QuestStatus", out var p) && p.Value.Payload is GvasPayload.Byte b ? b.Label : null;
    }

    private static string Shorten(string questId)
    {
        var tail = questId.StartsWith("Main_", StringComparison.Ordinal) ? questId["Main_".Length..] : questId;
        return Humanize(tail);
    }

    public static string Humanize(string id)
    {
        // "ForceCamps" / "SpringMeadows" / "Level_WorldMap_Main_V2" → spaced words.
        var cleaned = id;
        foreach (var prefix in new[] { "Level_", "Map_" })
            if (cleaned.StartsWith(prefix, StringComparison.Ordinal))
                cleaned = cleaned[prefix.Length..];
        // Trim engine suffixes like _Main_V2, _V2, _DLC01.
        var idx = cleaned.IndexOf("_Main", StringComparison.Ordinal);
        if (idx > 0) cleaned = cleaned[..idx];
        if (cleaned.Length > 2 && char.IsDigit(cleaned[^1]) && cleaned[^2] == 'V')
            cleaned = cleaned[..^2];
        var sb = new System.Text.StringBuilder(cleaned.Length + 8);
        foreach (var c in cleaned)
        {
            if (char.IsUpper(c) && sb.Length > 0 && !char.IsWhiteSpace(sb[^1]) && sb[^1] != '\'')
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    public static string Location(GvasFile file)
    {
        var prop = file.Properties.FirstOrDefault(p =>
            p.Name.StartsWith("LastUsedSavePoint", StringComparison.Ordinal));
        if (prop?.Value.Payload is GvasPayload.Struct st && st.Fields is not null)
        {
            var f = SaveGameEditor.IndexFieldsPublic(st.Fields);
            if (f.TryGetValue("LevelAssetName", out var lvl) && lvl.Value.Payload is string s && s.Length > 0)
                return Humanize(s);
        }
        var map = file.Properties.FirstOrDefault(p =>
            p.Name.StartsWith("MapToLoad", StringComparison.Ordinal))?.Value.Payload as string;
        return map is null ? "" : Humanize(map);
    }

    /// <returns>(average level of active party members, member names).</returns>
    public static (int AvgLevel, List<string> Members) PartyLevels(GvasFile file)
    {
        // Character id → level from CharactersCollection.
        var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in SaveGameEdits.MapEntries(file, SaveGameEdits.CharactersKey))
        {
            var id = e.Key.Payload as string ?? "";
            var fields = e.Value.Payload is GvasPayload.Struct st ? st.Fields : null;
            if (fields is null || id.Length == 0) continue;
            var f = SaveGameEditor.IndexFieldsPublic(fields);
            levels[id] = f.TryGetValue("CurrentLevel", out var lp) && lp.Value.Payload is int li ? li : 0;
            var ex = f.TryGetValue("IsExcluded", out var ep) && ep.Value.Payload is bool eb && eb;
            if (ex) excluded.Add(id);
        }

        // Active party members from CurrentParty array entries.
        var members = new List<string>();
        if (file.Properties.FirstOrDefault(p => p.Name.StartsWith("CurrentParty", StringComparison.Ordinal))
                ?.Value.Payload is GvasPayload.Array party)
        {
            foreach (var item in party.Items)
            {
                if (item.Payload is not GvasPayload.Struct st || st.Fields is null) continue;
                var f = SaveGameEditor.IndexFieldsPublic(st.Fields);
                if (f.TryGetValue("CharacterHardcodedName", out var n) && n.Value.Payload is string name)
                    members.Add(name);
            }
        }
        if (members.Count == 0)
            members = levels.Keys.Where(id => !excluded.Contains(id)).ToList();

        if (members.Count == 0)
            return ((int)Math.Round(levels.Values.DefaultIfEmpty(0).Average()), members);

        var avg = members
            .Where(levels.ContainsKey)
            .Select(id => levels[id])
            .DefaultIfEmpty(0)
            .Average();
        return ((int)Math.Round(avg), members);
    }

    public static double PlaytimeSeconds(GvasFile file) =>
        file.Properties.FirstOrDefault(p => p.Name.StartsWith("TimePlayed", StringComparison.Ordinal))
            ?.Value.Payload is double d ? d : 0;

    /// <summary>"12h 05m" style; input is seconds.</summary>
    public static string FormatPlaytime(double seconds)
    {
        var total = (long)Math.Max(0, seconds);
        var h = total / 3600;
        var m = total % 3600 / 60;
        return h > 0 ? string.Create(CultureInfo.InvariantCulture, $"{h}h {m:00}m") : $"{m}m";
    }
}
