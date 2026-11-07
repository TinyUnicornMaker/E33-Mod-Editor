using System.Text.Json;
using E33ModEditor.Core;
using E33ModEditor.Core.Loot;
using E33ModEditor.Core.Packer;
using E33Packer.App;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace E33Packer.Tests;

[TestClass]
public sealed class GeneratorPresetTests
{
    [TestMethod]
    public void DefaultProfile_UsesStrictLightStains()
    {
        var fresh = new GeneratorProfile();
        var preset = GeneratorPresets.Create(GeneratorPresets.Expedition, 123456);

        Assert.IsFalse(fresh.QoL.LightStainWildcard);
        Assert.IsFalse(preset.QoL.LightStainWildcard);
    }

    [TestMethod]
    public void ExpeditionPreset_MatchesCanonicalCoreValues()
    {
        var profile = GeneratorPresets.Create(GeneratorPresets.Expedition, 123456);
        var tweaks = profile.Balance.ToTweaks(profile.QoL);

        Assert.AreEqual(333, profile.Balance.LevelCap);
        Assert.AreEqual(333, profile.Balance.AttributeCap);
        Assert.AreEqual(333, tweaks.LevelCap);
        Assert.AreEqual(333, tweaks.AttributeCap);
        Assert.AreEqual(21, tweaks.MaxAP);
        Assert.AreEqual(3, tweaks.MaxConsecutiveTurns);
        Assert.AreEqual(30f, tweaks.MaxForetell);
        Assert.AreEqual(5, tweaks.MaxShields);
        Assert.AreEqual(10, tweaks.MaxGradientCharges);
        Assert.AreEqual(1.5f, tweaks.CritDamageIncrease);
        Assert.IsTrue(tweaks.CritRateDamageScaling);
        Assert.IsFalse(tweaks.Permadeath);
        Assert.IsFalse(tweaks.InstantPictoMastery);
        Assert.AreEqual(5, tweaks.PictoPopupCap);
        Assert.AreEqual("none", profile.QoL.PictoMastery);
        Assert.IsTrue(profile.Balance.BuffRegularEnemies);
        Assert.IsFalse(profile.Randomizer.BattleGroups);
        Assert.IsFalse(profile.Randomizer.RandomBattles);
        Assert.AreEqual(5, profile.Randomizer.MaxEnemiesPerBattle);
    }

    [TestMethod]
    public void Presets_ChangeOnlyTheirDocumentedKnobs()
    {
        var easy = GeneratorPresets.Create(GeneratorPresets.Easy);
        Assert.IsTrue(easy.Balance.BuffRegularEnemies);
        Assert.IsFalse(easy.Balance.TightenDefenseWindows);
        Assert.AreEqual(1.0, easy.Balance.HpMult);
        Assert.IsFalse(easy.Balance.Permadeath);
        Assert.IsFalse(easy.Balance.ReduceLuminaDrops);
        Assert.AreEqual(1.0, easy.Balance.LuminaDropMultiplier);
        Assert.AreEqual("instant", easy.QoL.PictoMastery);
        Assert.IsFalse(easy.QoL.LightStainWildcard);
        Assert.IsTrue(easy.Balance.ToTweaks(easy.QoL).DisableTutorials);
        for (int i = 0; i < 5; i++)
        {
            Assert.AreEqual(RegularEnemyBuffKit.DefaultHpBonus[i] * 0.75,
                easy.Balance.RegularEnemyHpBonus[i]);
            Assert.AreEqual(RegularEnemyBuffKit.DefaultSpeedBonus[i] * 0.75,
                easy.Balance.RegularEnemySpeedBonus[i]);
            Assert.AreEqual(RegularEnemyBuffKit.DefaultAttackBonus[i] * 0.75,
                easy.Balance.RegularEnemyAttackBonus[i]);
        }

        var brutal = GeneratorPresets.Create(GeneratorPresets.Brutal);
        Assert.IsTrue(brutal.Balance.BuffRegularEnemies);
        Assert.IsTrue(brutal.Balance.ReduceLuminaDrops);
        Assert.AreEqual(0.5, brutal.Balance.LuminaDropMultiplier);
        Assert.IsFalse(brutal.Balance.Permadeath);
        for (int i = 0; i < 5; i++)
        {
            Assert.AreEqual(RegularEnemyBuffKit.DefaultHpBonus[i] * 2.0,
                brutal.Balance.RegularEnemyHpBonus[i]);
            Assert.AreEqual(RegularEnemyBuffKit.DefaultSpeedBonus[i] * 1.5,
                brutal.Balance.RegularEnemySpeedBonus[i]);
            Assert.AreEqual(RegularEnemyBuffKit.DefaultAttackBonus[i] * 1.5,
                brutal.Balance.RegularEnemyAttackBonus[i]);
        }

        Assert.AreEqual("Easy", GeneratorPresets.Normalize("Gentler"));
        CollectionAssert.DoesNotContain(GeneratorPresets.Names, "Loot only");
        CollectionAssert.DoesNotContain(GeneratorPresets.Names, "Stats only");
    }

