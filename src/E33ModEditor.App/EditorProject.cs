using E33ModEditor.Core;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.App.ViewModels;

/// <summary>
/// One editor working set: pending table edits, created-pictos toggles,
/// and gameplay tweaks. Written next to a packed mod as a sidecar and
/// auto-saved under work/projects so sessions can be split across days.
/// </summary>
public sealed class EditorProject
{
    public const int Format = 1;

    public int FormatVersion { get; set; } = Format;
    public string SavedAt { get; set; } = "";
    public JObject GameplayTweaks { get; set; } = new();
    public JObject PictosCreate { get; set; } = new();
    public JObject Randomizer { get; set; } = new();
    public JArray Edits { get; set; } = new();
    /// <summary>Skill Overhaul toggle state (work/skill_overhaul.json mirror).</summary>
    public JObject SkillOverhaul { get; set; } = new();
    /// <summary>Skill Graph Editor layouts (work/skill_graph.json mirror).</summary>
    public JObject SkillGraph { get; set; } = new();
    /// <summary>Third-party mod compatibility (work/compatibility.json mirror).</summary>
    public JObject Compatibility { get; set; } = new();
    /// <summary>Camera submodule settings (work/firstperson.json mirror).</summary>
    public JObject Camera { get; set; } = new();
    /// <summary>Party of Five submodule settings (work/party_five.json mirror).</summary>
    public JObject PartyFive { get; set; } = new();
    /// <summary>Pack language: en (default), fr, pl or ru.</summary>
    public string Language { get; set; } = "en";

    public static string AutosavePath =>
        Path.Combine(EditorPaths.RepoRoot, "work", "projects", "autosave.project.json");

    public static string SidecarPath =>
        Path.Combine(EditorPaths.ExportDir, "ExpeditionInfinity_P.project.json");

    public static EditorProject FromJson(string json)
    {
        var o = JObject.Parse(json);
        return new EditorProject
        {
            FormatVersion = o.Value<int?>("format") ?? Format,
            SavedAt = o.Value<string>("savedAt") ?? "",
            GameplayTweaks = o["gameplayTweaks"] as JObject ?? new JObject(),
            PictosCreate = o["pictosCreate"] as JObject ?? new JObject(),
            Randomizer = o["randomizer"] as JObject ?? new JObject(),
            Edits = o["edits"] as JArray ?? new JArray(),
            SkillOverhaul = o["skillOverhaul"] as JObject ?? new JObject(),
            SkillGraph = o["skillGraph"] as JObject ?? new JObject(),
            Compatibility = o["compatibility"] as JObject ?? new JObject(),
            Camera = o["camera"] as JObject ?? new JObject(),
            PartyFive = o["partyFive"] as JObject ?? o["partyFour"] as JObject ?? new JObject(),
            Language = o.Value<string>("language") ?? "en",
        };
    }

    public string ToJson() =>
        new JObject
        {
            ["format"] = FormatVersion,
            ["savedAt"] = SavedAt,
            ["gameplayTweaks"] = GameplayTweaks,
            ["pictosCreate"] = PictosCreate,
            ["randomizer"] = Randomizer,
            ["edits"] = Edits,
            ["skillOverhaul"] = SkillOverhaul,
            ["skillGraph"] = SkillGraph,
            ["compatibility"] = Compatibility,
            ["camera"] = Camera,
            ["partyFive"] = PartyFive,
            ["language"] = Language,
        }.ToString(Newtonsoft.Json.Formatting.Indented);

    public void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ToJson());
    }

    public static EditorProject? TryRead(string path)
    {
        if (!File.Exists(path)) return null;
        try { return FromJson(File.ReadAllText(path)); }
        catch { return null; }
    }
}
