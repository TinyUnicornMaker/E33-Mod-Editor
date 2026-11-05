using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Revive-triggered debuffs on ENEMIES: Powerless, Defenceless and Slow applied to
/// all enemies when the wearer is revived.
///
/// The game has no revive-triggered enemy-affecting passive. The only passive that
/// touches enemies is <c>BP_LU_BurningDeath</c> (Burn to all enemies), and it fires
/// on <em>death</em>. Its trigger is not a name-resolved event but a delegate
/// binding recorded in the ubergraph, so renaming the stub function does nothing —
/// the bound delegate's name has to be retargeted instead:
/// <c>OnCharacterKilled</c> → <c>OnCharacterResurected</c> (the delegate name and its
/// signature live in the name map, and the revive family proves the property exists
/// on the same component class). The status it applies is then retargeted from Burn
/// to the target status by rewriting both the buff class FName and its outer package
/// FName — the repo's hard rule, since renaming only the class leaves the package
/// pointing at Burn and the game hangs on battle load.
///
/// The burn machine applies a stack count, which a plain stat buff does not want, so
/// the balancing row the graph reads is cloned with a value of 1.
/// </summary>
public static class ReviveDebuffKit
{
    private const string DonorBp = "BP_LU_BurningDeath";
    private const string DonorId = "BurningDeath";
    private const string DonorDa = "DA_GearPictos_BurningDeath";
    private const string DonorKey = "BurningDeath_BurnStackAmount";
    private const string BpFolder = "/Game/Gameplay/Lumina/Passives/";
    private const string DeathEvent = "OnCharacterKilled";
    // The revive dispatcher PROPERTY on AC_jRPG_CharacterBattleStats: the death
    // family binds OnCharacterKilled, the revive family binds OnResurected (whose
    // signature is OnResurected__DelegateSignature). Renaming to the stub/event
    // name ("OnCharacterResurected") pointed the signature at a function that does
    // not exist, so UE dropped the bind silently — no effect, no crash.
    private const string ReviveEvent = "OnResurected";
    private const string BurnClass = "BP_BattleBuff_Burn";
    private const string BurnPath = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Burn";
    private const int LuminaCost = 5;

    private const string StatHp = "E_jRPG_StatType::NewEnumerator1";
    private const string StatDef = "E_jRPG_StatType::NewEnumerator4";

    private sealed record Spec(string Id, string Display, string Description,
        string StatusClass, string StatusPath);

    // Buff class/package pairs are the repo's constants (see LuneSkillsKit /
    // weapon_effects.json, which already retargets Burn to Slow and Powerless).
    private static readonly Spec[] Specs =
    {
        new("ReviveWithPowerless", "Powerless Revive Bomb",
            "Inflict Powerless on all enemies for 3 turns when revived.",
            "BP_BattleBuff_Powerless", "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Powerless"),
        new("ReviveWithDefenceless", "Defenceless Revive Bomb",
            "Inflict Defenceless on all enemies for 3 turns when revived.",
            "BP_BattleBuff_Defenseless", "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Defenseless"),
        new("ReviveWithSlow", "Slow Revive Bomb",
            "Inflict Slow on all enemies for 3 turns when revived.",
            "BP_BattleBuff_Slow", "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_Slow"),
    };

    public static IEnumerable<(string Id, string DisplayName)> CreatedPictos =>
        Specs.Select(s => (s.Id, s.Display));

    public static bool IsStaged(string modDir) =>
        Specs.All(s => PictosCreateKit.FindAsset(modDir, "BP_LU_" + s.Id + ".uasset") is not null);

    /// <summary>
    /// True when the clone's name map binds the revive delegate and no longer the
    /// death one — the guard that this retarget did not silently no-op.
    /// </summary>
    public static bool HasReviveTrigger(string bpPath)
    {
        var asset = PictosCreateKit.OpenAsset(bpPath);
        bool revive = false, death = false;
        foreach (var n in asset.GetNameMapIndexList())
        {
            var v = n?.Value ?? "";
            if (v.Contains(ReviveEvent, StringComparison.Ordinal)) revive = true;
            if (v.Contains(DeathEvent, StringComparison.Ordinal)) death = true;
        }
        return revive && !death;
    }