    [TestMethod]
    public void CustomPreset_KeepsCustomNameAndFormat()
    {
        var profile = GeneratorPresets.Create(GeneratorPresets.Custom, 42);
        Assert.AreEqual(GeneratorPresets.Custom, profile.Name);
        Assert.AreEqual(GeneratorProfile.CurrentFormat, profile.Format);
        Assert.AreEqual(42, profile.Randomizer.Seed);
    }

    [TestMethod]
    public void LuminaMultiplier_AllowsIncreasesAndDefaultsToOne()
    {
        var profile = GeneratorPresets.Create(GeneratorPresets.Custom, 7);
        Assert.AreEqual(1.0, profile.Balance.LuminaDropMultiplier);

        profile.Balance.ReduceLuminaDrops = true;
        profile.Balance.LuminaDropMultiplier = 2.0;
        var tweaks = profile.Balance.ToTweaks(profile.QoL);
        Assert.IsTrue(tweaks.ReduceLuminaDrops);
        Assert.AreEqual(2.0, tweaks.LuminaDropMultiplier);

        profile.Balance.LuminaDropMultiplier = 50;
        Assert.AreEqual(5.0, profile.Balance.ToTweaks(profile.QoL).LuminaDropMultiplier);
    }

    [TestMethod]
    public void NewProfile_UsesExpeditionCombatDefaults()
    {
        var profile = new GeneratorProfile();
        Assert.AreEqual(GeneratorPresets.Expedition, profile.Name);
        Assert.AreEqual(GeneratorProfile.CurrentFormat, profile.Format);
        Assert.IsTrue(profile.Balance.BuffRegularEnemies);
        Assert.AreEqual("none", profile.QoL.PictoMastery);
    }

    [TestMethod]
    public void Profile_RoundTripsThroughJson()
    {
        var original = GeneratorPresets.Create(GeneratorPresets.Brutal, 987654);
        original.QoL.LightStainWildcard = true;
        original.QoL.PictoPopupCap = 7;
        original.Randomizer.MaxEnemiesPerBattle = 8;

        var json = JsonSerializer.Serialize(original);
        var loaded = JsonSerializer.Deserialize<GeneratorProfile>(json);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(original.Name, loaded.Name);
        Assert.AreEqual(987654, loaded.Seed);
        Assert.AreEqual(original.Balance.LevelCap, loaded.Balance.LevelCap);
        Assert.AreEqual(original.Balance.AttributeCap, loaded.Balance.AttributeCap);
        Assert.AreEqual(original.QoL.PictoPopupCap, loaded.QoL.PictoPopupCap);
        Assert.IsTrue(loaded.QoL.LightStainWildcard);
        Assert.AreEqual(8, loaded.Randomizer.MaxEnemiesPerBattle);
    }
}

[TestClass]
public sealed class GeneratorOptionMappingTests
{
    [TestMethod]
    public void PackName_MatchesShippedV13Containers()
    {
        Assert.AreEqual("_v1.3.4", GeneratorInfo.PackRevision);
        Assert.AreEqual("ExpeditionInfinity_123456_v1.3.4_P", GeneratorInfo.PackName(123456));
        Assert.AreEqual("ExpeditionInfinity_7539815_v1.3.4_P", GeneratorInfo.PackName(7539815));
    }

    [TestMethod]
    public void SeedLibraryFolder_IsPerSeedAndKeepsLegacyFlatLayout()
    {
        var root = Path.Combine(Path.GetTempPath(), "e33-seed-library-test");
        Assert.AreEqual(Path.Combine(Path.GetFullPath(root), "ExpeditionInfinity_1_v1.3.4_P"),
            FullPackKit.SeedLibraryPackDir(root, "ExpeditionInfinity_1_v1.3.4_P"));
        Assert.AreEqual(EditorPaths.ExportDir,
            FullPackKit.SeedLibraryPackDir(null, "ExpeditionInfinity_1_v1.3.4_P"));
    }

