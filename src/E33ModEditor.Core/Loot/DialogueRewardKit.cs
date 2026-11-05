using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core.Loot;

/// <summary>
/// Randomizes dialogue gear grants: white-nevron rewards, Golgra's keyed gift
/// map, and the tournament registrar's unkeyed Rewards array (the payout
/// after the last arena fight against Sciel). Each grant is a fixed item ID
/// in the dialogue BP's CDO plus a loot-context level fed to
/// AddItemToInventory. Only size-preserving edits ship: CDO name swaps, the
/// registrar's raw FName swaps, and same-size IntConst rewrites (Blanche).
/// Context-trimming (const-for-call swaps) is implemented but NOT used — it
/// shifts bytecode and needs manual jump-offset recalculation
/// (ShiftAbsoluteSigned + RecalcContextSkips); without it the game
/// hard-crashes. Replacements draw from the shared placement so pools never
/// duplicate them while stock remains; a guaranteed gift repeats a same-tier
/// owner weapon when the tier is already placed, and swapped originals are
/// released back to pools by the caller.
/// </summary>
public static class DialogueRewardKit
{
    private static readonly (string Stem, string Original, string Type, int Tier, int? Context, bool NoDefense)[] Grants =
    {
        ("BP_Dialogue_TroubadourCantPlay", "HealingParry", "Pictos", 2, null, false),
        // Portier context left dynamic: replacing the GetEncounterLevel use
        // with a const changes bytecode size and stale jump offsets crash
        // the game (same root cause as the Keeper crash). ID swap only.
        ("BP_Dialog_SpiritPortier", "ProtectingHeal", "Pictos", 4, null, false),
        // Demineur is met in Act 1: the shared roster gate
        // (RandomizerKit.WeaponAllowedAtTier) restricts weapons to the cast
        // actually recruited, so a Verso weapon can never roll here.
        ("BP_Dialog_Goblu_DemineurMissingMine", "Deminerim", "Weapon", 1, null, false),
        // RedWoods fountain (the "Bénisseur"): pour Chroma in, get a T4 picto.
        // Never a Defense-stat picto, so the donation can't hand back a plain
        // defence bump (NoDefense filters RandomizerKit.DefensePictos).
        ("BP_Dialogue_SleepingBenisseur", "Recovery", "Pictos", 4, null, true),
        ("BP_Dialog_JarNeedLight", "HealingTint_Shard", "Pictos", 1, null, false),
    };

    /// <summary>Quantity patches: (dialogue stem, vanilla amount, new amount).
    /// Blanc's pacifist Lumina (100 → 1000); the IntConst must be unique.</summary>
    private static readonly (string Stem, int From, int To)[] QuantityPatches =
    {
        ("BP_Dialogue_JudgeOfMercy", 100, 1000),
    };

    /// <summary>Array grants: every known item id under the array prop is
    /// swapped for an exact-tier pick (Hidden Arena Keeper's 5 pictos → T1).
    /// Context, when set, replaces every encounter-fed loot-context
    /// assignment (fixed 8 lands T1 under direct, /2.5 and /3 mappings).</summary>
    private static readonly (string Stem, string ArrayProp, int Tier, int? Context)[] ArrayGrants =
    {
        // Keeper context left dynamic (see Portier note above): array swap
        // only. The enum passenger stays deleted (game resolves from vanilla).
        ("BP_Dialogue_HiddenArena_Keeper", "Rewards", 1, null),
    };

    /// <summary>Map grants: every character-keyed value under a CDO map prop
    /// becomes an owner-locked exact-tier weapon pick. Golgra's
    /// post-tournament gift (Reward_GolgraMap) hands one weapon per character
    /// via story text; each becomes a seeded T3 weapon of that character's
    /// own family drawn from the shared placement like any other drop
    /// (Maelle's vanilla T5 Medalum was the outlier). The tournament
    /// registrar's unkeyed <c>Rewards</c> array is the same payout on the
    /// Sciel-fight path and is patched by <see cref="OwnerArrayGrants"/>.
    /// Loot context stays dynamic (GetEncounterLevel, same as Portier) — ID
    /// swaps only, no bytecode. "Noah" is Gustave's internal character name.</summary>
    private static readonly (string Stem, string MapProp, int Tier)[] MapGrants =
    {
        ("BP_Dialogue_GV_Golgra", "Reward_GolgraMap", 3),
    };

    /// <summary>Map key → weapon-owning character for keyed map grants.</summary>
    private static string MapKeyOwner(string key) =>
        string.Equals(key, "Noah", System.StringComparison.OrdinalIgnoreCase) ? "Gustave" : key;

    /// <summary>
    /// Owner-locked arrays: the tournament registrar's <c>Rewards</c> array is
    /// the payout handed out after the last arena fight (Sciel), and it is a
    /// separate copy of the same six vanilla gift weapons as Golgra's map —
    /// with no character keys. The slot order is validated against the vanilla
    /// values before patching, so a game update that changes the array leaves
    /// it vanilla instead of granting the wrong character's gear.
    /// </summary>
    private static readonly (string Stem, string ArrayProp, int Tier, (string Original, string Owner)[] Slots)[] OwnerArrayGrants =
    {
        ("BP_Dialogue_GV_ArenaRegistrar", "Rewards", 3, new[]
        {
            ("Sakaram", "Gustave"),
            ("Trebuchim", "Lune"),
            ("Medalum", "Maelle"),
            ("Hevason", "Sciel"),
            ("Sakaram", "Verso"),
            ("Sidaro", "Monoco"),
        }),
    };

