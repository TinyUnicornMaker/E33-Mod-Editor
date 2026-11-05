using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace E33ModEditor.Core;

/// <summary>
/// Creates four new Lune skills that build on the LuneSkillsKit clone family:
/// AP-gain kismet for the two utilities, and Lune-native single-hit nukes for the
/// two Ultima moves (IceGust donor — no cross-character assets).
///
///  • Dark Power    (1 AP)  — consume all Dark stains, gain 5 AP per Dark on Lune.
///  • Angel's Gift  (1 AP)  — consume all Light stains, gain 3 AP per Light to
///                            Lune + every party member.
///  • Ultima        (21 AP) — requires 4 Light stains; IceGust (Ice Lance) anim
///                            with Light FX. Base row 53.44 = clamp(21).
///  • Ultima Noir   (21 AP) — requires 4 Dark stains; IceGust anim with Dark FX,
///                            64.13 flat (×1.2 over clamp(21)), 10 Blight on Lune.
///
/// Each skill is built by cloning a donor DA + skill-script BP, retargeting every
/// name reference, patching kismet, appending a tree node, and adding the icon +
/// string-table rows. Wired into rebuild-mod so the skills survive a full rebuild.
/// </summary>
public static class LuneApocalypseKit
{
    public sealed record NewMove(
        string DaStem,          // e.g. "DA_Skill_Lune_DarkPower"
        string BpStem,          // e.g. "BP_Battle_SkillScript_DarkPower"
        string NameId,          // e.g. "DarkPower"
        string DonorDa,         // e.g. "DA_Skill_Lune_ElementalTrick"
        string DonorBp,         // e.g. "BP_Battle_SkillScript_ElementalTrick"
        string DonorStem,       // retarget stem inside the BP, e.g. "ElementalTrick"
        int ApCost,
        string DisplayName,
        string Description,
        string ShortDescription,
        (string DonorRow, string NewRow, double? Value)[] BalancingRows,
        string PrereqNameId,    // graph prerequisite node NameID, e.g. "LumiereZenithale"
        FVector2D Position,
        string? ConsumeElement = null,  // "Dark"/"Light": consume ALL of this stain type
        int ApPerStain = 0,             // AP granted per stain consumed (Dark Power / Angel's Gift)
        bool PartyAp = false,           // Angel's Gift: also grant to every targeted ally
        bool UltimaDamage = false,      // IceGust → Light single-hit nuke (hard 4 Light)
        bool NoirDamage = false,        // IceGust → Dark single-hit nuke + 10 Blight on self
        string? DaNameId = null);       // DA donor's NameID (defaults to DonorStem; IceGust for Ultima/Noir)

    public static readonly NewMove[] Moves =
    {
        // Dark Power: HealingLight variant — 1 AP, consume all Dark, 5 AP × stains on Lune.
        new(
            "DA_Skill_Lune_DarkPower",
            "BP_Battle_SkillScript_DarkPower",
            "DarkPower",
            "DA_Skill_Lune_HealingLight",
            "BP_Battle_SkillScript_HealingLight",
            "HealingLight",
            1,
            "Dark Power",
            "Consumes all <img id=\"Stain_Dark\"/> stains: Lune gains 5 <keyword id=\"APShard\">AP</> per stain consumed.",
            "Consumes all <img id=\"Stain_Dark\"/>: Lune gains 5 <keyword id=\"APShard\">AP</> per stain.",
            Array.Empty<(string, string, double?)>(),
            "ElementalTrick",
            new FVector2D(475.0f, 825.0f),
            ConsumeElement: "Dark",
            ApPerStain: 5
            // Keeps the vanilla HealingLight sequence (donor == new, no rename).
        ),

        // Angel's Gift: HealingLight variant — 1 AP, consume all Light -> 3 AP to Lune + all allies.
        new(
            "DA_Skill_Lune_AngelsGift",
            "BP_Battle_SkillScript_AngelsGift",
            "AngelsGift",
            "DA_Skill_Lune_HealingLight",
            "BP_Battle_SkillScript_HealingLight",
            "HealingLight",
            1,
            "Angel's Gift",
            "Consumes all <img id=\"Stain_Light\"/> stains: grants 3 <keyword id=\"APShard\">AP</> to every party member per stain consumed.",
            "Consumes all <img id=\"Stain_Light\"/>: 3 <keyword id=\"APShard\">AP</> to all allies per stain.",
            Array.Empty<(string, string, double?)>(),
            "Chiaroscuro",
            new FVector2D(975.0f, 325.0f),
            ConsumeElement: "Light",
            ApPerStain: 3,
            PartyAp: true
            // Keeps the vanilla HealingLight sequence (donor == new, no rename).
        ),

        // Ultima: 21 AP, overcharge 4 Light, single-target Light nuke (Ice Lance anim).
        // Lune-native donor (IceGust) — same family as the proven Chiaroscuro clone:
        // every import stays /Lune/, no Maelle/Stendhal/stance references. The DA's
        // OverchargeRequiredStains map (4 Light) is consumed to overcharge at the standard
        // 4-stain x10: base 5.344 x10 = 53.44 max. The sequence stays the donor's vanilla
        // SEQ_SkillLune_IceGust (never cloned/written).
        new(
            "DA_Skill_Lune_Ultima",
            "BP_Battle_SkillScript_Ultima",
            "Ultima",
            "DA_Skill_Lune_IceGust",
            "BP_Battle_SkillScript_IceGust",
            "IceGust",
            21,
            "Ultima",
            "Deals <keyword id=\"DamageMultiplier\">5.34x</> single target <keyword id=\"Element_Light\">Light</> damage. 1 hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Light\"/><img id=\"Stain_Light\"/><img id=\"Stain_Light\"/><img id=\"Stain_Light\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage.",
            "Deals <keyword id=\"DamageMultiplier\">5.34x</> <keyword id=\"Element_Light\">Light</> damage. 1 hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Light\"/><img id=\"Stain_Light\"/><img id=\"Stain_Light\"/><img id=\"Stain_Light\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage.",
            new[]
            {
                ("IceGust_BaseDamage", "Ultima_BaseDamage", (double?)5.34428),
                ("IceGust_OverchargeDamageMultiplier", "Ultima_OverchargeDamageMultiplier", (double?)10.0),
                ("IceGust_BreakBarMultiplier", "Ultima_BreakBarMultiplier", (double?)1.0),
            },
            "LumiereZenithale",
            new FVector2D(225.0f, 625.0f),
            UltimaDamage: true,
            DaNameId: "IceGust"),

        // Ultima Noir: 21 AP, overcharge 4 Dark, single-target Dark nuke (Ice Lance anim).
        // Same Lune-native IceGust donor; Dark element + Dark consume + self-Blight.
        // Overcharge 4 Dark x10: base 6.413 x10 = 64.13 max.
        new(
            "DA_Skill_Lune_UltimaNoir",
            "BP_Battle_SkillScript_UltimaNoir",
            "UltimaNoir",
            "DA_Skill_Lune_IceGust",
            "BP_Battle_SkillScript_IceGust",
            "IceGust",
            21,
            "Ultima Noir",
            "Deals <keyword id=\"DamageMultiplier\">6.41x</> single target <keyword id=\"Element_Dark\">Dark</> damage. 1 hit.\n" +
            "Inflicts 10 <keyword id=\"Debuff_Blight\">Blight</> on self.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Dark\"/><img id=\"Stain_Dark\"/><img id=\"Stain_Dark\"/><img id=\"Stain_Dark\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage.",
            "Deals <keyword id=\"DamageMultiplier\">6.41x</> <keyword id=\"Element_Dark\">Dark</> damage. 1 hit.\n" +
            "10 <keyword id=\"Debuff_Blight\">Blight</> on self.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Dark\"/><img id=\"Stain_Dark\"/><img id=\"Stain_Dark\"/><img id=\"Stain_Dark\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage.",
            new[]
            {
                ("IceGust_BaseDamage", "UltimaNoir_BaseDamage", (double?)6.41314),
                ("IceGust_OverchargeDamageMultiplier", "UltimaNoir_OverchargeDamageMultiplier", (double?)10.0),
                ("IceGust_BreakBarMultiplier", "UltimaNoir_BreakBarMultiplier", (double?)1.0),
            },
            "CursedEarth",
            new FVector2D(975.0f, 525.0f),
            NoirDamage: true,
            DaNameId: "IceGust"),
    };