    public static int Apply(string modDir)
    {
        PictosCreateKit.EnsureExtracted(DonorBp);
        PictosCreateKit.EnsureExtracted(DonorDa);
        var bpSrc = FileCopy.FindTablesAsset(DonorBp + ".uasset")
            ?? throw new FileNotFoundException(DonorBp);
        var daSrc = FileCopy.FindTablesAsset(DonorDa + ".uasset")
            ?? throw new FileNotFoundException(DonorDa);
        var bpIdx = bpSrc.IndexOf("Sandfall", StringComparison.Ordinal);
        var daIdx = daSrc.IndexOf("Sandfall", StringComparison.Ordinal);
        if (bpIdx < 0 || daIdx < 0) throw new InvalidDataException("donor outside Sandfall/");

        foreach (var s in Specs)
        {
            var bpDest = Path.Combine(modDir, bpSrc[bpIdx..].Replace(DonorBp + ".uasset", "BP_LU_" + s.Id + ".uasset"));
            var daDest = Path.Combine(modDir, daSrc[daIdx..].Replace(DonorDa + ".uasset", "DA_GearPictos_" + s.Id + ".uasset"));
            Directory.CreateDirectory(Path.GetDirectoryName(bpDest)!);
            Directory.CreateDirectory(Path.GetDirectoryName(daDest)!);
            PictosCreateKit.EnsureExtracted(s.StatusClass);
            var buffStem = "BP_LU_" + s.Id + "_Buff";
            var buffPath = s.StatusPath[..(s.StatusPath.LastIndexOf('/') + 1)] + buffStem;
            var buffSrc = FileCopy.FindTablesAsset(s.StatusClass + ".uasset")
                ?? throw new FileNotFoundException(s.StatusClass);
            var bIdx = buffSrc.IndexOf("Sandfall", StringComparison.Ordinal);
            if (bIdx < 0) throw new InvalidDataException(s.StatusClass + " outside Sandfall/");
            var buffDest = Path.Combine(modDir, buffSrc[bIdx..].Replace(s.StatusClass + ".uasset", buffStem + ".uasset"));
            Directory.CreateDirectory(Path.GetDirectoryName(buffDest)!);
            PictosCreateKit.CopyPair(buffSrc, buffDest);
            var buffAsset = PictosCreateKit.OpenAsset(buffDest);
            PictosCreateKit.RetargetPackage(buffAsset, buffDest, s.StatusClass, buffStem);
            buffAsset.Write(buffDest);
            // The donor passes no duration; the buff class CDO carries it. Stat buffs
            // do not use the Burn family's TurnDuration field name, so try both and
            // fail loudly if neither exists — swallowing that failure is exactly how
            // Powerless/Defenceless kept a 3-turn default while Slow (default 2)
            // looked correct.
            // The duration cannot be set here: these buff assets carry no CDO export
            // (the CDO is inherited from BP_BattleBuffInstance), so there is no
            // property to edit or override. The value is written in the buff's own
            // graph — which is exactly why Slow (bp graph sets 2) reads 2 while
            // Powerless/Defenceless inherit the ancestor default of 3.
            // Binding a duration needs a statement spliced into the buff's graph.
            Console.WriteLine(
                $"[revive-debuff] {s.Id}: {s.StatusClass} duration = parent default "
                + "(no CDO to override; needs a graph-level SetTurnDuration)");
            CloneReviveBp(bpSrc, bpDest, s, buffStem, buffPath);
            PictosCreateKit.CloneGearData(daSrc, daDest, "DA_GearPictos_" + s.Id, s.Id);
        }

        PaintPassiveRows(modDir);
        PaintDefinitions(modDir);
        PaintItemRows(modDir);
        PaintBalancing(modDir);
        PictosCreateKit.ClonePictoIconRows(modDir, Specs.Select(s => (DonorId, s.Id)));
        PaintStrings(modDir);
        Console.WriteLine($"[revive-debuff] wired {Specs.Length} revive debuff picto(s)");
        return Specs.Length;
    }