    [TestMethod]
    public void Mayhem_DoesNotAcceptLightStainSubstitution()
    {
        var mayhem = LuneSkillsKit.Skills.Single(s => s.DonorStem == "Mayhem");
        Assert.IsNotNull(mayhem.Kismet);
        Assert.IsTrue(mayhem.Kismet!.NoLightSubstitution);
    }

    [TestMethod]
    public void InstantPictoMastery_IsToggleable()
    {
        var profile = GeneratorPresets.Create(GeneratorPresets.Expedition);
        Assert.AreEqual("none", profile.QoL.PictoMastery);

        profile.QoL.PictoMastery = "none";
        var off = profile.Balance.ToTweaks(profile.QoL);
        Assert.IsFalse(off.InstantPictoMastery);
        Assert.IsFalse(off.AutoPictoMastery);

        profile.QoL.PictoMastery = "instant";
        var on = profile.Balance.ToTweaks(profile.QoL);
        Assert.IsTrue(on.InstantPictoMastery);
        Assert.IsFalse(on.AutoPictoMastery);
    }

    [TestMethod]
    public void QoL_MapsToSkillOverhaulAndBulkUpgrade()
    {
        var qol = new QoLOptions
        {
            LightStainWildcard = true,
            BulkUpgradeEnabled = false,
            BulkUpgradeLevels = 99,
            BulkTapPlusOne = true,
            PictoPopupCap = 999,
        };

        Assert.IsFalse(qol.ToSkillOverhaul().LightStainWildcard);
        var bulk = qol.ToWeaponBulkUpgrade();
        Assert.IsFalse(bulk.Enabled);
        Assert.AreEqual(WeaponBulkUpgradeKit.BulkLevels, bulk.Levels);
        Assert.IsFalse(bulk.TapPlusOne);
    }

    [TestMethod]
    public void WeaponDropLevel_ClampsToSupportedRange()
    {
        var balance = new BalanceOptions { MaxDropLevel = 999 };
        Assert.AreEqual(333, balance.ToWeaponDropLevel().MaxDropLevel);

        balance.MaxDropLevel = 1;
        Assert.AreEqual(33, balance.ToWeaponDropLevel().MaxDropLevel);
    }

    [TestMethod]
    public void RandomizerDefaults_AreStableForV13()
    {
        var settings = new RandomizerKit.Settings();
        Assert.IsFalse(settings.BattleGroups);
        Assert.IsFalse(settings.RandomBattles);
        Assert.AreEqual(5, settings.MaxEnemiesPerBattle);
        Assert.AreEqual("", settings.BattleGroupsAreaPrefix);
    }

    [TestMethod]
    public void ExtremeBossDefense_RefitsTheT4T5Curve()
    {
        var regularFloor = EnemyDefenseKit.MaxDefense(EnemyDefenseKit.CurveStartLevel);
        var regularTop = EnemyDefenseKit.MaxDefense(EnemyDefenseKit.CurveTopLevel);

        Assert.AreEqual(1.0, EnemyDefenseKit.ExtremeDefenseScale(60), 1e-9);
        Assert.AreEqual(30.0, EnemyDefenseKit.ExtremeDefenseScale(99), 1e-9);
        Assert.AreEqual(regularFloor,
            EnemyDefenseKit.MaxDefense(EnemyDefenseKit.CurveStartLevel, extreme: true), 0.001);
        Assert.AreEqual(regularTop * 30.0,
            EnemyDefenseKit.MaxDefense(EnemyDefenseKit.CurveTopLevel, extreme: true), 0.1);

        var mid = EnemyDefenseKit.ExtremeDefenseScale(75);
        Assert.IsTrue(mid > 1.0 && mid < 30.0);
        Assert.IsTrue(mid < EnemyDefenseKit.ExtremeDefenseScale(90));
        Assert.IsFalse(new RandomizerKit.Settings().ExtremeBossDefense);
    }