    public static int Apply(string modDir, bool lightStainWildcard = false)
    {
        int total = 0;
        foreach (var m in Moves)
            total += Create(modDir, m, lightStainWildcard);
        return total;
    }

    private static int Create(string modDir, NewMove m, bool lightStainWildcard)
    {
        var map = DataTableEditor.Mappings;
        UAsset Open(string p) => map != null
            ? new UAsset(p, EngineVersion.VER_UE5_4, map)
            : new UAsset(p, EngineVersion.VER_UE5_4);

        string StageDest(string srcUasset, string newStem)
        {
            var full = srcUasset.Replace('\\', '/');
            const string marker = "/Content/";
            var idx = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) throw new InvalidDataException("not under Content/: " + srcUasset);
            var rel = full[(idx + marker.Length)..];
            rel = rel.Replace(Path.GetFileNameWithoutExtension(srcUasset), newStem, StringComparison.Ordinal);
            return Path.Combine(modDir, "Sandfall", "Content", rel);
        }

        // ---------- donors ----------
        // Clones (Chiaroscuro, Lumière, Cursed Earth) only exist in work/mod after
        // LuneSkillsKit stages them; vanilla skills live in work/tables.
        string FindDonor(string stem)
        {
            var staged = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "mod"), stem + ".uasset");
            if (staged != null) return staged;
            return FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), stem + ".uasset")
                ?? throw new FileNotFoundException(stem + ".uasset under work/tables or work/mod");
        }

        // Clone donors already exist in work/mod (staged by LuneSkillsKit) — skip
        // extraction for those. Vanilla donors need extracting into work/tables.
        bool InMod(string stem) =>
            FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "mod"), stem + ".uasset") != null;
        if (!InMod(m.DonorDa)) LuneSkillsKit.EnsureExtracted(m.DonorDa);
        if (!InMod(m.DonorBp)) LuneSkillsKit.EnsureExtracted(m.DonorBp);
        LuneSkillsKit.EnsureExtracted("DT_SkillIcons");
        LuneSkillsKit.EnsureExtracted("DA_SkillGraph_Lune");
        var donorDa = FindDonor(m.DonorDa);
        var donorBp = FindDonor(m.DonorBp);
        // The graph is mutated by LuneSkillsKit first — use the staged mod graph so the
        // clone prereqs (Lumière, Cursed Earth) resolve. Icon/balancing/string-table are
        // vanilla (extracted) but also staged; prefer the staged copies for consistency.
        var graphSrc = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "mod"), "DA_SkillGraph_Lune.uasset")
            ?? FileCopy.FindTablesAsset("DA_SkillGraph_Lune.uasset")
            ?? throw new FileNotFoundException("DA_SkillGraph_Lune.uasset");
        var iconSrc = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "mod"), "DT_SkillIcons.uasset")
            ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), "DT_SkillIcons.uasset")
            ?? throw new FileNotFoundException("DT_SkillIcons.uasset under work/tables or work/mod");
        var balSrc = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "mod"), "DT_Balancing_MainCharacterSkills.uasset")
            ?? FileCopy.FindTablesAsset("DT_Balancing_MainCharacterSkills.uasset")
            ?? throw new FileNotFoundException("DT_Balancing_MainCharacterSkills.uasset");

        var daDest = StageDest(donorDa, m.DaStem);
        var bpDest = StageDest(donorBp, m.BpStem);
        var gDest = StageDest(graphSrc, "DA_SkillGraph_Lune");
        var bDest = StageDest(balSrc, "DT_Balancing_MainCharacterSkills");
        var iDest = StageDest(iconSrc, "DT_SkillIcons");
        iDest = iDest.Replace("/Resources/Datatables/", "/Resources/DataTables/");
        foreach (var d in new[] { daDest, bpDest, gDest, bDest, iDest })
            Directory.CreateDirectory(Path.GetDirectoryName(d)!);

        // ---------- string table ----------
        var stSrc = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), "ST_MainCharacters_Skills.uasset")
            ?? throw new FileNotFoundException("ST_MainCharacters_Skills.uasset");
        var stDest = StageDest(stSrc, "ST_MainCharacters_Skills");
        Directory.CreateDirectory(Path.GetDirectoryName(stDest)!);
        if (!File.Exists(stDest)) FileCopy.AssetPair(stSrc, stDest);
        var stKey = $"Lune_{m.NameId}";
        DataTableEditor.AddStringTableEntries(stDest, new Dictionary<string, string>
        {
            [stKey + "_Name"] = m.DisplayName,
            [stKey + "_Description_Long"] = m.Description,
            [stKey + "_Description_Short"] = m.ShortDescription,
        });

        // ---------- 1. DA: clone + retarget ----------
        FileCopy.AssetPair(donorDa, daDest);
        var da = Open(daDest);
        // NameID retarget uses the DA donor's own NameID (DaNameId). For Ultima/Noir the
        // DA donor is IceGust (its NameID stem "IceGust" is also the BP donor stem), so
        // the two pairs rename the package + the balancing-row/NameID stems separately.
        LuneSkillsKit.RetargetPairs(da, daDest, (m.DonorDa, m.DaStem), (m.DaNameId ?? m.DonorStem, m.NameId));
        foreach (var ex in da.Exports.OfType<NormalExport>())
            foreach (var p in ex.Data)
            {
                if (p.Name.ToString().Equals("SkillScript", StringComparison.OrdinalIgnoreCase) && p is SoftObjectPropertyData soft)
                {
                    var pkg = "/Game/Gameplay/Battle/Skills/Content/Lune/" + m.BpStem;
                    soft.Value = new FSoftObjectPath(new FName(da, pkg), new FName(da, m.BpStem + "_C"), null);
                    if (da.SoftObjectPathList is { Count: > 0 } list) list.Add(soft.Value);
                }
                else if (p.Name.ToString() == "Name" && p is TextPropertyData tname)
                    tname.Value = new FString(stKey + "_Name");
                else if (p.Name.ToString() == "Description" && p is TextPropertyData tdesc)
                    tdesc.Value = new FString(stKey + "_Description_Long");
                else if (p.Name.ToString() == "ShortDescription" && p is TextPropertyData tshort)
                    tshort.Value = new FString(stKey + "_Description_Short");
                else if (p.Name.ToString() == "APCost" && p is IntPropertyData ap)
                    ap.Value = m.ApCost;
                else if (p.Name.ToString() == "BaseElementalType" && p is EnumPropertyData bpe && m.UltimaDamage)
                    bpe.Value = FName.FromString(da, "EAttackType::NewEnumerator5"); // Light
                else if (p.Name.ToString() == "BaseElementalType" && p is EnumPropertyData bpd && m.NoirDamage)
                    bpd.Value = FName.FromString(da, "EAttackType::NewEnumerator4"); // Dark
                else if (p.Name.ToString() == "TargetingType" && p is EnumPropertyData tgt && (m.UltimaDamage || m.NoirDamage))
                    tgt.Value = FName.FromString(da, "NewEnumerator1"); // single enemy (IceGust's value)
                else if (p.Name.ToString() == "TargetingType" && p is EnumPropertyData tgtSelf && m.ConsumeElement is not null && !m.PartyAp)
                    tgtSelf.Value = FName.FromString(da, "NewEnumerator2"); // Dark Power: self-buff (SpeedUp/GuardUp shape)
                else if (p.Name.ToString() == "SkillTags" && p is StructPropertyData st
                    && m.ConsumeElement is not null && !m.PartyAp)
                {
                    // Dark Power is an AP self-buff, not a heal. Self-buffs keep
                    // TargetingType=NewEnumerator2 but carry SkillEffect.Buff (SpeedUp,
                    // GuardUp) — the Heal tag is what made the game offer ally targets.
                    // Rewrite the container's single tag rather than emptying it (an empty
                    // GameplayTagContainer breaks ToString and changes semantics).
                    foreach (var inner in st.Value ?? new List<PropertyData>())
                        if (inner is GameplayTagContainerPropertyData gtc)
                            gtc.Value = new[] { FName.FromString(da, "SkillInfos.SkillEffect.Buff") };
                }
            }
        da.FolderName = new FString("/Game/Gameplay/SkillTree/Content/Lune/Skills/" + m.DaStem);
        da.Write(daDest);
        // Override the stain map: hard 4-Light/4-Dark for Ultima/Noir (also clears the
        // donor's produce map — IceGust generates Ice+Light, Ultima/Noir generate none);
        // the consume element for Dark Power / Angel's Gift (drives the UI consume icon).
        Dictionary<string, int>? overcharge = null;
        bool ultima = m.UltimaDamage || m.NoirDamage;
        if (ultima)
            overcharge = new Dictionary<string, int> { [m.UltimaDamage ? "Light" : "Dark"] = 4 };
        else if (m.ConsumeElement is not null)
            overcharge = new Dictionary<string, int> { [m.ConsumeElement] = 1 };
        if (overcharge is not null)
            StainsMechanicKit.WriteFile(daDest, overcharge,
                ultima || m.ConsumeElement is not null ? new Dictionary<string, int>() : null);

        // ---------- 2. skill script BP: retarget + kismet ----------
        // Ultima/Noir use a Lune-native donor (IceGust), so the clone stages under
        // Content/Lune like every other LuneSkillsKit clone. No Maelle/Stendhal.
        FileCopy.AssetPair(donorBp, bpDest);
        var bp = Open(bpDest);
        var pairs = new List<(string, string)> { (m.DonorBp, m.BpStem) };
        foreach (var (donorRow, newRow, _) in m.BalancingRows)
            pairs.Add((donorRow, newRow));
        // FX element swaps (the proven Chiaroscuro/Lumière ExtraPairs pattern —
        // IceGust's FX_ExplosionIce becomes the move's element explosion).
        if (m.UltimaDamage)
        {
            pairs.Add(("ElementalFX/Ice/FX_ExplosionIce", "ElementalFX/Light/FX_ExplosionLight"));
            pairs.Add(("FX_ExplosionIce", "FX_ExplosionLight"));
        }
        else if (m.NoirDamage)
        {
            pairs.Add(("ElementalFX/Ice/FX_ExplosionIce", "ElementalFX/Dark/FX_ExplosionDark"));
            pairs.Add(("FX_ExplosionIce", "FX_ExplosionDark"));
        }
        LuneSkillsKit.RetargetPairs(bp, bpDest, pairs.ToArray());
        // Don't rename the sequence: the clone keeps the donor's vanilla
        // SEQ_SkillLune_IceGust (already a Lune sequence in the game paks).
        if (m.ConsumeElement is not null)
            PatchConsumeForAp(bp, m);
        if (m.UltimaDamage)
        {
            PatchUltimaDamage(bp, m);
            // PortAvailabilityGate(bp, "Light", 4); // disabled: export-splice corrupts BP
        }
        if (m.NoirDamage)
        {
            PatchNoir(bp, m, lightStainWildcard);
            // PortAvailabilityGate(bp, "Dark", 4); // disabled: export-splice corrupts BP
            // Sequence clone (SEQ_SkillLune_UltimaNoir) DISABLED: packing a written LevelSequence
            // crashes the game on battle load (the possessable/hit-sound raw exports don't survive
            // the UAssetAPI re-serialize for the game's loader). Noir points at the vanilla
            // SEQ_SkillLune_IceGust — the ice VO/SFX/FX therefore stay vanilla.
            // CloneNoirSequence(modDir, bp, m);
        }
        bp.FolderName = new FString("/Game/Gameplay/Battle/Skills/Content/Lune/" + m.BpStem);
        bp.Write(bpDest);

        // ---------- 3. balancing rows ----------
        if (!File.Exists(bDest)) FileCopy.AssetPair(balSrc, bDest);
        var balEd = new DataTableEditor(bDest);
        foreach (var (donorRow, newRow, val) in m.BalancingRows)
        {
            balEd.CloneRow(donorRow, newRow);
            if (val is double v)
                balEd.Apply($"{{\"{newRow}\":{{\"FloatValue\":{v.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}}}");
        }
        balEd.Save(bDest);

        // ---------- 4. icon row ----------
        if (!File.Exists(iDest)) FileCopy.AssetPair(iconSrc, iDest);
        var iconEd = new DataTableEditor(iDest);
        iconEd.CloneRow(m.DonorStem, m.NameId);
        if (!iconEd.TryGetRow(m.NameId, out _)) throw new InvalidDataException("icon row missing after clone");
        iconEd.Save(iDest);

        // ---------- 5. graph node ----------
        if (!File.Exists(gDest)) FileCopy.AssetPair(graphSrc, gDest);
        var g = Open(gDest);
        var gx = g.Exports.OfType<NormalExport>().First();
        var nodes = (ArrayPropertyData)gx.Data[0];
        var edges = (ArrayPropertyData)gx.Data[1];
        FPackageIndex prereqIdx = null;
        Import templateImp = null;
        bool alreadyOnGraph = false;
        for (int i = 0; i < nodes.Value.Length; i++)
        {
            var nodeStruct = (StructPropertyData)nodes.Value[i];
            var su = (StructPropertyData)nodeStruct.Value[0];
            var skillPtr = ((ObjectPropertyData)su.Value[0]).Value;
            if (skillPtr is null || skillPtr.IsNull() || !skillPtr.IsImport()) continue;
            var impName = g.Imports[Math.Abs(skillPtr.Index) - 1].ObjectName.ToString();
            if (impName == m.DaStem) alreadyOnGraph = true;
            if (impName == "DA_Skill_Lune_" + m.PrereqNameId)
            {
                prereqIdx = skillPtr;
                templateImp = g.Imports[Math.Abs(skillPtr.Index) - 1];
            }
        }
        if (alreadyOnGraph)
            return 5;
        if (prereqIdx is null)
            throw new InvalidDataException($"prerequisite node {m.PrereqNameId} not found in Lune graph");
        var daPkgPath = "/Game/Gameplay/SkillTree/Content/Lune/Skills/" + m.DaStem;
        var newDaIdx = LuneSkillsKit.EnsureObjectImport(g, daPkgPath, m.DaStem,
            templateImp.ClassName.ToString(), templateImp.ClassPackage.ToString());

        var node = (StructPropertyData)nodes.Value[0].Clone();
        node.Value[0] = (StructPropertyData)node.Value[0].Clone();
        node.Value[1] = (StructPropertyData)node.Value[1].Clone();
        var suN = (StructPropertyData)node.Value[0];
        ((ObjectPropertyData)suN.Value[0]).Value = newDaIdx;
        ((IntPropertyData)suN.Value[1]).Value = m.ApCost;
        ((BoolPropertyData)suN.Value[2]).Value = false;
        var req = (StructPropertyData)suN.Value[3];
        ((ObjectPropertyData)req.Value[0]).Value = null;
        ((NamePropertyData)req.Value[1]).Value = FName.FromString(g, "None");
        ((BoolPropertyData)suN.Value[4]).Value = false;
        var posWrap = (StructPropertyData)node.Value[1];
        ((Vector2DPropertyData)posWrap.Value[0]).Value = m.Position;
        nodes.Value = nodes.Value.Append(node).ToArray();

        var edge = (StructPropertyData)edges.Value[0].Clone();
        ((ObjectPropertyData)edge.Value[0]).Value = prereqIdx;
        ((ObjectPropertyData)edge.Value[1]).Value = newDaIdx;
        edges.Value = edges.Value.Append(edge).ToArray();
        g.Write(gDest);

        Console.WriteLine($"[apocalypse] {m.DisplayName} (AP {m.ApCost}) staged");
        return 5;
    }

    /// <summary>
    /// Dark Power / Angel's Gift: keep HealingLight's declared-local consume
    /// (LetObj GetComponentByClass → TryConsumeOverchargeStainsFromSkill), then
    /// grant AP using HealingLight's declared Array_Get local, then jump to Return
    /// so the donor heal never runs.
    ///
    /// The previous path synthesized GetComponentByClass.ConsumeStainByType as a
    /// chained context with no LetObj into a declared local — that crashes the VM
    /// at stain consumption. Dark still cannot use the native TryConsume (it does
    /// not eat Dark); that one call is replaced with ConsumeStainByType on the
    /// same declared holder local (Chiaroscuro's CustomDarkConsume pattern).
    /// </summary>
    private static void PatchConsumeForAp(UAsset bp, NewMove m)
    {
        var fn = LuneSkillsKit.OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return;
        var stmts = fn.ScriptBytecode.ToList();
        var element = m.ConsumeElement!;

        int consumeIdx = -1;
        EX_LocalVariable? holder = null;
        int returnIdx = -1;
        EX_JumpIfNot? overchargeGate = null;
        KismetExpression? arrayGetStmt = null;
        KismetExpression? letIndex0 = null;
        KismetPropertyPointer? arrayGetItem = null;
        for (int i = 0; i < stmts.Count; i++)
        {
            if (stmts[i] is EX_Return && returnIdx < 0) returnIdx = i;
            if (stmts[i] is EX_JumpIfNot jn && overchargeGate is null && IsOverchargedCondition(jn))
                overchargeGate = jn;

            EX_VirtualFunction? tc = null;
            EX_LocalVariable? h = null;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "TryConsumeOverchargeStainsFromSkill" && tc is null) tc = vf;
                if (e is EX_LocalVariable lv && h is null)
                {
                    var nm = PtrName(lv.Variable);
                    if (nm.StartsWith("CallFunc_GetComponentByClass_ReturnValue", StringComparison.Ordinal)) h = lv;
                }
            });
            if (tc is not null && consumeIdx < 0) { consumeIdx = i; holder = h; }

            if (arrayGetStmt is null)
            {
                KismetPropertyPointer? item = null;
                MayhemFixKit.Walk(new[] { stmts[i] }, e =>
                {
                    if (e is EX_LocalVariable lv && PtrName(lv.Variable) == "CallFunc_Array_Get_Item")
                        item = lv.Variable;
                });
                if (item is not null) { arrayGetStmt = stmts[i]; arrayGetItem = item; }
            }
            if (letIndex0 is null && stmts[i] is EX_Let let
                && let.Variable is EX_LocalVariable idxLv
                && PtrName(idxLv.Variable) == "Temp_int_Array_Index_Variable"
                && let.Expression is EX_IntConst ic && ic.Value == 0)
                letIndex0 = stmts[i];
        }
        EX_InstanceVariable? statsIv = null;
        EX_InstanceVariable? selfIv = null;
        EX_InstanceVariable? targetedIv = null;
        KismetPropertyPointer? lengthPtr = null;
        KismetPropertyPointer? lessPtr = null;
        KismetExpression? letCounter0 = null;
        KismetExpression? letLess = null;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_InstanceVariable iv)
                {
                    var nm = PtrName(iv.Variable);
                    if (nm == "AC_jRPG_CharacterStats" && statsIv is null) statsIv = iv;
                    if (nm == "CurrentCharacter" && selfIv is null) selfIv = iv;
                    if (nm == "TargetedCharacters" && targetedIv is null) targetedIv = iv;
                }
                if (e is EX_LocalVariable lv)
                {
                    var nm = PtrName(lv.Variable);
                    if (nm == "CallFunc_Array_Length_ReturnValue" && lengthPtr is null) lengthPtr = lv.Variable;
                    if (nm == "CallFunc_Less_IntInt_ReturnValue" && lessPtr is null) lessPtr = lv.Variable;
                }
            });
        foreach (var s in stmts)
        {
            if (letCounter0 is null && s is EX_Let letC
                && letC.Variable is EX_LocalVariable cv
                && PtrName(cv.Variable) == "Temp_int_Loop_Counter_Variable"
                && letC.Expression is EX_IntConst ic && ic.Value == 0)
                letCounter0 = s;
            if (letLess is null && s is EX_LetBool)
            {
                bool hasLess = false;
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (e is EX_FinalFunction ff && ff.StackNode.IsImport()
                        && bp.Imports[Math.Abs(ff.StackNode.Index) - 1].ObjectName.ToString() == "Less_IntInt")
                        hasLess = true;
                });
                if (hasLess) letLess = s;
            }
        }

        if (consumeIdx < 0 || holder is null || returnIdx < 0 || arrayGetStmt is null || letIndex0 is null
            || arrayGetItem is null || statsIv is null || selfIv is null || lengthPtr is null
            || lessPtr is null || letCounter0 is null || letLess is null)
        {
            Console.WriteLine($"[{m.NameId}] PatchConsumeForAp aborted (consume={consumeIdx} holder={holder is not null} len={lengthPtr is not null} less={letLess is not null})");
            return;
        }

        uint returnOffset = 0;
        for (int i = 0; i < returnIdx; i++) returnOffset += (uint)stmts[i].GetSize(bp);
        if (overchargeGate is not null) overchargeGate.CodeOffset = returnOffset;

        var emptyPtr = new KismetPropertyPointer(new FFieldPath());
        // Count the held stains of the consume element via GetStainCountOfType(element,
        // out count) — the holder's real single-element count function. The previous call
        // "Get Active Stain Count By Type" returns a full element->count MAP (one out
        // param) and takes no element input; passing (byte element, out int) against that
        // signature misaligns the VM stack and crashes the move the moment the consume
        // block runs.
        var countCall = new EX_Context
        {
            ObjectExpression = MayhemFixKit.CloneKismet(holder),
            ContextExpression = new EX_VirtualFunction
            {
                VirtualFunctionName = FName.FromString(bp, "GetStainCountOfType"),
                Parameters = new KismetExpression[]
                {
                    new EX_ByteConst { Value = (byte)StainsMechanicKit.Ordinal(element) },
                    new EX_LocalVariable { Variable = lengthPtr },
                },
            },
            RValuePointer = emptyPtr,
        };
        uint consumeOff = 0;
        for (int i = 0; i < consumeIdx; i++) consumeOff += (uint)stmts[i].GetSize(bp);
        long d = (long)countCall.GetSize(bp) - stmts[consumeIdx].GetSize(bp);
        if (d > 0) MayhemFixKit.ShiftAbsolute(bp, fn, consumeOff, (uint)d, null);
        else if (d < 0) LuneSkillsKit.ShiftAbsoluteSigned(bp, fn, consumeOff, (uint)(-d));
        var replaced = fn.ScriptBytecode.ToList();
        replaced[consumeIdx] = countCall;
        fn.ScriptBytecode = replaced.ToArray();
        stmts = replaced;

        // Consume EVERY held stain of the element. ConsumeStainByType(element, reason)
        // eats exactly ONE stain (its second param is a Reason, not a count), so issue
        // four back-to-back calls with Reason=1 — each re-finds the next remaining stain
        // of the type. With N held, N calls eat N stains and the rest no-op (Ultima
        // Noir's proven 4× ConsumeStainByType(Dark,1) shape).
        var eats = new List<KismetExpression>();
        for (int i = 0; i < 4; i++)
            eats.Add(ConsumeOnHolder(bp, holder, element, 1));
        var block = new List<KismetExpression>(eats);
        if (m.PartyAp && targetedIv is not null)
        {
            var targetedExpr = new EX_Context
            {
                ObjectExpression = new EX_Context
                {
                    ObjectExpression = (EX_InstanceVariable)MayhemFixKit.CloneKismet(selfIv),
                    ContextExpression = (EX_InstanceVariable)MayhemFixKit.CloneKismet(statsIv),
                    RValuePointer = emptyPtr,
                },
                ContextExpression = (EX_InstanceVariable)MayhemFixKit.CloneKismet(targetedIv),
                RValuePointer = emptyPtr,
            };
            block.Add(new EX_Context
            {
                ObjectExpression = (EX_InstanceVariable)MayhemFixKit.CloneKismet(selfIv),
                ContextExpression = new EX_VirtualFunction
                {
                    VirtualFunctionName = FName.FromString(bp, "FillTargetsWithOtherHeroes"),
                    Parameters = new KismetExpression[]
                    {
                        MayhemFixKit.CloneKismet(targetedExpr),
                        MayhemFixKit.CloneKismet(targetedExpr),
                    },
                },
                RValuePointer = emptyPtr,
            });
        }

        // 4 gated GainAP(per) bursts: if stainCount > i, grant once per stain.
        // Solo (Dark Power): the single grant goes to Lune (CurrentCharacter).
        // Party (Angel's Gift): every grant goes through the filled TargetedCharacters
        // array (which includes Lune), one per member — no separate self grant, which
        // paid Lune twice while the third member was never paid (only indices 0-1).
        //
        // TargetedCharacters is filled with every hero (FillTargetsWithOtherHeroes),
        // so a 5-member party needs all five indices. 5 is the Party of Five
        // ceiling; the fetch is KismetArrayLibrary.Array_Get (bounds-checked), so
        // a 3-member vanilla party simply reads a null slot and skips.
        const int MaxPartyTargets = 5;
        for (int i = 0; i < 4; i++)
        {
            var setI = (EX_Let)MayhemFixKit.CloneKismet(letCounter0);
            if (setI.Expression is EX_IntConst ic) ic.Value = i;
            var less = MayhemFixKit.CloneKismet(letLess);
            var gate = new EX_JumpIfNot
            {
                CodeOffset = 0,
                BooleanExpression = new EX_LocalVariable { Variable = lessPtr },
            };
            var burst = new List<KismetExpression> { setI, less, gate };
            if (!m.PartyAp)
            {
                burst.Add(GainApOn(bp, (EX_InstanceVariable)MayhemFixKit.CloneKismet(selfIv), statsIv, m.ApPerStain));
            }
            else
            {
                for (int t = 0; t < MaxPartyTargets; t++)
                {
                    var setT = (EX_Let)MayhemFixKit.CloneKismet(letIndex0);
                    if (setT.Expression is EX_IntConst ix) ix.Value = t;
                    burst.Add(setT);
                    burst.Add(MayhemFixKit.CloneKismet(arrayGetStmt));
                    burst.Add(GainApOn(bp, new EX_LocalVariable { Variable = arrayGetItem }, statsIv, m.ApPerStain));
                }
            }
            uint burstAfter = (uint)burst.Sum(b => (long)b.GetSize(bp));
            // gate target filled after we know insert offset; store relative end in a dummy then fix below
            block.AddRange(burst);
        }

        var skip = new EX_Jump { CodeOffset = 0 };
        block.Add(skip);

        uint insertOffset = 0;
        for (int i = 0; i <= consumeIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        uint blockSize = (uint)block.Sum(b => (long)b.GetSize(bp));
        uint newReturn = 0;
        for (int i = 0; i < returnIdx; i++) newReturn += (uint)stmts[i].GetSize(bp);
        skip.CodeOffset = newReturn + blockSize;

        // Fill JumpIfNot targets: each gate skips the rest of its burst (to the next setI or skip).
        uint pos = insertOffset;
        for (int bi = 0; bi < block.Count; bi++)
        {
            if (block[bi] is EX_JumpIfNot jn && jn.BooleanExpression is EX_LocalVariable)
            {
                uint end = pos;
                for (int k = bi; k < block.Count; k++)
                {
                    end += (uint)block[k].GetSize(bp);
                    // burst ends at the next counter-set (EX_Let of loop counter) or the final skip
                    if (k > bi && (block[k] is EX_Jump || (block[k] is EX_Let letN
                        && letN.Variable is EX_LocalVariable lvN
                        && PtrName(lvN.Variable) == "Temp_int_Loop_Counter_Variable")))
                    {
                        // Land ON the terminator: a counter-set re-checks the next burst's
                        // condition; the final EX_Jump (skip) redirects to Return. Without
                        // backing off the jump's size the last burst's gate overshoots the
                        // skip and falls into the donor heal/generate body (in-game crash
                        // when the move is cast with fewer than 4 stains).
                        if (block[k] is EX_Let || block[k] is EX_Jump) end -= (uint)block[k].GetSize(bp);
                        break;
                    }
                }
                jn.CodeOffset = end;
            }
            pos += (uint)block[bi].GetSize(bp);
        }

        MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, blockSize, null);
        var list = fn.ScriptBytecode.ToList();
        list.InsertRange(consumeIdx + 1, block);
        fn.ScriptBytecode = list.ToArray();
        MayhemFixKit.RecalcContextSkips(bp, fn);
        Console.WriteLine($"[{m.NameId}] consume {element} x4, {m.ApPerStain}×N AP to {(m.PartyAp ? "party" : "Lune")} (heal skipped)");
    }

    private static EX_Context ConsumeOnHolder(UAsset bp, EX_LocalVariable holder, string element, int count)
    {
        var emptyPtr = new KismetPropertyPointer(new FFieldPath());
        return new EX_Context
        {
            ObjectExpression = MayhemFixKit.CloneKismet(holder),
            ContextExpression = new EX_VirtualFunction
            {
                VirtualFunctionName = FName.FromString(bp, "ConsumeStainByType"),
                Parameters = new KismetExpression[]
                {
                    new EX_ByteConst { Value = (byte)StainsMechanicKit.Ordinal(element) },
                    new EX_ByteConst { Value = (byte)count },
                },
            },
            RValuePointer = emptyPtr,
        };
    }

    private static EX_Context GainApOn(UAsset bp, KismetExpression character, EX_InstanceVariable statsIv, int ap)
    {
        var emptyPtr = new KismetPropertyPointer(new FFieldPath());
        return new EX_Context
        {
            ObjectExpression = new EX_Context
            {
                ObjectExpression = character,
                ContextExpression = (EX_InstanceVariable)MayhemFixKit.CloneKismet(statsIv),
                RValuePointer = emptyPtr,
            },
            ContextExpression = new EX_VirtualFunction
            {
                VirtualFunctionName = FName.FromString(bp, "GainAP"),
                Parameters = new KismetExpression[]
                {
                    new EX_IntConst { Value = ap },
                    new EX_ByteConst { Value = 1 },
                },
            },
            RValuePointer = emptyPtr,
        };
    }

    private static string PtrName(KismetPropertyPointer? p) =>
        p?.New?.Path?.FirstOrDefault()?.ToString() ?? "";

    private static bool IsOverchargedCondition(EX_JumpIfNot jn)
    {
        if (jn.BooleanExpression is not EX_Context ctx) return false;
        return ctx.ObjectExpression is EX_InstanceVariable iv1 && PtrName(iv1.Variable) == "SkillState"
            && ctx.ContextExpression is EX_InstanceVariable iv2 && PtrName(iv2.Variable) == "IsOvercharged";
    }

    /// <summary>Ultima: Lune-native IceGust donor. Set the MakeDamageBuilder element byte
    /// from Ice(3) to Light(7) and drop the donor's Ice Slow tail (a Light nuke must not
    /// apply Slow). The 4-Light hard requirement + consume come from the DA's
    /// OverchargeRequiredStains map via the native TryConsumeOverchargeStainsFromSkill
    /// that IceGust already calls — no stance block exists on this donor.</summary>
    private static void PatchUltimaDamage(UAsset bp, NewMove m)
    {
        var fn = LuneSkillsKit.OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return;
        int n = 0;
        foreach (var s in fn.ScriptBytecode)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "MakeDamageBuilder")
                    foreach (var p in vf.Parameters)
                        if (p is EX_ByteConst pb && pb.Value == StainsMechanicKit.Ordinal("Ice"))
                        { pb.Value = (byte)StainsMechanicKit.Ordinal("Light"); n++; }
            });
        n += RemoveSlowTail(bp, fn);
        n += PatchImpactFx(bp, fn, 10f); // screen-filling white impact (FX_ExplosionLight, keep)
        Console.WriteLine($"[Ultima] IceGust donor: Ice(3)->Light(7), Slow tail removed, 4 Light consumed via native map ({n})");
    }

    /// <summary>
    /// Removes IceGust's per-hit Slow application (the donor applies Slow on every last
    /// hit). The statement is the ApplyBuff(BP_BattleBuff_Slow_C) block — its leading
    /// LetBool/PopExecutionFlowIfNot(BooleanAND) gate and trailing PopExecutionFlow are
    /// left intact so the loop-body flow stays balanced. Returns 1 when removed.
    /// </summary>
    private static int RemoveSlowTail(UAsset bp, FunctionExport fn)
    {
        var stmts = fn.ScriptBytecode.ToList();
        int slowIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool has = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (has) return;
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "ApplyBuff")
                {
                    foreach (var p in vf.Parameters)
                        MayhemFixKit.Walk(new[] { p }, e2 =>
                        {
                            if (e2 is EX_ObjectConst oc && oc.Value.IsImport() &&
                                oc.Value.ToImport(bp).ObjectName.ToString() == "BP_BattleBuff_Slow_C") has = true;
                        });
                }
            });
            if (has) { slowIdx = i; break; }
        }
        if (slowIdx < 0) return 0;
        uint rmOffset = 0;
        for (int i = 0; i < slowIdx; i++) rmOffset += (uint)stmts[i].GetSize(bp);
        uint rmSize = (uint)stmts[slowIdx].GetSize(bp);
        LuneSkillsKit.ShiftAbsoluteSigned(bp, fn, rmOffset, rmSize);
        var list = fn.ScriptBytecode.ToList();
        list.RemoveAt(slowIdx);
        fn.ScriptBytecode = list.ToArray();
        MayhemFixKit.RecalcContextSkips(bp, fn);
        Console.WriteLine($"[apocalypse] IceGust Slow tail removed (stmt {slowIdx})");
        return 1;
    }

    /// <summary>Ultima Noir: Lune-native IceGust donor. Set the MakeDamageBuilder element
    /// byte from Ice(3) to Dark(6), replace the native consume with 4×
    /// ConsumeStainByType(Dark) (the native path does not reliably eat Dark), drop the
    /// donor's Ice Slow, and apply 10 Blight to the caster in the Slow statement's slot.</summary>
    private static void PatchNoir(UAsset bp, NewMove m, bool lightStainWildcard)
    {
        var fn = LuneSkillsKit.OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return;
        int n = 0;
        foreach (var s in fn.ScriptBytecode)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "MakeDamageBuilder")
                    foreach (var p in vf.Parameters)
                        if (p is EX_ByteConst pb && pb.Value == StainsMechanicKit.Ordinal("Ice"))
                        { pb.Value = (byte)StainsMechanicKit.Ordinal("Dark"); n++; }
            });

        // With Light-stain substitution enabled, keep the native consume so the
        // shared holder can spend exact Dark first and Light for the remainder.
        // With substitution off, replace it with four hard Dark consumes because
        // the native path does not reliably eat Dark in that mode.
        if (lightStainWildcard)
        {
            Console.WriteLine("[UltimaNoir] Light-stain substitution ON: using native 4-Dark consume");
        }
        else
        {
            var stmts = fn.ScriptBytecode.ToList();
            int consumeIdx = -1;
            EX_LocalVariable? holder = null;
            for (int i = 0; i < stmts.Count; i++)
            {
                EX_VirtualFunction? tc = null;
                EX_LocalVariable? h = null;
                MayhemFixKit.Walk(new[] { stmts[i] }, e =>
                {
                    if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "TryConsumeOverchargeStainsFromSkill" && tc is null) tc = vf;
                    if (e is EX_LocalVariable lv && h is null)
                    {
                        var nm = PtrName(lv.Variable);
                        if (nm.StartsWith("CallFunc_GetComponentByClass_ReturnValue", StringComparison.Ordinal)) h = lv;
                    }
                });
                if (tc is not null) { consumeIdx = i; holder = h; break; }
            }
            if (consumeIdx >= 0 && holder is not null)
            {
                var eats = new List<KismetExpression>();
                for (int i = 0; i < 4; i++)
                    eats.Add(ConsumeOnHolder(bp, holder, "Dark", 1));
                uint eatOffset = 0;
                for (int i = 0; i < consumeIdx; i++) eatOffset += (uint)stmts[i].GetSize(bp);
                long delta = eats.Sum(e => (long)e.GetSize(bp)) - stmts[consumeIdx].GetSize(bp);
                if (delta > 0) MayhemFixKit.ShiftAbsolute(bp, fn, eatOffset, (uint)delta, null);
                else if (delta < 0) LuneSkillsKit.ShiftAbsoluteSigned(bp, fn, eatOffset, (uint)(-delta));
                var list = fn.ScriptBytecode.ToList();
                list.RemoveAt(consumeIdx);
                list.InsertRange(consumeIdx, eats);
                fn.ScriptBytecode = list.ToArray();
                n++;
            }
            else
            {
                Console.WriteLine("[UltimaNoir] dark consume not found; keeping native consume (map = 4 Dark)");
            }
        }

        // Drop the donor's Ice Slow and, in that statement's slot, apply 10 Blight to
        // the caster (gated by the existing per-hit gate the Slow used, so it applies
        // once on the hit — no extra statement inserted, flow stays balanced).
        n += RepurposeSlowToSelfBlight(bp, fn);
        // Screen-filling black+purple circular impact: swap FX_ExplosionDark ->
        // FX_ExplosionVoid (the proven Lune-safe Void blast) and scale it up 10x.
        n += PatchImpactFx(bp, fn, 10f,
            "/Game/FX/FX_Leo/Fx/ElementalFX/Void/FX_ExplosionVoid", "FX_ExplosionVoid");
        Console.WriteLine($"[UltimaNoir] IceGust donor: Ice(3)->Dark(6), 4x Dark consume, Slow -> self-Blight 10 ({n})");
    }

    /// <summary>
    /// Replaces the IceGust donor's per-hit ApplyBuff(BP_BattleBuff_Slow_C, target, 3) with N
    /// copies of ApplyBuff(BP_BattleBuff_Blight_C, self) — same statement cloned N times, so
    /// the existing per-hit gate + loop-body flow stay balanced and no new (undeclared) locals
    /// are introduced. The recipient is changed from the targeted enemy to Lune herself; each
    /// copy writes the already-declared CallFunc_ApplyBuff_CreatedBuffInstance out-local.
    ///
    /// NOTE: the earlier SpawnObject + ChangeStackCount(10) + ActivateBuff version crashed the
    /// VM on the overcharged cast (its synthesized SpawnObject_ReturnValue local was undeclared
    /// — the HANDOFF's documented VM-crash class). Repeating the proven ApplyBuff statement is
    /// the safe way to try for more Blight stacks.
    /// </summary>
    private static int RepurposeSlowToSelfBlight(UAsset bp, FunctionExport fn)
    {
        var stmts = fn.ScriptBytecode.ToList();
        int slowIdx = -1;
        EX_VirtualFunction? apply = null;
        for (int i = 0; i < stmts.Count; i++)
        {
            EX_VirtualFunction? vfSlow = null;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (vfSlow is null && e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "ApplyBuff")
                {
                    foreach (var p in vf.Parameters)
                        MayhemFixKit.Walk(new[] { p }, e2 =>
                        {
                            if (e2 is EX_ObjectConst oc && oc.Value.IsImport() &&
                                oc.Value.ToImport(bp).ObjectName.ToString() == "BP_BattleBuff_Slow_C")
                                vfSlow = vf;
                        });
                }
            });
            if (vfSlow is not null) { slowIdx = i; apply = vfSlow; break; }
        }
        if (apply is null || apply.Parameters.Length < 5) return 0;

        var blightIdx = LuneSkillsKit.EnsureObjectImport(bp,
            "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Blight",
            "BP_BattleBuff_Blight_C", "BP_BattleBuff_Blight_C", "/Script/Engine");

        // Parameters (ApplyBuff convention in this codebase):
        //   0 = buff class, 1 = recipient (target), 2 = stacks,
        //   3 = instigator/owner (CurrentCharacter.Stats), 4 = byte, 5 = out local.
        apply.Parameters[0] = new EX_ObjectConst { Value = blightIdx };
        apply.Parameters[1] = MayhemFixKit.CloneKismet(apply.Parameters[3]); // target -> self
        if (apply.Parameters[2] is EX_IntConst ic) ic.Value = 10;

        // Clone the (now Blight, self-targeted, 10) statement 10x. Each is a full top-level
        // statement sharing the declared out-local, so no new locals are introduced.
        const int repeats = 10;
        var block = new List<KismetExpression>();
        for (int i = 0; i < repeats; i++)
            block.Add(MayhemFixKit.CloneKismet(stmts[slowIdx]));

        uint rmOffset = 0;
        for (int i = 0; i < slowIdx; i++) rmOffset += (uint)stmts[i].GetSize(bp);
        long delta = block.Sum(e => (long)e.GetSize(bp)) - stmts[slowIdx].GetSize(bp);
        if (delta > 0) MayhemFixKit.ShiftAbsolute(bp, fn, rmOffset, (uint)delta, null);
        else if (delta < 0) LuneSkillsKit.ShiftAbsoluteSigned(bp, fn, rmOffset, (uint)(-delta));
        var list = fn.ScriptBytecode.ToList();
        list.RemoveAt(slowIdx);
        list.InsertRange(slowIdx, block);
        fn.ScriptBytecode = list.ToArray();
        MayhemFixKit.RecalcContextSkips(bp, fn);
        Console.WriteLine($"[UltimaNoir] Slow statement -> self-Blight x{repeats} (ApplyBuff, stmt {slowIdx})");
        return 1;
    }

    /// <summary>
    /// Recolours + enlarges the BP-driven impact FX of the Ultima / Ultima Noir moves.
    ///
    /// The IceGust donor's OnActionEffect spawns the move's hit system with
    /// SpawnSystemAtLocation(Self, FX, Location, Rotation, Scale(1,1,1), …). This helper:
    ///   1. Finds that spawn statement (the EX_LetObj whose assignment is the
    ///      SpawnSystemAtLocation FinalFunction).
    ///   2. Optionally swaps the spawned system to a recoloured target (Noir:
    ///      FX_ExplosionDark → FX_ExplosionVoid — the Lune-safe black+purple blast).
    ///   3. Scales the spawn's EX_VectorConst scale up (1,1,1 → factor) so the impact
    ///      reads as a huge final blast.
    ///
    /// Pure in-statement edits: no new statements are inserted, so no offset shifting
    /// is required. Returns the number of edits made.
    /// </summary>
    private static int PatchImpactFx(UAsset bp, FunctionExport fn, float sizeMult,
        string? swapSystemPath = null, string? swapSystemName = null)
    {
        var stmts = fn.ScriptBytecode.ToList();
        int n = 0;
        for (int i = 0; i < stmts.Count; i++)
        {
            // The FX spawn is a LetObj whose assignment is SpawnSystemAtLocation.
            if (stmts[i] is not EX_LetObj letObj) continue;
            EX_FinalFunction? spawn = null;
            MayhemFixKit.Walk(new[] { letObj.AssignmentExpression }, e =>
            {
                if (spawn is null && e is EX_FinalFunction ff &&
                    ff.StackNode.IsImport() &&
                    ff.StackNode.ToImport(bp).ObjectName.ToString() == "SpawnSystemAtLocation")
                    spawn = ff;
            });
            if (spawn is null) continue;

            foreach (var p in spawn.Parameters)
            {
                if (swapSystemPath is not null && p is EX_ObjectConst oc && oc.Value.IsImport())
                {
                    var impName = oc.Value.ToImport(bp).ObjectName.ToString();
                    // Only swap the FX system reference, not character/class consts.
                    if (impName.StartsWith("FX_", StringComparison.Ordinal))
                    {
                        // Niagara systems import as class NiagaraSystem (SuperSpark pattern);
                        // a fake FX_*-classed import would crash BP link at load.
                        var idx = LuneSkillsKit.EnsureObjectImport(bp, swapSystemPath,
                            swapSystemName!, "NiagaraSystem", "/Script/Niagara");
                        oc.Value = idx;
                        n++;
                        Console.WriteLine($"[apocalypse] impact FX {impName} -> {swapSystemName}");
                    }
                }
                else if (p is EX_VectorConst vc)
                {
                    // Scale the spawn scale (default 1,1,1) up to the requested size.
                    vc.Value = new FVector(vc.Value.X * sizeMult, vc.Value.Y * sizeMult, vc.Value.Z * sizeMult);
                    n++;
                    Console.WriteLine($"[apocalypse] impact scale x{sizeMult}");
                }
            }
        }
        return n;
    }
}
