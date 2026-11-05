using System.Globalization;
using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Skill Graph Editor backing kit: reads a character's DA_SkillGraph_*.uasset
/// (nodes with Position2D + SkillUnlock, edges as prerequisite→child imports),
/// persists node positions and link sets as JSON, and rewrites the graph's
/// node positions / edge array on pack. The node set is never
/// added to or removed from; layouts only re-map existing skill stems.
/// </summary>
public static class SkillGraphKit
{
    /// <summary>
    /// The playable characters in party join (discovery) order, each mapped to
    /// the graph asset folder + stem. Gustave's internal name is Noah; his tree
    /// is the shared NoahVerso DA_SkillGraph_Noah. NoahVerso holds the Noah and
    /// Verso graphs; the extra DA_SkillGraph_Empty is not a playable tree.
    /// Julie is cut content (unused character) and has no tab.
    /// </summary>
    public static readonly (string Id, string Folder, string GraphStem)[] Characters =
    {
        ("Gustave", "NoahVerso", "DA_SkillGraph_Noah"),
        ("Lune", "Lune", "DA_SkillGraph_Lune"),
        ("Maelle", "Maelle", "DA_SkillGraph_Maelle"),
        ("Sciel", "Sciel", "DA_SkillGraph_Sciel"),
        ("Verso", "NoahVerso", "DA_SkillGraph_Verso"),
        ("Monoco", "Monoco", "DA_SkillGraph_Monoco"),
    };

    public static string SettingsPath => Path.Combine(EditorPaths.RepoRoot, "work", "skill_graph.json");

    // ───────────────────────── model ─────────────────────────

    public sealed class GraphNode
    {
        /// <summary>Graph import name, e.g. DA_Skill_Lune_Immolation.</summary>
        public string Skill { get; set; } = "";
        public int UnlockCost { get; set; }
        /// <summary>Cost the editor shows and the writer syncs the node to: the DA
        /// APCost (the value Skill Overhaul / the skill editors tune) when known,
        /// else the authored kit cost, else the file's UnlockCost.</summary>
        public int DisplayCost { get; set; }
        public bool IsStartingSkill { get; set; }
        public bool IsSecret { get; set; }
        /// <summary>RequiresUnlockItem RowName, e.g. GradientUnlock_* for the Lune secret nodes.</summary>
        public string RequiresUnlockItem { get; set; } = "";
        /// <summary>Authored display name (kit-created nodes; empty for vanilla nodes whose
        /// names resolve from the string table via the skill-DA rows).</summary>
        public string DisplayName { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public string Stem => Skill.StartsWith("DA_Skill_", StringComparison.Ordinal)
            ? Skill["DA_Skill_".Length..]
            : Skill;
    }

    public sealed class GraphData
    {
        public string SourcePath { get; set; } = "";
        public List<GraphNode> Nodes { get; } = new();
        /// <summary>Index pairs into Nodes: From = prerequisite, To = child.</summary>
        public List<(int From, int To)> Links { get; } = new();
    }

    /// <summary>One arrangement of a character's tree — positions + links keyed by DA stem.</summary>
    public sealed class GraphLayout
    {
        public List<(string Skill, double X, double Y)> Positions { get; } = new();
        public List<(string From, string To)> Links { get; } = new();

        public JObject ToJson()
        {
            var pos = new JArray();
            foreach (var (skill, x, y) in Positions)
                pos.Add(new JObject { ["s"] = skill, ["x"] = x, ["y"] = y });
            var links = new JArray();
            foreach (var (from, to) in Links)
                links.Add(new JObject { ["f"] = from, ["t"] = to });
            return new JObject { ["positions"] = pos, ["links"] = links };
        }

        public static GraphLayout FromJson(JToken? o)
        {
            var layout = new GraphLayout();
            if (o is not JObject jobj) return layout;
            foreach (var p in jobj["positions"] as JArray ?? new JArray())
            {
                if (p is not JObject po) continue;
                var skill = po.Value<string>("s") ?? "";
                if (skill.Length == 0) continue;
                var x = po.Value<double?>("x") ?? 0;
                var y = po.Value<double?>("y") ?? 0;
                layout.Positions.Add((skill, x, y));
            }
            foreach (var l in jobj["links"] as JArray ?? new JArray())
            {
                if (l is not JObject lo) continue;
                var from = lo.Value<string>("f") ?? "";
                var to = lo.Value<string>("t") ?? "";
                if (from.Length == 0 || to.Length == 0) continue;
                layout.Links.Add((from, to));
            }
            return layout;
        }
    }