    [TestMethod]
    public void GameLocator_AcceptsRootAndPaksPaths()
    {
        var root = Path.Combine(AppContext.BaseDirectory,
            "e33-locator-" + Guid.NewGuid().ToString("N"));
        var paks = Path.Combine(root, "Sandfall", "Content", "Paks");
        Directory.CreateDirectory(paks);
        File.WriteAllText(Path.Combine(paks, "pakchunk0-Windows.utoc"), "stub");
        try
        {
            Assert.AreEqual(root, GameLocator.NormalizeGameRoot(root));
            Assert.AreEqual(root, GameLocator.NormalizeGameRoot(paks));
            Assert.AreEqual(paks, GameLocator.PaksDirOf(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void MakeExecutable_RepairsBundledToolPermission()
    {
        if (OperatingSystem.IsWindows()) return;

        var path = Path.Combine(Path.GetTempPath(),
            "e33-tool-permission-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(path, "#!/bin/sh\nexit 0\n");
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);

            EditorPaths.MakeExecutable(path);

            var mode = File.GetUnixFileMode(path);
            Assert.IsTrue(mode.HasFlag(UnixFileMode.UserExecute));
            Assert.IsTrue(mode.HasFlag(UnixFileMode.GroupExecute));
            Assert.IsTrue(mode.HasFlag(UnixFileMode.OtherExecute));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void DonorExtraction_UsesFriendlyDescriptions()
    {
        Assert.AreEqual("enemy data",
            DonorExtract.DescribeFilter("DT_jRPG_Enemies_"));
        Assert.AreEqual("weapon and gear data",
            DonorExtract.DescribeFilter("DT_WeaponDefinitions"));
        Assert.AreEqual("skill data",
            DonorExtract.DescribeFilter("BP_Battle_SkillScript_"));
        Assert.AreEqual("loot and merchant data",
            DonorExtract.DescribeFilter("DT_ChestsContent"));
        Assert.AreEqual("text and localization",
            DonorExtract.DescribeFilter("ST_Enemies_Name"));
        Assert.AreEqual("checking the game data", DonorExtract.DescribeFilter(""));
    }

    [TestMethod]
    public void Build_ReportsStagesAndStopsAtCancellationBoundary()
    {
        var seen = new List<BuildStage>();
        using var cts = new CancellationTokenSource();
        var options = new FullPackKit.Options
        {
            CancellationToken = cts.Token,
            OnStage = (stage, progress, _) =>
            {
                seen.Add(stage);
                Assert.IsTrue(progress >= 0 && progress <= 1);
                cts.Cancel();
            },
        };

        var cancelled = false;
        try
        {
            FullPackKit.RebuildAndPack(options);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Assert.IsTrue(cancelled);
        Assert.AreEqual(1, seen.Count);
        Assert.AreEqual(BuildStage.Validating, seen[0]);
    }

    [TestMethod]
    public void InstallCleanup_LeavesSeedLibraryUntouched()
    {
        var root = Path.Combine(Path.GetTempPath(), "e33-generator-test-" + Guid.NewGuid().ToString("N"));
        var install = Path.Combine(root, "mods");
        var library = Path.Combine(root, "library");
        Directory.CreateDirectory(install);
        Directory.CreateDirectory(library);
        try
        {
            var keep = "ExpeditionInfinity_123_v1.3.4_P";
            var stale = "ExpeditionInfinity_456_v1.3.4_P";
            foreach (var name in new[] { keep, stale })
            {
                foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
                {
                    File.WriteAllText(Path.Combine(install, name + ext), "installed");
                    File.WriteAllText(Path.Combine(library, name + ext), "library");
                }
                File.WriteAllText(Path.Combine(install, "zz_" + name + ".pak"), "installed");
                File.WriteAllText(Path.Combine(library, "zz_" + name + ".pak"), "library");
            }

            FullPackKit.CleanInstalledSeedPacks(keep, _ => { }, install);

            foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
            {
                Assert.IsTrue(File.Exists(Path.Combine(install, keep + ext)));
                Assert.IsFalse(File.Exists(Path.Combine(install, stale + ext)));
                Assert.IsTrue(File.Exists(Path.Combine(library, keep + ext)));
                Assert.IsTrue(File.Exists(Path.Combine(library, stale + ext)));
            }
            Assert.IsTrue(File.Exists(Path.Combine(install, "zz_" + keep + ".pak")));
            Assert.IsFalse(File.Exists(Path.Combine(install, "zz_" + stale + ".pak")));
            Assert.IsTrue(File.Exists(Path.Combine(library, "zz_" + keep + ".pak")));
            Assert.IsTrue(File.Exists(Path.Combine(library, "zz_" + stale + ".pak")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void RecoverSuspendedMods_RestoresAsideFolder()
    {
        var root = Path.Combine(AppContext.BaseDirectory,
            "e33-aside-" + Guid.NewGuid().ToString("N"));
        var mods = Path.Combine(root, "mods");
        var aside = mods + ".packer-aside";
        Directory.CreateDirectory(aside);
        File.WriteAllText(Path.Combine(aside, "old.pak"), "old");
        try
        {
            DonorExtract.RecoverSuspendedMods(mods);
            Assert.IsTrue(File.Exists(Path.Combine(mods, "old.pak")));
            Assert.IsFalse(Directory.Exists(aside));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Tutorials_AreAlwaysSkipped()
    {
        var tweaks = new BalanceOptions().ToTweaks(new QoLOptions { SkipTutorials = false });
        Assert.IsTrue(tweaks.DisableTutorials);
    }

    [TestMethod]
    public void ToTweaks_ClampsDangerousCustomValues()
    {
        var balance = new BalanceOptions
        {
            LevelCap = 1,
            HpMult = 0,
            RegularEnemyHpBonus = new[] { -5.0, -5.0, -5.0, -5.0, -5.0 },
        };
        var tweaks = balance.ToTweaks(new QoLOptions());
        Assert.AreEqual(99, tweaks.LevelCap);
        Assert.AreEqual(0.1, tweaks.HpMult);
        foreach (var v in tweaks.RegularEnemyHpBonus)
            Assert.AreEqual(-0.95, v);
    }

    [TestMethod]
    public void InstallPackAtomic_ReplacesOldTrioAndRemovesStaleExtensions()
    {
        var root = Path.Combine(AppContext.BaseDirectory,
            "e33-atomic-install-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "mods");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        try
        {
            foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
                File.WriteAllText(Path.Combine(target, "ExpeditionInfinity_111_v1.3.4_P" + ext), "old");

            var files = new List<string>();
            foreach (var ext in new[] { ".utoc", ".ucas" })
            {
                var path = Path.Combine(source, "ExpeditionInfinity_222_v1.3.4_P" + ext);
                File.WriteAllText(path, "new");
                files.Add(path);
            }

            var installed = new Retoc().InstallPackAtomic(files, target, _ => { });

            Assert.AreEqual(2, installed.Count);
            Assert.IsFalse(File.Exists(Path.Combine(target, "ExpeditionInfinity_111_v1.3.4_P.pak")));
            Assert.IsTrue(File.Exists(Path.Combine(target, "ExpeditionInfinity_222_v1.3.4_P.utoc")));
            Assert.IsTrue(File.Exists(Path.Combine(target, "ExpeditionInfinity_222_v1.3.4_P.ucas")));
            Assert.AreEqual(0, Directory.EnumerateDirectories(target, ".install-backup-*").Count());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void InstallPackAtomic_RestoresOldTrioWhenCommitFails()
    {
        var root = Path.Combine(AppContext.BaseDirectory,
            "e33-atomic-rollback-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "mods");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        try
        {
            foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
                File.WriteAllText(Path.Combine(target, "ExpeditionInfinity_111_v1.3.4_P" + ext), "old");

            var files = new List<string>();
            foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
            {
                var path = Path.Combine(source, "ExpeditionInfinity_222_v1.3.4_P" + ext);
                File.WriteAllText(path, "new");
                files.Add(path);
            }

            // Force the commit to fail after the old trio is moved aside.
            Directory.CreateDirectory(Path.Combine(target,
                "ExpeditionInfinity_222_v1.3.4_P.utoc"));

            var failed = false;
            try { new Retoc().InstallPackAtomic(files, target, _ => { }); }
            catch (Exception) { failed = true; }

            Assert.IsTrue(failed);
            foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
            {
                var old = Path.Combine(target, "ExpeditionInfinity_111_v1.3.4_P" + ext);
                Assert.IsTrue(File.Exists(old));
                Assert.AreEqual("old", File.ReadAllText(old));
            }
            Assert.AreEqual(0, Directory.EnumerateDirectories(target, ".install-backup-*").Count());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void InstallPack_StagesFilesInInstallFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "e33-install-test-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "mods");
        Directory.CreateDirectory(source);
        try
        {
            var files = new List<string>();
            foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
            {
                var path = Path.Combine(source, "ExpeditionInfinity_1_v1.3.4_P" + ext);
                File.WriteAllText(path, ext);
                files.Add(path);
            }

            var committed = false;
            var installed = new Retoc().InstallPack(files, target, () =>
            {
                // Staging happens before the live names are touched.
                Assert.AreEqual(0, Directory.EnumerateFiles(target, "*.utoc").Count());
                committed = true;
            });

            Assert.IsTrue(committed);
            Assert.AreEqual(3, installed.Count);
            foreach (var file in files)
            {
                var dest = Path.Combine(target, Path.GetFileName(file));
                Assert.IsTrue(File.Exists(dest));
                Assert.AreEqual(Path.GetExtension(file), File.ReadAllText(dest));
            }
            Assert.AreEqual(0,
                Directory.EnumerateFiles(target, "*.installing").Count());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
