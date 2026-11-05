using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Visages Mask Keeper: keep the full mask moveset even when all three mask
/// sub-bosses have been defeated.
///
/// BP_EnemyBattle_MaskKeeper_VisagesPhase2 drives the fight from three
/// persistent flags. UpdateDefeatedMasks() asks
/// BP_GlobalVariablesFunctionLibrary.HasPersistentFlag for
/// NID_JoyMaskDefeated / NID_AngerMaskDefeated / NID_SadnessMaskDefeated, which
/// resolves through BP_jRPG_GI_Custom.GetPersistentFlagValue — a Map_Find on the
/// game instance's NamedIDsStates map whose miss branch returns false. The three
/// booleans then gate the mask turn-actions
/// (TurnStartAction_{Joy,Anger,Sadness}Mask), the mask actors bound by
/// "Bossfight.Visages.MaskActors.*" and Skill_SummonMasks — so beating a mask
/// deletes its turn from the boss's rotation.
///
/// This kit re-points those three references at a flag of our own that nothing
/// can ever set: the clone keeps the vanilla NamedID class and shape but lives
/// at a new package path with a fresh GUID, so no save can contain it and every
/// lookup misses. The masks always read as alive, the full moveset stays, the
/// player can still fight the three sub-bosses (and keep their drops), and the
/// real flags keep gating the island's progression.
///
/// The edit is an import/name-map rename inside the boss BP: no kismet statement
/// is inserted, deleted or resized, so every bytecode offset is untouched.
/// </summary>
public static class MaskKeeperKit
{
    public const string BpStem = "BP_EnemyBattle_MaskKeeper_VisagesPhase2";
    public const string BpRelDir = "Sandfall/Content/Characters/Enemies/Axons/Mask_Keeper";
    public const string FlagStem = "NID_VisagesMasksNeverDefeated";
    public const string FlagRelDir = "Sandfall/Content/Gameplay/ProgressionFlags/Visages";

    /// <summary>Flags the boss reads to decide which masks are already dead.</summary>
    public static readonly string[] DefeatedFlags =
    {
        "NID_JoyMaskDefeated", "NID_AngerMaskDefeated", "NID_SadnessMaskDefeated",
    };

    /// <summary>
    /// Fresh identity for the mod's flag. The flag id is the asset GUID (the
    /// vanilla assets' Name property is even mislabelled), so the clone must not
    /// inherit it.
    /// </summary>
    private static readonly Guid MaskFlagGuid = new("8f2c1d5a-7b64-4c31-9a0e-33e33e33e33e");

    public static int Apply(string modDir, bool enabled = true)
    {
        if (!enabled)
        {
            Console.WriteLine("[mask-keeper] off (Gameplay Tweaks ▸ Mask Keeper)");
            return 0;
        }

        var bpSrc = FileCopy.FindTablesAsset(BpStem + ".uasset");
        var flagSrc = FileCopy.FindTablesAsset(DefeatedFlags[0] + ".uasset");
        if (bpSrc is null || flagSrc is null)
        {
            Console.WriteLine($"[mask-keeper] source extract missing ({BpStem} / {DefeatedFlags[0]}.uasset); skipped");
            return 0;
        }

        // 1. Our own flag — never written by any content, so lookups miss.
        var flagDest = Path.Combine(modDir, FlagRelDir, FlagStem + ".uasset");
        Directory.CreateDirectory(Path.GetDirectoryName(flagDest)!);
        FileCopy.AssetPair(flagSrc, flagDest);
        var flagAsset = Open(flagDest);
        Rename(flagAsset, flagDest, Path.GetFileNameWithoutExtension(flagSrc), FlagStem);
        foreach (var export in flagAsset.Exports.OfType<NormalExport>())
            foreach (var prop in export.Data)
                if (prop is GuidPropertyData guid) guid.Value = MaskFlagGuid;
        flagAsset.Write(flagDest);

        // UAssetAPI re-serialises the Guid property from its parsed inner data, so
        // the setter above does not reach the file. Stamp the 16-byte identity in
        // the .uexp ourselves: same size, nothing moves, and the load path
        // (BP_jRPG_GI_Custom.LoadPersistentFlags -> FindNamedIDByGuid) then cannot
        // resolve our asset to a saved flag's state.
        StampGuid(Path.ChangeExtension(flagDest, ".uexp"));

        // 2. Boss BP — re-point the three flag references (name-map rename only).
        var bpDest = Path.Combine(modDir, BpRelDir, BpStem + ".uasset");
        Directory.CreateDirectory(Path.GetDirectoryName(bpDest)!);
        FileCopy.AssetPair(bpSrc, bpDest);
        var bp = Open(bpDest);
        var names = bp.GetNameMapIndexList();
        int renamed = 0;
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            var next = s;
            foreach (var from in DefeatedFlags)
                next = next.Replace(from, FlagStem, StringComparison.Ordinal);
            if (next == s) continue;
            bp.SetNameReference(i, FString.FromString(next));
            renamed++;
        }
        bp.Write(bpDest);

