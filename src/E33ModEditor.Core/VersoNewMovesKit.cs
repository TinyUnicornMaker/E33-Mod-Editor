using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace E33ModEditor.Core;

/// <summary>
/// Verso second-pass: five brand-new moves cloned from vanilla donors.
/// Same pipeline as Assault Zero (DA+BP clone, own rows, ST, icon, graph
/// node); kismet edits reuse only same-BP-cloned shapes + engine imports.
///
/// Moves:
/// - Zanmoto (24 AP, Steeled donor): 13 hits at 100/13 per hit, Physical.
///   Two-turn wait via the donor's own forced-execution chain (damage branch
///   moved count 1 -&gt; 2, middle turn re-forces + replays charge).
///   Rank S: two extra Light hits per hit (Simoso DoubleHit routine:
///   MakeDamageBuilder + AddStraightDamageFlat(same damage) + DealDamages,
///   plus a deferred-spawned BP_FX_FollowingSwordVerso at the target).
/// - Repainted (17 AP, Overload donor): pure setup. Rank -&gt; S, 12 Berserk
///   (3 turns), Greater Rush/Shell/Powerful (3 turns), Cursed (3-countdown),
///   100 Burn self, HP -&gt; 33 last. No damage, no AP gain.
/// - Red Bushido (18 AP, Berserk Slash donor): 0.75xCurve(18)/3 per hit +
///   missing-HP bonus (FClamp [1,2]) + S mult, mirroring Berserk Slash.
/// - Voided Stars (17 AP, Phantom donor): Void element, Rank S 3x break,
///   zero ApplyBuff. TryPerformBreakStun kept (break mechanic, not status).
/// - Overcharged Blitzer (19 AP, Speed Burst donor): Lightning, Rank S adds
///   floor((selfSpeed-targetSpeed)/500) extra same-damage strikes per hit.
/// </summary>
public static partial class VersoSkillsKit
{
    private sealed record NewMove(
        string Key, string Name, int Ap,
        string DaStem, string BpStem, string NameId,
        string DonorDa, string DonorBp, string Element);

    private static readonly NewMove[] NewMoves =
    {
        new("Verso_Zanmoto", "Zanmoto", 24,
            "DA_Skill_Verso_Zanmoto", "BP_Battle_SkillScript_Verso_Zanmoto", "Zanmoto",
            "DA_Skill_Verso_SteeledStrike", "BP_Battle_SkillScript_Verso_SteeledStrike", "Physical"),
        new("Verso_Repainted", "Repainted", 17,
            "DA_Skill_Verso_Repainted", "BP_Battle_SkillScript_Verso_Repainted", "Repainted",
            "DA_Skill_Verso_NEW_Overcharge", "BP_Battle_SkillScript_Verso_Overcharge", "Physical"),        new("Verso_RedBushido", "Red Bushido", 18,
            "DA_Skill_Verso_RedBushido", "BP_Battle_SkillScript_Verso_RedBushido", "RedBushido",
            "DA_Skill_Verso_BerserkSlash", "BP_Battle_SkillScript_Verso_BerserkSlash", "Physical"),
        new("Verso_VoidedStars", "Voided Stars", 17,
            "DA_Skill_Verso_VoidedStars", "BP_Battle_SkillScript_Verso_VoidedStars", "VoidedStars",
            "DA_Skill_Verso_NEW_PhantomStars", "BP_Battle_SkillScript_Verso_PhantomStars", "Void"),
        new("Verso_Blitzer", "Overcharged Blitzer", 19,
            "DA_Skill_Verso_Blitzer", "BP_Battle_SkillScript_Verso_Blitzer", "Blitzer",
            "DA_Skill_Verso_NEW_Speedburst", "BP_Battle_SkillScript_Verso_SpeedBurst", "Lightning"),
    };

    private static NewMove Move(string key) => NewMoves.First(m => m.Key == key);

    private const string Void = "<keyword id=\"Element_Void\">Void</>";
    private const string BurnOne = "<keyword id=\"StatusEffect_Burn\">Burn</>";