    /// <summary>
    /// Per-character graph editor state. shape:
    /// "vanilla" (default / not packed), "custom" (current live arrangement),
    /// or a saved preset name. A custom layout (auto-saved on every edit)
    /// captures the working tree; presets are named arrangements.
    /// </summary>
    public sealed class Settings
    {
        public sealed class CharacterState
        {
            /// <summary>Currently *viewed* shape ("vanilla", "custom", or a preset name).</summary>
            public string Shape { get; set; } = "vanilla";
            /// <summary>The shape the pack applies ("vanilla", "custom", or a preset name).
            /// Distinct from <see cref="Shape"/> so you can browse without changing what ships.</summary>
            public string Active { get; set; } = "vanilla";
            public GraphLayout Custom { get; set; } = new();
            public Dictionary<string, GraphLayout> Presets { get; } = new(StringComparer.Ordinal);

            /// <summary>The layout identity the pack should apply (the Active slot).</summary>
            public string PackShape() => Active;
        }

        private readonly Dictionary<string, CharacterState> _byId = new(StringComparer.Ordinal);

        public CharacterState For(string id) =>
            _byId.TryGetValue(id, out var s) ? s : _byId[id] = new CharacterState();

        /// <summary>Any character deviating from vanilla must be asserted on pack.</summary>
        public bool IsActive => _byId.Values.Any(s => s.PackShape() != "vanilla" && !string.IsNullOrEmpty(s.PackShape()));

        /// <summary>Any recorded state at all (used to decide whether a save is needed).</summary>
        public bool HasRecorded => _byId.Count > 0 && _byId.Values.Any(s => s.Shape != "vanilla"
            || s.Active != "vanilla"
            || s.Custom.Positions.Count > 0 || s.Custom.Links.Count > 0 || s.Presets.Count > 0);

        public JObject ToJson()
        {
            var chars = new JObject();
            foreach (var (id, s) in _byId.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var presets = new JObject();
                foreach (var (name, layout) in s.Presets)
                    presets[name] = layout.ToJson();
                chars[id] = new JObject
                {
                    ["shape"] = s.Shape,
                    ["active"] = s.Active,
                    ["custom"] = s.Custom.ToJson(),
                    ["presets"] = presets,
                };
            }
            return chars;
        }

        public void FromJson(JObject? o)
        {
            _byId.Clear();
            if (o is null) return;
            foreach (var p in o.Properties())
            {
                if (p.Value is not JObject state) continue;
                var s = new CharacterState
                {
                    Shape = state.Value<string>("shape") ?? "vanilla",
                    // Older files predate the Active slot; default Active to whatever
                    // was viewed (the old pack behaviour) so nothing silently stops packing.
                    Active = state.Value<string>("active") ?? state.Value<string>("shape") ?? "vanilla",
                    Custom = GraphLayout.FromJson(state["custom"]),
                };
                if (state["presets"] is JObject presets)
                    foreach (var pr in presets.Properties())
                        s.Presets[pr.Name] = GraphLayout.FromJson(pr.Value);
                _byId[p.Name] = s;
            }
        }
    }

    public static Settings LoadSettings()
    {
        var s = new Settings();
        try
        {
            if (File.Exists(SettingsPath))
                s.FromJson(JObject.Parse(File.ReadAllText(SettingsPath)));
        }
        catch { /* optional */ }
        return s;
    }

    // ───────────────────────── editor baseline (union tree) ─────────────────────────

    /// <summary>One mod-added skill node the Graph Editor should show by default.</summary>
    public sealed record AuthoredSkill(
        string DaStem,          // full DA stem, e.g. DA_Skill_Lune_Ultima
        string? PrereqDaStem,   // DA stem of the node it chains from (already in the tree);
                                // NULL = a floating node with no prerequisite edge
        double X, double Y,     // authored graph position
        string DisplayName,     // display label (also the fallback when no DA is found)
        int ApCost);            // node unlock AP (mirrors the DA APCost when known)