        Console.WriteLine(renamed > 0
            ? $"[mask-keeper] {BpStem}: {renamed} reference(s) re-pointed at {FlagStem} — masks always read as alive"
            : $"[mask-keeper] {BpStem}: no mask-defeat references found");
        return renamed > 0 ? 1 : 0;
    }

    /// <summary>
    /// The vanilla mask flag's Guid as serialised in the .uexp (four uint32, LE).
    /// Found by content so the stamp never depends on a byte offset.
    /// </summary>
    private static readonly byte[] VanillaFlagGuidBytes =
    {
        0xED, 0x6A, 0x0E, 0x06, 0x7D, 0xC2, 0x43, 0x47,
        0xAB, 0x22, 0xA2, 0x41, 0xC0, 0x97, 0xD0, 0x22,
    };

    /// <summary>
    /// Replaces the flag's 16-byte identity in place. Same length, so the export's
    /// offsets are untouched; the search-and-replace on the known vanilla value
    /// keeps it offset-independent and safe to re-run.
    /// </summary>
    private static void StampGuid(string uexpPath)
    {
        if (!File.Exists(uexpPath)) return;
        var bytes = File.ReadAllBytes(uexpPath);
        var at = VanillaGuidOffset(bytes);
        if (at < 0)
        {
            Console.WriteLine($"[mask-keeper] warning: vanilla flag GUID not found in {Path.GetFileName(uexpPath)}");
            return;
        }
        MaskFlagGuid.ToByteArray().CopyTo(bytes, at);
        File.WriteAllBytes(uexpPath, bytes);
    }

    /// <summary>
    /// True while the file still carries the vanilla mask-flag GUID — i.e. a save
    /// can still hand this asset the "mask defeated" state. Guards the identity
    /// without parsing the property.
    /// </summary>
    public static bool CarriesVanillaGuid(string uexpPath)
    {
        if (!File.Exists(uexpPath)) return false;
        return VanillaGuidOffset(File.ReadAllBytes(uexpPath)) >= 0;
    }

    private static int VanillaGuidOffset(byte[] bytes)
    {
        for (int i = 0; i + VanillaFlagGuidBytes.Length <= bytes.Length; i++)
            if (bytes.AsSpan(i, VanillaFlagGuidBytes.Length).SequenceEqual(VanillaFlagGuidBytes)) return i;
        return -1;
    }

    private static UAsset Open(string path)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }

    /// <summary>Cloned-asset rename: FolderName + every name-map entry carrying the old stem or package.</summary>
    private static void Rename(UAsset asset, string destUasset, string oldStem, string newStem)
    {
        var full = destUasset.Replace('\\', '/');
        var idx = full.IndexOf("/Content/", StringComparison.OrdinalIgnoreCase);
        var rel = full[(idx + "/Content/".Length)..];
        if (rel.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            rel = rel[..^".uasset".Length];
        var newPackage = "/Game/" + rel;
        var oldPackage = asset.FolderName?.Value;
        asset.FolderName = FString.FromString(newPackage);
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            var next = s;
            if (!string.IsNullOrEmpty(oldPackage))
                next = next.Replace(oldPackage, newPackage, StringComparison.Ordinal);
            if (!next.Contains(newStem, StringComparison.Ordinal))
                next = next.Replace(oldStem, newStem, StringComparison.Ordinal);
            if (next != s) asset.SetNameReference(i, FString.FromString(next));
        }
    }
}