    private static readonly Dictionary<string, string[]> Siblings = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["BP_Dialogue_HiddenArena_Keeper"] = ["E_GestralFightClub_Fighters"],
    };

    /// <summary>Sibling asset stems a dialogue BP needs alongside it (offline
    /// parser only; the game resolves them from its own files).</summary>
    public static IReadOnlyList<string> SiblingsOf(string stem) =>
        Siblings.TryGetValue(stem, out var s) ? s : Array.Empty<string>();

    /// <summary>Draws same-tier replacements, stages and patches the dialogue
    /// BPs. Returns original → replacement swaps for the caller to release;
    /// entries whose original already is the target stay placed and are
    /// omitted. Guaranteed gifts repeat a same-tier owner weapon rather than
    /// leaving the vanilla value.</summary>
    public static Dictionary<string, string> Apply(string modDir, LootCatalog catalog, Random rng,
        RandomizerKit.LootPlacement placement)
    {
        var swaps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stem, original, type, tier, context, noDefense) in Grants)
        {
            // Exact tier first: the dialogue loot-context is calibrated per
            // tier, so a cascaded lower-tier pick would stamp out of band.
            // Weapons pass the shared roster gate (unrecruited characters'
            // gear silently fails in-game); pictos are unrestricted.
            // NoDefense cascades across tiers without ever giving up the
            // defence exclusion (the RedWoods fountain must stay non-Defense).
            var pick = noDefense
                ? TakeCascadeExcluding(catalog, placement, type, tier, rng, RandomizerKit.DefensePictos)
                : TakeExactTier(catalog, placement, type, tier, rng)
                    ?? (string.Equals(type, "Weapon", System.StringComparison.OrdinalIgnoreCase)
                        ? null : placement.TakeUnique(catalog, type, tier, rng));
            if (pick is null)
            {
                System.Console.WriteLine($"[dialogue] pool dry at T{tier}, {stem} left vanilla");
                continue;
            }
            string path;
            try { path = CopyIntoMod(stem, modDir); }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[dialogue] {stem} not staged ({ex.GetType().Name}), left vanilla");
                continue;
            }
            if (PatchRewardId(path, original, pick) == 0)
            {
                System.Console.WriteLine($"[dialogue] {stem} has no {original} grant, left vanilla");
                continue;
            }
            if (context is int ctx && PatchLootContext(path, ctx) != 1)
                System.Console.WriteLine($"[dialogue] {stem} context pattern not unique, kept vanilla context");
            System.Console.WriteLine($"[dialogue] {stem}: {original} -> {pick}");
            swaps[original] = pick;
        }
        foreach (var (stem, arrayProp, tier, context) in ArrayGrants)
        {
            string path;
            try { path = CopyIntoMod(stem, modDir); }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[dialogue] {stem} not staged ({ex.GetType().Name}), left vanilla");
                continue;
            }
            var got = PatchRewardArray(path, arrayProp, tier, catalog, rng, placement);
            // The staged enum passenger exists only for the offline parser;
            // the game resolves it from vanilla files, so remove it — a
            // duplicate user-defined enum in the pack is itself a suspect.
            RemoveStagedSiblings(path);
            if (got.Count == 0)
            {
                System.Console.WriteLine($"[dialogue] {stem} has no swappable array grants, left vanilla");
                continue;
            }
            if (context is int ctx2)
            {
                var n = PatchLootContext(path, ctx2);
                System.Console.WriteLine($"[dialogue] {stem}: {got.Count} array grants, {n} context site(s) -> {ctx2}");
                if (n == 0)
                    System.Console.WriteLine($"[dialogue] {stem} context pattern missing, kept vanilla context");
            }
            else
            {
                System.Console.WriteLine($"[dialogue] {stem}: {got.Count} array grants");
            }
            foreach (var (o, r) in got) swaps[o] = r;
        }
        foreach (var (stem, from, to) in QuantityPatches)
        {
            string path;
            try { path = CopyIntoMod(stem, modDir); }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[dialogue] {stem} not staged ({ex.GetType().Name}), left vanilla");
                continue;
            }
            var n = PatchIntConst(path, from, to);
            if (n == 1) System.Console.WriteLine($"[dialogue] {stem}: quantity {from} -> {to}");
            else System.Console.WriteLine($"[dialogue] {stem} quantity pattern not unique ({n}), left vanilla");
        }
        // Owner draws already made by the keyed map, reused by the registrar's
        // unkeyed copy of the same payout: both paths then grant the same T3
        // weapon instead of reserving two different T3s per character.
        var ownerPicks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stem, mapProp, tier) in MapGrants)
        {
            string srcPath;
            try { srcPath = EnsureLocal(stem); }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[dialogue] {stem} not staged ({ex.GetType().Name}), left vanilla");
                continue;
            }
            // Character-keyed values first, so every owner draw is seeded
            // like a drop via the shared placement (random but never
            // duplicating pool gear while the tier still has stock). No
            // cascade — the dialogue loot-context is calibrated per tier.
            // If an owner's exact-tier pool is already placed, repeat one of
            // that owner's tier weapons rather than leaving the vanilla value
            // (e.g. Maelle's vanilla T5 Medalum): a duplicate T3 gift is
            // preferable to silently shipping the old T5.
            var entries = ReadMapRewardEntries(srcPath, mapProp);
            if (entries.Count == 0)
            {
                System.Console.WriteLine($"[dialogue] {stem} has no grants under {mapProp}, left vanilla");
                continue;
            }
            string path;
            try { path = CopyIntoMod(stem, modDir); }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[dialogue] {stem} not staged ({ex.GetType().Name}), left vanilla");
                continue;
            }
            foreach (var (key, original) in entries)
            {
                var owner = MapKeyOwner(key);
                var owned = TakeOwnedExactTierOrRepeat(catalog, placement, owner, tier, rng);
                if (owned is null)
                {
                    System.Console.WriteLine(
                        $"[dialogue] {stem}[{key}]: no {owner} T{tier} weapon in the tier list, kept vanilla");
                    continue;
                }
                if (string.Equals(original, owned, System.StringComparison.OrdinalIgnoreCase))
                {
                    System.Console.WriteLine($"[dialogue] {stem}[{key}]: already {owned}, kept");
                    continue;
                }
                var n = PatchMapRewardKey(path, mapProp, key, owned);
                if (n == 0)
                {
                    System.Console.WriteLine($"[dialogue] {stem}[{key}]: patch missed, kept vanilla");
                    continue;
                }
                System.Console.WriteLine($"[dialogue] {stem}[{key}]: {original} -> {owned} ({owner} T{tier})");
                swaps[original] = owned;
                ownerPicks[owner] = owned;
            }
        }
        foreach (var (stem, arrayProp, tier, slots) in OwnerArrayGrants)
        {
            try { _ = EnsureLocal(stem); }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[dialogue] {stem} not staged ({ex.GetType().Name}), left vanilla");
                continue;
            }
            string path;
            try { path = CopyIntoMod(stem, modDir); }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[dialogue] {stem} not staged ({ex.GetType().Name}), left vanilla");
                continue;
            }
            var got = PatchOwnerRewardArray(path, arrayProp, slots, tier, catalog, placement, rng, ownerPicks);
            if (got.Count == 0)
            {
                System.Console.WriteLine(
                    $"[dialogue] {stem} has no matching {arrayProp} array, left vanilla");
                continue;
            }
            foreach (var (original, replacement) in got)
                if (!string.Equals(original, replacement, System.StringComparison.OrdinalIgnoreCase))
                    swaps[original] = replacement;
            System.Console.WriteLine(
                $"[dialogue] {stem}: {got.Count} owner-locked array grants -> T{tier}");
        }
        return swaps;
    }

    /// <summary>Swaps every known item id under an array prop for an
    /// exact-tier pick. Returns the applied (original → replacement) pairs
    /// in array order.</summary>
    public static List<(string Original, string Replacement)> PatchRewardArray(string uassetPath,
        string arrayProp, int tier, LootCatalog catalog, Random rng,
        RandomizerKit.LootPlacement placement)
    {
        var got = new List<(string, string)>();
        var asset = OpenAsset(uassetPath);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", System.StringComparison.Ordinal));
        var arr = cdo?.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == arrayProp);
        if (arr is null) return got;
        var nodes = WalkProps(arr).OfType<NamePropertyData>()
            .Where(nm => catalog.Items.ContainsKey(nm.Value.ToString()))
            .ToList();
        foreach (var nm in nodes)
        {
            var original = nm.Value.ToString();
            var pick = TakeExactTier(catalog, placement, "Pictos", tier, rng)
                ?? placement.TakeUnique(catalog, "Pictos", tier, rng);
            if (pick is null) break;
            nm.Value = new FName(asset, pick);
            got.Add((original, pick));
        }
        if (got.Count > 0) WriteAsset(asset, uassetPath);
        return got;
    }

    /// <summary>
    /// Patches an owner-locked weapon array whose CDO UAssetAPI cannot parse
    /// into a NormalExport (the tournament registrar's <c>Rewards</c>). The
    /// array is a fixed run of 16-byte S_ItemStack entries (4-byte tag,
    /// 4-byte DataTable index, 4-byte FName index, 4-byte FName number), so
    /// the vanilla FName sequence locates it without a typed parse; the six
    /// RowName indices are then rewritten in place. A parsed NormalExport
    /// (future game build) is handled by slot order instead. Returns the
    /// applied pairs in slot order; an entry already at its target id is kept.
    /// </summary>
    public static List<(string Original, string Replacement)> PatchOwnerRewardArray(
        string uassetPath, string arrayProp, (string Original, string Owner)[] slots, int tier,
        LootCatalog catalog, RandomizerKit.LootPlacement placement, Random rng,
        IReadOnlyDictionary<string, string>? sharedPicks = null)
    {
        return PatchOwnerRewardArray(uassetPath, arrayProp, slots, tier, catalog, placement, rng,
            sharedPicks, out _);
    }

    /// <inheritdoc cref="PatchOwnerRewardArray(string,string,(string,string)[],int,LootCatalog,RandomizerKit.LootPlacement,Random,IReadOnlyDictionary{string,string}?)"/>
    public static List<(string Original, string Replacement)> PatchOwnerRewardArray(
        string uassetPath, string arrayProp, (string Original, string Owner)[] slots, int tier,
        LootCatalog catalog, RandomizerKit.LootPlacement placement, Random rng,
        IReadOnlyDictionary<string, string>? sharedPicks, out int foundStart)
    {
        foundStart = -1;
        var got = new List<(string, string)>();
        var asset = OpenAsset(uassetPath);
        var cdo = asset.Exports.FirstOrDefault(
            e => e.ObjectName.ToString().StartsWith("Default__", System.StringComparison.Ordinal));
        if (cdo is null) return got;

        // Reuse the keyed map's owner picks when the same payout exists in
        // both assets, so the two paths cannot reserve two different T3s.
        var picks = new string?[slots.Length];
        for (int k = 0; k < slots.Length; k++)
        {
            var owner = slots[k].Owner;
            if (sharedPicks is not null && sharedPicks.TryGetValue(owner, out var shared))
                picks[k] = shared;
            else
                picks[k] = TakeOwnedExactTierOrRepeat(catalog, placement, owner, tier, rng);
        }

        if (cdo is RawExport raw)
        {
            var indices = slots.Select(s => asset.SearchNameReference(new FString(s.Original))).ToArray();
            if (indices.Any(i => i < 0))
            {
                System.Console.WriteLine(
                    $"[dialogue] {System.IO.Path.GetFileName(uassetPath)}: vanilla {arrayProp} names missing from name map");
                return got;
            }
            var data = raw.Data;
            var start = -1;
            for (int i = 0; i + slots.Length * 16 + 8 <= data.Length && start < 0; i++)
            {
                bool ok = true;
                for (int k = 0; k < slots.Length; k++)
                {
                    if (System.BitConverter.ToInt32(data, i + k * 16) != indices[k]
                        || System.BitConverter.ToInt32(data, i + k * 16 + 4) != 0)
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok) start = i;
            }
            if (start < 0)
            {
                System.Console.WriteLine(
                    $"[dialogue] {System.IO.Path.GetFileName(uassetPath)}: vanilla {arrayProp} array not found (layout changed)");
                return got;
            }
            foundStart = start;
            for (int k = 0; k < slots.Length; k++)
            {
                if (picks[k] is null) continue;
                var ni = asset.AddNameReference(new FString(picks[k]!));
                System.BitConverter.GetBytes(ni).CopyTo(data, start + k * 16);
                System.BitConverter.GetBytes(0).CopyTo(data, start + k * 16 + 4);
                got.Add((slots[k].Original, picks[k]!));
            }
        }
        else if (cdo is NormalExport ne)
        {
            var arr = ne.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == arrayProp);
            if (arr is null) return got;
            var nodes = WalkProps(arr).OfType<NamePropertyData>().ToList();
            if (nodes.Count != slots.Length) return got;
            for (int k = 0; k < slots.Length; k++)
            {
                if (!string.Equals(nodes[k].Value.ToString(), slots[k].Original,
                        System.StringComparison.OrdinalIgnoreCase))
                    return got;
            }
            for (int k = 0; k < slots.Length; k++)
            {
                if (picks[k] is null) continue;
                nodes[k].Value = new FName(asset, picks[k]!);
                got.Add((slots[k].Original, picks[k]!));
            }
        }
        else return got;

        if (got.Count > 0) WriteAsset(asset, uassetPath);
        return got;
    }

    /// <summary>Reads back the owner array names at a known raw start
    /// (smoke/tests).</summary>
    public static List<string> ReadOwnerRewardArrayAt(string uassetPath, int start, int count)
    {
        var out_ = new List<string>();
        var asset = OpenAsset(uassetPath);
        var cdo = asset.Exports.FirstOrDefault(
            e => e.ObjectName.ToString().StartsWith("Default__", System.StringComparison.Ordinal));
        if (cdo is not RawExport raw || start < 0) return out_;
        for (int k = 0; k < count; k++)
        {
            var idx = System.BitConverter.ToInt32(raw.Data, start + k * 16);
            out_.Add(asset.GetNameReference(idx)?.ToString() ?? "");
        }
        return out_;
    }

    /// <summary>Uniform draw from the unplaced exact-tier pool (marks
    /// the pick placed), or null when the tier is spent. Weapons pass the
    /// shared roster gate (RandomizerKit.WeaponAllowedAtTier) so only the
    /// recruited cast's gear can roll; granting an unrecruited character's
    /// weapon silently fails in-game.
    /// Candidates are sorted so identical seeds reproduce identical picks.</summary>
    private static string? TakeExactTier(LootCatalog catalog,
        RandomizerKit.LootPlacement placement, string type, int tier, Random rng,
        ICollection<string>? exclude = null)
    {
        var cands = catalog.PoolFor(type, tier)
            .Where(id => !placement.Placed.Contains(id)
                && (exclude is null || !exclude.Contains(id))
                && RandomizerKit.WeaponAllowedAtTier(catalog, type, id, tier))
            .OrderBy(id => id, System.StringComparer.Ordinal)
            .ToList();
        if (cands.Count == 0) return null;
        var pick = cands[rng.Next(cands.Count)];
        placement.Placed.Add(pick);
        return pick;
    }

    /// <summary>Exact-tier draw that keeps an exclusion set while cascading
    /// down tiers (used for the fountain's non-Defense T4 picto, so even the
    /// fallback can never land on a Defense-stat picto).</summary>
    private static string? TakeCascadeExcluding(LootCatalog catalog,
        RandomizerKit.LootPlacement placement, string type, int tier, Random rng,
        ICollection<string> exclude)
    {
        for (int t = Math.Clamp(tier, 1, 5); t >= 1; t--)
        {
            var pick = TakeExactTier(catalog, placement, type, t, rng, exclude);
            if (pick is not null) return pick;
        }
        return null;
    }

    /// <summary>Owner-locked exact-tier weapon draw (marks the pick placed),
    /// or null when that character has nothing left at the tier. Candidates
    /// are sorted so identical seeds reproduce identical picks. Used for
    /// character-specific story gifts (Golgra's Maelle weapon).</summary>
    public static string? TakeOwnedExactTier(LootCatalog catalog,
        RandomizerKit.LootPlacement placement, string owner, int tier, Random rng)
    {
        var cands = catalog.PoolFor("Weapon", tier)
            .Where(id => string.Equals(WeaponCharacter(catalog, id), owner, System.StringComparison.OrdinalIgnoreCase)
                && !placement.Placed.Contains(id)
                && RandomizerKit.WeaponAllowedAtTier(catalog, "Weapon", id, tier))
            .OrderBy(id => id, System.StringComparer.Ordinal)
            .ToList();
        if (cands.Count == 0) return null;
        var pick = cands[rng.Next(cands.Count)];
        placement.Placed.Add(pick);
        return pick;
    }

    /// <summary>
    /// Owner-locked exact-tier weapon draw for guaranteed story gifts. Prefers
    /// an unplaced exact-tier weapon of <paramref name="owner"/>; once that
    /// pool is spent, repeats one of the owner's already-placed tier weapons
    /// (a guaranteed gift outranks uniqueness) rather than returning null and
    /// leaving the vanilla value. Returns null only when the owner has no
    /// weapon at that tier at all. Candidates are sorted so identical seeds
    /// reproduce identical picks.
    /// </summary>
    public static string? TakeOwnedExactTierOrRepeat(LootCatalog catalog,
        RandomizerKit.LootPlacement placement, string owner, int tier, Random rng)
    {
        var cands = catalog.PoolFor("Weapon", tier)
            .Where(id => string.Equals(WeaponCharacter(catalog, id), owner, System.StringComparison.OrdinalIgnoreCase)
                && RandomizerKit.WeaponAllowedAtTier(catalog, "Weapon", id, tier))
            .OrderBy(id => id, System.StringComparer.Ordinal)
            .ToList();
        if (cands.Count == 0) return null;
        var fresh = cands.Where(id => !placement.Placed.Contains(id)).ToList();
        var pool = fresh.Count > 0 ? fresh : cands;
        var pick = pool[rng.Next(pool.Count)];
        placement.Placed.Add(pick);
        return pick;
    }

    /// <summary>Rewrites every value-side NameProperty equal to
    /// <paramref name="original"/> under the CDO map prop
    /// <paramref name="mapProp"/> to <paramref name="replacement"/>
    /// (map keys are never touched). Data-only edit: namemap indices, no
    /// bytecode. Returns the replacement count.</summary>
    public static int PatchMapRewardId(string uassetPath, string mapProp, string original, string replacement)
    {
        var asset = OpenAsset(uassetPath);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", System.StringComparison.Ordinal));
        var map = cdo?.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == mapProp)
            as MapPropertyData;
        if (map?.Value is null) return 0;
        int n = 0;
        foreach (var entry in map.Value)
        {
            foreach (var nm in WalkProps(entry.Value).OfType<NamePropertyData>())
            {
                if (!string.Equals(nm.Value.ToString(), original, System.StringComparison.OrdinalIgnoreCase)) continue;
                nm.Value = new FName(asset, replacement);
                n++;
            }
        }
        if (n > 0) WriteAsset(asset, uassetPath);
        return n;
    }

    /// <summary>All value-side NameProperty values under a CDO map prop
    /// (read-back for smoke/tests).</summary>
    public static List<string> ReadMapRewardIds(string uassetPath, string mapProp)
    {
        var out_ = new List<string>();
        var asset = OpenAsset(uassetPath);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", System.StringComparison.Ordinal));
        var map = cdo?.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == mapProp)
            as MapPropertyData;
        if (map?.Value is null) return out_;
        foreach (var entry in map.Value)
            foreach (var nm in WalkProps(entry.Value).OfType<NamePropertyData>())
                out_.Add(nm.Value.ToString());
        return out_;
    }

    /// <summary>Owning character of a weapon ("Tireso (Verso Weapon)" →
    /// "Verso"), else "".</summary>
    public static string WeaponCharacter(LootCatalog catalog, string id)
    {
        var display = catalog.Describe(id).DisplayName;
        var open = display.LastIndexOf(" (", System.StringComparison.Ordinal);
        const string tail = " Weapon)";
        if (open < 0 || !display.EndsWith(tail, System.StringComparison.Ordinal)) return "";
        return display.Substring(open + 2, display.Length - open - 2 - tail.Length);
    }

    /// <summary>All (key, first value-side RowName) pairs under a CDO map
    /// prop, in file order (read-back for smoke/tests).</summary>
    public static List<(string Key, string Value)> ReadMapRewardEntries(string uassetPath, string mapProp)
    {
        var out_ = new List<(string, string)>();
        var asset = OpenAsset(uassetPath);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", System.StringComparison.Ordinal));
        var map = cdo?.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == mapProp)
            as MapPropertyData;
        if (map?.Value is null) return out_;
        foreach (var entry in map.Value)
        {
            var key = WalkProps(entry.Key).OfType<NamePropertyData>()
                .Select(nm => nm.Value.ToString()).FirstOrDefault() ?? "";
            var value = WalkProps(entry.Value).OfType<NamePropertyData>()
                .Select(nm => nm.Value.ToString()).FirstOrDefault() ?? "";
            out_.Add((key, value));
        }
        return out_;
    }

    /// <summary>Rewrites the value-side RowName of the map entry whose key
    /// equals <paramref name="key"/> (case-insensitive) to
    /// <paramref name="replacement"/>. Data-only edit: namemap indices, no
    /// bytecode. Returns the replacement count (expect exactly 1).</summary>
    public static int PatchMapRewardKey(string uassetPath, string mapProp, string key, string replacement)
    {
        var asset = OpenAsset(uassetPath);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", System.StringComparison.Ordinal));
        var map = cdo?.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == mapProp)
            as MapPropertyData;
        if (map?.Value is null) return 0;
        int n = 0;
        foreach (var entry in map.Value)
        {
            var keyHit = WalkProps(entry.Key).OfType<NamePropertyData>()
                .Any(nm => string.Equals(nm.Value.ToString(), key, System.StringComparison.OrdinalIgnoreCase));
            if (!keyHit) continue;
            foreach (var nm in WalkProps(entry.Value).OfType<NamePropertyData>())
            {
                nm.Value = new FName(asset, replacement);
                n++;
            }
        }
        if (n > 0) WriteAsset(asset, uassetPath);
        return n;
    }

    /// <summary>Replaces every IntConst <paramref name="from"/> with
    /// <paramref name="to"/> across all functions. Returns the count.</summary>
    public static int PatchIntConst(string uassetPath, int from, int to)
    {
        if (from == to) return 0;
        var asset = OpenAsset(uassetPath);
        int n = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            WalkKismet(fn.ScriptBytecode, e =>
            {
                if (e is EX_IntConst c && c.Value == from) { c.Value = to; n++; }
            });
        }
        if (n > 0) WriteAsset(asset, uassetPath);
        return n;
    }

    /// <summary>Rewrites every NameProperty equal to <paramref name="original"/>
    /// inside the CDO Reward/RewardOnCompletion struct to
    /// <paramref name="replacement"/>. Returns the replacement count.</summary>
    public static int PatchRewardId(string uassetPath, string original, string replacement)
    {
        var asset = OpenAsset(uassetPath);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", System.StringComparison.Ordinal));
        if (cdo is null) return 0;
        int n = 0;
        foreach (var propName in new[] { "Reward", "RewardOnCompletion" })
        {
            var reward = cdo.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == propName);
            if (reward is null) continue;
            foreach (var nm in WalkProps(reward).OfType<NamePropertyData>())
            {
                if (!string.Equals(nm.Value.ToString(), original, System.StringComparison.OrdinalIgnoreCase)) continue;
                nm.Value = new FName(asset, replacement);
                n++;
            }
        }
        if (n > 0) WriteAsset(asset, uassetPath);
        return n;
    }

    /// <summary>
    /// Replaces the existing CDO Reward/RewardOnCompletion row with a fixed
    /// item id. This is a data-only swap (no bytecode change), used when a
    /// dialogue should drop a different item rather than add a second grant.
    /// Returns false when the original id is not present.
    /// </summary>
    public static bool ReplaceFixedReward(string modDir, string stem, string fromId, string toId)
    {
        if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId)) return false;
        string path;
        try { path = CopyIntoMod(stem, modDir); }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"[dialogue] {stem} not staged ({ex.GetType().Name}), reward left vanilla");
            return false;
        }
        var n = PatchRewardId(path, fromId, toId);
        if (n == 0)
        {
            System.Console.WriteLine($"[dialogue] {stem}: reward {fromId} not found, left vanilla");
            return false;
        }
        System.Console.WriteLine($"[dialogue] {stem}: reward {fromId} -> {toId}");
        return true;
    }

    /// <summary>
    /// Adds a second, fixed item grant to a dialogue BP's existing reward
    /// branch. The vanilla asset already contains one
    /// <c>AddItemToInventory</c> call after the 9,999-damage objective; the
    /// cloned statement is inserted immediately after it with its item id
    /// replaced. Bytecode offsets (including the ubergraph entry constants the
    /// event thunks pass) are shifted and revalidated, so the dialogue keeps
    /// its shape. Returns false when the grant already exists.
    /// </summary>
    public static bool AddFixedItemReward(string modDir, string stem, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        string path;
        try { path = CopyIntoMod(stem, modDir); }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"[dialogue] {stem} not staged ({ex.GetType().Name}), reward skipped");
            return false;
        }

        var asset = OpenAsset(path);
        var uber = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString().StartsWith("ExecuteUbergraph",
                System.StringComparison.Ordinal));
        if (uber?.ScriptBytecode is null)
            throw new System.IO.InvalidDataException(stem + ": ExecuteUbergraph not found");

        var stmts = uber.ScriptBytecode.ToList();
        bool already = false;
        foreach (var st in stmts)
            MayhemFixKit.Walk(new[] { st }, e =>
            {
                if (e is EX_NameConst nc && nc.Value.ToString() == itemId) already = true;
            });
        if (already) return false;

        int addIdx = -1;
        EX_Context? template = null;
        for (int i = 0; i < stmts.Count && template is null; i++)
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (template is null && e is EX_Context ctx
                    && ctx.ContextExpression is EX_LocalVirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "AddItemToInventory")
                {
                    template = ctx;
                    addIdx = i;
                }
            });
        if (template is null || addIdx < 0)
            throw new System.IO.InvalidDataException(stem + ": AddItemToInventory statement not found");

        var clone = (EX_Context)MayhemFixKit.CloneKismet(template);
        if (clone.ContextExpression is not EX_LocalVirtualFunction call
            || call.Parameters is not { Length: >= 1 })
            throw new System.IO.InvalidDataException(stem + ": AddItemToInventory call shape changed");
        asset.AddNameReference(FString.FromString(itemId));
        call.Parameters[0] = new EX_NameConst { Value = new FName(asset, itemId) };

        uint point = 0;
        for (int i = 0; i <= addIdx; i++) point += (uint)stmts[i].GetSize(asset);
        uint delta = (uint)clone.GetSize(asset);
        ShiftOffsetsAtOrAfter(asset, uber, point, delta);
        stmts.Insert(addIdx + 1, clone);
        uber.ScriptBytecode = stmts.ToArray();
        MayhemFixKit.RecalcContextSkips(asset, uber);
        ShiftUbergraphEntries(asset, uber, point, delta, stem);
        ValidateJumpTargets(asset, uber, stem + " / ExecuteUbergraph");
        WriteAsset(asset, path);
        System.Console.WriteLine($"[dialogue] {stem}: + {itemId} reward");
        return true;
    }

    private static void ShiftUbergraphEntries(UAsset asset, FunctionExport uber,
        uint point, uint delta, string tag)
    {
        var starts = new HashSet<uint>();
        uint pos = 0;
        foreach (var st in uber.ScriptBytecode!)
        {
            starts.Add(pos);
            pos += (uint)st.GetSize(asset);
        }
        int uberExp = asset.Exports.IndexOf(uber);
        foreach (var f in asset.Exports.OfType<FunctionExport>())
        {
            if (ReferenceEquals(f, uber)) continue;
            foreach (var st in f.ScriptBytecode ?? System.Array.Empty<KismetExpression>())
                MayhemFixKit.Walk(new[] { st }, e =>
                {
                    if (e is not EX_FinalFunction ff || ff.StackNode.IsImport()) return;
                    if (Math.Abs(ff.StackNode.Index) - 1 != uberExp) return;
                    foreach (var p in ff.Parameters)
                    {
                        if (p is not EX_IntConst ic) continue;
                        if ((uint)ic.Value >= point) ic.Value += (int)delta;
                        if (!starts.Contains((uint)ic.Value))
                            throw new System.IO.InvalidDataException(
                                $"{tag}: ubergraph entry {ic.Value} is not a statement start");
                    }
                });
        }
    }

    private static void ShiftOffsetsAtOrAfter(UAsset asset, FunctionExport fn, uint point, uint delta)
    {
        if (delta == 0) return;
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when j.CodeOffset >= point: j.CodeOffset += delta; break;
                    case EX_JumpIfNot jn when jn.CodeOffset >= point: jn.CodeOffset += delta; break;
                    case EX_PushExecutionFlow pf when pf.PushingAddress >= point: pf.PushingAddress += delta; break;
                    // EX_SwitchValue offsets are relative to the switch, not
                    // absolute statement starts; leave them untouched.
                }
            });
    }

    private static void ValidateJumpTargets(UAsset asset, FunctionExport fn, string tag)
    {
        var starts = new HashSet<uint>();
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
        {
            starts.Add(pos);
            pos += (uint)st.GetSize(asset);
        }
        var bad = new List<string>();
        uint p = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref p, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when !starts.Contains(j.CodeOffset): bad.Add($"Jump->{j.CodeOffset}"); break;
                    case EX_JumpIfNot jn when !starts.Contains(jn.CodeOffset): bad.Add($"JumpIfNot->{jn.CodeOffset}"); break;
                    case EX_PushExecutionFlow pf when !starts.Contains(pf.PushingAddress): bad.Add($"PushFlow->{pf.PushingAddress}"); break;
                    // Switch cases use expression-relative offsets.
                }
            });
        if (bad.Count > 0)
            throw new System.IO.InvalidDataException(
                $"{tag} has {bad.Count} dangling jump target(s): {string.Join(", ", bad)}");
    }

    /// <summary>Replaces the GetEncounterLevel-fed loot-context assignment with
    /// a fixed <paramref name="target"/>. Returns the replacement count
    /// (expect exactly 1).
    /// WARNING: this changes bytecode size; production callers must follow
    /// with LuneSkillsKit.ShiftAbsoluteSigned + MayhemFixKit.RecalcContextSkips
    /// or stale jump offsets hard-crash the game. Currently UNUSED in
    /// production (smoke validates the matcher only).</summary>
    public static int PatchLootContext(string uassetPath, int target)
    {
        var asset = OpenAsset(uassetPath);
        int n = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            WalkKismet(fn.ScriptBytecode, e =>
            {
                if (e is not EX_Let let) return;
                if (let.Expression is not EX_LocalVariable lv) return;
                if (PtrName(lv.Variable) != "CallFunc_GetEncounterLevel_ReturnValue") return;
                if (!Mentions(let.Variable, "K2Node_MakeStruct_S_LootContext")) return;
                let.Expression = new EX_IntConst { Value = target };
                n++;
            });
        }
        if (n > 0) WriteAsset(asset, uassetPath);
        return n;
    }

    /// <summary>Reads back the Reward.RowName grant id (smoke/tests).</summary>
    public static string? ReadRewardId(string uassetPath)
    {
        var all = ReadRewardIds(uassetPath, "Reward");
        if (all.Count > 0) return all[0];
        all = ReadRewardIds(uassetPath, "RewardOnCompletion");
        return all.Count > 0 ? all[0] : null;
    }

    /// <summary>All NameProperty values under a CDO struct prop.</summary>
    public static List<string> ReadRewardIds(string uassetPath, string prop)
    {
        var out_ = new List<string>();
        var asset = OpenAsset(uassetPath);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", System.StringComparison.Ordinal));
        var reward = cdo?.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == prop);
        if (reward is null) return out_;
        foreach (var nm in WalkProps(reward).OfType<NamePropertyData>())
            out_.Add(nm.Value.ToString());
        return out_;
    }

    private static bool Mentions(KismetExpression? root, string local)
    {
        if (root is null) return false;
        bool hit = false;
        WalkKismet(new[] { root }, e =>
        {
            if (e is EX_LocalVariable lv && PtrName(lv.Variable).Contains(local, System.StringComparison.Ordinal))
                hit = true;
        });
        return hit;
    }

    private static IEnumerable<PropertyData> WalkProps(PropertyData root)
    {
        yield return root;
        if (root is StructPropertyData s && s.Value != null)
            foreach (var c in s.Value)
                foreach (var d in WalkProps(c)) yield return d;
        if (root is ArrayPropertyData a && a.Value != null)
            foreach (var c in a.Value)
                foreach (var d in WalkProps(c)) yield return d;
        if (root is MapPropertyData m && m.Value != null)
            foreach (var entry in m.Value)
            {
                foreach (var d in WalkProps(entry.Key)) yield return d;
                foreach (var d in WalkProps(entry.Value)) yield return d;
            }
    }

    private static void WalkKismet(IEnumerable<KismetExpression> exprs, System.Action<KismetExpression> visit)
    {
        foreach (var e in exprs)
        {
            if (e is null) continue;
            visit(e);
            switch (e)
            {
                case EX_Context ctx:
                    if (ctx.ObjectExpression != null) WalkKismet(new[] { ctx.ObjectExpression }, visit);
                    if (ctx.ContextExpression != null) WalkKismet(new[] { ctx.ContextExpression }, visit);
                    break;
                case EX_StructMemberContext smc:
                    if (smc.StructExpression != null) WalkKismet(new[] { smc.StructExpression }, visit);
                    break;
                case EX_Let let:
                    if (let.Variable != null) WalkKismet(new[] { let.Variable }, visit);
                    if (let.Expression != null) WalkKismet(new[] { let.Expression }, visit);
                    break;
                case EX_LetObj lo:
                    if (lo.VariableExpression != null) WalkKismet(new[] { lo.VariableExpression }, visit);
                    if (lo.AssignmentExpression != null) WalkKismet(new[] { lo.AssignmentExpression }, visit);
                    break;
                case EX_LetBool lb:
                    if (lb.VariableExpression != null) WalkKismet(new[] { lb.VariableExpression }, visit);
                    if (lb.AssignmentExpression != null) WalkKismet(new[] { lb.AssignmentExpression }, visit);
                    break;
                case EX_VirtualFunction vf when vf.Parameters is { Length: > 0 }:
                    WalkKismet(vf.Parameters, visit);
                    break;
                case EX_FinalFunction ff when ff.Parameters is { Length: > 0 }:
                    WalkKismet(ff.Parameters, visit);
                    break;
                case EX_CallMath cm when cm.Parameters is { Length: > 0 }:
                    WalkKismet(cm.Parameters, visit);
                    break;
            }
        }
    }

    private static string PtrName(KismetPropertyPointer? p)
    {
        if (p?.New?.Path is { Length: > 0 } path)
            return string.Join(".", path.Select(n => n.ToString()));
        return "";
    }

    private static UAsset OpenAsset(string uassetPath)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
    }

    private static void WriteAsset(UAsset asset, string path)
    {
        try { asset.Write(path); }
        catch (System.FormatException ex)
        {
            throw new System.FormatException(System.IO.Path.GetFileName(path) + ": " + ex.Message, ex);
        }
    }

    private static void RemoveStagedSiblings(string stagedBpPath)
    {
        var dir = System.IO.Path.GetDirectoryName(stagedBpPath);
        if (dir is null) return;
        if (!Siblings.TryGetValue(
                System.IO.Path.GetFileNameWithoutExtension(stagedBpPath), out var sibs)) return;
        foreach (var sib in sibs)
        {
            foreach (var ext in new[] { ".uasset", ".uexp" })
            {
                var p = System.IO.Path.Combine(dir, sib + ext);
                if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
            }
        }
    }

    /// <summary>
    /// Locates a dialogue BP for patching: the extracted scratch copy first, then
    /// the work/tables donor cache, then the dialogue_rewards extraction cache,
    /// and only then a fresh retoc extraction. The extraction runs with ~mods
    /// suspended so an installed pack cannot shadow it — without that, retoc
    /// refuses (or worse, reads a modded copy) and every white-nevron grant is
    /// silently left vanilla.
    /// </summary>
    public static string EnsureLocal(string stem)
    {
        var cached = System.IO.Path.Combine(EditorPaths.RepoRoot, "work", "dialogue_rewards");
        bool present = GameplayTweaksKit.FindAsset(cached, stem + ".uasset") is not null;
        bool sibsPresent = SiblingsOf(stem)
            .All(sib => GameplayTweaksKit.FindAsset(cached, sib + ".uasset") is not null);
        if (!present || !sibsPresent)
        {
            System.IO.Directory.CreateDirectory(cached);
            using (E33ModEditor.Core.Packer.DonorExtract.SuspendMods())
            {
                if (!present)
                    new E33ModEditor.Core.Retoc().ToLegacy(cached, stem);
                // Sibling enum assets the CDO schema needs; the game resolves
                // them from its own files, these copies only unblock the parser.
                foreach (var sib in SiblingsOf(stem))
                    if (GameplayTweaksKit.FindAsset(cached, sib + ".uasset") is null)
                        new E33ModEditor.Core.Retoc().ToLegacy(cached, sib);
            }
        }

        // Prefer the cache so any sibling asset resolves next to the BP; the
        // older scratch/donor caches are a fallback only.
        return GameplayTweaksKit.FindAsset(cached, stem + ".uasset")
            ?? GameplayTweaksKit.FindAsset(
                System.IO.Path.Combine(EditorPaths.RepoRoot, "extracted"), stem + ".uasset")
            ?? GameplayTweaksKit.FindAsset(
                System.IO.Path.Combine(EditorPaths.RepoRoot, "work", "tables"), stem + ".uasset")
            ?? throw new System.IO.FileNotFoundException(stem + ".uasset");
    }

    private static string CopyIntoMod(string stem, string modDir)
    {
        var src = EnsureLocal(stem);
        var idx = src.IndexOf("Sandfall", System.StringComparison.Ordinal);
        if (idx < 0) throw new System.IO.InvalidDataException(stem + " is not under Sandfall/");
        var dest = System.IO.Path.Combine(modDir, src[idx..]);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        // Sibling assets the offline parser needs next to the BP
        // (user-defined enums referenced by CDO properties). The game
        // resolves these from its own files; the copies only unblock
        // UAssetAPI and pack as vanilla-identical passengers.
        if (Siblings.TryGetValue(stem, out var sibs))
        {
            var srcDir = System.IO.Path.GetDirectoryName(src)!;
            var destDir = System.IO.Path.GetDirectoryName(dest)!;
            foreach (var sib in sibs)
            {
                var sibSrc = System.IO.Path.Combine(srcDir, sib + ".uasset");
                if (System.IO.File.Exists(sibSrc))
                    FileCopy.AssetPair(sibSrc, System.IO.Path.Combine(destDir, sib + ".uasset"));
            }
        }
        return dest;
    }
}
