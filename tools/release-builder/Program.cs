using E33ModEditor.Core;
using E33ModEditor.Core.Localization;
using E33ModEditor.Core.Packer;
using E33Packer.App;

var repo = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var paks = Environment.GetEnvironmentVariable("E33_PAKS")
    ?? Path.Combine(repo, "work", "paks-placeholder");
var output = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(repo, "dist", "1.3.10"));
var seedRoot = Path.Combine(output, "seeds");
var langRoot = Path.Combine(output, "LanguagePacks");
Directory.CreateDirectory(seedRoot);
EditorPaths.Configure(repo, paks, Path.Combine(repo, "tools"), Path.Combine(repo, "work", "mods-release-install"));
DataTableEditor.ResetMappings();
ModLocalization.SetLanguage("en");

int NewSeed(HashSet<int> used)
{
    while (true)
    {
        var s = Random.Shared.Next(0, 99_999_999 + 1);
        if (used.Add(s)) return s;
    }
}

var used = new HashSet<int>();
var modes = new (string Name, string Preset)[]
{
    ("easy", GeneratorPresets.Easy),
    ("normal", GeneratorPresets.Expedition),
    ("hard", GeneratorPresets.Brutal),
};

var lines = new List<string>();
lines.Add($"Expedition Infinity {GeneratorInfo.AppVersion} — release build");
lines.Add($"Built: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
lines.Add($"Pack revision: {GeneratorInfo.PackRevision}");
lines.Add("Proc cards: LimitBattleProcCards=on, PictoPopupCap=0 (all seeds)");
lines.Add("Close camera: off (all seeds)");
lines.Add("Language packs: shared under LanguagePacks/<culture>/, installed alongside any seed");
lines.Add("");

foreach (var (name, preset) in modes)
{
    var seed = NewSeed(used);
    var profile = GeneratorPresets.Create(preset, seed);
    profile.QoL.PictoPopupCap = 0;
    profile.QoL.LimitBattleProcCards = true;
    var options = new FullPackKit.Options
    {
        Seed = seed,
        Tweaks = profile.Balance.ToTweaks(profile.QoL),
        Randomizer = profile.Randomizer,
        SkillOverhaul = profile.QoL.ToSkillOverhaul(),
        WeaponBulkUpgrade = profile.QoL.ToWeaponBulkUpgrade(),
        WeaponDropLevel = profile.Balance.ToWeaponDropLevel(),
        PartyFive = PartyFiveKit.Settings.Load(),
        FirstPerson = new FirstPersonCameraKit.Settings { Enabled = false },
        Language = "en",
        LanguagePackRoot = langRoot,
        PackRevisionOverride = GeneratorInfo.PackRevision,
        SeedLibraryRoot = seedRoot,
        InstallToMods = false,
        VerifyPack = true,
        IncludeSpoiler = true,
        ForceRandomizerOn = false,
        Log = m => Console.WriteLine($"[{name}] {m}"),
    };
    Console.WriteLine($"=== {name}: preset={preset} seed={seed} ===");
    var result = FullPackKit.RebuildAndPack(options);
    lines.Add($"{name}: preset={preset} seed={seed} pack={result.PackName}");
    lines.Add($"  dir: {result.PackDirectory}");
    lines.Add($"  languagePack: {(string.IsNullOrEmpty(result.LanguagePack) ? "(English)" : result.LanguagePack)}");
    lines.Add($"  edits: {result.Edits}");
    lines.Add("");
}

lines.Add("Shared language packs:");
foreach (var f in Directory.EnumerateFiles(langRoot, "*.pak", SearchOption.AllDirectories).OrderBy(x => x))
    lines.Add("  " + Path.GetRelativePath(output, f));

File.WriteAllText(Path.Combine(output, "BUILD.txt"), string.Join("\n", lines) + "\n");
Console.WriteLine("Wrote " + Path.Combine(output, "BUILD.txt"));
