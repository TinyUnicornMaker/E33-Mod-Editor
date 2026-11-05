using E33ModEditor.Core.Localization;
using E33ModEditor.Core.Loot;
using E33ModEditor.Core.Spoiler;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core.Packer;

/// <summary>Human-readable stages a front-end can show while a pack builds.</summary>
public enum BuildStage
{
    Validating,
    Tools,
    Donors,
    Core,
    Balance,
    QualityOfLife,
    Randomizer,
    Localization,
    Spoiler,
    Packing,
    Verifying,
    Installing,
    Done,
}

/// <summary>
/// One-shot rebuild of the full overhaul pack (skills, pictos, pins, weapon
/// clones, combat rebalance) plus optional randomizer. Shared by CLI
/// rebuild-mod and the standalone packer window.
/// </summary>
public static class FullPackKit
{
    public const string ModBase = "ExpeditionInfinity";
    public const string ModSuffix = "_P";

    /// <summary>Pack-name revision. Bumped when a previously installed container
    /// name has been poisoned (for example, a bad pack cached under that name by
    /// the game/Proton). Keeping the randomizer seed unchanged while changing the
    /// container name preserves the loot roll; the old name is removed by
    /// <see cref="CleanInstalledSeedPacks"/>.</summary>
    public const string PackRevision = "_v7";

    /// <summary>Seed-suffixed pack name, e.g. <c>ExpeditionInfinity_333333_v2_P</c>.
    /// The seed rides on the end so every shipped trio is traceable to its roll.
    /// <paramref name="revision"/> lets a front-end override the default
    /// <see cref="PackRevision"/> (the standalone app uses <c>_v1.3</c>).</summary>
    public static string ModNameFor(int seed, string? revision = null) =>
        $"{ModBase}_{seed}{revision ?? PackRevision}{ModSuffix}";

    public sealed class Options
    {
        public int? Seed { get; set; }
        public GameplayTweaksKit.Settings? Tweaks { get; set; }
        public SkillOverhaulKit.Settings? SkillOverhaul { get; set; }
        public WeaponBulkUpgradeKit.Settings? WeaponBulkUpgrade { get; set; }
        public WeaponDropLevelKit.Settings? WeaponDropLevel { get; set; }
        public FirstPersonCameraKit.Settings? FirstPerson { get; set; }
        public PartyFiveKit.Settings? PartyFive { get; set; }
        public RandomizerKit.Settings? Randomizer { get; set; }
        public CompatibilityKit.Settings? Compatibility { get; set; }
        public bool IncludeSpoiler { get; set; } = true;
        /// <summary>"en" (default), "fr", "pl" or "ru".</summary>
        public string Language { get; set; } = "en";
        /// <summary>Optional pack-name revision suffix, e.g. <c>_v1.3</c>.
        /// Null keeps the shared default (<see cref="PackRevision"/>).</summary>
        public string? PackRevisionOverride { get; set; }

        /// <summary>When set, each build gets its own folder under this root
        /// (<c>&lt;root&gt;/&lt;packName&gt;</c>). Null keeps the legacy flat
        /// <see cref="EditorPaths.ExportDir"/> layout used by the CLI/editor.</summary>
        public string? SeedLibraryRoot { get; set; }

        /// <summary>Copy the finished trio into <see cref="EditorPaths.ModsDir"/>.
        /// Set false for export-only builds.</summary>
        public bool InstallToMods { get; set; } = true;

        /// <summary>Run <c>retoc verify</c> on the packed container before it is
        /// installed. A failed verification aborts the build.</summary>
        public bool VerifyPack { get; set; } = true;

        public bool ForceRandomizerOn { get; set; }
        public Action<string>? Log { get; set; }
        /// <summary>Donor retoc progress: done, total, current filter name.</summary>
        public Action<int, int, string>? OnExtractProgress { get; set; }
        /// <summary>Coarse build-stage progress for a UI checklist/progress bar.</summary>
        public Action<BuildStage, double, string>? OnStage { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }

    public sealed class Result
    {
        public int Edits { get; init; }
        public int Seed { get; init; }
        public string PackName { get; init; } = "";
        public string PackDirectory { get; init; } = "";
        public string SpoilerPath { get; init; } = "";
        public IReadOnlyList<string> PackedFiles { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> InstalledFiles { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Folder that holds one seed's library copy. A null root keeps the legacy
    /// flat Export/ layout; a root produces Export/Seeds/&lt;packName&gt;.
    /// </summary>
    public static string SeedLibraryPackDir(string? root, string packName) =>
        string.IsNullOrWhiteSpace(root)
            ? EditorPaths.ExportDir
            : Path.Combine(Path.GetFullPath(root), packName);

    public static Result RebuildAndPack(Options opt)
    {
        var log = opt.Log ?? (_ => { });
        void Stage(BuildStage stage, double progress, string message, bool honorCancellation = true)
        {
            if (honorCancellation) opt.CancellationToken.ThrowIfCancellationRequested();
            opt.OnStage?.Invoke(stage, Math.Clamp(progress, 0, 1), message);
            log($"[{stage}] {message}");
        }

        Stage(BuildStage.Validating, 0.01, "Validating build options…");
        ModLocalization.ResetCache();
        var language = ModLocalization.Find(opt.Language) ?? ModLocalization.English;
        ModLocalization.SetLanguage(language.Code);
        log($"[loc] language: {language.DisplayName} ({language.Culture})");
        var tweaks = opt.Tweaks ?? new GameplayTweaksKit.Settings();
        var rando = opt.Randomizer ?? new RandomizerKit.Settings();
        if (opt.Seed is int seed) rando.Seed = seed;
        if (rando.Seed < 0) rando.Seed = Random.Shared.Next(0, 99_999_999 + 1);
        if (opt.ForceRandomizerOn)
        {
            rando.Chests = rando.EnemyDrops = rando.Merchants = true;
            rando.WeaponPassives = rando.MonsterStats = rando.WeaponStats = true;
        }
        var randoOn = opt.ForceRandomizerOn || !rando.IsOff;
        var modName = ModNameFor(rando.Seed, opt.PackRevisionOverride);
        var packDir = SeedLibraryPackDir(opt.SeedLibraryRoot, modName);
        var partyFive = opt.PartyFive ?? PartyFiveKit.Settings.Load();
        var compat = opt.Compatibility ?? new CompatibilityKit.Settings();
        string? compatUtoc = null;
        if (partyFive.Enabled)
        {
            // Our own Party of Five supersedes the third-party Bigger Party Mod:
            // both own the same BattleManager/CharactersManager/FL assets, so the
            // compatibility patch (which strips ours and edits theirs) is not run.
            if (compat.Enabled)
                log("[party5] Party of Five is on — skipping Bigger Party Mod compatibility " +
                    "(our pack owns the party assets).");
        }
        else if (compat.Enabled)
        {
            compatUtoc = CompatibilityKit.Resolve(compat);
            if (compatUtoc is null)
                log("[compat] Bigger Party Mod not found — leaving its assets in our pack " +
                    "(4th-member targeting may stay broken).");
            else
                log("[compat] Bigger Party Mod: " + Path.GetFileName(compatUtoc));
        }

        Stage(BuildStage.Tools, 0.03, "Checking retoc, repak and mappings…");
        _ = EditorPaths.RetocExe;
        if (language.Code != ModLocalization.English.Code) _ = EditorPaths.RepakExe;

        DataTableEditor.ResetMappings();
        var modDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod");
        if (Directory.Exists(modDir)) Directory.Delete(modDir, true);
        Directory.CreateDirectory(modDir);

        Stage(BuildStage.Donors, 0.06, "Extracting donor tables if needed…");
        log("Extracting donors if needed…");
        DonorExtract.EnsureAll(log, opt.OnExtractProgress, opt.CancellationToken);
        opt.OnExtractProgress?.Invoke(0, 0, "");

        // Keep ~mods suspended for all staging. Several kits extract additional
        // donor assets later; restoring the installed mods before they run makes
        // retoc read modded assets or refuse the extraction outright.
        using var suspendedMods = DonorExtract.SuspendMods();

        Stage(BuildStage.Core, 0.12, "Building core content…");
        log("Building pictos and weapon effects…");
        int total = 0;
        var fp = opt.FirstPerson ?? FirstPersonCameraKit.Settings.Load();
        if (fp.Enabled)
        {
            log("Applying first-person camera…");
            total += FirstPersonCameraKit.Apply(modDir, fp);
        }
        PictosCreateKit.CopyCreateTables(modDir);
        total += PictosCreateKit.InjectPainterConversions(modDir);
        total += PictosCreateKit.InjectStatusAffinities(modDir);
        total += PictosCreateKit.InjectHitBurn(modDir);
        PictosCreateKit.RemoveStainPictos(modDir);
        total += PictosMagnitudeKit.Inject(modDir, copyTables: false);
        total += SpecialPictosKit.Apply(modDir);
        total += CutPictosKit.Apply(modDir);
        total += BombPictosKit.Apply(modDir);
        total += ReviveDebuffKit.Apply(modDir);
        total += CleanseTweakKit.Apply(modDir);
        total += DamageLimitKit.RemoveThreeTier(modDir);
        total += DamageLimitKit.RenamePaintedPower(modDir);
        // Must run last among the picto kits: it repairs rows created by older
        // packs (CloneRow is a no-op once a row exists) and stages the extended
        // E_PassiveEffectCategory enum for the new Element/Revive/Lifesteal/Luck tags.
        total += PictoTagKit.Apply(modDir);
        WeaponEffectsCreateKit.Reload();
        total += WeaponEffectsCreateKit.Inject(modDir, copyTables: false);
        total += WeaponCurveKit.Apply(modDir);
        total += WeaponOverLevelKit.Apply(modDir);
        log("Weapon drop levels…");
        total += WeaponDropLevelKit.Apply(modDir,
            opt.WeaponDropLevel ?? WeaponDropLevelKit.Settings.Load());
        total += WeaponBulkUpgradeKit.Apply(modDir,
            opt.WeaponBulkUpgrade ?? WeaponBulkUpgradeKit.Settings.Load());
        total += WeaponPassiveSlotKit.Apply(modDir);

        var catalog = BuildCatalog();
        Stage(BuildStage.Balance, 0.42, "Applying balance and progression…");
        if (!tweaks.IsVanilla)
        {
            log("Applying gameplay tweaks…");
            total += GameplayTweaksKit.Apply(modDir, tweaks);
            log("Might weapon scaling…");
            total += MightScalingKit.Apply(modDir);
            MightScalingKit.Verify(modDir);
        }
        if (tweaks.NeedsCritDamage)
        {
            var critPath = GameplayTweaksKit.FindAsset(modDir, "BP_BattleDamages.uasset")
                ?? throw new FileNotFoundException(
                    "crit-rate patch was requested but BP_BattleDamages is not staged");
            if (!CritDamageKit.Verify(critPath, tweaks.CritDamageIncrease, tweaks.CritRateDamageScaling))
                throw new InvalidDataException(
                    "crit-rate verification failed: BP_BattleDamages.ApplyCriticalHitModifiers " +
                    "does not match the configured addend/scaling");
            log("[crit] verified crit-rate damage scaling in the staged pack");
        }

        var overhaul = opt.SkillOverhaul ?? SkillOverhaulKit.LoadSettings();
        var gustaveOn = overhaul.CharacterEnabled("Gustave");
        var luneOn = overhaul.CharacterEnabled("Lune");
        var maelleOn = overhaul.CharacterEnabled("Maelle");
        var scielOn = overhaul.CharacterEnabled("Sciel");
        var versoOn = overhaul.CharacterEnabled("Verso");
        var monocoOn = overhaul.CharacterEnabled("Monoco");

        if (luneOn)
        {
            log("Lune overhaul…");
            total += MayhemFixKit.Apply(modDir);
            total += LuneSkillsKit.Apply(modDir, overhaul.LightStainWildcard);
            total += LuneApocalypseKit.Apply(modDir, overhaul.LightStainWildcard);
        }
        else
        {
            var (cells, files) = SkillOverhaulKit.RevertCharacter(modDir, "Lune");
            log($"Lune overhaul off (reverted {cells} cells, {files} files)");
        }

        total += SkillDescriptionsKit.Apply(modDir);
        total += CombatBalanceKit.Apply(modDir, maelleOn);

        if (gustaveOn)
        {
            log("Gustave overhaul…");
            total += GustaveSkillsKit.Apply(modDir);
        }
        else
        {
            var (cells, files) = SkillOverhaulKit.RevertCharacter(modDir, "Gustave");
            log($"Gustave overhaul off (reverted {cells} cells, {files} files)");
        }

        if (maelleOn)
        {
            log("Maelle overhaul…");
            total += StanceEffectsKit.Apply(modDir);
            total += MaelleSkillsKit.Apply(modDir);
        }
        else
        {
            var (_, files) = SkillOverhaulKit.RevertCharacter(modDir, "Maelle");
            log($"Maelle overhaul off (reverted {files} files)");
        }

        if (scielOn)
        {
            log("Sciel overhaul…");
            total += ScielSkillsKit.Apply(modDir);
            total += ScielCustomKit.Apply(modDir);
        }
        else
        {
            var (cells, files) = SkillOverhaulKit.RevertCharacter(modDir, "Sciel");
            log($"Sciel overhaul off (reverted {cells} cells, {files} files)");
        }

        if (versoOn)
        {
            log("Verso overhaul…");
            total += VersoSkillsKit.Apply(modDir);
        }
        else
        {
            var (cells, files) = SkillOverhaulKit.RevertCharacter(modDir, "Verso");
            log($"Verso overhaul off (reverted {cells} cells, {files} files)");
        }

        if (monocoOn)
        {
            log("Monoco overhaul…");
            total += MonocoSkillsKit.Apply(modDir);
        }
        else
        {
            var (cells, files) = SkillOverhaulKit.RevertCharacter(modDir, "Monoco");
            log($"Monoco overhaul off (reverted {cells} cells, {files} files)");
        }

        if (overhaul.IsActive)
        {
            log("Skill Overhaul cells…");
            total += SkillOverhaulKit.Apply(modDir, overhaul);
        }
        // Lune setup-burden pricing AFTER the overhaul catalog: the catalog
        // carries these BaseDamage rows at overhaul values, so absolute pricing
        // must win. (Lune-off reverts them to vanilla via RevertCharacter.)
        if (luneOn) total += LuneSetupPricingKit.Apply(modDir);
        total += GradientEditsKit.Apply(modDir);
        var graph = SkillGraphKit.LoadSettings();
        if (graph.IsActive) total += SkillGraphKit.Apply(modDir, graph);

        if (scielOn) AssertRows(modDir, "Sciel",
            "SpectersEdge_BaseDamage", "BlackDeath_BaseDamage", "ScielLastChance_APGain",
            "FalseStart_BaseDamage", "SoulTransplant_ReviveHP", "UltimaPath_BaseDamage",
            "UltimasBargain_BaseDamage", "PhantomSever_BaseDamage", "Corruption_BaseDamage");
        if (versoOn) AssertRows(modDir, "Verso",
            "AssaultZero_BaseDamage", "FromFire_Verso_BaseDamage",
            "MarkingShot_Verso_BaseDamage", "StrikeStorm_Verso_BaseDamage",
            "Powerful_Verso_BasePowerfuLDuration", "Zanmoto_BaseDamage",
            "Repainted_NewHpValue", "RedBushido_BaseDamage",
            "RedBushido_DamageIncreasePerPercentHP", "VoidedStars_BaseDamage", "Blitzer_BaseDamage");

        if (randoOn)
        {
            Stage(BuildStage.Randomizer, 0.62, $"Rolling seed {rando.Seed}…");
            log($"Randomizer seed {rando.Seed}…");
            var extra = new List<string>();
            extra.AddRange(PictosCreateKit.PainterConversions.Select(p => p.Id));
            extra.AddRange(PictosCreateKit.Powers.Select(p => p.Id));
            extra.AddRange(PictosMagnitudeKit.Variants.Select(p => p.Id));
            extra.AddRange(PictosCreateKit.HitBurn.Select(p => p.Id));
            total += RandomizerKit.Apply(modDir, rando, catalog, extra);
        }

        log("Petank defense…");
        total += PetankDefenseKit.Apply(modDir);

        // Late balance toggles: both must run after RandomizerKit, which writes
        // absolute EnemyScaling bands and regenerates the loot tables.
        // PetankDefenseKit runs first: it copies Petank attack scaling into
        // defense, and that copy must see the unbuffed attack value.
        if (tweaks.BuffRegularEnemies && rando.MonsterStats)
        {
            log("Buffing regular enemies…");
            total += RegularEnemyBuffKit.Apply(modDir, catalog,
                tweaks.RegularEnemyHpBonus, tweaks.RegularEnemySpeedBonus,
                tweaks.RegularEnemyAttackBonus);
        }
        if (tweaks.ReduceLuminaDrops)
        {
            log("Adjusting regular lumina drops…");
            total += LuminaDropKit.Apply(modDir, catalog, tweaks.LuminaDropMultiplier);
        }

        // RandomizerKit restores/re-stages DT_jRPG_Encounters and BossVenueKit
        // edits it late; re-apply the tutorial->no-tutorial repoint after that.
        total += TutorialDisableKit.ApplyLate(modDir, tweaks.DisableTutorials);

        log("Pinning exclusive drops…");
        total += PictosMagnitudeKit.PinExclusiveDrops(modDir);

        log("Pinning Act 3 Lumiere loot to pictos 33…");
        total += RandomizerKit.PinAct3LootLevels(modDir, catalog);

        log("Mask Keeper: keeping the full mask moveset…");
        total += MaskKeeperKit.Apply(modDir, tweaks.MaskKeeperFullMoveset);

        log("Sirène: keeping the full Tisseur-synergy moveset…");
        total += SireneKit.Apply(modDir, tweaks.SireneFullMoveset);

        Stage(BuildStage.QualityOfLife, 0.72, "Applying quality-of-life changes…");

        // Party of Five submodule: last of the asset kits so nothing restages
        // (and thus reverts) the shared CharactersManager / FL / BattleManager /
        // Character_World copies it patches. Composes with the camera kit, which
        // stages Character_World first and is preserved by the kit's reuse logic.
        if (partyFive.Enabled)
        {
            log("Party of Five…");
            total += PartyFiveKit.Apply(modDir, partyFive, log);
        }

        if (partyFive.Enabled)
        {
            var rowDamage = RowDamageKit.Settings.Load();
            if (rowDamage.Enabled)
            {
                log("Back-row damage…");
                total += RowDamageKit.Apply(modDir, partyFive, rowDamage, log);
            }
        }

        StripUnroundtrippable(modDir, luneOn, log);

        string? locCulture = null;
        if (language.Code != ModLocalization.English.Code)
        {
            Stage(BuildStage.Localization, 0.80, $"Localizing for {language.DisplayName}…");
            log($"Localizing for {language.DisplayName}…");
            LocalizationKit.Apply(modDir, language.Code, log);
            locCulture = language.Culture;
            var issues = LocalizationKit.Validate(language.Code);
            if (issues.Count > 0)
                log($"[loc] warning: {issues.Count} structural issue(s); first: " +
                    string.Join(" | ", issues.Take(5)));
            // Keep the locres as the preferred path, but also inline the
            // selected translation into the staged StringTable source. This
            // guarantees the pack still reads in-language if the base locres
            // wins a duplicate vanilla key or the engine rejects a hash.
            LocalizationKit.InlineStagedTables(modDir, language.Code, log);
        }

        // Compatibility: when we own the Bigger Party Mod, drop the three assets
        // we would otherwise shadow it with. Their values are patched into the
        // mod itself below, after our pack is installed.
        if (compatUtoc is not null)
        {
            var stripped = CompatibilityKit.StripConflicts(modDir);
            log($"[compat] removed {stripped} asset(s) from our pack so the mod's copies win");
        }

        var sweep = RandomizerKit.SweepWeaponEligibility(modDir);
        if (sweep.Count > 0)
            throw new InvalidOperationException(
                "Pack aborted — forbidden weapon passives:\n  "
                + string.Join("\n  ", sweep.Take(25))
                + (sweep.Count > 25 ? $"\n  …and {sweep.Count - 25} more" : ""));

        string spoilerPath = "";
        if (opt.IncludeSpoiler)
        {
            Stage(BuildStage.Spoiler, 0.86, "Building spoiler codex…");
            log("Building spoiler codex…");
            try
            {
                var spoilerOutput = string.IsNullOrWhiteSpace(opt.SeedLibraryRoot)
                    ? null
                    : Path.Combine(packDir, $"ExpeditionInfinity_{Math.Max(0, rando.Seed)}_Spoiler.html");
                spoilerPath = SpoilerCodexKit.Build(modDir, Math.Max(0, rando.Seed), catalog, log,
                    outputPath: spoilerOutput,
                    packRevision: opt.PackRevisionOverride);
            }
            catch (Exception ex)
            {
                // A spoiler failure must never block shipping the playable pack.
                log("[spoiler] failed: " + ex.Message);
            }
        }

        Stage(BuildStage.Packing, 0.90, "Packing IoStore container…");
        log("Packing IoStore container…");
        var retoc = new Retoc();
        var packed = retoc.PackCombined(modDir, modName, locCulture, packDir, install: false);
        foreach (var f in packed) log("  " + f);

        Stage(BuildStage.Verifying, 0.94, "Verifying packed container…");
        if (opt.VerifyPack)
        {
            var utoc = packed.FirstOrDefault(f =>
                f.EndsWith(".utoc", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException(
                    $"Verification failed: {modName} did not produce a .utoc container.");
            retoc.Verify(utoc, log);
            if (!packed.Any(f => f.EndsWith(".ucas", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException(
                    $"Verification failed: {modName} did not produce a .ucas payload.");
        }

        // Restore the user's mods before installing so the new trio can be
        // committed and the old seed removed by the atomic installer.
        suspendedMods.Dispose();

        IReadOnlyList<string> installed = Array.Empty<string>();
        if (opt.InstallToMods)
        {
            Stage(BuildStage.Installing, 0.98, "Installing into ~mods…");
            installed = retoc.InstallPackAtomic(packed, EditorPaths.ModsDir, log);
            log($"Installed {modName} into {EditorPaths.ModsDir}");
        }

        if (compatUtoc is not null)
        {
            log("Patching Bigger Party Mod in place…");
            CompatibilityKit.Apply(compatUtoc, tweaks, log);
        }

        Stage(BuildStage.Done, 1.0, "Build complete.", honorCancellation: false);
        return new Result
        {
            Edits = total,
            Seed = rando.Seed,
            PackName = modName,
            PackDirectory = packDir,
            SpoilerPath = spoilerPath,
            PackedFiles = packed.ToList(),
            InstalledFiles = installed,
        };
    }

    /// <summary>
    /// Removes stale seed-suffixed trios from the install folder only. The seed
    /// library is intentionally left alone so previous seeds remain shareable.
    /// Two trios covering the same assets shadow each other by load order, so
    /// only the freshly packed seed may remain in ~mods.
    /// </summary>
    public static void CleanInstalledSeedPacks(string keepName, Action<string> log,
        string? installDir = null)
    {
        var dir = installDir ?? EditorPaths.ModsDir;
        if (!Directory.Exists(dir)) return;
        var keepLoc = "zz_" + keepName;
        foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
        {
            foreach (var pattern in new[] { ModBase + "*" + ModSuffix + ext, "zz_" + ModBase + "*" + ModSuffix + ext })
            {
                foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                {
                    var stem = Path.GetFileNameWithoutExtension(file);
                    if (string.Equals(stem, keepName, StringComparison.Ordinal)
                        || string.Equals(stem, keepLoc, StringComparison.Ordinal)) continue;
                    try { File.Delete(file); log("removed stale installed pack " + file); }
                    catch { /* best effort */ }
                }
            }
        }
    }

    /// <summary>
    /// Legacy cleanup used by the full editor: removes stale seed-suffixed
    /// trios (plus the legacy seedless <c>ExpeditionInfinity_P</c>) from both
    /// Export/ and ~mods. The standalone generator uses
    /// <see cref="CleanInstalledSeedPacks"/> instead so its seed library is
    /// preserved.
    /// </summary>
    public static void CleanOldSeedPacks(string keepName, Action<string> log)
    {
        foreach (var dir in new[] { EditorPaths.ExportDir, EditorPaths.ModsDir })
        {
            if (!Directory.Exists(dir)) continue;
            var keepLoc = "zz_" + keepName;
            foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
            {
                foreach (var pattern in new[] { ModBase + "*" + ModSuffix + ext, "zz_" + ModBase + "*" + ModSuffix + ext })
                {
                    foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                    {
                        var stem = Path.GetFileNameWithoutExtension(file);
                        if (string.Equals(stem, keepName, StringComparison.Ordinal)
                            || string.Equals(stem, keepLoc, StringComparison.Ordinal)) continue;
                        try { File.Delete(file); log("removed stale " + file); }
                        catch { /* best effort */ }
                    }
                }
            }
        }
    }

    private static LootCatalog BuildCatalog()
    {
        var catalog = new LootCatalog();
        var chestsPath = FileCopy.FindTablesAsset("DT_ChestsContent.uasset")
            ?? throw new FileNotFoundException("DT_ChestsContent.uasset");
        var enemiesPath = FileCopy.FindTablesAsset("DT_jRPG_Enemies.uasset")
            ?? throw new FileNotFoundException("DT_jRPG_Enemies.uasset");
        var merchants = new List<(string, JObject)>();
        var tablesRoot = Path.Combine(EditorPaths.RepoRoot, "work", "tables");
        if (Directory.Exists(tablesRoot))
        {
            foreach (var file in Directory.EnumerateFiles(tablesRoot, "DT_Merchant_*.uasset",
                         SearchOption.AllDirectories))
            {
                try { merchants.Add((file, JObject.Parse(new DataTableEditor(file).Dump()))); }
                catch { /* skip a broken extract */ }
            }
        }
        catalog.Rebuild(
            JObject.Parse(new DataTableEditor(chestsPath).Dump()),
            JObject.Parse(new DataTableEditor(enemiesPath).Dump()),
            merchants);
        return catalog;
    }

    private static void AssertRows(string modDir, string who, params string[] need)
    {
        var balPath = FileCopy.FindUnder(modDir, "DT_Balancing_MainCharacterSkills.uasset")
            ?? throw new FileNotFoundException("rebuilt mod balancing table missing");
        var balCheck = new DataTableEditor(balPath);
        foreach (var row in need)
            if (!balCheck.TryGetRow(row, out _))
                throw new InvalidDataException(
                    $"[pack] {who} custom row '{row}' missing after all kits — aborting, {ModBase}_<seed>{ModSuffix} NOT packed");
    }

    internal static void StripUnroundtrippable(string modDir, bool luneOn, Action<string> log)
    {
        int n = 0;
        void DelDir(string dirName)
        {
            if (!Directory.Exists(dirName)) return;
            n += Directory.GetFiles(dirName, "*", SearchOption.AllDirectories).Length;
            Directory.Delete(dirName, true);
            log("stripped " + dirName);
        }
        void DelStem(string stem)
        {
            foreach (var file in Directory.EnumerateFiles(modDir, stem + ".*", SearchOption.AllDirectories).ToList())
            {
                File.Delete(file);
                n++;
                log("stripped " + file);
            }
        }

        DelDir(Path.Combine(modDir, "Sandfall", "Content", "Gameplay", "EpicBattleSequence"));
        DelStem("FX_ElementalCharge");
        DelStem("BP_BattleBuff_Stance_Base");
        DelStem("BP_BattleBuff_Stance_Stanceless");
        DelStem("BP_BattleBuff_Stance_Defensive");
        DelStem("BP_BattleBuffInstance");
        DelStem("BP_BattleBuff_Berserk");
        if (!luneOn)
        {
            DelDir(Path.Combine(modDir, "Sandfall", "Content", "Gameplay", "Battle", "Skills", "Content", "Lune"));
            DelDir(Path.Combine(modDir, "Sandfall", "Content", "Gameplay", "SkillTree", "Content", "Lune"));
            DelStem("DA_SkillGraph_Lune");
        }
        if (n > 0) log("stripped " + n + " unroundtrippable/skipped file(s)");
    }
}
