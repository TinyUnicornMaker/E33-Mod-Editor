using E33ModEditor.Core.Localization;
using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Clones cooked weapon lumina Blueprints into new passive ids with
/// <b>their own</b> DT_Balancing_Luminas keys. Vanilla keys are never written.
/// Specs live in Weapons/Data/weapon_effects.json (overlay: work/weapon_effects.json).
/// </summary>
public static class WeaponEffectsCreateKit
{
    public const string IdPrefix = "WepFx_";
    public const string CreatedFolder = "Sandfall/Content/Gameplay/Lumina/Passives_Weapons/Created";
    public const string CreatedPackage = "/Game/Gameplay/Lumina/Passives_Weapons/Created";

    public const string FamilyDirect = "direct";
    public const string FamilyElementalSkillDmg = "elemental-skill-dmg";
    public const string FamilyStainEcho = "stain-echo";
    public const string FamilyStainStart = "stain-start";

    public static string OverlayPath =>
        Path.Combine(EditorPaths.RepoRoot, "work", "weapon_effects.json");

    public static string StockSourcePath =>
        Path.Combine(EditorPaths.RepoRoot, "src", "E33ModEditor.Core", "Weapons", "Data", "weapon_effects.json");

    public sealed record BalancingKey(string Key, string CloneFrom, float Value);

    public sealed record EffectSpec(
        string Id,
        string DisplayName,
        string Description,
        string Family,
        string SourceBp,
        string SourcePassive,
        string? SourcePathContains,
        string? Character,
        string? Element,
        IReadOnlyList<(string From, string To)> ReplaceNames,
        IReadOnlyList<BalancingKey> Balancing,
        string? FromStain = null,
        string? ToStain = null,
        IReadOnlyList<(int From, int To)>? PatchInts = null,
        IReadOnlyList<(double From, double To)>? PatchFloats = null);

    private static readonly object LoadLock = new();
    private static IReadOnlyList<EffectSpec>? _specs;

    public static IReadOnlyList<EffectSpec> Specs
    {
        get
        {
            lock (LoadLock)
            {
                _specs ??= LoadSpecs();
                return _specs;
            }
        }
    }

    public static void Reload()
    {
        lock (LoadLock) _specs = LoadSpecs();
    }