    public static int ApplyNewMoves(string modDir)
    {
        int n = 0;
        n += NewMoveRows(modDir);
        n += NewMoveDataAssets(modDir);
        n += NewMoveBlueprints(modDir);
        n += PatchZanmoto(modDir);
        n += PatchRepainted(modDir);
        n += PatchRedBushido(modDir);
        n += PatchVoidedStars(modDir);
        n += PatchBlitzer(modDir);
        n += NewMoveStrings(modDir);
        n += NewMoveIcons(modDir);
        n += PatchVersoGraphAdds(modDir);
        Console.WriteLine($"[VersoNew] staged {n} asset edit(s)");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────
    // A. Balancing rows (donor values cloned; only new names + retunes)
    // ──────────────────────────────────────────────────────────────────

    private static int NewMoveRows(string modDir)
    {
        var balSrc = FindTables(BalTable);
        Stage(balSrc, modDir, out var balDest);
        var bal = new DataTableEditor(balDest);
        int n = 0;
        void MustExist(string row)
        {
            if (!bal.HasRow(row))
                throw new InvalidDataException($"[VersoNew] missing donor row {row}");
        }

        // Zanmoto: FULL Lune curve at 24 AP (no 0.75x Verso discount),
        // over 13 hits. Break/overcharge mirror Steeled.
        foreach (var r in new[] { "SteeledStrike_BaseDamage", "SteeledStrike_BreakBarMultiplier",
            "SteeledStrike_OverchargeDamageMultiplier" }) MustExist(r);
        bal.CloneRow("SteeledStrike_BaseDamage", "Zanmoto_BaseDamage");
        bal.CloneRow("SteeledStrike_BreakBarMultiplier", "Zanmoto_BreakBarMultiplier");
        bal.CloneRow("SteeledStrike_OverchargeDamageMultiplier", "Zanmoto_OverchargeDamageMultiplier");
        double zanmotoHit = Clamp(24) / 13.0;
        n += bal.Apply("{\"Zanmoto_BaseDamage\":{\"FloatValue\":" + zanmotoHit.ToString("0.0000",
            System.Globalization.CultureInfo.InvariantCulture) + "}}");

        // Repainted: HP set value (Overload donor reads a row for SetHP).
        MustExist("Overcharge_NewHpValue");
        bal.CloneRow("Overcharge_NewHpValue", "Repainted_NewHpValue");
        n += bal.Apply("{\"Repainted_NewHpValue\":{\"FloatValue\":33.0}}");

        // Red Bushido: 0.75xCurve(18) over the donor's 3 instances, plus an
        // owned missing-HP factor (0.02: cap 2x exactly at half HP).
        foreach (var r in new[] { "BerserkSlash_BaseDamage", "BerserkSlash_BreakBarMultiplier",
            "BerserkSlash_OverchargeDamageMultiplier", "BerserkSlash_DamageIncreasePerPercentHP" }) MustExist(r);
        bal.CloneRow("BerserkSlash_BaseDamage", "RedBushido_BaseDamage");
        bal.CloneRow("BerserkSlash_BreakBarMultiplier", "RedBushido_BreakBarMultiplier");
        bal.CloneRow("BerserkSlash_OverchargeDamageMultiplier", "RedBushido_OverchargeDamageMultiplier");
        bal.CloneRow("BerserkSlash_DamageIncreasePerPercentHP", "RedBushido_DamageIncreasePerPercentHP");
        double bushidoTotal = 0.75 * Clamp(18);
        double bushidoHit = bushidoTotal / 3.0;
        n += bal.Apply("{\"RedBushido_BaseDamage\":{\"FloatValue\":" + bushidoHit.ToString("0.0000",
            System.Globalization.CultureInfo.InvariantCulture) + "},"
            + "\"RedBushido_DamageIncreasePerPercentHP\":{\"FloatValue\":0.02}}");

        // Voided Stars: 0.75xCurve(17) over 5 hits; S break x3 is code.
        foreach (var r in new[] { "PhantomStars_BaseDamage", "PhantomStars_BreakBarMultiplier" }) MustExist(r);
        bal.CloneRow("PhantomStars_BaseDamage", "VoidedStars_BaseDamage");
        bal.CloneRow("PhantomStars_BreakBarMultiplier", "VoidedStars_BreakBarMultiplier");
        double voidedHit = 0.75 * Clamp(17) / 5.0;
        n += bal.Apply("{\"VoidedStars_BaseDamage\":{\"FloatValue\":" + voidedHit.ToString("0.0000",
            System.Globalization.CultureInfo.InvariantCulture) + "}}");

        // Blitzer: 0.75xCurve(19) over 5 hits; S strikes are code.
        foreach (var r in new[] { "SpeedBurst_BaseDamage", "SpeedBurst_BreakBarMultiplier",
            "SpeedBurst_OverchargeDamageMultiplier", "SpeedBurst_MaxSpeedDifferentialMultiplier" }) MustExist(r);
        bal.CloneRow("SpeedBurst_BaseDamage", "Blitzer_BaseDamage");
        bal.CloneRow("SpeedBurst_BreakBarMultiplier", "Blitzer_BreakBarMultiplier");
        bal.CloneRow("SpeedBurst_OverchargeDamageMultiplier", "Blitzer_OverchargeDamageMultiplier");
        bal.CloneRow("SpeedBurst_MaxSpeedDifferentialMultiplier", "Blitzer_MaxSpeedDifferentialMultiplier");
        double blitzerHit = 0.75 * Clamp(19) / 5.0;
        n += bal.Apply("{\"Blitzer_BaseDamage\":{\"FloatValue\":" + blitzerHit.ToString("0.0000",
            System.Globalization.CultureInfo.InvariantCulture) + "}}");

        bal.Save(balDest);
        Console.WriteLine($"[VersoNew] rows staged (RedBushido {bushidoHit:0.0000}/hit over 3)");
        return n + 9;
    }

    // ──────────────────────────────────────────────────────────────────
    // B. DA clones (APCost, NameID, text keys, SkillScript, element)
    // ──────────────────────────────────────────────────────────────────

    private static int NewMoveDataAssets(string modDir)
    {
        int n = 0;
        foreach (var m in NewMoves)
        {
            var daSrc = FindDa(m.DonorDa);
            var daDest = StageDest(daSrc, modDir).Replace(m.DonorDa, m.DaStem, StringComparison.Ordinal);
            Directory.CreateDirectory(Path.GetDirectoryName(daDest)!);
            if (!File.Exists(daDest)) FileCopy.AssetPair(daSrc, daDest);
            var da = Open(daDest);
            LuneSkillsKit.RetargetPairs(da, daDest,
                (m.DonorDa, m.DaStem),
                (m.DonorBp, m.BpStem));
            foreach (var ex in da.Exports.OfType<NormalExport>())
                foreach (var p in ex.Data)
                {
                    if (p is NamePropertyData np && np.Name.ToString() == "NameID")
                        np.Value = FName.FromString(da, m.NameId);
                    if (p is TextPropertyData tp && tp.TableId?.Value?.Value is { Length: > 0 })
                    {
                        var pn = p.Name.ToString();
                        var suffix = pn == "Name" ? "Name"
                            : pn == "Description" ? "Description_Long"
                            : pn == "ShortDescription" ? "Description_Short" : null;
                        if (suffix != null)
                            tp.Value = new FString(m.Key + "_" + suffix);
                    }
                }
            var en = FName.FromString(da, "EAttackType::" + StainsMechanicKit.Enumerator(m.Element));
            bool elSet = false;
            foreach (var ex in da.Exports.OfType<NormalExport>())
                foreach (var p in ex.Data)
                {
                    if (p.Name.ToString() == "BaseElementalType" && p is EnumPropertyData bpe)
                    {
                        bpe.Value = en;
                        elSet = true;
                    }
                }
            if (!elSet && m.Element != "Physical")
                throw new InvalidDataException($"[VersoNew] {m.DaStem}: no BaseElementalType to set {m.Element}");
            if (!elSet)
                Console.WriteLine($"[VersoNew] {m.DaStem}: element default (Physical) kept");
            da.FolderName = new FString("/Game/Gameplay/SkillTree/Content/NoahVerso/DA_Skills/" + m.DaStem);
            da.Write(daDest);
            var daEd = DataTableEditor.OpenPropertyAsset(daDest);
            n += daEd.ApplyToProperties("{\"APCost\":" + m.Ap + "}");
            daEd.Save(daDest);
            n += 2;
        }
        Console.WriteLine("[VersoNew] 5 DA clones staged");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────
    // C. BP clones (stem + DA + row retargets; buff/SEQ names untouched —
    //    RetargetPairs already skips BP_Buff_* and SEQ_*).
    // ──────────────────────────────────────────────────────────────────

    private static readonly (string Move, string From, string To)[] BpRowPairs =
    {
        ("Verso_Zanmoto", "SteeledStrike_BaseDamage", "Zanmoto_BaseDamage"),
        ("Verso_Zanmoto", "SteeledStrike_BreakBarMultiplier", "Zanmoto_BreakBarMultiplier"),
        ("Verso_Zanmoto", "SteeledStrike_OverchargeDamageMultiplier", "Zanmoto_OverchargeDamageMultiplier"),
        ("Verso_Repainted", "Overcharge_NewHpValue", "Repainted_NewHpValue"),
        ("Verso_RedBushido", "BerserkSlash_BaseDamage", "RedBushido_BaseDamage"),
        ("Verso_RedBushido", "BerserkSlash_BreakBarMultiplier", "RedBushido_BreakBarMultiplier"),
        ("Verso_RedBushido", "BerserkSlash_OverchargeDamageMultiplier", "RedBushido_OverchargeDamageMultiplier"),
        ("Verso_RedBushido", "BerserkSlash_DamageIncreasePerPercentHP", "RedBushido_DamageIncreasePerPercentHP"),
        ("Verso_VoidedStars", "PhantomStars_BaseDamage", "VoidedStars_BaseDamage"),
        ("Verso_VoidedStars", "PhantomStars_BreakBarMultiplier", "VoidedStars_BreakBarMultiplier"),
        ("Verso_Blitzer", "SpeedBurst_BaseDamage", "Blitzer_BaseDamage"),
        ("Verso_Blitzer", "SpeedBurst_BreakBarMultiplier", "Blitzer_BreakBarMultiplier"),
        ("Verso_Blitzer", "SpeedBurst_OverchargeDamageMultiplier", "Blitzer_OverchargeDamageMultiplier"),
        ("Verso_Blitzer", "SpeedBurst_MaxSpeedDifferentialMultiplier", "Blitzer_MaxSpeedDifferentialMultiplier"),
    };

    private static int NewMoveBlueprints(string modDir)
    {
        int n = 0;
        foreach (var m in NewMoves)
        {
            var bpSrc = FindBp(m.DonorBp);
            var bpDest = StageDest(bpSrc, modDir).Replace(m.DonorBp, m.BpStem, StringComparison.Ordinal);
            Directory.CreateDirectory(Path.GetDirectoryName(bpDest)!);
            if (!File.Exists(bpDest)) FileCopy.AssetPair(bpSrc, bpDest);
            var bp = Open(bpDest);
            var pairs = new List<(string, string)> { (m.DonorBp, m.BpStem), (m.DonorDa, m.DaStem) };
            foreach (var (mv, from, to) in BpRowPairs)
                if (mv == m.Key) pairs.Add((from, to));
            LuneSkillsKit.RetargetPairs(bp, bpDest, pairs.ToArray());
            bp.FolderName = new FString("/Game/Gameplay/Battle/Skills/Content/Verso/" + m.BpStem);
            bp.Write(bpDest);
            n++;
        }
        Console.WriteLine("[VersoNew] 5 BP clones staged");
        return n;
    }

    // ── staged-clone opener (modDir copy must exist) ──
    private static UAsset OpenStagedBp(string modDir, string bpStem, out string dest)
    {
        var src = FileCopy.FindUnder(modDir, bpStem + ".uasset")
            ?? throw new InvalidDataException($"[VersoNew] {bpStem} not staged");
        Stage(src, modDir, out dest);
        return Open(dest);
    }

    // ──────────────────────────────────────────────────────────────────
    // D. String table (vanilla voice, concise full sentences)
    // ──────────────────────────────────────────────────────────────────

    private sealed record NewDesc(string Key, string Name, string Long, string Short);

    private static NewDesc[] NewDescs()
    {
        double zanmotoHit = Clamp(24) / 13.0;
        double bushidoHit = 0.75 * Clamp(18) / 3.0;
        double voidedHit = 0.75 * Clamp(17) / 5.0;
        double blitzerHit = 0.75 * Clamp(19) / 5.0;
        return new[]
        {
            new NewDesc("Verso_Zanmoto", "Zanmoto",
                $"After 2 turns, deals {Gold(zanmotoHit)} single target {Physical} damage. 13 hits.\nInterrupted if any damage taken.\n{Rank('S')}: Ethereal swords strike twice with each hit.",
                $"After 2 turns, deals {Gold(zanmotoHit)} {Physical} damage. 13 hits.\nInterrupted if any damage taken.\n{Rank('S')}: Swords strike twice per hit."),
            new NewDesc("Verso_Repainted", "Repainted",
                $"Sets {Perfection} Rank to {Rank('S')}.\nGains 12 {Berserk}, Greater Rush, Greater Shell and Greater Powerful for 3 turns.\nBecomes Cursed for 3 turns, gains 100 {BurnOne} and Health falls to 33.",
                $"Sets {Perfection} Rank to {Rank('S')}.\nGains 12 {Berserk}, Greater Rush, Greater Shell, Greater Powerful for 3 turns.\nBecomes Cursed for 3 turns, gains 100 {BurnOne}, Health falls to 33."),
            new NewDesc("Verso_RedBushido", "Red Bushido",
                $"Deals {Gold(bushidoHit)} single target {Physical} damage. 3 hits.\nDeal more damage for each missing Health, up to +100%.\n{Rank('S')}: deal 50% more damage.",
                $"Deals {Gold(bushidoHit)} {Physical} damage. 3 hits.\nDeal more damage for each missing Health, up to +100%.\n{Rank('S')}: deal 50% more damage."),
            new NewDesc("Verso_VoidedStars", "Voided Stars",
                $"Deals {Gold(voidedHit)} {Void} damage to all enemies. 5 hits.\nCan {Break}.\n{Rank('S')}: +200% {Break} damage.",
                $"Deals {Gold(voidedHit)} {Void} damage to all enemies. 5 hits.\nCan {Break}.\n{Rank('S')}: +200% {Break} damage."),
            new NewDesc("Verso_Blitzer", "Overcharged Blitzer",
                $"Deals {Gold(blitzerHit)} {Lightning} damage. 5 hits.\nDeals more damage the faster Verso is than the target.\n{Rank('S')}: For every 500 Speed above the target, lightning strikes again with each hit.",
                $"Deals {Gold(blitzerHit)} {Lightning} damage. 5 hits.\nFaster than the target, deals more damage.\n{Rank('S')}: Lightning strikes again per 500 Speed above the target."),
        };
    }

    private static int NewMoveStrings(string modDir)
    {
        var stSrc = FindTables(StringTable);
        Stage(stSrc, modDir, out var stDest);
        int n = 0;
        foreach (var s in NewDescs())
        {
            DataTableEditor.AddStringTableEntries(stDest, new Dictionary<string, string>
            {
                [s.Key + "_Name"] = s.Name,
                [s.Key + "_Description_Long"] = s.Long,
                [s.Key + "_Description_Short"] = s.Short,
            });
            n += 3;
        }
        Console.WriteLine("[VersoNew] 5x3 string entries staged");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────
    // E. Icons (clone donor DT_SkillIcons row under the new NameID)
    // ──────────────────────────────────────────────────────────────────

    private static int NewMoveIcons(string modDir)
    {
        var iconSrc = FileCopy.FindTablesAsset("DT_SkillIcons.uasset")
            ?? throw new FileNotFoundException("DT_SkillIcons not under work/tables");
        var iconDest = StageDest(iconSrc, modDir).Replace("/Resources/Datatables/", "/Resources/DataTables/");
        Directory.CreateDirectory(Path.GetDirectoryName(iconDest)!);
        if (!File.Exists(iconDest)) FileCopy.AssetPair(iconSrc, iconDest);
        var ed = new DataTableEditor(iconDest);
        foreach (var m in NewMoves)
        {
            var donorDaSrc = FindDa(m.DonorDa);
            var donorDa = Open(donorDaSrc);
            string? donorIcon = null;
            foreach (var ex in donorDa.Exports.OfType<NormalExport>())
                foreach (var p in ex.Data)
                    if (p is NamePropertyData np && np.Name.ToString() == "NameID")
                        donorIcon = np.Value.ToString();
            if (donorIcon is null)
                throw new InvalidDataException($"[VersoNew] {m.DonorDa}: no NameID");
            if (!ed.HasRow(donorIcon))
                throw new InvalidDataException($"[VersoNew] icon row '{donorIcon}' missing for {m.Key}");
            if (!ed.HasRow(m.NameId))
                ed.CloneRow(donorIcon, m.NameId);
        }
        ed.Save(iconDest);
        Console.WriteLine("[VersoNew] 5 icon rows staged");
        return 1;
    }

    // ──────────────────────────────────────────────────────────────────
    // F. BP element byte (first MakeDamageBuilder ByteConst; Maelle shape)
    // ──────────────────────────────────────────────────────────────────

    private static int SetBpElement(string modDir, string bpStem, string element, int expectOld)
    {
        var bp = OpenStagedBp(modDir, bpStem, out var dest);
        int ordinal = StainsMechanicKit.Ordinal(element);
        int n = 0;
        foreach (var fn in bp.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            bool inBuilder = false;
            foreach (var s in fn.ScriptBytecode)
            {
                bool done = false;
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (done) return;
                    if (e is EX_LocalVirtualFunction vf
                        && vf.VirtualFunctionName.ToString() == "MakeDamageBuilder")
                    {
                        foreach (var p in vf.Parameters)
                        {
                            if (p is EX_ByteConst bc)
                            {
                                if (bc.Value != expectOld)
                                    throw new InvalidDataException(
                                        $"[VersoNew] {bpStem}: element byte {bc.Value} (expect {expectOld})");
                                bc.Value = (byte)ordinal;
                                n++;
                                done = true;
                                break;
                            }
                        }
                    }
                });
                if (done) break;
            }
        }
        if (n != 1)
            throw new InvalidDataException($"[VersoNew] {bpStem}: element writes x{n} (expect 1)");
        bp.Write(dest);
        Console.WriteLine($"[VersoNew] {bpStem} damage byte -> {element} ({ordinal})");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────
    // G. Graph appends (template = SteeledStrike node; chain east on y=525)
    // ──────────────────────────────────────────────────────────────────

    private static readonly (string DaStem, string Prereq, double X, double Y)[] GraphAdds =
    {
        ("DA_Skill_Verso_Zanmoto", "DA_Skill_Verso_SteeledStrike", 975, 525),
        ("DA_Skill_Verso_Repainted", "DA_Skill_Verso_Zanmoto", 1100, 525),
        ("DA_Skill_Verso_RedBushido", "DA_Skill_Verso_Repainted", 1225, 525),
        ("DA_Skill_Verso_VoidedStars", "DA_Skill_Verso_RedBushido", 1350, 525),
        ("DA_Skill_Verso_Blitzer", "DA_Skill_Verso_VoidedStars", 1475, 525),
    };

    private static int PatchVersoGraphAdds(string modDir)
    {
        var graphSrc = FileCopy.FindTablesAsset(GraphStem + ".uasset")
            ?? throw new FileNotFoundException(GraphStem + " not under work/tables");
        var idx = graphSrc.IndexOf("Sandfall", StringComparison.Ordinal);
        var dest = Path.Combine(modDir, graphSrc[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest)) FileCopy.AssetPair(graphSrc, dest);
        var g = Open(dest);
        var gx = g.Exports.OfType<NormalExport>().FirstOrDefault()
            ?? throw new InvalidDataException("[VersoNew] graph has no export");
        var nodes = gx.Data[0] as ArrayPropertyData
            ?? throw new InvalidDataException("[VersoNew] graph nodes missing");
        var edges = gx.Data[1] as ArrayPropertyData
            ?? throw new InvalidDataException("[VersoNew] graph edges missing");

        string? NodeStem(StructPropertyData nodeStruct)
        {
            if (nodeStruct.Value is not { Count: >= 1 } || nodeStruct.Value[0] is not StructPropertyData su) return null;
            if (su.Value is not { Count: >= 1 } || su.Value[0] is not ObjectPropertyData op) return null;
            var pi = op.Value;
            if (pi is null || !pi.IsImport()) return null;
            return g.Imports[Math.Abs(pi.Index) - 1].ObjectName.ToString();
        }

        StructPropertyData? template = null;
        Import? templateImp = null;
        for (int i = 0; i < nodes.Value.Length; i++)
        {
            if (nodes.Value[i] is StructPropertyData ns && NodeStem(ns) == "DA_Skill_Verso_SteeledStrike")
            {
                template = ns;
                var su = (StructPropertyData)ns.Value[0];
                var ptr = ((ObjectPropertyData)su.Value[0]).Value!;
                templateImp = g.Imports[Math.Abs(ptr.Index) - 1];
                break;
            }
        }
        if (template is null || templateImp is null)
            throw new InvalidDataException("[VersoNew] SteeledStrike template node missing");

        FPackageIndex FindSkillImport(string daStem)
        {
            for (int i = 0; i < nodes.Value.Length; i++)
            {
                if (nodes.Value[i] is StructPropertyData ns && NodeStem(ns) == daStem)
                {
                    var su = (StructPropertyData)ns.Value[0];
                    return ((ObjectPropertyData)su.Value[0]).Value
                        ?? throw new InvalidDataException("[VersoNew] null skill ptr " + daStem);
                }
            }
            throw new InvalidDataException("[VersoNew] prereq node missing: " + daStem);
        }

        foreach (var (daStem, prereq, x, y) in GraphAdds)
        {
            var m = NewMoves.First(mm => "DA_Skill_Verso_" + mm.NameId == daStem
                || ("DA_Skill_" + mm.NameId) == daStem || mm.DaStem == daStem);
            var daPkgPath = "/Game/Gameplay/SkillTree/Content/NoahVerso/DA_Skills/" + daStem;
            var newDaIdx = LuneSkillsKit.EnsureObjectImport(g, daPkgPath, daStem,
                templateImp.ClassName.ToString(), templateImp.ClassPackage.ToString());

            var node = (StructPropertyData)template.Clone();
            node.Value[0] = (StructPropertyData)((StructPropertyData)node.Value[0]).Clone();
            node.Value[1] = (StructPropertyData)((StructPropertyData)node.Value[1]).Clone();
            var suN = (StructPropertyData)node.Value[0];
            ((ObjectPropertyData)suN.Value[0]).Value = newDaIdx;
            ((IntPropertyData)suN.Value[1]).Value = m.Ap;
            ((BoolPropertyData)suN.Value[2]).Value = false;
            var req = (StructPropertyData)suN.Value[3];
            ((ObjectPropertyData)req.Value[0]).Value = null;
            ((NamePropertyData)req.Value[1]).Value = FName.FromString(g, "None");
            ((BoolPropertyData)suN.Value[4]).Value = false;
            var posWrap = (StructPropertyData)node.Value[1];
            ((Vector2DPropertyData)posWrap.Value[0]).Value = new FVector2D(x, y);
            nodes.Value = nodes.Value.Append(node).ToArray();

            var prereqImp = FindSkillImport(prereq);
            var edge = (StructPropertyData)((StructPropertyData)edges.Value[0]).Clone();
            ((ObjectPropertyData)edge.Value[0]).Value = prereqImp;
            ((ObjectPropertyData)edge.Value[1]).Value = newDaIdx;
            edges.Value = edges.Value.Append(edge).ToArray();
        }
        g.Write(dest);
        Console.WriteLine("[VersoNew] appended 5 graph nodes + edges");
        return 1;
    }

    // ──────────────────────────────────────────────────────────────────
    // H. Zanmoto: 2-turn wait + Rank S gate + ethereal swords
    // ──────────────────────────────────────────────────────────────────

    private const string SwordFxPkg = "/Game/FX/FX_Leo/Fx/Enemies/Simon/BP_FX_FollowingSwordVerso";
    private const string SwordFxCls = "BP_FX_FollowingSwordVerso_C";

    // Vanilla Simoso/Cleim shape (BP_LU_DoubleHit.SpawnFollowingSword):
    // MakeTransform -> BeginDeferredActorSpawnFromClass -> SetDoublePropertyByName
    // ("TimeOffset") -> FinishSpawningActor. The transform must live in an
    // FTransform local (96B in this build); the previous Vector-typed 24B local
    // corrupted the VM stack on first write (hard CTD at Rank S).

    private static int PatchZanmoto(string modDir)
    {
        var m = Move("Verso_Zanmoto");
        var bp = OpenStagedBp(modDir, m.BpStem, out var dest);
        var fn = OnAction(bp, m.BpStem);

        // 1. Damage branch count 1 -> 2 (the NotEqual compare holding
        //    IntConst 1; count-0 setup compare holds 0). Size-neutral.
        ReplaceOne(bp, fn,
            e => e is EX_LetBool && HasCallMath(bp, e, "NotEqual_IntInt")
                && HasLocalRead(e, "CallFunc_GetForcedSkillExecutionCount_ReturnValue")
                && HasIntConst(e, 1),
            e =>
            {
                MayhemFixKit.Walk(new[] { e }, x =>
                {
                    if (x is EX_IntConst ic && ic.Value == 1) ic.Value = 2;
                });
                return e;
            }, "Zanmoto count->2");

        // 2. Ubergraph: middle turn re-forces + replays charge.
        //    const 1->2 (count1 skips damage-prep), jump 1211->486 (count1
        //    falls into ForceSkillOnNextTurn + charge). Size-neutral.
        var uber = bp.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "ExecuteUbergraph_" + m.BpStem)
            ?? throw new InvalidDataException("[VersoNew] Zanmoto has no ubergraph");
        var u = uber.ScriptBytecode!.ToList();
        if (u.Count < 18 || u[15] is not EX_Jump uj || uj.CodeOffset != 1211)
            throw new InvalidDataException("[VersoNew] Zanmoto uber layout drifted (jump)");
        if (u[16] is not EX_LocalVirtualFunction lvf || lvf.VirtualFunctionName.ToString() != "ForceSkillOnNextTurn")
            throw new InvalidDataException("[VersoNew] Zanmoto uber layout drifted (force)");
        if (!HasCallMath(bp, u[13], "NotEqual_IntInt") || !HasIntConst(u[13], 1))
            throw new InvalidDataException("[VersoNew] Zanmoto uber const drifted");
        MayhemFixKit.Walk(new[] { u[13] }, x =>
        {
            if (x is EX_IntConst ic && ic.Value == 1) ic.Value = 2;
        });
        uj.CodeOffset = 486;
        uber.ScriptBytecode = u.ToArray();
        MayhemFixKit.RecalcContextSkips(bp, uber);

        // 3. Explicit Rank S damage gate (mirror Steeled).
        bp.Write(dest);
        SwapOneBp(m.BpStem, RankS, modDir);
        bp = Open(dest);
        fn = OnAction(bp, m.BpStem);
        var stmts = fn.ScriptBytecode!.ToList();

        // 4. Swords after each DealDamages: two extra Light builders with
        //    the SAME per-hit damage + a deferred-spawned sword FX actor at
        //    the target. Temp_bool is the S-select input (live) — read-only.
        //    Single scratch (builder local, dead between iterations); the
        //    transform is nested (no struct local needed).
        int dealIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
            if (CallsVirtual(stmts[i], "DealDamages")) { dealIdx = i; break; }
        if (dealIdx < 0) throw new InvalidDataException("[VersoNew] Zanmoto: no DealDamages");
        var targetActor = CloneLocalRead(stmts, "CallFunc_Array_Get_Item", "Zanmoto target");
        var builderLocal = CloneLocalRead(stmts, "CallFunc_MakeDamageBuilder_ReturnValue", "Zanmoto builder");
        var baseBuilder = stmts[dealIdx - 1];
        if (!CallsVirtual(baseBuilder, "MakeDamageBuilder"))
            throw new InvalidDataException("[VersoNew] Zanmoto: builder not before deal");
        var locFn = CloneNamedFinalImportOpt(bp, stmts, "K2_GetActorLocation")
            ?? EnsureEngineFunctionLocal(bp, "Actor", "K2_GetActorLocation");
        var makeTfFn = LuneSkillsKit.EnsureKismetMathFn(bp, "MakeTransform");
        var beginFn = EnsureEngineFunctionLocal(bp, "GameplayStatics", "BeginDeferredActorSpawnFromClass");
        var finishFn = EnsureEngineFunctionLocal(bp, "GameplayStatics", "FinishSpawningActor");
        var setDblFn = EnsureEngineFunctionLocal(bp, "KismetSystemLibrary", "SetDoublePropertyByName");
        var setObjFn = EnsureEngineFunctionLocal(bp, "KismetSystemLibrary", "SetObjectPropertyByName");
        var skelCls = LuneSkillsKit.EnsureObjectImport(bp, "/Script/Engine", "SkeletalMeshComponent",
            "Class", "/Script/CoreUObject");
        var fxIdx = LuneSkillsKit.EnsureObjectImport(bp, SwordFxPkg, SwordFxCls,
            "BlueprintGeneratedClass", "/Script/Engine");

        KismetExpression SwordTransform(EX_LocalVariable loc) => new EX_CallMath
        {
            StackNode = makeTfFn,
            Parameters = new KismetExpression[]
            {
                // Flat read of our struct local (never a nested call).
                (KismetExpression)MayhemFixKit.CloneKismet(loc),
                new EX_RotationConst { Value = new FRotator(0, 0, 0) },
                new EX_VectorConst { Value = new FVector(1, 1, 1) },
            },
        };
        KismetExpression SwordBuilder()
        {
            var clone = (KismetExpression)MayhemFixKit.CloneKismet(baseBuilder);
            bool flipped = false;
            MayhemFixKit.Walk(new[] { clone }, x =>
            {
                if (flipped || x is not EX_LocalVirtualFunction vf
                    || vf.VirtualFunctionName.ToString() != "MakeDamageBuilder") return;
                foreach (var p in vf.Parameters)
                {
                    if (p is EX_ByteConst bc && bc.Value == 1)
                    {
                        bc.Value = 7; // Physical -> Light (Simoso swords)
                        flipped = true;
                        break;
                    }
                }
            });
            if (!flipped)
                throw new InvalidDataException("[VersoNew] Zanmoto: sword element flip failed");
            return clone;
        }
        KismetExpression SwordDeal()
        {
            var clone = (KismetExpression)MayhemFixKit.CloneKismet(stmts[dealIdx]);
            return clone;
        }

        // Dedicated struct scratch (flat evaluation only — CursedEarth
        // shape: K2 result into a vector local, transform into another).
        var locVar = EnsureStructLocal(bp, fn, "ZanSwordLoc", "Zanmoto");
        var tfVar = EnsureStructLocal(bp, fn, "ZanSwordTf", "Zanmoto", "Transform", 96);

        // Follow anchor, tracing the wielder's weapon like the Simoso passive
        // (SourceCharacterStats.GetCharacterBattleBase -> cast -> GetWeaponActor
        // -> WeaponMesh_R_SK). BP_FX_AfterImage snapshots TargetToFollow's world
        // transform each tick and replays it delayed by TimeOffset, so the anchor
        // must be a component whose world transform reads as "blade in hand".
        // The weapon's mesh is reached by name instead of cloning the other BP's
        // WeaponMesh_R_SK InstVar (which cannot be synthesized safely):
        //   CurrentCharacter.GetWeaponActor().GetComponentByClass(SkeletalMeshComponent)
        var curChar = CloneMember(bp, stmts, "CurrentCharacter", "Zanmoto");
        var weaponLocal = EnsureObjectLocal(bp, fn, "ZanSwordWeapon", "Zanmoto");
        var weaponPtr = new KismetPropertyPointer(new FFieldPath(
            new[] { FName.FromString(bp, "ZanSwordWeapon") }, FPackageIndex.FromExport(bp.Exports.IndexOf(fn))));
        var targetComp = EnsureObjectLocal(bp, fn, "ZanSwordTarget", "Zanmoto");
        var targetCompPtr = new KismetPropertyPointer(new FFieldPath(
            new[] { FName.FromString(bp, "ZanSwordTarget") }, FPackageIndex.FromExport(bp.Exports.IndexOf(fn))));

        var body = new List<KismetExpression>();
        foreach (var t in new[] { 0.0, 0.4 })
        {
            body.Add(new EX_Let
            {
                Value = locVar.Variable!,
                Variable = (KismetExpression)MayhemFixKit.CloneKismet(locVar),
                // Member call shape: the object goes in EX_Context, never in
                // the FinalFunction parameter list (every vanilla BP does this;
                // e.g. Monoco's mortar: Context(CurrentCharacter).K2_GetActorLocation).
                Expression = new EX_Context
                {
                    ObjectExpression = (KismetExpression)MayhemFixKit.CloneKismet(curChar),
                    ContextExpression = new EX_FinalFunction
                    {
                        StackNode = locFn,
                        Parameters = Array.Empty<KismetExpression>(),
                    },
                    RValuePointer = locVar.Variable,
                },
            });
            body.Add(new EX_Let
            {
                Value = tfVar.Variable!,
                Variable = (KismetExpression)MayhemFixKit.CloneKismet(tfVar),
                Expression = SwordTransform(locVar),
            });
            body.Add(new EX_LetObj
            {
                VariableExpression = (KismetExpression)MayhemFixKit.CloneKismet(weaponLocal),
                AssignmentExpression = new EX_Context
                {
                    ObjectExpression = (KismetExpression)MayhemFixKit.CloneKismet(curChar),
                    ContextExpression = new EX_LocalVirtualFunction
                    {
                        VirtualFunctionName = FName.FromString(bp, "GetWeaponActor"),
                        Parameters = Array.Empty<KismetExpression>(),
                    },
                    RValuePointer = weaponPtr,
                },
            });
            body.Add(new EX_LetObj
            {
                VariableExpression = (KismetExpression)MayhemFixKit.CloneKismet(targetComp),
                AssignmentExpression = new EX_Context
                {
                    ObjectExpression = (KismetExpression)MayhemFixKit.CloneKismet(weaponLocal),
                    ContextExpression = new EX_LocalVirtualFunction
                    {
                        VirtualFunctionName = FName.FromString(bp, "GetComponentByClass"),
                        Parameters = new KismetExpression[] { new EX_ObjectConst { Value = skelCls } },
                    },
                    RValuePointer = targetCompPtr,
                },
            });
            // World-context = EX_Self. Every vanilla skill-script spawn does
            // this (StandardSkill, CursedEarth, Monoco mortar/thunderstrike,
            // and the Simoso passive) — the skill object resolves a world.
            body.Add(new EX_LetObj
            {
                VariableExpression = (KismetExpression)MayhemFixKit.CloneKismet(builderLocal),
                AssignmentExpression = new EX_CallMath
                {
                    StackNode = beginFn,
                    Parameters = new KismetExpression[]
                    {
                        new EX_Self(),
                        new EX_ObjectConst { Value = fxIdx },
                        (KismetExpression)MayhemFixKit.CloneKismet(tfVar),
                        new EX_ByteConst { Value = 0 },
                        new EX_NoObject(),
                        new EX_ByteConst { Value = 1 },
                    },
                },
            });
            body.Add(new EX_CallMath
            {
                StackNode = setObjFn,
                Parameters = new KismetExpression[]
                {
                    (KismetExpression)MayhemFixKit.CloneKismet(builderLocal),
                    new EX_NameConst { Value = FName.FromString(bp, "TargetToFollow") },
                    (KismetExpression)MayhemFixKit.CloneKismet(targetComp),
                },
            });
            body.Add(new EX_CallMath
            {
                StackNode = setDblFn,
                Parameters = new KismetExpression[]
                {
                    (KismetExpression)MayhemFixKit.CloneKismet(builderLocal),
                    new EX_NameConst { Value = FName.FromString(bp, "TimeOffset") },
                    new EX_DoubleConst { Value = t },
                },
            });
            body.Add(new EX_CallMath
            {
                StackNode = finishFn,
                Parameters = new KismetExpression[]
                {
                    (KismetExpression)MayhemFixKit.CloneKismet(builderLocal),
                    (KismetExpression)MayhemFixKit.CloneKismet(tfVar),
                    new EX_ByteConst { Value = 1 },
                },
            });
            // Lifetime: 1.5s then self-destruct.
            // AActor::SetLifeSpan takes a FLOAT (the game's own call site inserts
            // an explicit double->float cast), so it must be handed EX_FloatConst
            // — the same way the compiler feeds Delay(float). A DoubleConst here
            // reads back as 0 and clears the timer instead of setting it.
            // The sword's own Deactivate() path only gives ~0.2s.
            body.Add(new EX_Context
            {
                ObjectExpression = (KismetExpression)MayhemFixKit.CloneKismet(builderLocal),
                ContextExpression = new EX_LocalVirtualFunction
                {
                    VirtualFunctionName = FName.FromString(bp, "SetLifeSpan"),
                    Parameters = new KismetExpression[] { new EX_FloatConst { Value = 1.5f } },
                },
                RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            });
            body.Add(SwordBuilder());
            body.Add(SwordDeal());
        }
        var gate = BuildRankGate(bp, fn, stmts, RankS, "Zanmoto swords");
        var prelude = new List<KismetExpression> { gate.Prelude };
        SpliceGated(bp, fn, dealIdx, gate.Check(), body, "Zanmoto swords", prelude);

        bp.Write(dest);
        Console.WriteLine("[VersoNew] Zanmoto: 2-turn + S gate + swords staged");
        return 6;
    }

    private static bool HasCallMath(UAsset bp, KismetExpression root, string fnName)
    {
        bool hit = false;
        MayhemFixKit.Walk(new[] { root }, e =>
        {
            if (e is EX_CallMath cm && cm.StackNode.IsImport()
                && cm.StackNode.ToImport(bp).ObjectName.ToString() == fnName) hit = true;
        });
        return hit;
    }

    private static bool HasIntConst(KismetExpression root, int v)
    {
        bool hit = false;
        MayhemFixKit.Walk(new[] { root }, e =>
        {
            if (e is EX_IntConst ic && ic.Value == v) hit = true;
        });
        return hit;
    }

    private static EX_Let CloneLetFor(List<KismetExpression> stmts, string localName, string what)
    {
        foreach (var s in stmts)
        {
            if (s is EX_Let let && HasLocalRead(let.Variable, localName))
                return (EX_Let)MayhemFixKit.CloneKismet(let);
        }
        throw new InvalidDataException($"{what}: no Let targeting '{localName}'");
    }

    private static FPackageIndex? CloneNamedFinalImportOpt(UAsset bp, List<KismetExpression> stmts, string callName)
    {
        FPackageIndex? hit = null;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (hit is not null) return;
                if (e is EX_FinalFunction ff && ff.StackNode.IsImport()
                    && ff.StackNode.ToImport(bp).ObjectName.ToString() == callName) hit = ff.StackNode;
            });
        return hit;
    }

    private static FPackageIndex CloneNamedFinalImport(UAsset bp, List<KismetExpression> stmts, string callName, string what)
        => CloneNamedFinalImportOpt(bp, stmts, callName)
            ?? throw new InvalidDataException($"{what}: no FinalFunction import '{callName}'");

    // ──────────────────────────────────────────────────────────────────
    // I. Repainted: no AP gain, rank S, buff battery, HP -> 33
    // ──────────────────────────────────────────────────────────────────

    private const string BuffGeneric = "/Game/Gameplay/Buffs/GenericBuff/";
    private const string BuffStats = "/Game/Gameplay/Buffs/StatsBuffs/";
    private const string BuffParent = "/Game/Gameplay/Buffs/ParentClasses/";

    private static int PatchRepainted(string modDir)
    {
        var m = Move("Verso_Repainted");
        var bp = OpenStagedBp(modDir, m.BpStem, out var dest);
        var fn = OnAction(bp, m.BpStem);
        var cur = fn.ScriptBytecode!.ToList();

        // 1. Delete the AP refill (single GainAP statement, whole).
        int gainIdx = -1, gainN = 0;
        for (int i = 0; i < cur.Count; i++)
            if (CallsVirtual(cur[i], "GainAP")) { gainIdx = i; gainN++; }
        if (gainN != 1)
            throw new InvalidDataException($"[VersoNew] Repainted: GainAP x{gainN} (expect 1)");
        uint rmOffset = 0;
        for (int i = 0; i < gainIdx; i++) rmOffset += (uint)cur[i].GetSize(bp);
        uint rmSize = (uint)cur[gainIdx].GetSize(bp);
        LuneSkillsKit.ShiftAbsoluteSigned(bp, fn, rmOffset, rmSize);
        cur.RemoveAt(gainIdx);
        fn.ScriptBytecode = cur.ToArray();
        MayhemFixKit.RecalcContextSkips(bp, fn);

        // 2. SetSpecificRank(4,.) -> (5,.).
        ReplaceOne(bp, fn,
            e => e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "SetSpecificRank"
                && vf.Parameters.Length > 0 && vf.Parameters[0] is EX_ByteConst bc && bc.Value == 4,
            e =>
            {
                MayhemFixKit.Walk(new[] { e }, x =>
                {
                    if (x is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "SetSpecificRank"
                        && vf.Parameters.Length > 0 && vf.Parameters[0] is EX_ByteConst bc) bc.Value = 5;
                });
                return e;
            }, "Repainted rank->S");

        var stmts = fn.ScriptBytecode!.ToList();
        int retIdx = -1, retN = 0;
        for (int i = 0; i < stmts.Count; i++)
            if (stmts[i] is EX_Return) { retIdx = i; retN++; }
        if (retIdx < 0 || retN != 1)
            throw new InvalidDataException($"[VersoNew] Repainted: Return x{retN} (expect 1)");

        int LastRead(string name)
        {
            int last = -1;
            for (int i = 0; i < stmts.Count; i++)
                if (HasLocalRead(stmts[i], name)) last = i;
            return last;
        }
        if (LastRead("CallFunc_GetComponentByClass_ReturnValue") >= retIdx
            || LastRead("CallFunc_IsLastActionEffectOfSkill_ReturnValue") >= retIdx)
            throw new InvalidDataException("[VersoNew] Repainted: scratch locals still live");
        var spawnLocal = CloneLocalRead(stmts, "CallFunc_GetComponentByClass_ReturnValue", "Repainted");
        var okLocal = CloneLocalRead(stmts, "CallFunc_IsLastActionEffectOfSkill_ReturnValue", "Repainted");
        EX_Context? selfStatsTpl = null;
        foreach (var s in stmts)
        {
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (selfStatsTpl is not null) return;
                if (e is EX_Context cx && cx.ObjectExpression is EX_InstanceVariable
                    && cx.ContextExpression is EX_InstanceVariable civ && PtrName(civ.Variable) == "AC_jRPG_CharacterStats")
                {
                    bool call = false;
                    MayhemFixKit.Walk(new[] { cx }, x => { if (x is EX_LocalVirtualFunction || x is EX_FinalFunction) call = true; });
                    if (!call) selfStatsTpl = cx;
                }
            });
            if (selfStatsTpl is not null) break;
        }
        if (selfStatsTpl is null)
            throw new InvalidDataException("[VersoNew] Repainted: no self stats read");

        var spawnFn = EnsureEngineFunctionLocal(bp, "GameplayStatics", "SpawnObject");
        var setIntFn = EnsureEngineFunctionLocal(bp, "KismetSystemLibrary", "SetIntPropertyByName");
        var selfStats = (KismetExpression)MayhemFixKit.CloneKismet(selfStatsTpl);

        List<KismetExpression> BuffBlock(string pkg, string cls, int stacks, int duration,
            bool curse = false)
        {
            var block = new List<KismetExpression>();
            var clsIdx = LuneSkillsKit.EnsureObjectImport(bp, pkg, cls, cls, "/Script/Engine");
            block.Add(new EX_LetObj
            {
                VariableExpression = (KismetExpression)MayhemFixKit.CloneKismet(spawnLocal),
                AssignmentExpression = new EX_FinalFunction
                {
                    StackNode = spawnFn,
                    Parameters = new KismetExpression[]
                    {
                        new EX_ObjectConst { Value = clsIdx },
                        (KismetExpression)MayhemFixKit.CloneKismet(selfStats),
                    },
                },
            });
            if (stacks > 1)
            {
                block.Add(new EX_Context
                {
                    ObjectExpression = (KismetExpression)MayhemFixKit.CloneKismet(spawnLocal),
                    ContextExpression = new EX_LocalVirtualFunction
                    {
                        VirtualFunctionName = FName.FromString(bp, "ChangeStackCount"),
                        Parameters = new KismetExpression[]
                        {
                            new EX_IntConst { Value = stacks },
                        },
                    },
                    RValuePointer = new KismetPropertyPointer(new FFieldPath()),
                });
            }
            if (curse)
            {
                block.Add(new EX_FinalFunction
                {
                    StackNode = setIntFn,
                    Parameters = new KismetExpression[]
                    {
                        (KismetExpression)MayhemFixKit.CloneKismet(spawnLocal),
                        new EX_NameConst { Value = FName.FromString(bp, "TurnDuration") },
                        new EX_IntConst { Value = duration },
                    },
                });
                block.Add(new EX_Context
                {
                    ObjectExpression = (KismetExpression)MayhemFixKit.CloneKismet(spawnLocal),
                    ContextExpression = new EX_LocalVirtualFunction
                    {
                        VirtualFunctionName = FName.FromString(bp, "SetCurseDuration"),
                        Parameters = new KismetExpression[]
                        {
                            new EX_IntConst { Value = duration },
                        },
                    },
                    RValuePointer = new KismetPropertyPointer(new FFieldPath()),
                });
            }
            else
            {
                block.Add(new EX_FinalFunction
                {
                    StackNode = setIntFn,
                    Parameters = new KismetExpression[]
                    {
                        (KismetExpression)MayhemFixKit.CloneKismet(spawnLocal),
                        new EX_NameConst { Value = FName.FromString(bp, "TurnDuration") },
                        new EX_IntConst { Value = duration },
                    },
                });
            }
            block.Add(new EX_Context
            {
                ObjectExpression = (KismetExpression)MayhemFixKit.CloneKismet(selfStats),
                ContextExpression = new EX_LocalVirtualFunction
                {
                    VirtualFunctionName = FName.FromString(bp, "ActivateBuff"),
                    Parameters = new KismetExpression[]
                    {
                        (KismetExpression)MayhemFixKit.CloneKismet(spawnLocal),
                        new EX_ByteConst { Value = 4 },
                        (KismetExpression)MayhemFixKit.CloneKismet(selfStats),
                        (KismetExpression)MayhemFixKit.CloneKismet(okLocal),
                    },
                },
                RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            });
            return block;
        }

        var block = new List<KismetExpression>();
        block.AddRange(BuffBlock(BuffGeneric + "BP_BattleBuff_Berserk", "BP_BattleBuff_Berserk_C", 12, 3));
        block.AddRange(BuffBlock(BuffParent + "BP_BattleBuff_GreaterSpeed", "BP_BattleBuff_GreaterSpeed_C", 1, 3));
        block.AddRange(BuffBlock(BuffStats + "BP_BattleBuff_GreaterShell", "BP_BattleBuff_GreaterShell_C", 1, 3));
        block.AddRange(BuffBlock(BuffStats + "BP_BattleBuff_GreaterPowerful", "BP_BattleBuff_GreaterPowerful_C", 1, 3));
        block.AddRange(BuffBlock(BuffGeneric + "BP_BattleBuff_Cursed", "BP_BattleBuff_Cursed_C", 1, 3, curse: true));
        block.AddRange(BuffBlock(BuffGeneric + "BP_BattleBuff_Burn", "BP_BattleBuff_Burn_C", 100, 3));

        uint insOff = 0;
        for (int i = 0; i < retIdx; i++) insOff += stmts[i].GetSize(bp);
        // Run the battery once: skip it unless this is the last action
        // effect (multi-effect casts would otherwise stack it N times).
        var onceJump = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = (KismetExpression)MayhemFixKit.CloneKismet(okLocal),
        };
        uint jumpSize = (uint)onceJump.GetSize(bp);
        uint ssize = 0;
        foreach (var s in block) ssize += s.GetSize(bp);
        uint total = jumpSize + ssize;
        stmts.Insert(retIdx, onceJump);
        stmts.InsertRange(retIdx + 1, block);
        fn.ScriptBytecode = stmts.ToArray();
        ShiftSignedLocal(bp, fn, insOff, (int)total);
        onceJump.CodeOffset = insOff + total;
        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(dest);
        Console.WriteLine("[VersoNew] Repainted: rank S, buff battery (once), HP row 33");
        return 9;
    }

    // ──────────────────────────────────────────────────────────────────
    // J. Red Bushido: missing-HP cap + Rank S gate (mirror Berserk Slash)
    // ──────────────────────────────────────────────────────────────────

    private static int PatchRedBushido(string modDir)
    {
        var m = Move("Verso_RedBushido");
        var bp = OpenStagedBp(modDir, m.BpStem, out var dest);
        var fn = OnAction(bp, m.BpStem);
        var clampFn = LuneSkillsKit.EnsureKismetMathFn(bp, "FClamp");
        bool done = false;
        ReplaceOne(bp, fn,
            e => e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "AddFinalDamageMultiplier",
            e =>
            {
                var vf = (EX_LocalVirtualFunction)e;
                if (vf.Parameters.Length < 1)
                    throw new InvalidDataException("[VersoNew] RedBushido: AddFinalDamageMultiplier arity");
                var amount = vf.Parameters[0];
                vf.Parameters[0] = new EX_CallMath
                {
                    StackNode = clampFn,
                    Parameters = new KismetExpression[]
                    {
                        amount,
                        new EX_DoubleConst { Value = 1.0 },
                        new EX_DoubleConst { Value = 2.0 },
                    },
                };
                done = true;
                return vf;
            }, "RedBushido cap");
        if (!done) throw new InvalidDataException("[VersoNew] RedBushido: no AddFinalDamageMultiplier");
        bp.Write(dest);
        SwapOneBp(m.BpStem, RankS, modDir);
        Console.WriteLine("[VersoNew] RedBushido: HP cap + S gate staged");
        return 2;
    }

    // ──────────────────────────────────────────────────────────────────
    // K. Voided Stars: Void element + Rank S triple break, no statuses
    // ──────────────────────────────────────────────────────────────────

    private static int PatchVoidedStars(string modDir)
    {
        var m = Move("Verso_VoidedStars");
        SetBpElement(modDir, m.BpStem, "Void", 7);
        NeutralizeOneCostOverride(modDir, m.BpStem);

        // Purple recolour: only BP-level FX swaps are allowed, and the
        // Phantom script spawns no FX systems of its own (sequence-baked).
        // Element byte carries the Void identity; no visual swap possible.
        var bp = OpenStagedBp(modDir, m.BpStem, out var dest);
        var fn = OnAction(bp, m.BpStem);
        var stmts = fn.ScriptBytecode!.ToList();

        // Vanilla Phantom has no IsOvercharged read (damage = base x QTE);
        // assert that, and assert zero ApplyBuff (no statuses, ever).
        // (SkillState.SkillDefinition reads are fine — only the exact
        // IsOvercharged member read counts.)
        foreach (var s in stmts)
        {
            bool over = false, buff = false;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (IsOverchargedRead(e)) over = true;
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "ApplyBuff") buff = true;
            });
            if (over) throw new InvalidDataException("[VersoNew] VoidedStars: unexpected rank read");
            if (buff) throw new InvalidDataException("[VersoNew] VoidedStars: unexpected ApplyBuff");
        }

        // Anchor: MakeDamageBuilder + its DealDamages.
        int builderIdx = -1, dealIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            if (CallsVirtual(stmts[i], "MakeDamageBuilder")) builderIdx = i;
            if (CallsVirtual(stmts[i], "DealDamages") && dealIdx < 0) dealIdx = i;
        }
        if (builderIdx < 0 || dealIdx < 0 || dealIdx != builderIdx + 1)
            throw new InvalidDataException("[VersoNew] VoidedStars: builder/deal layout drifted");

        // S-path: clone the builder with break x3 inline, then jump over
        // the base builder into the shared DealDamages (same locals).
        var sBuilder = (KismetExpression)MayhemFixKit.CloneKismet(stmts[builderIdx]);
        var multImport = CloneNamedCallImport(bp, stmts, "Multiply_DoubleDouble", "VoidedStars");
        bool tripled = false;
        MayhemFixKit.Walk(new[] { sBuilder }, e =>
        {
            if (tripled || e is not EX_LocalVirtualFunction vf
                || vf.VirtualFunctionName.ToString() != "MakeDamageBuilder") return;
            for (int i = 0; i < vf.Parameters.Length; i++)
            {
                if (vf.Parameters[i] is EX_StructMemberContext)
                {
                    vf.Parameters[i] = new EX_CallMath
                    {
                        StackNode = multImport,
                        Parameters = new KismetExpression[]
                        {
                            vf.Parameters[i],
                            new EX_DoubleConst { Value = 3.0 },
                        },
                    };
                    tripled = true;
                    break;
                }
            }
        });
        if (!tripled)
            throw new InvalidDataException("[VersoNew] VoidedStars: break operand not found");

        var gate = BuildRankGate(bp, fn, stmts, RankS, "VoidedStars break");
        // Insert BEFORE the base builder: falling into the block from the
        // preceding statement must hit the prelude first. (Inserting after
        // the builder loops builder->prelude->builder forever below S.)
        uint insOff = 0;
        for (int i = 0; i < builderIdx; i++) insOff += stmts[i].GetSize(bp);
        uint baseBuilderSize = (uint)stmts[builderIdx].GetSize(bp);
        var jumpS = new EX_JumpIfNot { CodeOffset = 0, BooleanExpression = gate.Check() };
        var jumpOver = new EX_Jump { CodeOffset = 0 };
        uint preSize = (uint)gate.Prelude.GetSize(bp);
        uint jsSize = (uint)jumpS.GetSize(bp);
        uint sbSize = (uint)sBuilder.GetSize(bp);
        uint joSize = (uint)jumpOver.GetSize(bp);
        uint total = preSize + jsSize + sbSize + joSize;
        var block = new List<KismetExpression> { gate.Prelude, jumpS, sBuilder, jumpOver };
        stmts.InsertRange(builderIdx, block);
        fn.ScriptBytecode = stmts.ToArray();
        MayhemFixKit.ShiftAbsolute(bp, fn, insOff, total, null);
        // Post-insert layout: [prelude][jumpS][sBuilder][jumpOver]
        // [orig builder][orig deal]. jumpS (non-S) -> orig builder (right
        // after the block); jumpOver -> orig deal.
        jumpS.CodeOffset = insOff + total;
        jumpOver.CodeOffset = insOff + total + baseBuilderSize;
        MayhemFixKit.RecalcContextSkips(bp, fn);

        bp.Write(dest);
        Console.WriteLine("[VersoNew] VoidedStars: Void + S triple-break staged");
        return 4;
    }

    // ──────────────────────────────────────────────────────────────────
    // L. Blitzer: Lightning + Rank B gate + Rank S speed-strike loop
    // ──────────────────────────────────────────────────────────────────

    private static int PatchBlitzer(string modDir)
    {
        var m = Move("Verso_Blitzer");
        SetBpElement(modDir, m.BpStem, "Lightning", 7);
        var bp = OpenStagedBp(modDir, m.BpStem, out _);
        SwapOneBp(m.BpStem, RankB, modDir);
        bp = OpenStagedBp(modDir, m.BpStem, out var dest);
        var fn = OnAction(bp, m.BpStem);
        var stmts = fn.ScriptBytecode!.ToList();

        int dealIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
            if (CallsVirtual(stmts[i], "DealDamages")) { dealIdx = i; break; }
        if (dealIdx < 0) throw new InvalidDataException("[VersoNew] Blitzer: no DealDamages");
        int builderIdx = -1, multIdx = -1;
        for (int i = 0; i < dealIdx; i++)
        {
            if (CallsVirtual(stmts[i], "MakeDamageBuilder")) builderIdx = i;
            if (CallsVirtual(stmts[i], "AddFinalDamageMultiplier")) multIdx = i;
        }
        if (builderIdx < 0 || multIdx < 0)
            throw new InvalidDataException("[VersoNew] Blitzer: builder/mult layout drifted");

        // Scratch liveness: everything reused must be dead past the deal.
        string[] scratch = { "CallFunc_GetCurrentSpeed_ReturnValue", "CallFunc_Map_Find_Value",
            "CallFunc_Array_Length_ReturnValue", "CallFunc_Add_IntInt_ReturnValue_1",
            "CallFunc_FClamp_ReturnValue" };
        foreach (var name in scratch)
            for (int i = dealIdx + 1; i < stmts.Count; i++)
                if (HasLocalRead(stmts[i], name))
                    throw new InvalidDataException($"[VersoNew] Blitzer: scratch '{name}' live past deal");

        var selfSpeed = CloneLocalRead(stmts, "CallFunc_GetCurrentSpeed_ReturnValue", "Blitzer");
        var foeSpeed = CloneLocalRead(stmts, "CallFunc_Map_Find_Value", "Blitzer");
        var countLocal = CloneLocalRead(stmts, "CallFunc_Array_Length_ReturnValue", "Blitzer");
        var counterLocal = CloneLocalRead(stmts, "CallFunc_Add_IntInt_ReturnValue_1", "Blitzer");
        var subFn = LuneSkillsKit.EnsureKismetMathFn(bp, "Subtract_DoubleDouble");
        var safeDivFn = CloneNamedCallImport(bp, stmts, "SafeDivide", "Blitzer");
        var truncFn = LuneSkillsKit.EnsureKismetMathFn(bp, "FTrunc");
        var greaterFn = LuneSkillsKit.EnsureKismetMathFn(bp, "Greater_IntInt");
        var lessFn = CloneNamedCallImport(bp, stmts, "Less_IntInt", "Blitzer");
        var addFn = CloneNamedCallImport(bp, stmts, "Add_IntInt", "Blitzer");

        var gate = BuildRankGate(bp, fn, stmts, RankS, "Blitzer strikes");
        var rankJump = new EX_JumpIfNot { CodeOffset = 0, BooleanExpression = gate.Check() };

        // count = FTrunc(SafeDivide(selfSpeed - foeSpeed, 500)).
        var countLet = CloneLetFor(stmts, "CallFunc_Array_Length_ReturnValue", "Blitzer");
        countLet.Expression = new EX_CallMath
        {
            StackNode = truncFn,
            Parameters = new KismetExpression[]
            {
                new EX_CallMath
                {
                    StackNode = safeDivFn,
                    Parameters = new KismetExpression[]
                    {
                        new EX_CallMath
                        {
                            StackNode = subFn,
                            Parameters = new KismetExpression[]
                            {
                                (KismetExpression)MayhemFixKit.CloneKismet(selfSpeed),
                                (KismetExpression)MayhemFixKit.CloneKismet(foeSpeed),
                            },
                        },
                        new EX_DoubleConst { Value = 500.0 },
                    },
                },
            },
        };
        var skipAll = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = new EX_CallMath
            {
                StackNode = greaterFn,
                Parameters = new KismetExpression[]
                {
                    (KismetExpression)MayhemFixKit.CloneKismet(countLocal),
                    new EX_IntConst { Value = 0 },
                },
            },
        };
        var initCounter = CloneLetFor(stmts, "CallFunc_Add_IntInt_ReturnValue_1", "Blitzer");
        initCounter.Expression = new EX_IntConst { Value = 0 };
        var condJump = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = new EX_CallMath
            {
                StackNode = lessFn,
                Parameters = new KismetExpression[]
                {
                    (KismetExpression)MayhemFixKit.CloneKismet(counterLocal),
                    (KismetExpression)MayhemFixKit.CloneKismet(countLocal),
                },
            },
        };
        var strikeBuilder = (KismetExpression)MayhemFixKit.CloneKismet(stmts[builderIdx]);
        var strikeMult = (KismetExpression)MayhemFixKit.CloneKismet(stmts[multIdx]);
        var strikeDeal = (KismetExpression)MayhemFixKit.CloneKismet(stmts[dealIdx]);
        var latch = CloneLetFor(stmts, "CallFunc_Add_IntInt_ReturnValue_1", "Blitzer");
        latch.Expression = new EX_CallMath
        {
            StackNode = addFn,
            Parameters = new KismetExpression[]
            {
                (KismetExpression)MayhemFixKit.CloneKismet(counterLocal),
                new EX_IntConst { Value = 1 },
            },
        };
        var backJump = new EX_Jump { CodeOffset = 0 };

        var block = new List<KismetExpression>
        {
            gate.Prelude, rankJump, countLet, skipAll, initCounter, condJump,
            strikeBuilder, strikeMult, strikeDeal, latch, backJump,
        };
        uint insOff = 0;
        for (int i = 0; i <= dealIdx; i++) insOff += stmts[i].GetSize(bp);
        uint total = 0;
        foreach (var s in block) total += s.GetSize(bp);
        stmts.InsertRange(dealIdx + 1, block);
        fn.ScriptBytecode = stmts.ToArray();
        MayhemFixKit.ShiftAbsolute(bp, fn, insOff, total, null);
        // Layout: [prelude][rankJump][count][skipAll][init][condJump][body][latch][back].
        uint preSize = (uint)block[0].GetSize(bp);
        uint rankSize = (uint)block[1].GetSize(bp);
        uint countSize = (uint)block[2].GetSize(bp);
        uint skipSize = (uint)block[3].GetSize(bp);
        uint initSize = (uint)block[4].GetSize(bp);
        uint loopHead = insOff + preSize + rankSize + countSize + skipSize + initSize;
        rankJump.CodeOffset = insOff + total;
        skipAll.CodeOffset = insOff + total;
        condJump.CodeOffset = insOff + total;
        backJump.CodeOffset = loopHead;
        MayhemFixKit.RecalcContextSkips(bp, fn);

        bp.Write(dest);
        Console.WriteLine("[VersoNew] Blitzer: Lightning + B gate + S strike loop staged");
        return 5;
    }

    // ──────────────────────────────────────────────────────────────────
    // K2. Cost override neutralize (single staged BP; mirrors
    //     NeutralizeCostOverrides — clones inherit the donor's S discount).
    // ──────────────────────────────────────────────────────────────────

    private static int NeutralizeOneCostOverride(string modDir, string bpStem)
    {
        var src = FileCopy.FindUnder(modDir, bpStem + ".uasset") ?? FindBp(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "GetSkillCostOverride")
            ?? throw new InvalidDataException($"[VersoNew] {bpStem} has no GetSkillCostOverride");
        var stmts = fn.ScriptBytecode!.ToList();
        int flips = 0;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (hit) return;
                if (e is not EX_LetBool lb) return;
                string target = lb.VariableExpression is EX_LocalVariable lv
                    ? PtrName(lv.Variable)
                    : lb.VariableExpression is EX_LocalOutVariable lov
                        ? PtrName(lov.Variable) : "";
                if (target.Contains("Override", StringComparison.OrdinalIgnoreCase)) hit = true;
            });
            if (!hit) continue;
            uint off = 0;
            for (int j = 0; j < i; j++) off += stmts[j].GetSize(bp);
            uint oldSize = stmts[i].GetSize(bp);
            stmts[i] = MapExpr(stmts[i], e =>
            {
                if (e is not EX_LetBool lb) return null;
                string target = lb.VariableExpression is EX_LocalVariable lv
                    ? PtrName(lv.Variable)
                    : lb.VariableExpression is EX_LocalOutVariable lov
                        ? PtrName(lov.Variable) : "";
                if (!target.Contains("Override", StringComparison.OrdinalIgnoreCase)) return null;
                lb.AssignmentExpression = new EX_False();
                flips++;
                return lb;
            });
            uint newSize = stmts[i].GetSize(bp);
            fn.ScriptBytecode = stmts.ToArray();
            if (newSize > oldSize)
                MayhemFixKit.ShiftAbsolute(bp, fn, off, newSize - oldSize, null);
            else if (newSize < oldSize)
                ShiftShrink(bp, fn, off, oldSize - newSize);
            MayhemFixKit.RecalcContextSkips(bp, fn);
        }
        if (flips != 1)
            throw new InvalidDataException($"[VersoNew] {bpStem}: Override writer x{flips} (expect 1)");
        bp.Write(dest);
        Console.WriteLine($"[VersoNew] {bpStem}: cost override neutralized");
        return 1;
    }

    // ──────────────────────────────────────────────────────────────────
    // Object scratch local. Same mechanism as the rank gate's EnsureGateLocal:
    // clone a same-function FObjectProperty template, rename it, and re-owner
    // it with a real function-export pointer (never a null/synthetic path).
    // ──────────────────────────────────────────────────────────────────

    private static EX_LocalVariable EnsureObjectLocal(UAsset bp, FunctionExport fn, string name, string what)
    {
        var destOwner = FPackageIndex.FromExport(bp.Exports.IndexOf(fn));
        var props = (fn.LoadedProperties ?? Array.Empty<UAssetAPI.FieldTypes.FProperty>()).ToList();
        if (!props.Any(p => p.Name.ToString() == name))
        {
            var tpl = props.FirstOrDefault(p => p.GetType().Name == "FObjectProperty")
                ?? bp.Exports.OfType<FunctionExport>()
                    .SelectMany(f => f.LoadedProperties ?? Array.Empty<UAssetAPI.FieldTypes.FProperty>())
                    .FirstOrDefault(p => p.GetType().Name == "FObjectProperty")
                ?? throw new InvalidDataException($"{what}: no object local template");
            var prop = (UAssetAPI.FieldTypes.FProperty)tpl.GetType().GetMethod("MemberwiseClone",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(tpl, null)!;
            prop.Name = FName.FromString(bp, name);
            props.Add(prop);
            fn.LoadedProperties = props.ToArray();
        }
        return new EX_LocalVariable
        {
            Variable = new KismetPropertyPointer(new FFieldPath(
                new[] { FName.FromString(bp, name) }, destOwner)),
        };
    }

    // ──────────────────────────────────────────────────────────────────
    // Struct scratch locals. Same mechanism as EnsureGateLocal (proven for
    // objects): clone a same-function FStructProperty template, point it at
    // the requested script struct, append to function locals. References use
    // owner-real pointers (never null-package).
    // ElementSize MUST match the real struct (FVector 24, FTransform 96 —
    // 16B-aligned in this build). A wrong size corrupts the stack on first
    // write (hard CTD); measure with `fn-locals <bp> <fn>` before adding one.
    // ──────────────────────────────────────────────────────────────────

    private static EX_LocalVariable EnsureStructLocal(UAsset bp, FunctionExport fn, string name, string what,
        string structObject = "Vector", int elementSize = 24)
    {
        var destOwner = FPackageIndex.FromExport(bp.Exports.IndexOf(fn));
        var props = (fn.LoadedProperties ?? Array.Empty<UAssetAPI.FieldTypes.FProperty>()).ToList();
        if (!props.Any(p => p.Name.ToString() == name))
        {
            var tpl = props.OfType<FStructProperty>().FirstOrDefault()
                ?? bp.Exports.OfType<FunctionExport>()
                    .SelectMany(f => f.LoadedProperties ?? Array.Empty<UAssetAPI.FieldTypes.FProperty>())
                    .OfType<FStructProperty>().FirstOrDefault()
                ?? throw new InvalidDataException($"{what}: no struct local template");
            var prop = (FStructProperty)tpl.GetType().GetMethod("MemberwiseClone",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(tpl, null)!;
            prop.Name = FName.FromString(bp, name);
            prop.Struct = LuneSkillsKit.EnsureObjectImport(bp, "/Script/CoreUObject", structObject,
                "Object", "/Script/CoreUObject");
            // MemberwiseClone copies the template's ElementSize (e.g. 8 for a
            // DataTable-row struct); override it with the real struct size.
            prop.ElementSize = elementSize;
            props.Add(prop);
            fn.LoadedProperties = props.ToArray();
        }
        return new EX_LocalVariable
        {
            Variable = new KismetPropertyPointer(new FFieldPath(
                new[] { FName.FromString(bp, name) }, destOwner)),
        };
    }

    // __NEWMOVES_APPLY__
}