    /// <summary>
    /// Kit-created skills that append graph nodes on pack, keyed by the graph
    /// stem they land on. Lune's come from LuneSkillsKit.Skills (19) +
    /// LuneApocalypseKit.Moves (4); Gustave's from GustaveSkillsKit.NewMoves
    /// (4, on the Noah graph); Maelle's eight new moves land on DA_SkillGraph_Maelle
    /// as FLOATING nodes (null prereq — the user connects them later in the editor).
    /// Only entries whose DA is NOT already a vanilla graph node append on pack —
    /// the same predicate the kits use.
    /// </summary>
    public static IReadOnlyDictionary<string, List<AuthoredSkill>> AuthoredAddsByGraph() =>
        new Dictionary<string, List<AuthoredSkill>>(StringComparer.Ordinal)
        {
            ["DA_SkillGraph_Lune"] = AuthoredLuneAdds(),
            ["DA_SkillGraph_Noah"] = AuthoredNoahAdds(),
            ["DA_SkillGraph_Maelle"] = AuthoredMaelleAdds(),
            ["DA_SkillGraph_Sciel"] = AuthoredScielAdds(),
        };

    private static List<AuthoredSkill> AuthoredScielAdds()
    {
        var list = new List<AuthoredSkill>();
        foreach (var (daStem, prereq, x, y, name, ap) in ScielCustomKit.AuthoredAdds())
            list.Add(new AuthoredSkill(daStem, prereq, x, y, name, ap));
        return list;
    }

    private static List<AuthoredSkill> AuthoredMaelleAdds()
    {
        var list = new List<AuthoredSkill>();
        foreach (var m in MaelleSkillsKit.NewMoves)
        {
            list.Add(new AuthoredSkill(
                m.DaStem,
                null, // floating — no prerequisite edge (user connects later)
                m.Position?.X ?? 125, m.Position?.Y ?? 1225,
                m.DisplayName,
                m.ApCost));
        }
        return list;
    }

    private static List<AuthoredSkill> AuthoredLuneAdds()
    {
        var list = new List<AuthoredSkill>();
        foreach (var s in LuneSkillsKit.Skills)
        {
            list.Add(new AuthoredSkill(
                s.DaStem,
                "DA_Skill_Lune_" + s.PrereqNameId,
                s.Position.X, s.Position.Y,
                s.DisplayName,
                s.ApCost));
        }
        foreach (var m in LuneApocalypseKit.Moves)
        {
            list.Add(new AuthoredSkill(
                m.DaStem,
                "DA_Skill_Lune_" + m.PrereqNameId,
                m.Position.X, m.Position.Y,
                m.DisplayName,
                m.ApCost));
        }
        return list;
    }

    private static List<AuthoredSkill> AuthoredNoahAdds()
    {
        var list = new List<AuthoredSkill>();
        foreach (var m in GustaveSkillsKit.NewMoves)
        {
            list.Add(new AuthoredSkill(
                m.DaStem,
                m.PrereqNode,
                m.Position.X, m.Position.Y,
                m.DisplayName,
                m.ApCost));
        }
        return list;
    }

    /// <summary>
    /// Loads the graph the editor should show: the union of vanilla nodes and
    /// every kit-created skill that appends on pack, at authored positions and
    /// with the authored prerequisite links. This is the "normal tree" baseline
    /// regardless of playtest grid-mode or pack state. The mod-staged graph is
    /// preferred when it is not the Lune grid artifact (Gustave's tree removes
    /// hidden nodes, which only the mod graph reflects).
    /// </summary>
    public static GraphData LoadEditorBaseline(string graphStem)
    {
        GraphData data;
        // Gustave: the mod graph is authoritative (vanilla minus hidden nodes plus
        // the 4 new moves) whenever a normal pack staged it. Verso likewise
        // (node0 Combo1 is replaced by the Assault Zero custom on pack).
        if (graphStem is "DA_SkillGraph_Noah" or "DA_SkillGraph_Verso")
        {
            var modNoah = FindModGraph(graphStem);
            if (modNoah is not null)
            {
                var g = Load(modNoah);
                if (g.Nodes.Count > 0)
                {
                    data = g;
                    goto applyCosts;
                }
            }
        }

        data = Load(FindCharacterGraphVanilla(graphStem)
            ?? throw new FileNotFoundException($"{graphStem} not found under work/tables/graphs"));
        var have = new HashSet<string>(data.Nodes.Select(n => n.Skill), StringComparer.Ordinal);
        var indexBySkill = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < data.Nodes.Count; i++) indexBySkill[data.Nodes[i].Skill] = i;

