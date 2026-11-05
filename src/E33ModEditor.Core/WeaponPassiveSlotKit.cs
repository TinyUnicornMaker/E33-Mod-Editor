using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace E33ModEditor.Core;

/// <summary>
/// Adds the fifth weapon-passive slot (<c>Level5Lumina</c>).
///
/// The engine-side ladder already has five quality steps: <c>EWeaponQuality</c>
/// holds enumerators 0–5, <c>GetWeaponQualityLevelsEnum</c> returns 1–5, and
/// <c>BP_ItemInstance_Gear_Weapon.GetUnlockedPassiveEffectsIDs</c> loops
/// qualities 0–5. Quality 5 (level 333+, the end of the T5 band) simply had
/// nothing to grant because <c>S_WeaponDefinition</c> only stored four
/// <c>LevelNLumina</c> fields and <c>GetWeaponPassivesByUpgradeLevel</c> only
/// had keys 1–4.
///
/// This kit:
/// <list type="number">
/// <item>adds a <c>Level5Lumina</c> Name property to the <c>S_WeaponDefinition</c>
/// UserDefinedStruct (both the <c>LoadedProperties</c> schema and the default
/// instance) — the struct is a UDS, not a native <c>FWeaponDefinition</c>, so it
/// is editable,</item>
/// <item>adds the empty field to every <c>DT_WeaponDefinitions</c> row, and</item>
/// <item>adds the quality-5 → <c>Level5Lumina</c> entry to
/// <c>FL_ItemUpgradeHelpers.GetWeaponPassivesByUpgradeLevel</c>.</item>
/// </list>
/// The tooltip (<c>WBP_WeaponTooltip.LoadWeaponLuminas</c>) and the battle-side
/// collector both read that map by quality, so they pick the new entry up with
/// no further patching.
/// </summary>
public static class WeaponPassiveSlotKit
{
    public const string Field = "Level5Lumina";
    /// <summary>Fixed name so every pack is byte-identical (UDS property names embed a GUID).</summary>
    public const string PropertyName = "Level5Lumina_46_A1B2C3D4E5F60718293A4B5C6D7E8F90";

    public const string StructStem = "S_WeaponDefinition";
    public const string DefTable = "DT_WeaponDefinitions";
    public const string HelperStem = "FL_ItemUpgradeHelpers";
    public const string HelperFn = "GetWeaponPassivesByUpgradeLevel";

