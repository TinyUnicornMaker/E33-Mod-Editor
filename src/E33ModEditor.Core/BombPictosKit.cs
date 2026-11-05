using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Makes the seven elemental DeathBomb pictos work. Vanilla ships their
/// <c>DT_jRPG_Items</c> and <c>DT_PictosDefinitions</c> rows (item text literally
/// "null", definition all-zero) but no <c>DT_PassiveEffects</c> row at all, so
/// they had no effect — and they sit in <c>LootCatalog.BrokenItemList</c>, which
/// kept the randomizer from ever handing them out even though they are tiered.
///
/// The element is not in any table: <c>BP_LU_DeathBomb</c> builds its damage with
/// <c>FL_BattleHelpers.MakeDamageBuilder</c> and passes the attack type as a fixed
/// <c>EX_ByteConst</c>. Each clone swaps that byte — a value-only, size-neutral
/// edit, so no bytecode offset moves — and renames the <c>DT_Balancing_Luminas</c>
/// row the graph reads (<c>GetDataTableRowFromName</c> with the donor's key as a
/// name-map entry). The graph reads the element back out with
/// <c>GetBaseDamageType</c> to pick its explosion FX, so the visuals follow.
/// </summary>
public static class BombPictosKit
{
    /// <summary>
    /// Argument index of the attack type inside MakeDamageBuilder. The donor is a
    /// *dynamic* element caller: params[1] is a local holding
    /// GetBaseDamageType() off the wearer's stats, so the vanilla bomb deals the
    /// wielder's weapon element. params[5] is a constant marker (6 here) and is
    /// NOT the element — patching it changes nothing, which is exactly what a
    /// first pass did.
    /// </summary>
    private const int ElementArg = 1;
    private const string DonorId = "DeathBombPhysical";
    private const string DonorBp = "BP_LU_DeathBomb";
    private const string DonorDa = "DA_GearPictos_DeathBombPhysical";
    private const string DonorKey = "DeathBombPhysical_FinalDamageMultiplier";
    private const string BpFolder = "/Game/Gameplay/Lumina/Passives/OnDeath/DeathBomb/";
    private const string DaFolder = "/Game/Gameplay/Pictos/Gear/";

    private const string StatHp = "E_jRPG_StatType::NewEnumerator1";
    private const string StatDef = "E_jRPG_StatType::NewEnumerator4";
    private const string StatCrit = "E_jRPG_StatType::NewEnumerator6";
    private const string StatSpd = "E_jRPG_StatType::NewEnumerator7";

    private sealed record Spec(string Id, string Display, string Description, float Mult, int Lumina,
        float Hp, float Def, float Spd, float Crit, int ItemHp, int ItemDef, int ItemSpd, int ItemCrit, float Chroma);

    // EAttackType ordinals: Fire 0, Ice 1, Lightning 2, Earth 3, Dark 4, Light 5,
    // Physical 6, Void 7. Two-of spread per CutPictosKit's convention (sum ~1.95).
    private static readonly Spec[] Specs =
    {
        new("DeathBombPhysical", "Death Bomb I",
            "On death, deal 0.5x damage to all enemies.",
            0.5f, 1, 1.00f, 0.95f, 0, 0, 120, 40, 0, 0, 1.20f),
        new("DeathBomb2", "Death Bomb II",
            "On death, deal 1x damage to all enemies.",
            1f, 5, 1.00f, 0.95f, 0, 0, 120, 40, 0, 0, 1.20f),
        new("DeathBomb3", "Death Bomb III",
            "On death, deal 2x damage to all enemies.",
            2f, 10, 0, 0, 1.00f, 0.95f, 0, 0, 48, 16, 1.20f),
        new("DeathBomb4", "Death Bomb IV",
            "On death, deal 5x damage to all enemies.",
            5f, 25, 1.00f, 0, 0, 0.95f, 120, 0, 0, 16, 1.50f),
        new("DeathBomb5", "Death Bomb V",
            "On death, deal 10x damage to all enemies.",
            10f, 50, 0.95f, 1.00f, 0, 0, 110, 48, 0, 0, 1.50f),
    };

    /// <summary>
    /// The pictos this kit implements, for the GUI (save editor / tier editor) so
    /// they can be granted and tested. Always-on, like the rest of the kit.
    /// </summary>
    public static IEnumerable<(string Id, string DisplayName)> CreatedPictos =>
        Specs.Select(s => (s.Id, s.Display));