        if (AuthoredAddsByGraph().TryGetValue(graphStem, out var adds))
        {
            foreach (var add in adds)
            {
                if (have.Contains(add.DaStem)) continue;
                // A null prereq = floating node (no link). Otherwise the prereq must
                // already be in the tree or the authored add is skipped (never dangle).
                if (add.PrereqDaStem is { } prereq && !indexBySkill.TryGetValue(prereq, out _)) continue;
                var node = new GraphNode
                {
                    Skill = add.DaStem,
                    UnlockCost = add.ApCost,
                    X = add.X,
                    Y = add.Y,
                    DisplayName = add.DisplayName, // authored name — the string table may not hold it yet
                };
                var idx = data.Nodes.Count;
                data.Nodes.Add(node);
                indexBySkill[add.DaStem] = idx;
                if (add.PrereqDaStem is { } prereq2)
                    data.Links.Add((indexBySkill[prereq2], idx));
            }
        }

    applyCosts:
        // The DA APCost is the tuned cast cost (Skill Overhaul / skill editors);
        // the graph node UnlockCost lags it. Show + sync the DA cost when known.
        foreach (var node in data.Nodes)
        {
            var da = DaApCost(node.Skill);
            node.DisplayCost = da > 0 ? da : node.UnlockCost;
        }
        return data;
    }