    public static bool TryGet(string id, out EffectSpec spec)
    {
        spec = Specs.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase))!;
        return spec is not null;
    }

    public static bool HasGeneratedAssets(string modDir) =>
        Directory.Exists(Path.Combine(modDir, CreatedFolder))
        && Directory.EnumerateFiles(Path.Combine(modDir, CreatedFolder), "BP_LU_*.uasset",
            SearchOption.AllDirectories).Any();

    public static int Inject(string modDir, bool copyTables = true)
    {
        Reload();
        if (Specs.Count == 0) return 0;
        PruneStale(modDir);
        if (copyTables) CopyTables(modDir);
        int added = 0;
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var spec in Specs)
        {
            Validate(spec);
            CloneGraph(modDir, spec);
            strings[$"PASSIVE_{spec.Id}_Name"] = spec.DisplayName;
            strings[$"PASSIVE_{spec.Id}_Description"] = spec.Description;
            added++;
        }
        foreach (var (rowId, text) in VanillaTextOverrides)
            strings[$"PASSIVE_{rowId}_Description"] = text;
        ApplyVanillaBpPatches(modDir);
        ApplyBurnStackRewrites(modDir);
        InjectPassiveRows(modDir);
        InjectBalancing(modDir);
        InjectStrings(modDir, strings);
        return added;
    }

    public static void RemoveGenerated(string modDir)
    {
        var dir = Path.Combine(modDir, CreatedFolder);
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            File.Delete(f);
    }

    public static void CopyTables(string modDir)
    {
        foreach (var table in new[] { "DT_PassiveEffects", "DT_Balancing_Luminas" })
        {
            try { CopyTableIntoMod(table, modDir); }
            catch { /* optional until preload */ }
        }
        EnsureExtracted("ST_PassiveEffects");
        var st = FileCopy.FindTablesAsset("ST_PassiveEffects.uasset");
        if (st != null)
        {
            var idx = st.IndexOf("Sandfall", StringComparison.Ordinal);
            if (idx >= 0)
                FileCopy.AssetPair(st, Path.Combine(modDir, st[idx..]));
        }
    }

    private static void CloneGraph(string modDir, EffectSpec spec)
    {
        if (spec.Family == FamilyElementalSkillDmg)
            CloneElementalSkillDmg(modDir, spec);
        else if (spec.Family == FamilyStainEcho)
            CloneStainEcho(modDir, spec);
        else if (spec.Family == FamilyStainStart)
            CloneStainStart(modDir, spec);
        else
            CloneDirect(modDir, spec);
        if (spec.PatchInts is { Count: > 0 })
        {
            var dest = Path.Combine(modDir, CreatedFolder, "BP_LU_" + spec.Id + ".uasset");
            foreach (var (from, to) in spec.PatchInts)
                PatchIntConst(dest, from, to);
        }
        if (spec.PatchFloats is { Count: > 0 })
        {
            var dest = Path.Combine(modDir, CreatedFolder, "BP_LU_" + spec.Id + ".uasset");
            foreach (var (from, to) in spec.PatchFloats)
                PatchFloatConst(dest, from, to);
        }
        if (spec.SourceBp == "BP_LU_CleasDeath")
            CloneCleasBuff(modDir, spec);
        // Critical Chain: same crit-counter surgery the picto uses, applied to
        // the weapon clone (counter clamped at CriticalChainMaxStacks).
        if (spec.Id.StartsWith("WepFx_CriticalChain", StringComparison.Ordinal))
            PictosMagnitudeKit.PatchCriticalChain(
                Path.Combine(modDir, CreatedFolder, "BP_LU_" + spec.Id + ".uasset"),
                PictosMagnitudeKit.CriticalChainMaxStacks);
        // ConsumingAttack: base versions stay DamageReason==4 (base attack only).
        // Skills versions (Id ends with Skills) become DamageReason==1 (SkillCast).
        if (spec.SourceBp == "BP_LU_ConsumingAttack" && spec.Id.EndsWith("Skills", StringComparison.Ordinal))
        {
            var dest = Path.Combine(modDir, CreatedFolder, "BP_LU_" + spec.Id + ".uasset");
            PatchConsumeSkillsOnly(dest);
        }
    }

    /// <summary>
    /// Vanilla ConsumingAttack gates on <c>DamageReason == 4</c> (base attack).
    /// The "Skills" variants used to flip that to <c>!= 4</c>, which also matched
    /// Burn/status damage-over-time ticks (<c>DamageReason == 2</c>, "Invalid")
    /// and every counter/Lumina/item hit. Because the burn aggregator credits
    /// its DOT to the character who first applied Burn, a wearer with this
    /// passive would eat the whole Burn stack on the DOT tick — making Burn
    /// impossible to stack for the rest of the party.
    ///
    /// Skill damage is <c>DamageReason == 1</c> (see vanilla
    /// <c>BP_LU_ThunderSkillConsumeBurn</c> and every skill script's
    /// MakeDamageBuilder call). Patch the constant instead of swapping the
    /// comparison so DOT ticks no longer trigger the consume.
    /// </summary>
    private static void PatchConsumeSkillsOnly(string uasset)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uasset, EngineVersion.VER_UE5_4, map)
            : new UAsset(uasset, EngineVersion.VER_UE5_4);
        int patched = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            uint off = 0;
            foreach (var e in fn.ScriptBytecode)
            {
                e.Visit(asset, ref off, (child, _) =>
                {
                    if (child is EX_FinalFunction ff)
                    {
                        var fnName = GetFuncName(asset, ff.StackNode);
                        if (fnName.Contains("EqualEqual_ByteByte", StringComparison.Ordinal)
                            && ff.Parameters.Length == 2
                            && ff.Parameters[1] is EX_ByteConst bc
                            && bc.Value == 4)
                        {
                            bc.Value = 1;
                            patched++;
                        }
                    }
                });
            }
        }
        if (patched == 0)
            throw new InvalidDataException(
                $"[wfx] {Path.GetFileName(uasset)}: skills-only consume patch found no DamageReason==4 check");
        asset.Write(uasset);
        Console.WriteLine($"[wfx] {Path.GetFileName(uasset)}: consume gated to DamageReason==1 (skill)");
    }

    private static void PatchSelfBurnStacks(string uasset, int want)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null ? new UAsset(uasset, EngineVersion.VER_UE5_4, map) : new UAsset(uasset, EngineVersion.VER_UE5_4);
        int patched = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            uint off = 0;
            foreach (var e in fn.ScriptBytecode)
            {
                e.Visit(asset, ref off, (child, _) =>
                {
                    if (child is EX_ByteConst bc && bc.Value == 1)
                    {
                        bc.Value = (byte)want;
                        patched++;
                    }
                    else if (child is EX_IntConst ic && ic.Value == 1)
                    {
                        ic.Value = want;
                        patched++;
                    }
                });
            }
        }
        if (patched == 0)
        {
            // Fallback: Source has no 1 — try ByteConst 4 (ActivateBuff duration/stack) as proxy for 1-stack
            // For SelfBurn2/3 we want 2/3 stacks, so patch 4 -> want
            foreach (var fn in asset.Exports.OfType<FunctionExport>())
            {
                if (fn.ScriptBytecode is null) continue;
                uint off = 0;
                foreach (var e in fn.ScriptBytecode)
                {
                    e.Visit(asset, ref off, (child, _) =>
                    {
                        if (child is EX_ByteConst bc2 && bc2.Value == 4)
                        {
                            bc2.Value = (byte)want;
                            patched++;
                        }
                    });
                }
            }
        }
        if (patched > 0) { Console.WriteLine($"[patch] {System.IO.Path.GetFileName(uasset)} patched {patched} Burn stacks -> {want}"); asset.Write(uasset); }
        else Console.WriteLine($"[patch] {System.IO.Path.GetFileName(uasset)} no Byte/Int 1/4 found for Burn stacks (want {want})");
    }


    private static string GetFuncName(UAsset asset, FPackageIndex idx)
    {
        if (idx == null || idx.IsNull()) return "";
        try
        {
            if (idx.IsImport())
            {
                var imp = idx.ToImport(asset);
                return imp?.ObjectName?.ToString() ?? "";
            }
            if (idx.IsExport())
                return idx.ToExport(asset)?.ObjectName?.ToString() ?? "";
        }
        catch { }
        return idx.Index.ToString();
    }




    private static void CloneDirect(string modDir, EffectSpec spec)
    {
        var src = FindSourceBp(spec);
        var dest = Path.Combine(modDir, CreatedFolder, "BP_LU_" + spec.Id + ".uasset");
        CloneBlueprint(src, dest, "BP_LU_" + spec.Id, spec.ReplaceNames);
    }

    private static void CloneStainStart(string modDir, EffectSpec spec)
    {
        // Stain-start BPs are children of OnBattleStart1 that override
        // StainsType / StainsAmount on their CDO and inherit the graph.
        // Open the specific elemental source (FireStart etc) from a shared
        // extraction root so its CDO parses with the parent's schemas.
        var shared = EnsureExtractedShared("StainStart", spec.SourceBp, "BP_LU_Stains_OnBattleStart1");
        var src = FindAsset(shared, spec.SourceBp + ".uasset") ?? FindBp(spec.SourceBp);
        var dest = Path.Combine(modDir, CreatedFolder, "BP_LU_" + spec.Id + ".uasset");
        // Prime the parent so the child's CDO can resolve its schema chain.
        var parentAssetPath = FindAsset(shared, "BP_LU_Stains_OnBattleStart1.uasset") ?? FindBp("BP_LU_Stains_OnBattleStart1");
        _ = OpenAsset(parentAssetPath);
        var asset = OpenAsset(src);
        var srcStem = Path.GetFileNameWithoutExtension(src);
        var newStem = "BP_LU_" + spec.Id;
        RetargetPackage(asset, dest, srcStem, newStem);
        // Balancing key rename (Stains_FireStart_StainsAmount -> WepFx_FireStart2_StainsAmount)
        var ordered = spec.ReplaceNames.Where(r => !string.IsNullOrEmpty(r.From) && r.From != r.To).OrderByDescending(r => r.From.Length).ToList();
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            var next = s;
            foreach (var (from, to) in ordered) if (next.Contains(from, StringComparison.Ordinal)) next = next.Replace(from, to, StringComparison.Ordinal);
            if (next != s) asset.SetNameReference(i, FString.FromString(next));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        asset.Write(dest);
        var amount = spec.Balancing.FirstOrDefault()?.Value ?? 1f;
        PatchStainAmount(dest, (int)amount);
    }

    private static string EnsureExtractedShared(string rootName, params string[] filters)
    {
        var root = Path.Combine(E33ModEditor.Core.EditorPaths.RepoRoot, "work", "tables", rootName);
        Directory.CreateDirectory(root);
        foreach (var filter in filters)
        {
            if (FindAsset(root, filter + ".uasset") != null) continue;
            new E33ModEditor.Core.Retoc().ToLegacy(root, filter);
        }
        return root;
    }

    private static UAsset OpenAsset(string path)
    {
        var map = E33ModEditor.Core.DataTableEditor.Mappings;
        return map != null ? new UAsset(path, EngineVersion.VER_UE5_4, map) : new UAsset(path, EngineVersion.VER_UE5_4);
    }

    private static void CloneStainEcho(string modDir, EffectSpec spec)
    {
        var src = FindBp("BP_LU_Stains_Stains_+StainOnSpecificStainGenerated");
        var dest = Path.Combine(modDir, CreatedFolder, "BP_LU_" + spec.Id + ".uasset");
        CloneBlueprint(src, dest, "BP_LU_" + spec.Id, spec.ReplaceNames);
        SetTypeToTypeMap(dest, spec.FromStain ?? "NewEnumerator0", spec.ToStain ?? "NewEnumerator0");
    }

    /// <summary>
    /// The vanilla parent graph fires CreateStainByType only when the gained
    /// stain equals the InitialStainTypeToListen property AND TypeToTypeMap
    /// has an entry for it. Cooked enum FNames are fully qualified
    /// ("EAttackType::NewEnumerator2") — bare leaf names never match, which is
    /// why early echo clones were silent no-ops in game.
    /// </summary>
    private static void SetTypeToTypeMap(string uasset, string fromEnum, string toEnum)
    {
        var map = DataTableEditor.Mappings;
        UAsset Open() => map != null
            ? new UAsset(uasset, EngineVersion.VER_UE5_4, map)
            : new UAsset(uasset, EngineVersion.VER_UE5_4);

        var asset = Open();
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal))
            ?? throw new InvalidDataException(Path.GetFileName(uasset) + ": no CDO export");
        var mp = cdo.Data.OfType<MapPropertyData>()
            .FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "TypeToTypeMap")
            ?? throw new InvalidDataException(Path.GetFileName(uasset) + ": no TypeToTypeMap on CDO");
        if (mp.Value.Count == 0)
            throw new InvalidDataException(Path.GetFileName(uasset) + ": TypeToTypeMap is empty");

        var sampleKey = mp.Value.First().Key;
        var sampleValue = mp.Value.First().Value;
        var enumType = EnumTypeNameOf(sampleKey);
        var wantKey = enumType + "::" + fromEnum;
        var wantValue = enumType + "::" + toEnum;

        PropertyData Mk(PropertyData src, string qualified)
        {
            var c = (PropertyData)src.Clone();
            switch (c)
            {
                case BytePropertyData b:
                    b.EnumValue = new FName(asset, qualified);
                    break;
                case EnumPropertyData e:
                    e.Value = new FName(asset, qualified);
                    break;
                default:
                    throw new InvalidDataException(
                        Path.GetFileName(uasset) + ": unsupported TypeToTypeMap entry " + c.GetType().Name);
            }
            return c;
        }

        var nk = Mk(sampleKey, wantKey);
        mp.Value.Clear();
        mp.Value.Add(nk, Mk(sampleValue, wantValue));

        // Gate property: absent from the vanilla CDO (defaults omitted), so it
        // must be added. Cloning the typed map key keeps PropertyTypeName valid.
        var listener = cdo.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "InitialStainTypeToListen");
        if (listener is EnumPropertyData le)
            le.Value = new FName(asset, wantKey);
        else if (listener is BytePropertyData lb)
            lb.EnumValue = new FName(asset, wantKey);
        else if (listener is null)
        {
            if (nk is not EnumPropertyData nke)
                throw new InvalidDataException(Path.GetFileName(uasset) + ": cannot derive listener property");
            var neu = (EnumPropertyData)nke.Clone();
            neu.Name = new FName(asset, "InitialStainTypeToListen");
            neu.ArrayIndex = 0;
            neu.Value = new FName(asset, wantKey);
            var at = cdo.Data.IndexOf(mp);
            cdo.Data.Insert(at < 0 ? cdo.Data.Count : at + 1, neu);
        }
        else
            throw new InvalidDataException(
                Path.GetFileName(uasset) + ": unexpected listener property " + listener.GetType().Name);

        asset.Write(uasset);

        // Round-trip: reopen and prove the game-readable state is exact.
        var back = Open();
        var vcdo = back.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        var vmp = vcdo?.Data.OfType<MapPropertyData>()
            .FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "TypeToTypeMap");
        if (vcdo is null || vmp is null || vmp.Value.Count != 1)
            throw new InvalidOperationException(
                Path.GetFileName(uasset) + " verification failed: TypeToTypeMap did not round-trip");
        var vk = EnumValueNameOf(vmp.Value.First().Key);
        var vv = EnumValueNameOf(vmp.Value.First().Value);
        var vl = vcdo.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "InitialStainTypeToListen");
        var vlv = EnumValueNameOf(vl);
        // Top-level enum properties are re-expressed through the usmap enum
        // table on read (bare leaf names); only the leaf is stable there.
        if (vk != wantKey || vv != wantValue || LeafOf(vlv) != fromEnum)
            throw new InvalidOperationException(
                $"{Path.GetFileName(uasset)} verification failed: map {vk}=>{vv} listener={vlv}, wanted {wantKey}=>{wantValue} listener={wantKey}");
        int bare = back.GetNameMapIndexList().Count(n =>
            (n.Value ?? "").StartsWith("NewEnumerator", StringComparison.Ordinal));
        if (bare > 0)
            throw new InvalidOperationException(
                Path.GetFileName(uasset) + $" verification failed: {bare} bare 'NewEnumerator*' names leaked into the name map");
    }

    private static string EnumTypeNameOf(PropertyData p) => p switch
    {
        EnumPropertyData e => NonNone(e.PropertyTypeName?.GetParameter(0).GetName()?.Value?.Value),
        BytePropertyData b => NonNone(b.EnumType?.Value?.Value),
        _ => "EAttackType",
    };

    private static string? EnumValueNameOf(PropertyData? p) => p switch
    {
        EnumPropertyData e => e.Value?.Value?.Value,
        BytePropertyData b when b.EnumValue?.Value?.Value is { Length: > 0 } s && s != "None" => s,
        BytePropertyData b => $"byte:{b.Value}",
        _ => null,
    };

    private static string NonNone(string? s) =>
        string.IsNullOrEmpty(s) || s == "None" ? "EAttackType" : s;

    private static string LeafOf(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        var i = v.IndexOf("::", StringComparison.Ordinal);
        return i >= 0 ? v[(i + 2)..] : v;
    }

    private static byte StainByte(string enumerator) => enumerator switch
    {
        "NewEnumerator0" => 2,
        "NewEnumerator1" => 3,
        "NewEnumerator2" => 4,
        "NewEnumerator3" => 5,
        "NewEnumerator4" => 6,
        "NewEnumerator5" => 7,
        "NewEnumerator7" => 8,
        _ => 2,
    };

    private static void PatchStainAmount(string uasset, int amount)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null ? new UAsset(uasset, EngineVersion.VER_UE5_4, map) : new UAsset(uasset, EngineVersion.VER_UE5_4);
        var cdo = asset.Exports.OfType<NormalExport>().FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        if (cdo is null) return;
        var prop = cdo.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "StainsAmount");
        if (prop is IntPropertyData ip)
        {
            ip.Value = amount;
            asset.Write(uasset);
        }
        else if (prop is BytePropertyData bp)
        {
            bp.Value = (byte)amount;
            asset.Write(uasset);
        }
    }

    private static void PatchByteConst(string uasset, byte from, byte to)
    {
        if (from == to) return;
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uasset, EngineVersion.VER_UE5_4, map)
            : new UAsset(uasset, EngineVersion.VER_UE5_4);
        int n = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            WalkKismet(fn.ScriptBytecode, e =>
            {
                if (e is EX_ByteConst b && b.Value == from)
                {
                    b.Value = to;
                    n++;
                }
            });
        }
        if (n > 0) asset.Write(uasset);
    }

    private static void PatchIntConst(string uasset, int from, int to)
    {
        if (from == to) return;
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uasset, EngineVersion.VER_UE5_4, map)
            : new UAsset(uasset, EngineVersion.VER_UE5_4);
        int n = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            WalkKismet(fn.ScriptBytecode, e =>
            {
                if (e is EX_IntConst c && c.Value == from)
                {
                    c.Value = to;
                    n++;
                }
            });
        }
        if (n > 0) asset.Write(uasset);
    }

    private static void PatchFloatConst(string uasset, double from, double to)
    {
        if (Math.Abs(from - to) < 1e-9) return;
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uasset, EngineVersion.VER_UE5_4, map)
            : new UAsset(uasset, EngineVersion.VER_UE5_4);
        int n = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            // Full walker (handles EX_LetBool etc.) so consts nested in a
            // bool-let expression (e.g. RandomBoolWithWeight(0.2)) are reached.
            MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                if (e is EX_DoubleConst dc && Math.Abs(dc.Value - from) < 1e-6)
                {
                    dc.Value = to;
                    n++;
                }
                else if (e is EX_FloatConst fc && Math.Abs(fc.Value - from) < 1e-5)
                {
                    fc.Value = (float)to;
                    n++;
                }
            });
        }
        if (n > 0) asset.Write(uasset);
    }

    private static void CloneCleasBuff(string modDir, EffectSpec spec)
    {
        EnsureExtracted("BP_LU_CleasDeath_Buff");
        var src = FindBp("BP_LU_CleasDeath_Buff");
        var dest = Path.Combine(modDir, CreatedFolder, "BP_LU_" + spec.Id + "_Buff.uasset");
        var key = spec.Balancing.Count > 0 ? spec.Balancing[0].Key : "CleasDeath_DamageIncrease";
        CloneBlueprint(src, dest, "BP_LU_" + spec.Id + "_Buff",
            new[] { ("CleasDeath_DamageIncrease", key) });
    }

    private static void WalkKismet(IEnumerable<KismetExpression> exprs, Action<KismetExpression> visit)
    {
        foreach (var e in exprs)
        {
            visit(e);
            switch (e)
            {
                case EX_Context ctx:
                    if (ctx.ObjectExpression != null) WalkKismet(new[] { ctx.ObjectExpression }, visit);
                    if (ctx.ContextExpression != null) WalkKismet(new[] { ctx.ContextExpression }, visit);
                    break;
                case EX_Let let:
                    if (let.Variable != null) WalkKismet(new[] { let.Variable }, visit);
                    if (let.Expression != null) WalkKismet(new[] { let.Expression }, visit);
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
                case EX_JumpIfNot jn when jn.BooleanExpression != null:
                    WalkKismet(new[] { jn.BooleanExpression }, visit);
                    break;
            }
        }
    }

    private static readonly (string Element, string Enum, string ChildBp, string VanillaKey)[] Elemental =
    {
        ("Fire", "NewEnumerator0", "BP_LU_SkillsFireDMG+", "SkillsFireDMG+_DamageIncrease"),
        ("Ice", "NewEnumerator1", "BP_LU_SkillsIceDMG+", "SkillsIceDMG+_DamageIncrease"),
        ("Lightning", "NewEnumerator2", "BP_LU_SkillsLightningDMG+", "SkillsLightningDMG+_DamageIncrease"),
        ("Earth", "NewEnumerator3", "BP_LU_SkillsEarthDMG+", "SkillsEarthDMG+_DamageIncrease"),
        ("Dark", "NewEnumerator4", "BP_LU_SkillsDarkDMG+", "SkillsDarkDMG+_DamageIncrease"),
        ("Light", "NewEnumerator5", "BP_LU_SkillsLightDMG+", "SkillsLightDMG+_DamageIncrease"),
        ("Physical", "NewEnumerator6", "BP_LU_SkillsPhysicalDMG+", "SkillsPhysicalDMG+_DamageIncrease"),
        ("Void", "NewEnumerator7", "BP_LU_SkillsVoidDMG+", "SkillsVoidDMG+_DamageIncrease"),
    };

    private static void CloneElementalSkillDmg(string modDir, EffectSpec spec)
    {
        var el = Elemental.FirstOrDefault(e =>
            e.Element.Equals(spec.Element, StringComparison.OrdinalIgnoreCase));
        if (el.ChildBp is null)
            throw new InvalidDataException(
                spec.Id + " elemental-skill-dmg needs Element = Fire|Ice|Lightning|Earth|Dark|Light|Physical|Void");

        // Child BPs (SkillsFireDMG+) only set DamageType. The 8 balancing keys
        // live on BP_LU_ElementalDMG+. A cloned child cannot inherit a cloned
        // parent: the new parent class is missing from usmap. Clone the parent
        // graph as a sibling class and copy the child's element filter onto it.
        var parentSrc = FindBp("BP_LU_ElementalDMG+");
        var childSrc = FindBp(el.ChildBp);
        var dest = Path.Combine(modDir, CreatedFolder, "BP_LU_" + spec.Id + ".uasset");
        var newKey = spec.Id + "_DamageIncrease";
        var repl = Elemental.Select(e => (e.VanillaKey, newKey)).Concat(spec.ReplaceNames).ToList();
        CloneBlueprint(parentSrc, dest, "BP_LU_" + spec.Id, repl);
        CopyDefaultArrays(childSrc, dest, "CheckDamageReason", "DamageType");
    }

    private static void CopyDefaultArrays(string srcUasset, string destUasset, params string[] propNames)
    {
        var src = DataTableEditor.OpenPropertyAsset(srcUasset);
        var dest = DataTableEditor.OpenPropertyAsset(destUasset);
        var srcDef = src.Asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        var destDef = dest.Asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        if (srcDef is null || destDef is null) return;
        var want = new HashSet<string>(propNames, StringComparer.Ordinal);
        foreach (var p in srcDef.Data)
        {
            var n = p.Name?.ToString() ?? "";
            if (!want.Contains(n)) continue;
            var existing = destDef.Data.FindIndex(x => x.Name?.ToString() == n);
            var clone = (PropertyData)p.Clone();
            if (existing >= 0) destDef.Data[existing] = clone;
            else destDef.Data.Add(clone);
        }
        dest.Asset.Write(destUasset);
    }

    /// <summary>Vanilla passive tooltip rewrites (numeric, vanilla voice). Applied to
    /// the pristine rows — no clone, no balance change, BattleBuffs untouched.</summary>
    private static readonly (string RowId, string Description)[] VanillaTextOverrides =
    {
        ("TriggerLightningOnCrit", "Critical hits trigger a 0.50x Lightning strike."),
        // Counter status appliers share one voice: "Apply <Status> on Counterattack."
        ("PowerlessOnCounter", "Apply Powerless on Counterattack."),
        ("CounterDefenseless", "Apply Defenceless on Counterattack."),
        ("CounterBurn", "Apply 50 Burn on Counterattack."),
        ("APOnCounter", "Gain 5 AP on Counterattack."),
        // Burn reworks.
        ("BurnDamage", "25% increased Burn damage."),
        ("AttackBurn", "Base Attack applies 5 Burn."),
        ("LightDMGBurnOnCrit", "Light damage applies 5 Burn on Critical hits."),
        ("BurnOnCrit", "Critical hits apply 5 Burn."),
        ("CritAppliesBurn", "Critical hits apply Burn at 5x damage."),
        ("LightDamageBurn", "Applies Burn on dealing Light damage."),
        ("MarkIsBurn", "Apply 100 Burn instead of Mark."),
        ("BurnInVirtuose", "On Virtuose, all hits apply 50 Burn."),
        ("Foretell_ApplyBurnEveryXForetell", "Apply 10 Burn per Foretell applied with Skills."),
        ("Perfection_BurnOnBaseAtkPerRank", "Base Attack applies 50 Burn per Rank."),
        // Element Skill Damage ladder ^ to 100%.
        ("SkillsFireDMG+", "100% increased Fire damage with Skills."),
        ("SkillsIceDMG+", "100% increased Ice damage with Skills."),
        ("SkillsLightningDMG+", "100% increased Lightning damage with Skills."),
        ("SkillsEarthDMG+", "100% increased Earth damage with Skills."),
        ("SkillsPhysicalDMG+", "100% increased Physical damage with Skills."),
        ("SkillsDarkDMG+", "100% increased Dark damage with Skills."),
        ("SkillsLightDMG+", "100% increased Light damage with Skills."),
        ("SkillsVoidDMG+", "100% increased Void damage with Skills."),
        // Stance heal / Shield AP.
        ("Stance_HealOnSwitch", "Recover 20% Health on Stance switch."),
        ("APOnShield", "On applying Shields, also give 5 AP."),
        // Tooltip fixes: the graph reads _DamageIncrease = 0.5 (50%), the ST said 10%.
        ("AugmentedAttack_Weapon", "50% increased Base Attack damage."),
        ("AugmentedAim_Weapon", "50% increased Free Aim damage."),
        // Lune stain/monoco overcharge retunes.
        ("Stains_IncreaseDMGPerLightStain", "50% increased damage with Skills per active Light Stain."),
        ("Stains_ElementalCyclingDMG+", "Casting a Skill increases the Skill damage of all other elements by 50%."),
        ("Stains_OverchargeDMG+", "Using a Skill that consumes Stains increases damage by 20%. Can stack up to 21 times."),
        ("Masks_DMG+OverchargedSkills", "Upgraded Skills deal 100% more damage."),
    };

    /// <summary>Rewrites of existing vanilla DT_Balancing_Luminas rows. Use only
    /// to rework a named vanilla weapon passive in place (all weapons that carry
    /// it share the change) — never to retune a custom clone's source.</summary>
    private static readonly (string Key, double Value)[] VanillaBalancingOverrides =
    {
        ("APOnCounter_APAmount", 5.0),
        // Burn damage ladder I: BurnDamage (vanilla base tier).
        ("BurnDamage_BurnDamage_DamageMultiplier", 0.25),
        // Burn application reworks.
        ("LightDMGBurnOnCrit_BurnStack", 5.0),
        ("Stains_BurnOnCrit_StackToApply", 5.0),
        ("CritAppliesBurn_BurnDamageMultiplier", 5.0),
        ("LightDamageBurn_BurnChance", 1.0),
        ("MarkIsBurn_BurnStack", 100.0),
        ("Stance_BurnInVirtuose_StackToApply", 50.0),
        ("Foretell_ApplyBurnEveryXForetell_BurnDamageMultiplier", 10.0),
        ("Foretell_ApplyBurnEveryXForetell_BurnStackInterval", 1.0),
        ("Perfection_BurnOnBaseAtkPerRank_BurnAmountOnRankD", 50.0),
        ("Perfection_BurnOnBaseAtkPerRank_BurnAmountOnRankC", 100.0),
        ("Perfection_BurnOnBaseAtkPerRank_BurnAmountOnRankB", 150.0),
        ("Perfection_BurnOnBaseAtkPerRank_BurnAmountOnRankA", 200.0),
        ("Perfection_BurnOnBaseAtkPerRank_BurnAmountOnRankS", 250.0),
        // Element Skill Damage ladder -> 100%.
        ("SkillsFireDMG+_DamageIncrease", 1.0),
        ("SkillsIceDMG+_DamageIncrease", 1.0),
        ("SkillsLightningDMG+_DamageIncrease", 1.0),
        ("SkillsEarthDMG+_DamageIncrease", 1.0),
        ("SkillsPhysicalDMG+_DamageIncrease", 1.0),
        ("SkillsDarkDMG+_DamageIncrease", 1.0),
        ("SkillsLightDMG+_DamageIncrease", 1.0),
        ("SkillsVoidDMG+_DamageIncrease", 1.0),
        // Stance switch heal -> 20%.
        ("Stance_HealOnSwitch_HealPercentOnStanceSwitch", 0.2),
        // Lune stain / Monoco overcharge retunes.
        ("Stains_IncreaseDMGPerLightStain_DamageIncrease", 0.5),
        ("Stains_ElementalCyclingDMG+_DamageIncreasePerDifferentElementalDamage", 0.5),
        // 0.2/hit capped so up to 21 stain-consuming hits stack (21 x 0.2 = 4.2).
        ("Stains_OverchargeDMG+_MaxMultiplier", 4.2),
        ("Masks_DMG+OverchargedSkills_DamageIncrease", 1.0),
    };

    /// <summary>Vanilla blueprint int-constant patches for a reworked passive
    /// (the clone path only patches clones). The staged copy shadows the game
    /// asset at the same path.</summary>
    private static readonly (string Bp, int From, int To)[] VanillaBpIntPatches =
    {
        // CounterBurn stacks: BP_LU_CounterBurn spawns Burn then ChangeStackCount(5).
        ("BP_LU_CounterBurn", 5, 50),
        // AttackBurn stacks: BP_LU_AttackBurn ChangeStackCount(2) -> 5.
        ("BP_LU_AttackBurn", 2, 5),
        // APOnShield: nGainAP(1) -> 5.
        ("BP_LU_APOnShield", 1, 5),
    };

    /// <summary>Vanilla blueprint float-constant patches (e.g. a hardcoded
    /// chance). The staged copy shadows the game asset at the same path.</summary>
    private static readonly (string Bp, double From, double To)[] VanillaBpFloatPatches =
    {
        // LightDamageBurn: RandomBoolWithWeight(0.2) -> 1.0 (Burn on every Light hit).
        ("BP_LU_LightDamageBurn", 0.2, 1.0),
    };

    /// <summary>Vanilla burn passives that apply Burn through the buff's
    /// <c>BurnMultiplier</c> property (a per-stack <b>damage</b> multiplier) get
    /// rewritten to call <c>ChangeStackCount(N)</c> instead, so their "N Burn"
    /// tooltips are literally true (and count for stack synergies). The baked
    /// stack count is kept in step with the balancing row of the same name and
    /// with <see cref="VanillaTextOverrides"/>.</summary>
    private static readonly (string Bp, int Stacks)[] VanillaBurnStackRewrites =
    {
        ("BP_LU_LightDMGBurnOnCrit", 5),
        ("BP_LU_Foretell_ApplyBurnEveryXForetell", 10),
    };

    private static void InjectPassiveRows(string modDir)
    {
        var path = FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? CopyTableIntoMod("DT_PassiveEffects", modDir);
        var ed = new DataTableEditor(path);
        foreach (var spec in Specs)
        {
            if (!ed.HasRow(spec.Id))
                ed.CloneRow(spec.SourcePassive, spec.Id);
            if (!ed.TryGetRow(spec.Id, out var row)) continue;
            var bpPkg = CreatedPackage + "/BP_LU_" + spec.Id;
            var bpCls = "BP_LU_" + spec.Id + "_C";
            foreach (var f in row.Value.ToList())
            {
                var n = DataTableEditor.CleanName(f.Name);
                if (n == "Name" && f is TextPropertyData t)
                    SetLocText(t, $"PASSIVE_{spec.Id}_Name", spec.DisplayName);
                else if (n == "Description" && f is TextPropertyData d)
                    SetLocText(d, $"PASSIVE_{spec.Id}_Description", spec.Description);
                else if (n == "BattleBuffs" && f is ArrayPropertyData arr && arr.Value.Length > 0
                         && arr.Value[0] is SoftObjectPropertyData)
                {
                    var pathRef = new FSoftObjectPath(
                        new FName(ed.Asset, bpPkg), new FName(ed.Asset, bpCls), null);
                    var elem = (SoftObjectPropertyData)arr.Value[0].Clone();
                    elem.Value = pathRef;
                    var neu = (ArrayPropertyData)arr.Clone();
                    neu.Value = new PropertyData[] { elem };
                    var idx = row.Value.IndexOf(f);
                    if (idx >= 0) row.Value[idx] = neu;
                    if (ed.Asset.SoftObjectPathList is { Count: > 0 } list)
                        list.Add(pathRef);
                }
            }
        }
        foreach (var (rowId, text) in VanillaTextOverrides)
        {
            if (!ed.TryGetRow(rowId, out var vrow)) continue;
            foreach (var f in vrow.Value.ToList())
            {
                if (DataTableEditor.CleanName(f.Name) == "Description" && f is TextPropertyData d)
                    SetLocText(d, $"PASSIVE_{rowId}_Description", text);
            }
        }
        ed.Save(path);
        VerifyPassiveRows(ed, path);
    }

    /// <summary>
    /// Reopens the saved table and proves every created id exists and points
    /// its BattleBuffs at the Created package. A pack without these rows would
    /// ship passives that silently do nothing in game.
    /// </summary>
    private static void VerifyPassiveRows(DataTableEditor ed, string path)
    {
        var back = new DataTableEditor(path);
        foreach (var spec in Specs)
        {
            if (!back.TryGetRow(spec.Id, out var row))
                throw new InvalidOperationException(
                    $"{Path.GetFileName(path)}: passive row '{spec.Id}' missing after injection");
            var buffs = row.Value.FirstOrDefault(f =>
                DataTableEditor.CleanName(f.Name) == "BattleBuffs") as ArrayPropertyData;
            var target = buffs is { Value.Length: > 0 }
                ? buffs.Value[0]?.ToString() ?? ""
                : "";
            var wantSub = "/Created/BP_LU_" + spec.Id;
            if (!target.Contains(wantSub, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"{Path.GetFileName(path)}: '{spec.Id}' BattleBuffs = '{target}', expected *{wantSub}*");
        }
    }

    private static void InjectBalancing(string modDir)
    {
        var path = FindAsset(modDir, "DT_Balancing_Luminas.uasset")
            ?? CopyTableIntoMod("DT_Balancing_Luminas", modDir);
        var ed = new DataTableEditor(path);
        var edits = new JObject();
        foreach (var spec in Specs)
        {
            foreach (var b in spec.Balancing)
            {
                if (string.Equals(b.Key, b.CloneFrom, StringComparison.Ordinal))
                    throw new InvalidDataException(
                        spec.Id + " balancing key '" + b.Key + "' equals cloneFrom — that would overwrite vanilla");
                if (!ed.HasRow(b.Key))
                    ed.CloneRow(b.CloneFrom, b.Key);
                edits[b.Key] = new JObject { ["FloatValue"] = b.Value };
            }
        }
        foreach (var (key, value) in VanillaBalancingOverrides)
        {
            if (!ed.HasRow(key))
            {
                Console.Error.WriteLine("[wfx] vanilla balancing row missing: " + key);
                continue;
            }
            edits[key] = new JObject { ["FloatValue"] = value };
        }
        if (edits.Count > 0)
            ed.Apply(edits.ToString());
        ed.Save(path);
    }

    private static void InjectStrings(string modDir, Dictionary<string, string> entries)
    {
        var path = FindAsset(modDir, "ST_PassiveEffects.uasset");
        if (path is null) return;
        DataTableEditor.AddStringTableEntries(path, entries);
    }

    private static void CloneBlueprint(string srcUasset, string destUasset, string newStem,
        IEnumerable<(string From, string To)> replacements)
    {
        CopyPair(srcUasset, destUasset);
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(destUasset, EngineVersion.VER_UE5_4, map)
            : new UAsset(destUasset, EngineVersion.VER_UE5_4);
        var srcStem = Path.GetFileNameWithoutExtension(srcUasset);
        RetargetPackage(asset, destUasset, srcStem, newStem);
        var ordered = replacements
            .Where(r => !string.IsNullOrEmpty(r.From) && r.From != r.To)
            .OrderByDescending(r => r.From.Length)
            .ToList();
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            var next = s;
            foreach (var (from, to) in ordered)
            {
                if (next.Contains(from, StringComparison.Ordinal))
                    next = next.Replace(from, to, StringComparison.Ordinal);
            }
            if (next != s) asset.SetNameReference(i, FString.FromString(next));
        }
        asset.Write(destUasset);
    }

    private static void RetargetPackage(UAsset asset, string destUasset, string srcStem, string newStem)
    {
        var newPackage = GamePackagePath(destUasset);
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
                next = next.Replace(srcStem, newStem, StringComparison.Ordinal);
            if (next != s) asset.SetNameReference(i, FString.FromString(next));
        }
    }

    private static string GamePackagePath(string destUasset)
    {
        var full = destUasset.Replace('\\', '/');
        const string marker = "/Content/";
        var idx = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            throw new InvalidDataException("cloned asset is not under Content/: " + destUasset);
        var rel = full[(idx + marker.Length)..];
        if (rel.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            rel = rel[..^".uasset".Length];
        return "/Game/" + rel;
    }

    private static void SetLocText(TextPropertyData t, string key, string display)
    {
        display = ModLocalization.Key(key, display);
        t.HistoryType = TextHistoryType.None;
        t.Flags = ETextFlag.CultureInvariant;
        t.Namespace = null;
        t.Value = new FString(display);
        t.CultureInvariantString = new FString(display);
        t.TableId = null;
    }

    private static void Validate(EffectSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.Id) || spec.Id.Contains(' '))
            throw new InvalidDataException("weapon effect id must be a single token: " + spec.Id);
        if (!spec.Id.StartsWith(IdPrefix, StringComparison.Ordinal))
            throw new InvalidDataException("weapon effect id must start with " + IdPrefix + ": " + spec.Id);
        if (spec.Balancing.Count == 0 && spec.Family != FamilyStainEcho
            && spec.PatchFloats is not { Count: > 0 })
            throw new InvalidDataException(spec.Id + " needs at least one balancing key");
        foreach (var b in spec.Balancing)
            if (b.Key == b.CloneFrom)
                throw new InvalidDataException(spec.Id + " must not write the vanilla key " + b.Key);
    }

    private static IReadOnlyList<EffectSpec> LoadSpecs()
    {
        var map = new Dictionary<string, EffectSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in ParseSpecs(ReadEmbedded()))
            map[s.Id] = s;
        if (File.Exists(OverlayPath))
        {
            foreach (var s in ParseSpecs(File.ReadAllText(OverlayPath)))
                map[s.Id] = s;
        }
        foreach (var extra in WeaponEffectsBuiltins.All())
            if (!map.ContainsKey(extra.Id))
                map[extra.Id] = extra;
        return map.Values.ToList();
    }

    public static IReadOnlyList<EffectSpec> ParseSpecs(string json)
    {
        var root = JObject.Parse(json);
        var arr = root["effects"] as JArray ?? new JArray();
        var list = new List<EffectSpec>();
        foreach (var tok in arr.OfType<JObject>())
        {
            var id = tok.Value<string>("id") ?? "";
            var family = tok.Value<string>("family") ?? FamilyDirect;
            var sourceBp = tok.Value<string>("sourceBp") ?? "";
            var sourcePassive = tok.Value<string>("sourcePassive") ?? "";
            var balancing = new List<BalancingKey>();
            if (tok["balancing"] is JArray bals)
            {
                foreach (var b in bals.OfType<JObject>())
                {
                    var key = b.Value<string>("key") ?? "";
                    var cloneFrom = b.Value<string>("cloneFrom") ?? "";
                    var val = b.Value<float?>("value") ?? 0;
                    if (key.Length > 0 && cloneFrom.Length > 0)
                        balancing.Add(new BalancingKey(key, cloneFrom, val));
                }
            }
            if (family == FamilyElementalSkillDmg && balancing.Count == 0 && id.Length > 0)
            {
                var elName = tok.Value<string>("element") ?? "Fire";
                var el = Elemental.FirstOrDefault(e =>
                    e.Element.Equals(elName, StringComparison.OrdinalIgnoreCase));
                if (el.VanillaKey != null)
                    balancing.Add(new BalancingKey(id + "_DamageIncrease", el.VanillaKey,
                        tok.Value<float?>("value") ?? 0.2f));
                if (sourceBp.Length == 0) sourceBp = el.ChildBp;
                if (sourcePassive.Length == 0) sourcePassive = el.ChildBp.Replace("BP_LU_", "");
            }
            var replace = new List<(string, string)>();
            if (tok["replaceNames"] is JObject rn)
                foreach (var p in rn.Properties())
                    if (p.Value.Type == JTokenType.String)
                        replace.Add((p.Name, p.Value.ToString()));
            foreach (var b in balancing)
                if (replace.All(r => r.Item1 != b.CloneFrom))
                    replace.Add((b.CloneFrom, b.Key));

            var patchInts = new List<(int, int)>();
            if (tok["patchInts"] is JArray pi)
            {
                foreach (var p in pi.OfType<JObject>())
                    patchInts.Add((p.Value<int?>("from") ?? 0, p.Value<int?>("to") ?? 0));
            }
            var patchFloats = new List<(double, double)>();
            if (tok["patchFloats"] is JArray pf)
            {
                foreach (var p in pf.OfType<JObject>())
                    patchFloats.Add((p.Value<double?>("from") ?? 0, p.Value<double?>("to") ?? 0));
            }
            list.Add(new EffectSpec(
                id,
                tok.Value<string>("displayName") ?? id,
                tok.Value<string>("description") ?? "",
                family,
                sourceBp,
                sourcePassive,
                tok.Value<string>("sourcePathContains"),
                tok.Value<string>("character"),
                tok.Value<string>("element"),
                replace,
                balancing,
                tok.Value<string>("fromStain"),
                tok.Value<string>("toStain"),
                patchInts,
                patchFloats));
        }
        return list;
    }

    private static string ReadEmbedded()
    {
        var asm = typeof(WeaponEffectsCreateKit).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("weapon_effects.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException("weapon_effects.json");
        using var s = asm.GetManifestResourceStream(name)
                      ?? throw new FileNotFoundException("weapon_effects.json");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    private static string FindSourceBp(EffectSpec spec)
    {
        EnsureExtracted(spec.SourceBp);
        var hits = Directory.Exists(Path.Combine(EditorPaths.RepoRoot, "work", "tables"))
            ? Directory.EnumerateFiles(
                    Path.Combine(EditorPaths.RepoRoot, "work", "tables"),
                    spec.SourceBp + ".uasset", SearchOption.AllDirectories)
                .ToList()
            : new List<string>();
        if (hits.Count == 0)
            throw new FileNotFoundException(spec.SourceBp + ".uasset");
        if (!string.IsNullOrEmpty(spec.SourcePathContains))
        {
            var hinted = hits.FirstOrDefault(h =>
                h.Replace('\\', '/').Contains(spec.SourcePathContains, StringComparison.OrdinalIgnoreCase));
            if (hinted != null) return hinted;
        }
        return hits[0];
    }

    /// <summary>Stages a vanilla effect Blueprint into the mod (same game path,
    /// so it shadows the original) and applies its int-constant patches. Used to
    /// rework a named vanilla weapon passive in place.</summary>
    private static void ApplyVanillaBpPatches(string modDir)
    {
        foreach (var (bp, from, to) in VanillaBpIntPatches)
        {
            try
            {
                var dest = StageVanillaBp(modDir, bp);
                if (dest is not null) PatchIntConst(dest, from, to);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[wfx] vanilla bp int patch failed for {bp}: {ex.Message}");
            }
        }
        foreach (var (bp, from, to) in VanillaBpFloatPatches)
        {
            try
            {
                var dest = StageVanillaBp(modDir, bp);
                if (dest is not null) PatchFloatConst(dest, from, to);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[wfx] vanilla bp float patch failed for {bp}: {ex.Message}");
            }
        }
    }

    /// <summary>Stages and rewrites the vanilla burn passives that apply Burn via
    /// the buff's <c>BurnMultiplier</c> (damage) property so they apply real
    /// stacks instead. Fails loud: a missing pattern means the graph changed and
    /// the tooltip would silently lie.</summary>
    private static void ApplyBurnStackRewrites(string modDir)
    {
        foreach (var (bp, stacks) in VanillaBurnStackRewrites)
        {
            var dest = StageVanillaBp(modDir, bp)
                ?? throw new FileNotFoundException($"[wfx] {bp}.uasset not found for burn-stack rewrite");
            RewriteBurnMultiplierToStacks(dest, bp, stacks);
        }
    }

    /// <summary>Replaces the burn buff's <c>BurnMultiplier</c> assignment with a
    /// <c>ChangeStackCount(N)</c> call, and repoints any sibling
    /// <c>ChangeStackCount</c> that drew its count from an accumulator so the
    /// final call still lands on N (Foretell feeds it an interval counter).</summary>
    private static void RewriteBurnMultiplierToStacks(string dest, string bp, int stacks)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(dest, EngineVersion.VER_UE5_4, map)
            : new UAsset(dest, EngineVersion.VER_UE5_4);

        FunctionExport? fn = null;
        int setterIdx = -1;
        KismetExpression? buffExpr = null;
        foreach (var f in asset.Exports.OfType<FunctionExport>())
        {
            var bc = f.ScriptBytecode;
            if (bc is null) continue;
            for (int i = 0; i < bc.Length; i++)
            {
                if (bc[i] is not EX_CallMath cm || !cm.StackNode.IsImport()) continue;
                if (asset.Imports[Math.Abs(cm.StackNode.Index) - 1].ObjectName.ToString() != "SetDoublePropertyByName") continue;
                var ps = cm.Parameters;
                if (ps.Length >= 3 && ps[1] is EX_NameConst nc && nc.Value.ToString() == "BurnMultiplier")
                {
                    fn = f; setterIdx = i; buffExpr = ps[0]; break;
                }
            }
            if (fn is not null) break;
        }
        if (fn is null || buffExpr is null)
            throw new InvalidDataException($"[wfx] {bp}: BurnMultiplier setter not found");

        // 1) BurnMultiplier set -> ChangeStackCount(stacks).
        var buffTpl = (KismetExpression)MayhemFixKit.CloneKismet(buffExpr);
        MutateStatement(asset, fn, setterIdx, _ => new EX_Context
        {
            Offset = 0,
            PropertyType = 0,
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            ObjectExpression = buffTpl,
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "ChangeStackCount"),
                Parameters = new KismetExpression[] { new EX_IntConst { Value = stacks } },
            },
        });

        // 2) Any other ChangeStackCount in this function must also land on stacks.
        var stmts = fn.ScriptBytecode!;
        for (int i = 0; i < stmts.Length; i++)
        {
            if (i == setterIdx) continue;
            if (stmts[i] is not EX_Context ctx) continue;
            if (ctx.ContextExpression is not EX_LocalVirtualFunction vf) continue;
            if (vf.VirtualFunctionName.ToString() != "ChangeStackCount") continue;
            if (vf.Parameters.Length == 1 && vf.Parameters[0] is EX_IntConst ic && ic.Value == stacks) continue;
            MutateStatement(asset, fn, i, s =>
            {
                var c = (EX_Context)s;
                var v = (EX_LocalVirtualFunction)c.ContextExpression;
                v.Parameters = new KismetExpression[] { new EX_IntConst { Value = stacks } };
                return c;
            });
        }

        MayhemFixKit.RecalcContextSkips(asset, fn);
        asset.Write(dest);
        Console.WriteLine($"[wfx] {bp}: BurnMultiplier -> ChangeStackCount({stacks})");
    }

    /// <summary>Swaps statement <paramref name="idx"/> for the mutated result and
    /// shifts every jump/push target past the statement by the size delta, so the
    /// bytecode stays consistent.</summary>
    private static void MutateStatement(
        UAsset asset, FunctionExport fn, int idx, Func<KismetExpression, KismetExpression> mutate)
    {
        var stmts = fn.ScriptBytecode!;
        uint start = 0;
        for (int i = 0; i < idx; i++) start += (uint)stmts[i].GetSize(asset);
        uint oldSize = (uint)stmts[idx].GetSize(asset);
        stmts[idx] = mutate(stmts[idx]);
        uint newSize = (uint)stmts[idx].GetSize(asset);
        if (newSize > oldSize)
            ShiftOffsetsAtOrAfter(asset, fn, start + oldSize, newSize - oldSize);
        else if (newSize < oldSize)
            LuneSkillsKit.ShiftAbsoluteSigned(asset, fn, start + newSize, oldSize - newSize);
        MayhemFixKit.RecalcContextSkips(asset, fn);
    }

    /// <summary>Adds <paramref name="delta"/> to every jump/push target at or after
    /// <paramref name="point"/> (an insert exactly at a target shifts it too).</summary>
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
                    case EX_SwitchValue sv:
                        if (sv.EndGotoOffset >= point) sv.EndGotoOffset += delta;
                        for (int i = 0; i < sv.Cases.Length; i++)
                            if (sv.Cases[i].NextOffset >= point) sv.Cases[i].NextOffset += delta;
                        break;
                }
            });
    }

    /// <summary>Copies a vanilla BP into the mod at its original game path (so
    /// it shadows) and returns the staged path, or null if it can't be found.</summary>
    private static string? StageVanillaBp(string modDir, string bp)
    {
        var src = FindBp(bp);
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return null;
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    private static string FindBp(string stem)
    {
        EnsureExtracted(stem);
        return FileCopy.FindTablesAsset(stem + ".uasset")
            ?? throw new FileNotFoundException(stem + ".uasset");
    }

    private static string CopyTableIntoMod(string table, string modDir)
    {
        var srcDir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", table);
        if (!Directory.Exists(srcDir))
            new Retoc().ToLegacy(srcDir, table);
        CopyTree(srcDir, modDir);
        return FindAsset(modDir, table + ".uasset")
            ?? throw new FileNotFoundException(table + ".uasset missing after copy");
    }

    private static void EnsureExtracted(string filter)
    {
        if (FileCopy.FindTablesAsset(filter + ".uasset") != null)
            return;
        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", filter);
        Directory.CreateDirectory(dir);
        new Retoc().ToLegacy(dir, filter);
    }

    private static void CopyPair(string srcUasset, string destUasset) =>
        FileCopy.AssetPair(srcUasset, destUasset);

    private static void CopyTree(string src, string dst)
    {
        if (!Directory.Exists(src)) return;
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var idx = file.IndexOf("Sandfall", StringComparison.Ordinal);
            if (idx < 0) continue;
            var dest = Path.Combine(dst, file[idx..]);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            FileCopy.Overwrite(file, dest);
        }
    }

    public static string? FindAsset(string root, string fileName)
    {
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }

    /// <summary>
    /// Deletes cloned BP_LU_WepFx_* pairs whose id is no longer in the spec
    /// list, so renamed or removed effects cannot linger and get packed.
    /// </summary>
    private static void PruneStale(string modDir)
    {
        var dir = Path.Combine(modDir, CreatedFolder);
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.EnumerateFiles(dir, "BP_LU_*.u*", SearchOption.AllDirectories))
        {
            var stem = Path.GetFileNameWithoutExtension(f);
            if (!stem.StartsWith("BP_LU_", StringComparison.Ordinal)) continue;
            if (!TryGet(stem["BP_LU_".Length..], out _))
                File.Delete(f);
        }
    }
}