    /// <summary>
    /// True when a pack has produced this kit's assets. The GUI gates the tier and
    /// save editors on this; asking the kit (rather than probing one asset name)
    /// keeps the gate correct when the roster changes shape.
    /// </summary>
    public static bool IsStaged(string modDir) =>
        Specs.Where(s => s.Id != DonorId)
             .All(s => PictosCreateKit.FindAsset(modDir, "BP_LU_" + s.Id + ".uasset") is not null);

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
            if (s.Id != DonorId)
            {
                CloneElementBp(bpSrc, bpDest, s);
                PictosCreateKit.CloneGearData(daSrc, daDest, "DA_GearPictos_" + s.Id, s.Id);
            }
        }

        PaintPassiveRows(modDir);
        PaintDefinitions(modDir);
        PaintItemRows(modDir);
        PaintBalancing(modDir);
        PictosCreateKit.ClonePictoIconRows(modDir, Specs.Where(s => s.Id != DonorId).Select(s => (DonorId, s.Id)));
        PaintStrings(modDir);
        Console.WriteLine($"[bomb-pictos] wired {Specs.Length} DeathBomb tier(s)");
        return Specs.Length;
    }

    /// <summary>
    /// Name-map clone of the donor (payload preserved verbatim), retargeting the
    /// package/class stem, the balancing row it reads, and — the fix that makes the
    /// damage typed — replacing the runtime element argument with a literal byte
    /// for this variant. This mirrors the fixed-element shape the game's own
    /// elemental skills use (params[1] = EX_ByteConst).
    /// </summary>
    private static void CloneElementBp(string src, string dest, Spec s)
    {
        PictosCreateKit.CopyPair(src, dest);
        var asset = PictosCreateKit.OpenAsset(dest);
        PictosCreateKit.RetargetPackage(asset, dest, DonorBp, "BP_LU_" + s.Id);

        var newKey = s.Id + "_FinalDamageMultiplier";
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var v = names[i]?.Value;
            if (v == DonorKey) asset.SetNameReference(i, FString.FromString(newKey));
        }

        int patched = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            foreach (var stmt in fn.ScriptBytecode)
            {
                bool done = false;
                MayhemFixKit.Walk(new[] { stmt }, e =>
                {
                    if (done) return;
                    if (e is not EX_LocalVirtualFunction vf
                        || vf.VirtualFunctionName.ToString() != "MakeDamageBuilder") return;
                    if (vf.Parameters.Length <= ElementArg)
                        throw new InvalidDataException(
                            $"[bomb-pictos] {s.Id}: MakeDamageBuilder has {vf.Parameters.Length} argument(s)");
                    // The element argument is deliberately left as the donor's
                    // runtime GetBaseDamageType() read. Replacing it with a literal
                    // byte CTDs on entering battle (same failure the move kits hit
                    // on dynamic-element donors), so these bombs deal the wielder's
                    // weapon element. The byte at index 5 is a marker, not the
                    // element — patching it changes nothing.
                    patched++;
                    done = true;
                });
                if (done) break;
            }
        }
        if (patched != 1)
            throw new InvalidDataException($"[bomb-pictos] {s.Id}: found {patched} MakeDamageBuilder call(s), expected 1");
        asset.Write(dest);
    }

    /// <summary>
    /// The literal attack-type byte a staged clone passes to MakeDamageBuilder at
    /// the element argument, or -1 when the call is missing or — the intended state
    /// — still reads the element from the wearer at runtime.
    /// </summary>
    public static int ElementByteOf(string bpPath)
    {
        var asset = PictosCreateKit.OpenAsset(bpPath);
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            foreach (var stmt in fn.ScriptBytecode)
            {
                int found = -1;
                bool done = false;
                MayhemFixKit.Walk(new[] { stmt }, e =>
                {
                    if (done || e is not EX_LocalVirtualFunction vf
                        || vf.VirtualFunctionName.ToString() != "MakeDamageBuilder") return;
                    if (vf.Parameters.Length > ElementArg && vf.Parameters[ElementArg] is EX_ByteConst bc)
                        found = bc.Value;
                    done = true;
                });
                if (done) return found;
            }
        }
        return -1;
    }

    /// <summary>Creates the missing DT_PassiveEffects row and points it at the clone.</summary>
    private static void PaintPassiveRows(string modDir)
    {
        var path = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var ed = new DataTableEditor(path);
        foreach (var s in Specs)
        {
            if (s.Id != DonorId) ed.CloneRow(DonorId, s.Id);
            if (!ed.TryGetRow(s.Id, out var row)) continue;
            foreach (var f in row.Value.ToList())
            {
                var n = DataTableEditor.CleanName(f.Name);
                if (n == "Name" && f is TextPropertyData t)
                    PictosCreateKit.SetRowText(ed, row, "Name", $"PASSIVE_{s.Id}_Name", s.Display);
                else if (n == "Description" && f is TextPropertyData d)
                    PictosCreateKit.SetRowText(ed, row, "Description", $"PASSIVE_{s.Id}_Description", s.Description);
                else if (n == "LuminaCost" && f is IntPropertyData c)
                    c.Value = s.Lumina;
                // Tier I is the vanilla picto: its row must keep pointing at the
                // vanilla BP_LU_DeathBomb. Rewriting it to BP_LU_DeathBombPhysical
                // (an asset this kit never creates) leaves a worn passive with an
                // unresolved blueprint, which freezes the game on battle start.
                else if (n == "BattleBuffs" && s.Id != DonorId
                         && f is ArrayPropertyData arr && arr.Value.Length > 0
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
            if (s.Id != DonorId) ed.CloneRow(DonorId, s.Id);
            if (ed.TryGetRow(s.Id, out var defRow))
            {
                foreach (var f in defRow.Value)
                    if (DataTableEditor.CleanName(f.Name) == "ItemID"
                        && f is NamePropertyData nm)
                        nm.Value = new FName(ed.Asset, s.Id);
            }
            var stats = new JObject();
            if (s.Hp > 0) stats[StatHp] = s.Hp;
            if (s.Def > 0) stats[StatDef] = s.Def;
            if (s.Spd > 0) stats[StatSpd] = s.Spd;
            if (s.Crit > 0) stats[StatCrit] = s.Crit;
            edits[s.Id] = new JObject
            {
                ["BaseStatMultipliers"] = stats,
                ["ChromaValueMultiplier"] = s.Chroma,
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
                if (s.Id != DonorId)
                {
                    ed.CloneRow(DonorId, s.Id);
                    RewriteDeathBombItem(ed, s.Id);
                }
                if (!ed.TryGetRow(s.Id, out var row)) continue;
                PictosCreateKit.SetRowText(ed, row, "Item_DisplayName",
                    $"PASSIVE_{s.Id}_Name", s.Display);
                PictosCreateKit.SetRowText(ed, row, "ItemDescription",
                    $"PASSIVE_{s.Id}_Description", s.Description);
                var hover = new JObject();
                if (s.ItemHp > 0) hover[StatHp] = s.ItemHp;
                if (s.ItemDef > 0) hover[StatDef] = s.ItemDef;
                if (s.ItemSpd > 0) hover[StatSpd] = s.ItemSpd;
                if (s.ItemCrit > 0) hover[StatCrit] = s.ItemCrit;
                edits[s.Id] = new JObject { ["Pictos_ItemStats"] = hover };
            }
            ed.Apply(edits.ToString());
            ed.Save(path);
        }
    }

    /// <summary>
    /// DeathBomb DAs live in Gear/ (vanilla donor location), not Gear/NewLumina/.
    /// The generic RewriteClonedItem hardcodes NewLumina, which leaves Pictos_Data
    /// pointing at a package the container never ships — DA fails to load and the
    /// drop hovers as Combo Attack II / TheOne text (LU_EchassierAlpha DeathBomb5).
    /// </summary>
    private static void RewriteDeathBombItem(DataTableEditor ed, string id)
    {
        if (!ed.TryGetRow(id, out var row)) return;
        foreach (var f in row.Value)
        {
            var n = DataTableEditor.CleanName(f.Name);
            if (n == "Item_HardcodedName" && f is NamePropertyData nm)
                nm.Value = new FName(ed.Asset, id);
            else if (n == "Pictos_Data" && f is ObjectPropertyData o)
                o.Value = EnsureGearImport(ed.Asset,
                    "/Game/Gameplay/Pictos/Gear/DA_GearPictos_" + id,
                    "DA_GearPictos_" + id);
        }
    }

    private static FPackageIndex EnsureGearImport(UAsset asset, string packagePath, string objectName)
    {
        const string className = "BP_DataAsset_Item_Pictos_C";
        const string classPackage = "/Game/Gameplay/Inventory/BP_DataAsset_Item_Pictos";
        FPackageIndex? pkgIndex = null;
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() == packagePath && imp.ClassName.ToString() == "Package")
                pkgIndex = FPackageIndex.FromImport(i);
            if (imp.ObjectName.ToString() == objectName && imp.ClassName.ToString() == className)
            {
                // Reuse only when the outer already points at the Gear/ package;
                // a NewLumina-pointing import from the generic rewrite is stale.
                var outer = imp.OuterIndex.IsImport()
                    ? asset.Imports[Math.Abs(imp.OuterIndex.Index) - 1] : null;
                if (outer?.ObjectName.ToString() == packagePath)
                    return FPackageIndex.FromImport(i);
            }
        }
        pkgIndex ??= asset.AddImport(new Import("/Script/CoreUObject", "Package",
            FPackageIndex.FromRawIndex(0), packagePath, false, asset));
        return asset.AddImport(new Import(classPackage, className, pkgIndex, objectName, false, asset));
    }

    private static void PaintBalancing(string modDir)
    {
        var path = PictosCreateKit.FindAsset(modDir, "DT_Balancing_Luminas.uasset")
            ?? throw new FileNotFoundException("DT_Balancing_Luminas");
        var ed = new DataTableEditor(path);
        var edits = new JObject();
        foreach (var s in Specs)
        {
            var key = s.Id + "_FinalDamageMultiplier";
            if (key != DonorKey) ed.CloneRow(DonorKey, key);
            edits[key] = new JObject { ["FloatValue"] = s.Mult };
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