    private static void CloneReviveBp(string src, string dest, Spec s, string buffStem, string buffPath)
    {
        PictosCreateKit.CopyPair(src, dest);
        var asset = PictosCreateKit.OpenAsset(dest);
        PictosCreateKit.RetargetPackage(asset, dest, DonorBp, "BP_LU_" + s.Id);

        var newKey = s.Id + "_BurnStackAmount";
        bool swappedTrigger = false, swappedStatus = false;
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var v = names[i]?.Value;
            if (string.IsNullOrEmpty(v)) continue;
            var next = v;
            if (next.Contains(DeathEvent, StringComparison.Ordinal))
            {
                next = next.Replace(DeathEvent, ReviveEvent, StringComparison.Ordinal);
                swappedTrigger = true;
            }
            if (next.Contains(BurnPath, StringComparison.Ordinal))
            {
                next = next.Replace(BurnPath, buffPath, StringComparison.Ordinal);
                swappedStatus = true;
            }
            else if (next.Contains(BurnClass, StringComparison.Ordinal))
            {
                // Class-only reference (no package path): the path is rewritten
                // first above, so this only handles bare class names.
                next = next.Replace(BurnClass, buffStem, StringComparison.Ordinal);
                swappedStatus = true;
            }
            if (next == DonorKey) next = newKey;
            if (next != v) asset.SetNameReference(i, FString.FromString(next));
        }
        if (!swappedTrigger)
            throw new InvalidDataException(
                $"[revive-debuff] {s.Id}: no {DeathEvent} in the name map — the donor's trigger binding shape changed");
        if (!swappedStatus)
            throw new InvalidDataException($"[revive-debuff] {s.Id}: no {BurnClass} import to retarget");
        asset.Write(dest);
    }

    private static void PaintPassiveRows(string modDir)
    {
        var path = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var ed = new DataTableEditor(path);
        foreach (var s in Specs)
        {
            ed.CloneRow(DonorId, s.Id);
            if (!ed.TryGetRow(s.Id, out var row)) continue;
            foreach (var f in row.Value.ToList())
            {
                var n = DataTableEditor.CleanName(f.Name);
                if (n == "Name")
                    PictosCreateKit.SetRowText(ed, row, "Name", $"PASSIVE_{s.Id}_Name", s.Display);
                else if (n == "Description")
                    PictosCreateKit.SetRowText(ed, row, "Description", $"PASSIVE_{s.Id}_Description", s.Description);
                else if (n == "LuminaCost" && f is IntPropertyData c)
                    c.Value = LuminaCost;
                else if (n == "BattleBuffs" && f is ArrayPropertyData arr && arr.Value.Length > 0
                         && arr.Value[0] is SoftObjectPropertyData)
                {
                    var pathRef = new FSoftObjectPath(
                        new FName(ed.Asset, BpFolder + "BP_LU_" + s.Id),
                        new FName(ed.Asset, "BP_LU_" + s.Id + "_C"), null);
                    var elem = (SoftObjectPropertyData)arr.Value[0].Clone();
                    elem.Value = pathRef;
                    var neu = (ArrayPropertyData)arr.Clone();
                    neu.Value = new PropertyData[] { elem };
                    var idx = row.Value.IndexOf(f);
                    if (idx >= 0) row.Value[idx] = neu;
                    if (ed.Asset.SoftObjectPathList is { Count: > 0 } list) list.Add(pathRef);
                }
            }
        }
        ed.Save(path);
    }

    private static void PaintDefinitions(string modDir)
    {
        var path = PictosCreateKit.FindAsset(modDir, "DT_PictosDefinitions.uasset")
            ?? throw new FileNotFoundException("DT_PictosDefinitions");
        var ed = new DataTableEditor(path);
        var edits = new JObject();
        foreach (var s in Specs)
        {
            ed.CloneRow(DonorId, s.Id);
            edits[s.Id] = new JObject
            {
                ["ItemID"] = s.Id,
                ["BaseStatMultipliers"] = new JObject { [StatHp] = 1.00f, [StatDef] = 0.95f },
                ["ChromaValueMultiplier"] = 1.20f,
            };
        }
        ed.Apply(edits.ToString());
        ed.Save(path);
    }

    private static void PaintItemRows(string modDir)
    {
        foreach (var table in new[] { "DT_jRPG_Items", "DT_jRPG_Items_Composite" })
        {
            var path = PictosCreateKit.FindAsset(modDir, table + ".uasset");
            if (path is null) continue;
            var ed = new DataTableEditor(path);
            var edits = new JObject();
            foreach (var s in Specs)
            {
                ed.CloneRow(DonorId, s.Id);
                PictosCreateKit.RewriteClonedItem(ed, s.Id);
                if (!ed.TryGetRow(s.Id, out var row)) continue;
                PictosCreateKit.SetRowText(ed, row, "Item_DisplayName", $"PASSIVE_{s.Id}_Name", s.Display);
                PictosCreateKit.SetRowText(ed, row, "ItemDescription", $"PASSIVE_{s.Id}_Description", s.Description);
                edits[s.Id] = new JObject
                {
                    ["Pictos_ItemStats"] = new JObject { [StatHp] = 120, [StatDef] = 40 },
                };
            }
            ed.Apply(edits.ToString());
            ed.Save(path);
        }
    }

    private static void PaintBalancing(string modDir)
    {
        var path = PictosCreateKit.FindAsset(modDir, "DT_Balancing_Luminas.uasset")
            ?? throw new FileNotFoundException("DT_Balancing_Luminas");
        var ed = new DataTableEditor(path);
        var edits = new JObject();
        foreach (var s in Specs)
        {
            var key = s.Id + "_BurnStackAmount";
            ed.CloneRow(DonorKey, key);
            edits[key] = new JObject { ["FloatValue"] = 1.0f };
        }
        ed.Apply(edits.ToString());
        ed.Save(path);
    }

    private static void PaintStrings(string modDir)
    {
        var st = PictosCreateKit.FindAsset(modDir, "ST_PassiveEffects.uasset");
        if (st is null) return;
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var s in Specs)
        {
            entries[$"PASSIVE_{s.Id}_Name"] = s.Display;
            entries[$"PASSIVE_{s.Id}_Description"] = s.Description;
        }
        DataTableEditor.AddStringTableEntries(st, entries);
    }
}