    private static string? FindModGraph(string graphStem) =>
        FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "mod"), graphStem + ".uasset");

    /// <summary>Display info for a skill DA, read from the staged mod copy when
    /// present (kit-created DAs only exist there), else the vanilla extract.</summary>
    public static (string Element, string SkillType, string NameKey, int ApCost) ReadDaDisplayInfo(string daStem)
    {
        var file = FindSkillDa(daStem);
        if (file is null) return ("", "", "", 0);
        try
        {
            var leaves = DataTableEditor.OpenPropertyAsset(file).FlattenProperties();
            string Get(string path)
            {
                var hit = leaves.FirstOrDefault(l => l.Path == path);
                return hit?.Value?.ToString() ?? "";
            }
            var cost = 0;
            int.TryParse(Get("APCost"), out cost);
            return (Get("BaseElementalType"), Get("SkillType"), Get("Name"), cost);
        }
        catch
        {
            return ("", "", "", 0);
        }
    }

    private static string? FindSkillDa(string daStem) =>
        FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "mod"), daStem + ".uasset")
        ?? FileCopy.FindTablesAsset(daStem + ".uasset");

    /// <summary>Elemental colour overrides for moves whose element is not on the DA
    /// (kismet / description-driven). "Multi" = mixed-element (Genesis family);
    /// "Weapon" = follows the equipped weapon's element.</summary>
    public static string? ElementOverride(string daStem) => daStem switch
    {
        "DA_Skill_Lune_BlitzOvercharge" => "Lightning",
        "DA_Skill_Lune_ElementalGenesis" => "Multi",
        "DA_Skill_Lune_ElementalCataclysm" => "Multi",
        "DA_Skill_Lune_Mayhem" => "Lightning",   // consumes Lightning; desc says Lightning
        "DA_Skill_Gustave_HellStorm" => "Weapon",
        "DA_Skill_Gustave_CinderSwarm" => "Weapon",
        _ => null,
    };

    /// <summary>Reads a skill DA's APCost once and caches it (DA APCost is the value
    /// the Skill Overhaul module and the skill editors tune).</summary>
    private static readonly Dictionary<string, int> _daApCostCache = new(StringComparer.Ordinal);

    public static int DaApCost(string daStem)
    {
        if (_daApCostCache.TryGetValue(daStem, out var hit)) return hit;
        var cost = ReadDaDisplayInfo(daStem).ApCost;
        _daApCostCache[daStem] = cost;
        return cost;
    }

    /// <summary>Drops the DA APCost cache so the next read re-reads the DAs (call
    /// when the module opens, in case the user rebalanced costs this session).</summary>
    public static void ResetDaApCostCache() => _daApCostCache.Clear();

    // ───────────────────────── graph reader ─────────────────────────

    public static string? FindCharacterGraphVanilla(string graphStem)
    {
        var root = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "graphs");
        return FileCopy.FindUnder(root, graphStem + ".uasset");
    }

    /// <summary>Opens a DA_SkillGraph_*.uasset and reads its nodes + links.</summary>
    public static GraphData Load(string graphPath)
    {
        var data = new GraphData { SourcePath = graphPath };
        var map = DataTableEditor.Mappings;
        var g = map != null
            ? new UAsset(graphPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(graphPath, EngineVersion.VER_UE5_4);
        var gx = g.Exports.OfType<NormalExport>().First();
        var nodes = (ArrayPropertyData)gx.Data[0];
        var edges = (ArrayPropertyData)gx.Data[1];

        var importNameByIndex = new Dictionary<int, string>();
        for (int i = 0; i < nodes.Value.Length; i++)
        {
            var nodeStruct = nodes.Value[i] as StructPropertyData;
            var su = nodeStruct?.Value.Count > 0 ? nodeStruct.Value[0] as StructPropertyData : null;
            var skillPtr = su?.Value.Count > 0 ? (su.Value[0] as ObjectPropertyData)?.Value : null;
            var impName = skillPtr is not null && !skillPtr.IsNull() && skillPtr.IsImport()
                && Math.Abs(skillPtr.Index) - 1 < g.Imports.Count
                ? g.Imports[Math.Abs(skillPtr.Index) - 1].ObjectName.ToString()
                : null;
            if (skillPtr is not null && !skillPtr.IsNull() && skillPtr.IsImport() && impName is not null)
                importNameByIndex[Math.Abs(skillPtr.Index) - 1] = impName;

            var node = new GraphNode
            {
                Skill = impName ?? "",
                UnlockCost = su is { Value.Count: > 1 } && su.Value[1] is IntPropertyData cost ? cost.Value : 0,
                IsStartingSkill = su is { Value.Count: > 2 } && su.Value[2] is BoolPropertyData start && start.Value,
                IsSecret = su is { Value.Count: > 4 } && su.Value[4] is BoolPropertyData secret && secret.Value,
                X = 0,
                Y = 0,
            };
            if (su is { Value.Count: > 3 } && su.Value[3] is StructPropertyData reqWrap &&
                reqWrap.Value.Count > 1 && reqWrap.Value[1] is NamePropertyData rn && rn.Value is { } rnVal)
                node.RequiresUnlockItem = rnVal.ToString() ?? "";
            if (nodeStruct is { Value.Count: > 1 } && nodeStruct.Value[1] is StructPropertyData posWrap &&
                posWrap.Value.Count > 0 && posWrap.Value[0] is Vector2DPropertyData pos)
            {
                node.X = pos.Value.X;
                node.Y = pos.Value.Y;
            }
            data.Nodes.Add(node);
        }

        for (int i = 0; i < edges.Value.Length; i++)
        {
            if (edges.Value[i] is not StructPropertyData edge || edge.Value.Count < 2) continue;
            var fromPtr = (edge.Value[0] as ObjectPropertyData)?.Value;
            var toPtr = (edge.Value[1] as ObjectPropertyData)?.Value;
            int IdxOf(FPackageIndex? ptr)
            {
                if (ptr is null || ptr.IsNull() || !ptr.IsImport()) return -1;
                var idx = Math.Abs(ptr.Index) - 1;
                if (!importNameByIndex.TryGetValue(idx, out var name)) return -1;
                for (int n = 0; n < data.Nodes.Count; n++)
                    if (data.Nodes[n].Skill == name) return n;
                return -1;
            }
            var from = IdxOf(fromPtr);
            var to = IdxOf(toPtr);
            if (from >= 0 && to >= 0 && from != to)
                data.Links.Add((from, to));
        }
        return data;
    }

    // ───────────────────────── uniform graph lattice ─────────────────────────
    // Every character's tree uses the same in-game grid: columns every 125
    // units, rows every 100, sharing Lune's phase (x ≡ 100 mod 125, y ≡ 125
    // mod 100). The editor snaps to this lattice; the pack writer snaps too so
    // whatever ships is exactly what the editor shows, even for shapes saved
    // before the uniform grid existed.

    public const double LatticeX0 = 100;
    public const double LatticeY0 = 125;
    public const double StepX = 125;
    public const double StepY = 100;

    /// <summary>Nearest lattice x (major column or the halfway sub-column).</summary>
    public static double SnapGraphX(double x)
    {
        var major = LatticeX0 + Math.Round((x - LatticeX0) / StepX) * StepX;
        var sub = LatticeX0 + StepX / 2 + Math.Round((x - LatticeX0 - StepX / 2) / StepX) * StepX;
        return Math.Abs(x - major) <= Math.Abs(x - sub) ? major : sub;
    }

    /// <summary>Nearest lattice y (major row or the halfway sub-row).</summary>
    public static double SnapGraphY(double y)
    {
        var major = LatticeY0 + Math.Round((y - LatticeY0) / StepY) * StepY;
        var sub = LatticeY0 + StepY / 2 + Math.Round((y - LatticeY0 - StepY / 2) / StepY) * StepY;
        return Math.Abs(y - major) <= Math.Abs(y - sub) ? major : sub;
    }

    // ───────────────────────── pack writer ─────────────────────────

    /// <summary>
    /// Writes every recorded character layout into the packed mod tree.
    /// Each graph is staged from vanilla (or reused if already staged) and its
    /// node positions + edge array are rewritten from the layout, preserving
    /// nodes the layout does not mention. Returns the number of graphs written.
    /// </summary>
    public static int Apply(string modDir, Settings settings)
    {
        int total = 0;
        foreach (var (id, _, graphStem) in Characters)
        {
            var state = settings.For(id);
            var packShape = state.PackShape();
            GraphLayout layout = packShape switch
            {
                "custom" => state.Custom,
                "vanilla" => null,
                _ => state.Presets.TryGetValue(packShape, out var preset) ? preset : null,
            };
            if (layout is null) continue;
            total += WriteStagedGraph(modDir, graphStem, layout);
        }
        return total;
    }

    /// <summary>
    /// Rewrites one character graph in work/mod from a layout. The vanilla
    /// graph under work/tables/graphs is the source of truth for node order;
    /// a staged mod copy is created if the graph was not already modified.
    /// Returns 1 when any node position or link changed.
    /// </summary>
    public static int WriteStagedGraph(string modDir, string graphStem, GraphLayout layout)
    {
        var modPath = FileCopy.FindUnder(modDir, graphStem + ".uasset");
        string path;
        if (modPath is not null)
        {
            path = modPath;
        }
        else
        {
            var src = FindCharacterGraphVanilla(graphStem);
            if (src is null)
            {
                Console.WriteLine($"[skill-graph] {graphStem} not found under work/tables/graphs — skipped");
                return 0;
            }
            var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
            if (idx < 0) return 0;
            path = Path.Combine(modDir, src[idx..]);
            FileCopy.AssetPair(src, path);
        }

        var map = DataTableEditor.Mappings;
        var g = map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
        var gx = g.Exports.OfType<NormalExport>().First();
        var nodes = (ArrayPropertyData)gx.Data[0];
        var edges = (ArrayPropertyData)gx.Data[1];

        // Match layout positions to nodes by import stem.
        var posByStem = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        foreach (var (skill, x, y) in layout.Positions)
        {
            var stem = skill.StartsWith("DA_Skill_", StringComparison.Ordinal)
                ? skill["DA_Skill_".Length..]
                : skill;
            posByStem[stem] = (x, y);
        }

        // import index of each node's skill stem, in file order
        var stemAtNode = new string[nodes.Value.Length];
        var idxByStem = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < nodes.Value.Length; i++)
        {
            if (nodes.Value[i] is not StructPropertyData nodeStruct) continue;
            var su = nodeStruct.Value.Count > 0 ? nodeStruct.Value[0] as StructPropertyData : null;
            var skillPtr = su?.Value.Count > 0 ? (su.Value[0] as ObjectPropertyData)?.Value : null;
            if (skillPtr is null || skillPtr.IsNull() || !skillPtr.IsImport()) continue;
            var impIdx = Math.Abs(skillPtr.Index) - 1;
            if (impIdx < 0 || impIdx >= g.Imports.Count) continue;
            var impName = g.Imports[impIdx].ObjectName.ToString();
            var stem = impName.StartsWith("DA_Skill_", StringComparison.Ordinal)
                ? impName["DA_Skill_".Length..]
                : impName;
            stemAtNode[i] = stem;
            if (!idxByStem.ContainsKey(stem)) idxByStem[stem] = i;
        }

        bool changed = false;

        // ── positions (+ sync node unlock cost to the DA APCost) ──
        var newNodeList = new List<PropertyData>();
        for (int i = 0; i < nodes.Value.Length; i++)
        {
            var original = nodes.Value[i] as StructPropertyData;
            if (original is null) { newNodeList.Add(nodes.Value[i]); continue; }
            var clone = (StructPropertyData)original.Clone();
            var stem = stemAtNode[i];
            var daCost = stem.Length > 0 ? DaApCost("DA_Skill_" + stem) : 0;
            if (posByStem.TryGetValue(stem, out var xy) || daCost > 0)
            {
                var su = (StructPropertyData)clone.Value[0].Clone();
                clone.Value[0] = su;
                if (posByStem.TryGetValue(stem, out xy))
                {
                    var posWrap = (StructPropertyData)clone.Value[1].Clone();
                    // Snap onto the shared uniform grid so shipped positions always
                    // match the editor's lattice, even for legacy saved shapes.
                    ((Vector2DPropertyData)posWrap.Value[0]).Value =
                        new FVector2D((float)SnapGraphX(xy.X), (float)SnapGraphY(xy.Y));
                    clone.Value[1] = posWrap;
                }
                // The DA APCost is the tuned cast cost; keep SP-unlock trees in
                // sync. Nodes that already cost 0 are item-gated (Monoco feet,
                // gradient unlock items) — never copy AP onto those.
                if (daCost > 0 && su.Value.Count > 1 && su.Value[1] is IntPropertyData cost
                    && cost.Value != 0 && cost.Value != daCost)
                {
                    cost.Value = daCost;
                }
                changed = true;
            }
            newNodeList.Add(clone);
        }
        nodes.Value = newNodeList.ToArray();

        // ── edges: rebuild the array from the layout ──
        if (edges.Value.Length > 0)
        {
            var edgeTemplate = (StructPropertyData)edges.Value[0].Clone();
            var newEdgeList = new List<PropertyData>();
            foreach (var (fromStem, toStem) in layout.Links)
            {
                if (!idxByStem.TryGetValue(fromStem, out var fromNode)) continue;
                if (!idxByStem.TryGetValue(toStem, out var toNode)) continue;
                if (fromNode == toNode) continue;
                var edge = (StructPropertyData)edgeTemplate.Clone();
                ((ObjectPropertyData)edge.Value[0]).Value = GetImportForNode(g, nodes, fromNode);
                ((ObjectPropertyData)edge.Value[1]).Value = GetImportForNode(g, nodes, toNode);
                newEdgeList.Add(edge);
            }
            edges.Value = newEdgeList.ToArray();
            changed = true;
        }

        if (!changed) return 0;
        g.Write(path);
        Console.WriteLine($"[skill-graph] {graphStem}: {layout.Positions.Count} position(s), {layout.Links.Count} link(s) written to {path}");
        return 1;
    }

    private static FPackageIndex? GetImportForNode(UAsset g, ArrayPropertyData nodes, int nodeIdx)
    {
        if (nodeIdx < 0 || nodeIdx >= nodes.Value.Length) return null;
        if (nodes.Value[nodeIdx] is not StructPropertyData nodeStruct) return null;
        var su = nodeStruct.Value.Count > 0 ? nodeStruct.Value[0] as StructPropertyData : null;
        return (su?.Value.Count > 0 ? (su.Value[0] as ObjectPropertyData)?.Value : null) ?? null;
    }
}