    public static int Apply(string modDir)
    {
        var structSrc = FileCopy.FindTablesAsset(StructStem + ".uasset")
            ?? throw new FileNotFoundException(
                $"{StructStem}.uasset not found under work/tables — add it to donor_filters.txt and re-extract.");
        var defSrc = FileCopy.FindTablesAsset(DefTable + ".uasset")
            ?? throw new FileNotFoundException($"{DefTable}.uasset not found under work/tables.");
        var helperSrc = FileCopy.FindTablesAsset(HelperStem + ".uasset")
            ?? throw new FileNotFoundException(
                $"{HelperStem}.uasset not found under work/tables — add it to donor_filters.txt and re-extract.");

        // Reuse anything an earlier kit (WeaponOverLevelKit patches the helper's
        // quality retime) already staged, so we only add to it. Parse the table
        // with the current (four-slot) schema before the schema grows.
        var defDest = FileCopy.FindUnder(modDir, DefTable + ".uasset") ?? CopyIntoMod(defSrc, modDir);
        var dt = new DataTableEditor(defDest);
        bool dtHasSlot = dt.Table.Table.Data.Count > 0
            && dt.Table.Table.Data.All(r => r.Value.Any(p => p.Name.ToString() == PropertyName));

        var structDest = FileCopy.FindUnder(modDir, StructStem + ".uasset") ?? CopyIntoMod(structSrc, modDir);
        var uds = new UAsset(structDest, EngineVersion.VER_UE5_4, DataTableEditor.Mappings);
        var ue = uds.Exports.OfType<UserDefinedStructExport>().FirstOrDefault()
            ?? throw new InvalidDataException($"{StructStem} is not a UserDefinedStruct.");
        bool udsHasSlot = ue.LoadedProperties.Any(p => p.Name.ToString() == PropertyName);

        int edits = 0;

        if (!udsHasSlot)
        {
            var srcProp = ue.LoadedProperties.First(p => DataTableEditor.CleanName(p.Name) == "Level4Lumina");
            var fname = new FName(uds, PropertyName);
            ue.LoadedProperties = ue.LoadedProperties.Append(new FGenericProperty
            {
                SerializedType = srcProp.SerializedType,
                Name = fname,
                Flags = srcProp.Flags,
                ArrayDim = srcProp.ArrayDim,
                ElementSize = srcProp.ElementSize,
                PropertyFlags = srcProp.PropertyFlags,
                RepIndex = srcProp.RepIndex,
                RepNotifyFunc = srcProp.RepNotifyFunc,
                BlueprintReplicationCondition = srcProp.BlueprintReplicationCondition,
            }).ToArray();

            var srcData = (NamePropertyData)ue.StructData.First(p => DataTableEditor.CleanName(p.Name) == "Level4Lumina");
            var empty = ((NamePropertyData)ue.StructData.First(p => DataTableEditor.CleanName(p.Name) == "Level1Lumina")).Value;
            var newData = new NamePropertyData(fname) { Value = empty, IsZero = empty == null };
            newData.Ancestry.Initialize(srcData.Ancestry, fname);
            ue.StructData.Add(newData);
            edits++;
        }

        // The struct's schema is re-derived from the asset on read, but the table
        // is written through the shared mappings, so register it there too.
        UsmapSchema? schema = null;
        DataTableEditor.Mappings?.Schemas.TryGetValue(StructStem, out schema);
        if (schema != null && !schema.Properties.Values.Any(p => p.Name == PropertyName))
            schema.AddOrReplaceProperty(PropertyName, schema.Properties.Count,
                new UsmapPropertyData(UsmapPropertyType.NameProperty));

        if (!udsHasSlot) uds.Write(structDest);

        if (!dtHasSlot)
        {
            var fname = new FName(dt.Asset, PropertyName);
            foreach (var row in dt.Table.Table.Data)
            {
                if (row.Value.Any(p => p.Name.ToString() == PropertyName)) continue;
                var src = (NamePropertyData)row.Value.First(p => DataTableEditor.CleanName(p.Name) == "Level4Lumina");
                var n = new NamePropertyData(fname) { Value = null, IsZero = true };
                n.Ancestry.Initialize(src.Ancestry, fname);
                row.Value.Add(n);
                edits++;
            }
            dt.Save(defDest);
        }

        var helperDest = FileCopy.FindUnder(modDir, HelperStem + ".uasset") ?? CopyIntoMod(helperSrc, modDir);
        var helper = new UAsset(helperDest, EngineVersion.VER_UE5_4, DataTableEditor.Mappings);
        var fn = helper.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == HelperFn);
        var setMap = fn?.ScriptBytecode?.OfType<EX_SetMap>().FirstOrDefault();
        if (setMap != null)
        {
            bool hasKey5 = false;
            for (int i = 0; i + 1 < setMap.Elements.Length; i += 2)
                if (setMap.Elements[i] is EX_ByteConst bc && bc.Value == 5) hasKey5 = true;
            if (!hasKey5)
            {
                var srcVal = (EX_StructMemberContext)setMap.Elements[^1];
                var fname = new FName(helper, PropertyName);
                var newVal = new EX_StructMemberContext
                {
                    StructMemberExpression = new KismetPropertyPointer(
                        new FFieldPath(new[] { fname }, srcVal.StructMemberExpression.New.ResolvedOwner)),
                    StructExpression = srcVal.StructExpression,
                };
                setMap.Elements = setMap.Elements
                    .Concat(new KismetExpression[] { new EX_ByteConst { Value = 5 }, newVal }).ToArray();
                helper.Write(helperDest);
                edits++;
            }
        }

        Console.WriteLine($"[weapon-slot5] {Field} staged ({edits} edit(s))");
        return edits;
    }

    private static string CopyIntoMod(string src, string modDir)
    {
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        var rel = idx >= 0 ? src[idx..] : Path.Combine("Sandfall", "Content", Path.GetFileName(src));
        var dest = Path.Combine(modDir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }
}
