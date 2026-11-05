using E33ModEditor.Core.Loot;

namespace E33ModEditor.Core.SaveGame;

/// <summary>
/// Conservative weapon-lumina hygiene for saves. Weapon passives themselves
/// are read live from DT_WeaponDefinitions, but lumina a weapon granted while
/// a broken table was installed stay unlocked in the save forever
/// (PassiveEffectsProgressions). The scrubber only removes true orphans —
/// entries no roster member may legally roll — and reports (without touching)
/// character-mechanic lumina equipped on characters that cannot use them.
/// </summary>
public static class SaveScrubber
{
    public sealed record Report(
        List<string> OrphanUnlocked,
        List<(string Character, string Id)> WrongEquipped);

    public static Report Scan(GvasFile file, LootCatalog catalog)
    {
        var poolIds = new HashSet<string>(StringComparer.Ordinal);
        for (int t = 1; t <= 5; t++)
            foreach (var id in catalog.EffectTiers.Pool(t))
                poolIds.Add(id);

        var orphans = new List<string>();
        foreach (var id in ProgressionIds(file))
        {
            if (!poolIds.Contains(id)) continue; // pictos/story lumina never qualify
            if (!WeaponEffectCharacters.Roster.Any(ch => catalog.EffectCharacters.Allows(id, ch)))
                orphans.Add(id);
        }

        var wrong = new List<(string, string)>();
        foreach (var c in SaveGameEdits.Characters(file))
            foreach (var e in c.EquippedPictos)
                if (WeaponEffectTiers.IsCharacterSystemEffect(e)
                    && !catalog.EffectCharacters.Allows(e, c.Id))
                    wrong.Add((c.Id, e));
        return new Report(orphans, wrong);
    }

    /// <summary>Removes the given ids from progressions and every equipment
    /// loadout. Returns the number of entries removed.</summary>
    public static int Remove(GvasFile file, IEnumerable<string> ids)
    {
        var set = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        if (set.Count == 0) return 0;
        int removed = 0;

        var arr = file.Properties
            .FirstOrDefault(p => p.Name.StartsWith("PassiveEffectsProgressions", StringComparison.Ordinal))
            ?.Value.Payload as GvasPayload.Array;
        if (arr is not null)
        {
            for (int i = arr.Items.Count - 1; i >= 0; i--)
            {
                if (arr.Items[i].Payload is not GvasPayload.Struct st) continue;
                var nm = st.Fields?.FirstOrDefault(
                             x => Key(x.Name) == "PassiveEffectName")?.Value.Payload as string ?? "";
                if (set.Contains(nm))
                {
                    arr.Items.RemoveAt(i);
                    removed++;
                }
            }
        }

        foreach (var e in SaveGameEdits.MapEntries(file, SaveGameEdits.CharactersKey))
        {
            var fields = SaveGameEdits.StructFields(e.Value.Payload);
            if (fields is null) continue;
            foreach (var id in set)
                if (SaveGameEdits.StringArray(fields, "EquippedPassiveEffects")
                    .Contains(id, StringComparer.OrdinalIgnoreCase))
                {
                    SaveGameEdits.RemoveFromNameArray(fields, "EquippedPassiveEffects", id);
                    removed++;
                }
        }
        return removed;
    }

    private static List<string> ProgressionIds(GvasFile file)
    {
        var result = new List<string>();
        var arr = file.Properties
            .FirstOrDefault(p => p.Name.StartsWith("PassiveEffectsProgressions", StringComparison.Ordinal))
            ?.Value.Payload as GvasPayload.Array;
        foreach (var it in arr?.Items ?? Enumerable.Empty<TypedValue>())
            if (it.Payload is GvasPayload.Struct st)
            {
                var nm = st.Fields?.FirstOrDefault(
                    x => Key(x.Name) == "PassiveEffectName")?.Value.Payload as string ?? "";
                if (nm.Length > 0) result.Add(nm);
            }
        return result;
    }

    private static string Key(string name)
    {
        var i = name.IndexOf('_');
        return i < 0 ? name : name[..i];
    }
}
