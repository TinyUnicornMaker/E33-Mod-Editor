using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using E33ModEditor.Core.Loot;
using E33ModEditor.Core.Packer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core.Spoiler;

/// <summary>
/// Builds the player-facing, single-file spoiler codex for one seed.
///
/// The HTML template is an embedded resource; the generated file has the
/// complete JSON payload inlined, so it can be handed to a player and opened
/// offline with no companion files. The seed-specific chest, enemy and weapon
/// rows are read from the staged <c>work/mod</c> tables after
/// <see cref="RandomizerKit.Apply"/>.
/// </summary>
public static class SpoilerCodexKit
{
    public const string FileSuffix = "_Spoiler.html";

    private static readonly string[] Archetypes =
        { "Regular", "Strong", "Weak", "Elite", "Elusive", "Boss", "Alpha", "Petank" };

    private static readonly HashSet<string> SpecialAreas = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cut Content", "Placeholder", "DLC", "Summons", "Camp", "World Map",
        "Generic templates", "Mini levels",
    };

    private static readonly Dictionary<string, int> SpecialAreaOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DLC"] = 1, ["World Map"] = 2, ["Summons"] = 3, ["Camp"] = 4,
        ["Mini levels"] = 5, ["Generic templates"] = 6, ["Cut Content"] = 7, ["Placeholder"] = 8,
    };

    /// <summary>Areas whose only native rows are low-level tutorial leftovers
    /// (or which have no chest rows to carry the curated interior level).</summary>
    private static readonly Dictionary<string, int> KnownAreaLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Crimson Forest"] = 60, ["Red Woods"] = 60, ["Sacred River"] = 60,
        ["Sky Island"] = 60, ["The Chosen Path"] = 60, ["Isle of the Eyes"] = 65,
        ["The Crows"] = 65, ["Dark Shores"] = 68, ["Endless Night Sanctuary"] = 73,
        ["Dark Gestral Arena"] = 75, ["The Reacher"] = 79, ["Frozen Hearts"] = 79,
        ["Flying Cemetery"] = 79, ["The Fountain"] = 80, ["Flying Manor"] = 83,
        ["Verso's Drafts"] = 86, ["Painting Workshop"] = 90, ["Sunless Cliffs"] = 92,
        ["Renoir's Drafts"] = 93, ["The Abyss"] = 99, ["Lumiere - Act 3"] = 99,
        ["Endgame (T5)"] = 99, ["Venue variant"] = 99,
    };

    /// <summary>
    /// Runtime combat level per area (`DT_LevelData.ScalingMinLevel`), which is
    /// what `CurrentBattleEncounterLevel` resolves to when an encounter has no
    /// override. Kept separate from <see cref="KnownAreaLevels"/> (which is the
    /// loot/area-ordering band, not the combat level).
    /// </summary>
    private static readonly Dictionary<string, int> RuntimeAreaLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Spring Meadows"] = 3, ["Flying Waters"] = 8, ["Ancient Sanctuary"] = 12,
        ["Gestral Village"] = 15, ["Esquie's Nest"] = 16, ["Stone Wave Cliffs"] = 19,
        ["Forgotten Battlefield"] = 23, ["Monoco's Station"] = 25, ["Old Lumiere"] = 28,
        ["Visages"] = 32, ["Sirene"] = 37, ["Sirene's Dress"] = 60, ["The Monolith"] = 43,
        ["The Reacher"] = 58, ["The Heart of the Canvas"] = 55,
        ["Lumiere - Act 3"] = 48, ["Frozen Hearts"] = 63,
        ["Flying Manor"] = 83, ["Flying Cemetery"] = 55, ["The Fountain"] = 80,
        ["Painting Workshop"] = 90, ["Renoir's Drafts"] = 91, ["Verso's Drafts"] = 86,
        ["Sunless Cliffs"] = 92, ["The Abyss"] = 99, ["Endless Tower"] = 55,
        ["Dark Shores"] = 68, ["Endless Night Sanctuary"] = 73, ["Dark Gestral Arena"] = 75,
        ["The Crows"] = 65, ["Isle of the Eyes"] = 65, ["Crimson Forest"] = 60,
        ["Red Woods"] = 60, ["Sacred River"] = 60, ["Sky Island"] = 60,
        ["The Chosen Path"] = 60, ["Yellow Harvest"] = 21, ["Abbest Cave"] = 30,
        ["The Small Bourgeon"] = 13, ["Stone Quarry"] = 30, ["Esoteric Ruins"] = 28,
        ["Crushing Cavern"] = 27, ["The Carousel"] = 30, ["Coastal Cave"] = 36,
        ["Falling Leaves"] = 41, ["Sinister Cave"] = 38, ["Many Faces"] = 32,
        ["DLC"] = 86, ["World Map"] = 1, ["Camp"] = 1,
    };

    /// <summary>
    /// Rows whose display parenthetical is a sub-area that the shared area map
    /// cannot distinguish (Sirene's Dress vs Sirene; the Paintress intro at the
    /// foot of the Monolith vs the interior climb).
    /// </summary>
    private static readonly Dictionary<string, int> RuntimeRowLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SI_Glissando_Alpha"] = 60,
        ["ML_PaintressIntro"] = 40,
    };

    /// <summary>Default path next to the seed packs. Old seeds are kept, so a
    /// host can hand out several seed spoilers.</summary>
    public static string DefaultOutputPath(int seed) =>
        Path.Combine(EditorPaths.ExportDir, "Spoilers", $"ExpeditionInfinity_{seed}{FileSuffix}");

    /// <summary>
    /// Build the spoiler file. <paramref name="modDir"/> is the staged
    /// <c>work/mod</c> tree; pass the live catalog so static item names /
    /// categories / area metadata match the editor.
    /// </summary>
    public static string Build(
        string modDir,
        int seed,
        LootCatalog catalog,
        Action<string>? log = null,
        string? outputPath = null,
        string? packRevision = null,
        string? edition = null,
        string? editionNote = null)
    {
        var data = new Builder(modDir, seed, catalog, packRevision, edition, editionNote).Build();
        var json = data.ToString(Formatting.None);
        // A literal </script> inside a name would terminate the inline script
        // block; JSON's \/ escape is equivalent and safe.
        json = json.Replace("</", "<\\/", StringComparison.Ordinal);

        var html = ReadTemplate()
            .Replace("__SPOILER_DATA__", json, StringComparison.Ordinal)
            .Replace("__CINZEL_FONT__", ReadResourceBase64("Cinzel-Variable.ttf"), StringComparison.Ordinal)
            .Replace("__GARAMOND_FONT__", ReadResourceBase64("EBGaramond-Variable.ttf"), StringComparison.Ordinal)
            .Replace("__BEBAS_FONT__", ReadResourceBase64("BebasNeue-Regular.ttf"), StringComparison.Ordinal);
        var path = outputPath ?? DefaultOutputPath(seed);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var temp = path + ".tmp";
        File.WriteAllText(temp, html, new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
        log?.Invoke($"[spoiler] wrote {path}");
        return path;
    }

    private static string ReadTemplate()
    {
        var asm = typeof(SpoilerCodexKit).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("Spoiler.template.html", StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException("Spoiler/template.html embedded resource");
        using var stream = asm.GetManifestResourceStream(name)
            ?? throw new FileNotFoundException(name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ReadResourceBase64(string fileName)
    {
        var asm = typeof(SpoilerCodexKit).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (name is null) return "";
        using var stream = asm.GetManifestResourceStream(name);
        if (stream is null) return "";
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Convert.ToBase64String(memory.ToArray());
    }

    private sealed class Builder
    {
        private readonly string _modDir;
        private readonly int _seed;
        private readonly LootCatalog _catalog;
        private readonly string? _packRevision;
        private readonly string? _edition;
        private readonly string? _editionNote;

        private readonly JObject _rawItems;
        private readonly JObject _stagedChests;
        private readonly JObject _stagedEnemies;
        private readonly JObject _stagedWeapons;
        private readonly JObject _baseEnemies;
        private readonly JObject _pictosDefs;
        private readonly JObject _passiveEffects;
        private readonly Dictionary<string, string> _stItems;
        private readonly Dictionary<string, string> _stPassives;
        private readonly Dictionary<string, string> _stEnemies;
        private readonly Dictionary<string, string> _weaponEffectNames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _weaponEffectDescriptions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CreatedPictos> _pictosCreated = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _categoryById = new(StringComparer.Ordinal);
        private readonly HashSet<string> _weaponDefinitionIds = new(StringComparer.Ordinal);

        private readonly Dictionary<string, JObject> _itemCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<JObject>> _sources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _chestLevels = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _encountersByEnemy = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<int>> _encounterOverridesByEnemy = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _encounterOverridesByName = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, LootCatalog.Check> _chestChecks;

        public Builder(string modDir, int seed, LootCatalog catalog, string? packRevision = null,
            string? edition = null, string? editionNote = null)
        {
            _modDir = modDir;
            _seed = seed;
            _catalog = catalog;
            _packRevision = packRevision;
            _edition = edition;
            _editionNote = editionNote;

            var repo = EditorPaths.RepoRoot;
            _rawItems = LoadJson(Path.Combine(repo, "work", "items.json"));
            _stagedChests = DumpStaged("DT_ChestsContent")
                ?? LoadJson(Path.Combine(repo, "work", "chests.json"));
            _stagedEnemies = DumpStaged("DT_jRPG_Enemies")
                ?? LoadJson(Path.Combine(repo, "work", "enemies.json"));
            _baseEnemies = LoadJson(Path.Combine(repo, "work", "enemies.json"));
            _stagedWeapons = DumpStaged("DT_WeaponDefinitions")
                ?? LoadJson(Path.Combine(repo, "work", "DT_WeaponDefinitions.json"));
            foreach (var prop in _stagedWeapons.Properties())
            {
                var definitionId = prop.Value.Value<string>("DefinitionID") ?? "";
                if (definitionId.Length > 0) _weaponDefinitionIds.Add(definitionId);
            }
            _pictosDefs = DumpVanilla("DT_PictosDefinitions") ?? new JObject();
            _passiveEffects = DumpVanilla("DT_PassiveEffects") ?? new JObject();

            _stItems = LoadStringTable("ST_Items");
            _stPassives = LoadStringTable("ST_PassiveEffects");
            _stEnemies = LoadStringTable("ST_Enemies_Name");

            LoadWeaponEffects(repo);
            LoadPictosCreated(repo);

            // Created content is not always in item_data.json; register the
            // kits' own display names so a seed drop never shows as "Unknown".
            _catalog.RegisterExtraPictos(
                E33ModEditor.Core.PictosCreateKit.PainterConversions.Select(p => (p.Id, p.DisplayName))
                    .Concat(E33ModEditor.Core.PictosCreateKit.Powers.Select(p => (p.Id, p.DisplayName)))
                    .Concat(E33ModEditor.Core.PictosMagnitudeKit.Variants.Select(p => (p.RowId, p.DisplayName)))
                    .Concat(E33ModEditor.Core.BombPictosKit.CreatedPictos)
                    .Concat(E33ModEditor.Core.ReviveDebuffKit.CreatedPictos)
                    .Concat(E33ModEditor.Core.PictosCreateKit.HitBurn.Select(p => (p.Id, p.DisplayName)))
                    .Concat(new[]
                    {
                        (E33ModEditor.Core.SpecialPictosKit.AliciaId, "Alicia's Suffering"),
                        (E33ModEditor.Core.SpecialPictosKit.LayeredId, "Layered Canvas"),
                        (E33ModEditor.Core.SpecialPictosKit.SuperSonicId, "Super Sonic"),
                        (E33ModEditor.Core.SpecialPictosKit.RuptureId, "Rupture Shot"),
                        (E33ModEditor.Core.SpecialPictosKit.UnoPunchoId, "Uno Puncho"),
                    }));

            foreach (var (category, ids) in catalog.Categories)
                foreach (var id in ids)
                {
                    if (!_categoryById.TryGetValue(id, out var list))
                        _categoryById[id] = list = new List<string>();
                    if (!list.Contains(category, StringComparer.OrdinalIgnoreCase))
                        list.Add(category);
                }

            _chestChecks = catalog.Checks
                .Where(c => c.Source == "Chest" || c.Source == LootCatalog.PaintCageSource)
                .GroupBy(c => c.Id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            LoadEncounters();
        }

        public JObject Build()
        {
            var chests = new JArray();
            foreach (var prop in _stagedChests.Properties())
            {
                if (prop.Value is not JObject row) continue;
                chests.Add(BuildChest(prop.Name, row));
            }

            var enemiesMod = new JArray();
            foreach (var prop in _stagedEnemies.Properties())
            {
                if (prop.Value is not JObject row) continue;
                enemiesMod.Add(BuildEnemy(prop.Name, row, "mod"));
            }

            var enemiesVanilla = new JArray();
            foreach (var prop in _baseEnemies.Properties())
            {
                if (prop.Value is not JObject row) continue;
                enemiesVanilla.Add(BuildEnemy(prop.Name, row, "vanilla"));
            }

            BuildSourceIndex(chests, enemiesMod);

            // Every item that appears anywhere, including category members
            // without a DT_jRPG_Items row (mod-created variants).
            var allIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in _catalog.Items.Keys) allIds.Add(id);
            foreach (var prop in _rawItems.Properties()) allIds.Add(prop.Name);
            foreach (var id in _sources.Keys) allIds.Add(id);
            foreach (var ids in _catalog.Categories.Values)
                foreach (var id in ids) allIds.Add(id);
            foreach (var id in allIds) ItemBase(id);
            foreach (var (id, item) in _itemCache) item["sources"] = SourcesFor(id);

            var pictos = BuildPictos();
            var weapons = BuildWeapons();
            var keyItems = BuildKeyItems();
            var tints = BuildTints();
            var chromaPacks = BuildChromaPacks();

            var itemsOut = new JObject();
            foreach (var (id, item) in _itemCache.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                item["sources"] = SourcesFor(id);
                itemsOut[id] = item;
            }

            var areaLevels = BuildAreaLevels(chests, enemiesMod);
            var areaOrder = BuildAreaOrder(areaLevels);

            var pack = FullPackKit.ModNameFor(_seed, _packRevision);
            var meta = new JObject
            {
                ["title"] = "Expedition 33 — Spoiler Codex",
                ["subtitle"] = "Every Nevron. Every drop. Every chest.",
                ["generated"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
                ["seed"] = _seed,
                ["pack"] = pack,
                ["edition"] = _edition,
                ["editionNote"] = _editionNote,
                ["counts"] = new JObject
                {
                    ["enemiesVanilla"] = enemiesVanilla.Count,
                    ["enemiesMod"] = enemiesMod.Count,
                    ["chests"] = chests.Count,
                    ["items"] = itemsOut.Count,
                    ["pictos"] = pictos.Count,
                    ["weapons"] = weapons.Count,
                    ["keyItems"] = keyItems.Count,
                    ["tints"] = tints.Count,
                    ["areas"] = areaLevels.Count,
                },
                ["datasets"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = "mod",
                        ["label"] = $"Seed {_seed}",
                        ["description"] = $"This seed's staged enemy stats, drops and chest loot ({pack}).",
                    },
                    new JObject
                    {
                        ["id"] = "vanilla",
                        ["label"] = "Vanilla",
                        ["description"] = "Original DT_jRPG_Enemies rows, for comparison.",
                    },
                },
                ["notes"] = new JArray
                {
                    "Effective stats are archetype[level] × EnemyScaling, the formula FL_EnemyScalingHelpers.ComputeCharacterStats uses at runtime.",
                    "Raw EnemyStats are shown for reference. Placeholder rows (HP 1 / ATK 0) are normal.",
                    "Chest and enemy drops are read from the staged tables for this seed, not from the base tables.",
                    "Enemy areas come from the curated enemy names, with special rows bucketed as World Map, DLC, Summons or Cut Content.",
                },
            };

            return new JObject
            {
                ["meta"] = meta,
                ["areaOrder"] = JArray.FromObject(areaOrder),
                ["areaLevels"] = JObject.FromObject(areaLevels),
                ["combatAreaLevels"] = JObject.FromObject(RuntimeAreaLevels),
                ["enemies"] = new JObject
                {
                    ["mod"] = SortEnemies(enemiesMod),
                    ["vanilla"] = SortEnemies(enemiesVanilla),
                },
                ["chests"] = SortChests(chests, areaOrder),
                ["items"] = itemsOut,
                ["pictos"] = pictos,
                ["weapons"] = weapons,
                ["keyItems"] = keyItems,
                ["tints"] = tints,
                ["chromaPacks"] = chromaPacks,
            };
        }

        // ── chests ───────────────────────────────────────────────────────

        private JObject BuildChest(string id, JObject row)
        {
            _chestChecks.TryGetValue(id, out var check);
            var area = check?.Area ?? "Unknown";
            var level = check?.AreaLevel ?? 1;
            var tier = check?.Tier ?? LootCatalog.TierFromLevel(level);

            // The curated catalog folds Act 1 and Act 3 Lumiere into one
            // "Lumiere" bucket; keep the chests and enemies aligned with the
            // separate Act 1 / Act 3 enemy names.
            if (id.StartsWith("Chest_Lumiere_ACT1", StringComparison.Ordinal))
            {
                area = "Lumiere - Act 1";
                level = 1;
                tier = 1;
            }
            else if (id.StartsWith("Chest_Lumiere_", StringComparison.Ordinal))
            {
                area = "Lumiere - Act 3";
                level = 99;
                tier = 5;
            }

            var drops = new JArray();

            if (row["LootTableReferences"] is JArray refs)
                foreach (var trace in refs.OfType<JObject>())
                {
                    var table = trace.Value<string>("LootTable") ?? "";
                    var shortName = table.Replace('\\', '/');
                    var slash = shortName.LastIndexOf('/');
                    if (slash >= 0) shortName = shortName[(slash + 1)..];
                    drops.Add(new JObject
                    {
                        ["id"] = table,
                        ["name"] = $"Upgrade roll — {shortName}",
                        ["type"] = "Upgrade roll",
                        ["qty"] = trace.Value<int?>("RollCount") ?? 1,
                        ["chance"] = 100.0,
                        ["ref"] = true,
                    });
                }

            if (row["LootTableSections"] is JArray sections)
                foreach (var section in sections.OfType<JObject>())
                {
                    var chance = section.Value<double?>("ChancePercent") ?? 100.0;
                    if (section["LootEntries"] is not JArray entries) continue;
                    var entryRows = entries.OfType<JObject>().ToList();
                    var weightTotal = entryRows.Sum(e => Math.Max(0.0001, e.Value<double?>("Weight") ?? 1.0));
                    if (weightTotal <= 0) weightTotal = 1;
                    foreach (var entry in entryRows)
                    {
                        var itemId = entry.Value<string>("ItemID") ?? "";
                        if (itemId.Length == 0) continue;
                        var weight = entry.Value<double?>("Weight") ?? 1.0;
                        var effective = entryRows.Count > 1 ? chance * Math.Max(0.0, weight) / weightTotal : chance;
                        var item = ItemBase(itemId);
                        drops.Add(new JObject
                        {
                            ["id"] = itemId,
                            ["name"] = item.Value<string>("name") ?? itemId,
                            ["type"] = item.Value<string>("type") ?? "Unknown",
                            ["qty"] = entry.Value<int?>("Quantity") ?? 1,
                            ["chance"] = Math.Round(effective, 4),
                            ["weight"] = weight,
                            ["ref"] = false,
                        });
                    }
                }

            var lower = id.ToLowerInvariant();
            var cut = lower.Contains("placeholder") || lower.Contains("test") || lower.Contains("cut")
                      || area is "Placeholder" or "Cut Content";

            return new JObject
            {
                ["id"] = id,
                ["name"] = ChestName(id, area),
                ["area"] = area,
                ["level"] = level,
                ["tier"] = tier,
                ["cut"] = cut,
                ["drops"] = drops,
            };
        }

        // ── enemies ──────────────────────────────────────────────────────

        private JObject BuildEnemy(string id, JObject row, string dataset)
        {
            var (info, variantArea, variantTag) = ResolveEnemyMeta(id);
            var custom = !string.IsNullOrWhiteSpace(info?.DisplayName)
                ? info!.DisplayName
                : _stEnemies.GetValueOrDefault(id) ?? PrettyId(id);
            var (name, area) = ParseEnemyNameArea(custom, id, row);
            if (variantArea is not null) area = variantArea;
            if (variantTag is not null) name = $"{name} [{variantTag}]";

            var tableLevel = row.Value<int?>("Level") ?? info?.Level ?? 1;
            if (tableLevel <= 0) tableLevel = 1;
            var level = Math.Max(1, RuntimeLevel(id, row, area));

            var arch = EnemyEffectiveStats.RuntimeArchetype(row, info?.Archetype);

            double Scaling(string field)
            {
                var token = row["EnemyScaling"]?[field];
                if (token is null) return 1.0;
                try { return token.Value<double>(); }
                catch { return 1.0; }
            }

            double ArchValue(string field) =>
                EnemyEffectiveStats.ArchetypeStat(arch, level, field);

            double DefenseBase(string field) =>
                dataset == "mod" && field is "PhysicalDefense" or "MagicalDefense"
                    ? 1.0
                    : ArchValue(field);

            var hpMult = Scaling("HP");
            var patkMult = Scaling("PhysicalAttack");
            var pdefMult = Scaling("PhysicalDefense");
            var matkMult = Scaling("MagicalAttack");
            var mdefMult = Scaling("MagicalDefense");
            var masteryMult = Scaling("Mastery");
            var accuracyMult = Scaling("Accuracy");
            var speedMult = Scaling("Speed");
            var critMult = Scaling("CriticalChance");
            var chromaMult = Scaling("Chroma");
            var xpMult = Scaling("Experience");

            var stats = new JObject
            {
                ["hp"] = Math.Round(ArchValue("HP") * hpMult),
                ["atk"] = Math.Round(ArchValue("PhysicalAttack") * patkMult),
                ["matk"] = Math.Round(ArchValue("MagicalAttack") * matkMult),
                ["pdef"] = Math.Round(DefenseBase("PhysicalDefense") * pdefMult),
                ["mdef"] = Math.Round(DefenseBase("MagicalDefense") * mdefMult),
                ["speed"] = Math.Round(ArchValue("Speed") * speedMult),
                ["crit"] = Math.Round(ArchValue("CriticalChance") * critMult, 4),
                ["xp"] = Math.Round(ArchValue("Experience") * xpMult),
                ["chroma"] = Math.Round(ArchValue("Chroma") * chromaMult),
            };

            var scaling = new JObject
            {
                ["hp"] = hpMult,
                ["patk"] = patkMult,
                ["pdef"] = pdefMult,
                ["matk"] = matkMult,
                ["mdef"] = mdefMult,
                ["mastery"] = masteryMult,
                ["accuracy"] = accuracyMult,
                ["speed"] = speedMult,
                ["crit"] = critMult,
                ["chroma"] = chromaMult,
                ["xp"] = xpMult,
            };

            var raw = new JObject();
            if (row["EnemyStats"] is JObject rawStats)
                foreach (var stat in rawStats.Properties())
                {
                    var key = stat.Name.Split("::").Last();
                    if (!RawEnemyKeys.TryGetValue(key, out var label)) continue;
                    try { raw[label] = stat.Value.Value<double>(); }
                    catch { /* ignore malformed row */ }
                }

            var drops = new JArray();
            if (row["PossibleLoot"] is JArray loot)
                foreach (var entry in loot.OfType<JObject>())
                {
                    var itemId = (entry["Item"] as JObject)?.Value<string>("RowName") ?? "";
                    if (itemId.Length == 0) continue;
                    var item = ItemBase(itemId);
                    drops.Add(new JObject
                    {
                        ["id"] = itemId,
                        ["name"] = item.Value<string>("name") ?? itemId,
                        ["type"] = item.Value<string>("type") ?? "Unknown",
                        ["qty"] = entry.Value<int?>("Quantity") ?? 1,
                        ["chance"] = entry.Value<double?>("LootPercentageChance") ?? 100.0,
                    });
                }

            var cut = info?.CutContent == true
                      || area is "Cut Content" or "Placeholder"
                      || id.StartsWith("Test_", StringComparison.Ordinal);
            var isBoss = row.Value<bool?>("IsBoss") == true
                         || arch is "Boss" or "Alpha"
                         || string.Equals(info?.Archetype, "SuperBoss", StringComparison.OrdinalIgnoreCase);

            var encounterList = _encountersByEnemy.GetValueOrDefault(id) ?? new List<string>();
            var encounterSample = JArray.FromObject(encounterList.Take(12));

            return new JObject
            {
                ["id"] = id,
                ["name"] = name,
                ["area"] = area,
                ["level"] = level,
                ["tableLevel"] = tableLevel,
                ["archetype"] = arch,
                ["isBoss"] = isBoss,
                ["isSuperBoss"] = string.Equals(info?.Archetype, "SuperBoss", StringComparison.OrdinalIgnoreCase),
                ["cut"] = cut,
                ["dataset"] = dataset,
                ["stats"] = stats,
                ["scaling"] = scaling,
                ["raw"] = raw,
                ["drops"] = drops,
                ["encounters"] = encounterSample,
                ["encounterCount"] = encounterList.Count,
            };
        }

        private (LootCatalog.EnemyInfo? Info, string? Area, string? Tag) ResolveEnemyMeta(string id)
        {
            if (_catalog.Enemies.TryGetValue(id, out var exact))
                return (exact, null, null);

            var baseId = id;
            string? area = null;
            string? tag = null;
            if (id.StartsWith(EndgameVariantKit.VariantPrefix, StringComparison.Ordinal))
            {
                baseId = id[EndgameVariantKit.VariantPrefix.Length..];
                area = "Endgame (T5)";
                tag = "T5";
            }
            else if (id.StartsWith(WorldMapVariantKit.VariantPrefix, StringComparison.Ordinal))
            {
                baseId = id[WorldMapVariantKit.VariantPrefix.Length..];
                area = "World Map";
                tag = "World Map";
            }
            else if (id.StartsWith(BossVenueKit.VariantPrefix, StringComparison.Ordinal))
            {
                baseId = Regex.Replace(id[BossVenueKit.VariantPrefix.Length..], @"_\d+$", "");
                area = "Venue variant";
                tag = "Venue";
            }

            return _catalog.Enemies.TryGetValue(baseId, out var baseInfo)
                ? (baseInfo, area, tag)
                : (null, area, tag);
        }

        /// <summary>
        /// Runtime combat level for a row: venue clones carry their venue level,
        /// explicit pins win, then the highest encounter override (the hardest
        /// venue), then the area's DT_LevelData level, then the row template.
        /// </summary>
        private int RuntimeLevel(string id, JObject row, string area)
        {
            var rowLevel = row.Value<int?>("Level") ?? 1;
            if (rowLevel <= 0) rowLevel = 1;
            // WMV_ world-map clones carry a loot-tier Level (40/60), not the
            // venue level; their fight level comes from the encounter override.
            var isWmv = id.StartsWith("WMV_", StringComparison.OrdinalIgnoreCase);
            if (!isWmv && (id.StartsWith("BV_", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("T5_", StringComparison.OrdinalIgnoreCase)))
                return rowLevel;
            if (RuntimeRowLevels.TryGetValue(id, out var rowPin) && rowPin > 0)
                return rowPin;
            if (LootCatalog.EncounterLevelOverrides.TryGetValue(id, out var forced) && forced > 0)
                return forced;
            if (_encounterOverridesByEnemy.TryGetValue(id, out var overs) && overs.Count > 0)
            {
                var max = overs.Max();
                if (max > 0) return max;
            }
            if (RuntimeAreaLevels.TryGetValue(area, out var areaLevel) && areaLevel > 0)
                return areaLevel;
            return rowLevel;
        }

        private (string Name, string Area) ParseEnemyNameArea(string custom, string id, JObject row)
        {
            var name = (custom ?? "").Trim();
            var area = "";
            var match = Regex.Match(name, @"\(([^()]*)\)\s*$");
            if (match.Success)
            {
                area = match.Groups[1].Value.Trim();
                name = name[..match.Index].Trim(' ', '-');
            }

            if (name.Length == 0)
                name = _stEnemies.GetValueOrDefault(id) ?? PrettyId(id);

            if (area.Length == 0)
            {
                if (id.StartsWith("WM_", StringComparison.Ordinal) || id.StartsWith("WMV_", StringComparison.Ordinal))
                    area = "World Map";
                else if (id.StartsWith("PETANK_", StringComparison.Ordinal))
                    area = "Summons";
                else
                {
                    var actor = row.Value<string>("EnemyActorClassSoft") ?? "";
                    var folder = Regex.Match(actor, @"/Enemies/([A-Za-z0-9_]+)/");
                    area = folder.Success ? Humanize(folder.Groups[1].Value) : "Unknown";
                }
            }

            area = area switch
            {
                "Blue" or "Green" or "Yellow" or "Orange" or "Red" or "Violet" => "World Map",
                "DLC Enemy" or "DLC Merchant" => "DLC",
                "Summon" => "Summons",
                "Sirene's Dress" => "Sirene",
                _ => area,
            };
            return (name, area);
        }

        // ── sources ──────────────────────────────────────────────────────

        private void BuildSourceIndex(JArray chests, JArray enemies)
        {
            foreach (var chestToken in chests.OfType<JObject>())
            {
                if (chestToken["drops"] is not JArray drops) continue;
                var chestCut = chestToken.Value<bool?>("cut") == true;
                foreach (var drop in drops.OfType<JObject>())
                {
                    if (drop.Value<bool?>("ref") == true) continue;
                    var id = drop.Value<string>("id") ?? "";
                    if (id.Length == 0) continue;
                    AddSource(id, new JObject
                    {
                        ["kind"] = "Chest",
                        ["name"] = chestToken.Value<string>("name") ?? "",
                        ["area"] = chestToken.Value<string>("area") ?? "",
                        ["level"] = chestToken.Value<int?>("level") ?? 0,
                        ["chance"] = drop.Value<double?>("chance") ?? 100.0,
                        ["qty"] = drop.Value<int?>("qty") ?? 1,
                        ["cut"] = chestCut,
                    });
                }
            }

            foreach (var enemy in enemies.OfType<JObject>())
            {
                if (enemy["drops"] is not JArray drops) continue;
                var enemyCut = enemy.Value<bool?>("cut") == true;
                foreach (var drop in drops.OfType<JObject>())
                {
                    var id = drop.Value<string>("id") ?? "";
                    if (id.Length == 0) continue;
                    AddSource(id, new JObject
                    {
                        ["kind"] = "Enemy",
                        ["name"] = enemy.Value<string>("name") ?? "",
                        ["area"] = enemy.Value<string>("area") ?? "",
                        ["level"] = enemy.Value<int?>("level") ?? 0,
                        ["chance"] = drop.Value<double?>("chance") ?? 100.0,
                        ["qty"] = drop.Value<int?>("qty") ?? 1,
                        ["cut"] = enemyCut,
                    });
                }
            }
        }

        private void AddSource(string itemId, JObject source)
        {
            if (!_sources.TryGetValue(itemId, out var list))
                _sources[itemId] = list = new List<JObject>();

            var kind = source.Value<string>("kind") ?? "";
            var name = source.Value<string>("name") ?? "";
            var chance = source.Value<double?>("chance") ?? 100.0;
            var qty = source.Value<int?>("qty") ?? 1;
            var cut = source.Value<bool?>("cut") == true;
            foreach (var existing in list)
            {
                if (existing.Value<string>("kind") == kind
                    && existing.Value<string>("name") == name
                    && Math.Abs((existing.Value<double?>("chance") ?? 100.0) - chance) < 0.0001
                    && (existing.Value<int?>("qty") ?? 1) == qty
                    && (existing.Value<bool?>("cut") == true) == cut)
                    return;
            }
            list.Add(source);
        }

        private JArray SourcesFor(string id)
        {
            var array = new JArray();
            if (_sources.TryGetValue(id, out var list))
                foreach (var source in list)
                    array.Add(source.DeepClone());
            return array;
        }

        // ── items ────────────────────────────────────────────────────────

        private JObject ItemBase(string id)
        {
            if (_itemCache.TryGetValue(id, out var cached)) return cached;
            _catalog.Items.TryGetValue(id, out var info);
            var raw = _rawItems[id] as JObject;
            var (type, subtype) = ItemType(id, info, raw);
            var cats = _categoryById.GetValueOrDefault(id) ?? new List<string>();
            var category = PrimaryCategory(cats);
            var cut = info?.CutContent == true
                      || cats.Contains("Cut Content Items", StringComparer.OrdinalIgnoreCase)
                      || id.StartsWith("Test_", StringComparison.Ordinal)
                      || id.StartsWith("Debug", StringComparison.Ordinal);
            int? tier = type switch
            {
                "Pictos" => TierOf("Pictos", id),
                "Weapon" => TierOf("Weapon", id),
                _ => null,
            };

            var item = new JObject
            {
                ["id"] = id,
                ["name"] = ItemName(id, info, raw),
                ["type"] = type,
                ["subtype"] = subtype,
                ["category"] = category,
                ["description"] = ItemDescription(id),
                ["buy"] = raw?["Item_BuyValue"]?.DeepClone() ?? JValue.CreateNull(),
                ["sell"] = raw?["Item_SellValue"]?.DeepClone() ?? JValue.CreateNull(),
                ["rarity"] = raw?.Value<string>("Item_Rarity") ?? "",
                ["cut"] = cut,
                ["dlc"] = cats.Contains("DLC Items", StringComparer.OrdinalIgnoreCase),
                ["tier"] = TierToken(tier),
                ["sources"] = new JArray(),
            };
            _itemCache[id] = item;
            return item;
        }

        private (string Type, string Subtype) ItemType(string id, LootCatalog.ItemInfo? info, JObject? raw)
        {
            var rawType = raw?.Value<string>("Item_Type") ?? "";
            var kind = info?.Type ?? RawItemType.GetValueOrDefault(rawType, "");
            var subtype = ItemSubtype.GetValueOrDefault(raw?.Value<string>("Item_Subtype") ?? "", "");
            var cats = _categoryById.GetValueOrDefault(id);

            if (string.IsNullOrEmpty(kind) && cats is not null)
            {
                if (cats.Contains("Pictos", StringComparer.OrdinalIgnoreCase)) kind = "Pictos";
                else if (cats.Contains("Weapon", StringComparer.OrdinalIgnoreCase)) kind = "Weapon";
                else if (cats.Contains("Key Item", StringComparer.OrdinalIgnoreCase)) kind = "Key";
            }

            if (id == "MusicRecord") kind = "Music Record";
            else if (id.StartsWith("ChromaPack_", StringComparison.Ordinal)) kind = "Chroma Pack";
            else if (id.StartsWith("UpgradeMaterial_", StringComparison.Ordinal)) kind = "Upgrade Material";
            else if (id.StartsWith("Quest_", StringComparison.Ordinal)) kind = "Key Item";
            else if (id.EndsWith("_Shard", StringComparison.Ordinal)) kind = "Tint Shard";
            else if (kind == "Inventory" && id.StartsWith("ConsumableUpgradeMaterial_", StringComparison.Ordinal))
                kind = "Upgrade Material";
            else if (string.IsNullOrEmpty(kind) && _weaponDefinitionIds.Contains(id))
                kind = "Weapon";

            var display = kind switch
            {
                "CharacterCustomization" => "Cosmetic",
                "SkillUnlocker" => "Skill Unlock",
                "Key" => "Key Item",
                "Gold" => id.StartsWith("ChromaPack_", StringComparison.Ordinal) ? "Chroma Pack" : "Chroma",
                "Shard" => "Tint Shard",
                "Consumable" => "Tint",
                "Inventory" => id.StartsWith("UpgradeMaterial_", StringComparison.Ordinal)
                               || id.StartsWith("ConsumableUpgradeMaterial_", StringComparison.Ordinal)
                    ? "Upgrade Material" : "Inventory",
                _ => string.IsNullOrEmpty(kind) ? "Unknown" : kind,
            };
            return (display, subtype);
        }

        private string PrimaryCategory(List<string> cats)
        {
            foreach (var category in CategoryPriority)
                if (cats.Contains(category, StringComparer.OrdinalIgnoreCase))
                    return category;
            return "";
        }

        private string ItemName(string id, LootCatalog.ItemInfo? info, JObject? raw)
        {
            var fromTable = _stItems.GetValueOrDefault($"ITEM_{id}_Name") ?? "";
            if (fromTable.Length > 0) return fromTable;

            var custom = CleanItemName(info?.DisplayName ?? "");
            if (custom.Length > 0) return custom;

            var display = raw?.Value<string>("Item_DisplayName") ?? "";
            var key = StripRef(display);
            return _stItems.GetValueOrDefault(key) ?? PrettyId(id);
        }

        private string ItemDescription(string id)
        {
            var description = _stItems.GetValueOrDefault($"ITEM_{id}_Description") ?? "";
            if (description.Length == 0)
                description = _stPassives.GetValueOrDefault($"PASSIVE_{id}_Description") ?? "";
            if (description.Length == 0)
                description = ResolveStRef((_passiveEffects[id] as JObject)?.Value<string>("Description"), _stPassives);
            return description.Trim();
        }

        private string PassiveName(string id)
        {
            var name = _stPassives.GetValueOrDefault($"PASSIVE_{id}_Name") ?? "";
            if (name.Length == 0)
            {
                var effect = _passiveEffects[id] as JObject;
                name = ResolveStRef(effect?.Value<string>("Name"), _stPassives);
                if (name is "Weapon" or "ITEM") name = "";
            }
            if (name.Length == 0) name = _weaponEffectNames.GetValueOrDefault(id) ?? "";
            return name.Length > 0 ? name : PrettyId(id);
        }

        private string PassiveDescription(string id)
        {
            var description = _stPassives.GetValueOrDefault($"PASSIVE_{id}_Description") ?? "";
            if (description.Length == 0)
                description = ResolveStRef((_passiveEffects[id] as JObject)?.Value<string>("Description"), _stPassives);
            if (description.Length == 0)
                description = _weaponEffectDescriptions.GetValueOrDefault(id) ?? "";
            return description.Trim();
        }

        private int? TierOf(string category, string id)
        {
            var column = _catalog.ItemTiers.FindColumn(category, id);
            if (column is null || !column.StartsWith("Tier", StringComparison.Ordinal)) return null;
            return int.TryParse(column[4..], out var tier) ? tier : null;
        }

        private static JToken TierToken(int? tier) =>
            tier.HasValue ? new JValue(tier.Value) : JValue.CreateNull();

        // ── pictos / weapons / catalogues ────────────────────────────────

        private JArray BuildPictos()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in CategoryIds("Pictos")) ids.Add(id);
            foreach (var (id, info) in _catalog.Items)
                if (info.Type == "Pictos") ids.Add(id);
            foreach (var id in _pictosCreated.Keys) ids.Add(id);

            var list = new List<JObject>();
            foreach (var id in ids)
            {
                var baseItem = ItemBase(id);
                var defs = _pictosDefs[id] as JObject;
                var multipliers = StatMultipliers(defs?["BaseStatMultipliers"] as JObject);
                var actual = StatMultipliers((_rawItems[id] as JObject)?["Pictos_ItemStats"] as JObject);
                var effect = _passiveEffects[id] as JObject;
                var created = _pictosCreated.GetValueOrDefault(id);
                var cut = baseItem.Value<bool?>("cut") == true;
                var description = created?.Description ?? baseItem.Value<string>("description") ?? "";
                var createdFlag = created is not null || ((_rawItems[id] as JObject) is null && !cut);

                list.Add(new JObject
                {
                    ["id"] = id,
                    ["name"] = created?.Name ?? baseItem.Value<string>("name") ?? id,
                    ["description"] = description,
                    ["tier"] = TierToken(TierOf("Pictos", id)),
                    ["cut"] = cut,
                    ["created"] = createdFlag,
                    ["group"] = created?.Group ?? "",
                    ["multipliers"] = multipliers,
                    ["actual"] = actual,
                    ["chromaMultiplier"] = defs?["ChromaValueMultiplier"]?.DeepClone() ?? JValue.CreateNull(),
                    ["luminaCost"] = effect?["LuminaCost"]?.DeepClone() ?? JValue.CreateNull(),
                    ["unlockSteps"] = effect?["LuminaUnlockStepsCount"]?.DeepClone() ?? JValue.CreateNull(),
                    ["sources"] = SourcesFor(id),
                });
            }

            return JArray.FromObject(list
                .OrderBy(p => p.Value<int?>("tier") ?? 99)
                .ThenBy(p => p.Value<string>("name"), StringComparer.OrdinalIgnoreCase));
        }

        private JArray BuildWeapons()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in CategoryIds("Weapon")) ids.Add(id);
            foreach (var (id, info) in _catalog.Items)
                if (info.Type == "Weapon") ids.Add(id);
            foreach (var prop in _stagedWeapons.Properties()) ids.Add(prop.Name);

            var list = new List<JObject>();
            foreach (var id in ids)
            {
                var baseItem = ItemBase(id);
                var row = _stagedWeapons[id] as JObject;
                var multipliers = StatMultipliers(row?["BaseStatsMultipliers"] as JObject);
                var scaling = new JArray();
                if (row?["AttributeScaling"] is JObject attributes)
                    foreach (var attribute in attributes.Properties())
                    {
                        var key = attribute.Name.Split("::").Last();
                        var grade = attribute.Value.ToString().Split("::").Last();
                        scaling.Add(new JObject
                        {
                            ["attribute"] = Attributes.GetValueOrDefault(key, key),
                            ["grade"] = Grades.GetValueOrDefault(grade, grade),
                        });
                    }

                var passives = new JArray();
                for (var n = 1; n <= 5; n++)
                {
                    var passiveId = row?.Value<string>($"Level{n}Lumina") ?? "";
                    if (passiveId.Length == 0 || passiveId.Equals("None", StringComparison.OrdinalIgnoreCase))
                        continue;
                    passives.Add(new JObject
                    {
                        ["level"] = new[] { 33, 66, 111, 222, 333 }[n - 1],
                        ["id"] = passiveId,
                        ["name"] = PassiveName(passiveId),
                        ["description"] = PassiveDescription(passiveId),
                    });
                }

                list.Add(new JObject
                {
                    ["id"] = id,
                    ["name"] = baseItem.Value<string>("name") ?? id,
                    ["owner"] = WeaponOwner(id),
                    ["tier"] = TierToken(TierOf("Weapon", id)),
                    ["cut"] = baseItem.Value<bool?>("cut") == true,
                    ["dlc"] = baseItem.Value<bool?>("dlc") == true,
                    ["damage"] = Element(row?.Value<string>("BaseDamageType")),
                    ["freeAim"] = Element(row?.Value<string>("FreeAimDamageType")),
                    ["multipliers"] = multipliers,
                    ["chromaMultiplier"] = row?["ChromaValueMultiplier"]?.DeepClone() ?? JValue.CreateNull(),
                    ["scaling"] = scaling,
                    ["passives"] = passives,
                    ["sources"] = SourcesFor(id),
                    ["description"] = baseItem.Value<string>("description") ?? "",
                });
            }

            return JArray.FromObject(list
                .OrderBy(w => w.Value<string>("owner") ?? "")
                .ThenBy(w => w.Value<int?>("tier") ?? 99)
                .ThenBy(w => w.Value<string>("name"), StringComparer.OrdinalIgnoreCase));
        }

        private string WeaponOwner(string id)
        {
            var raw = _rawItems[id] as JObject;
            var pictosData = raw?.Value<string>("Pictos_Data") ?? "";
            foreach (var (owner, tokens) in new[]
                     {
                         ("Lune", new[] { "Lune" }),
                         ("Maelle", new[] { "Maelle" }),
                         ("Sciel", new[] { "Sciel" }),
                         ("Monoco", new[] { "Monoco" }),
                         ("Verso", new[] { "Verso" }),
                         ("Gustave", new[] { "Gustave", "Noah" }),
                     })
                foreach (var token in tokens)
                    if (pictosData.Contains($"_{token}_", StringComparison.Ordinal)
                        || pictosData.Contains($"_{token}", StringComparison.Ordinal))
                        return owner;
            return ItemSubtype.GetValueOrDefault(raw?.Value<string>("Item_Subtype") ?? "", "");
        }

        private JArray BuildKeyItems()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in CategoryIds("Key Item")) ids.Add(id);
            foreach (var (id, info) in _catalog.Items)
                if (info.Type is "Key" or "Key Item") ids.Add(id);
            return JArray.FromObject(ids
                .Select(ItemBase)
                .OrderBy(i => i.Value<string>("name"), StringComparer.OrdinalIgnoreCase));
        }

        private JArray BuildTints()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, item) in _itemCache)
            {
                var type = item.Value<string>("type") ?? "";
                if (type is "Tint" or "Tint Shard") ids.Add(id);
            }
            ids.Add("PartyHealConsumable");
            ids.Add("PartyHealShard");

            var list = new List<JObject>();
            foreach (var id in ids)
            {
                var item = (JObject)ItemBase(id).DeepClone();
                var lower = id.ToLowerInvariant();
                var kind = lower switch
                {
                    _ when lower.Contains("revive") => "Revive",
                    _ when lower.Contains("energy") => "Energy",
                    _ when lower.Contains("partyheal") || lower.Contains("chroma elixir") => "Party",
                    _ when lower.Contains("health") || lower.Contains("healing") => "Healing",
                    _ => "Other",
                };
                item["kind"] = kind;
                list.Add(item);
            }

            return JArray.FromObject(list
                .OrderBy(i => TintOrder(i.Value<string>("kind") ?? "Other"))
                .ThenBy(i => i.Value<string>("name"), StringComparer.OrdinalIgnoreCase));
        }

        private JArray BuildChromaPacks()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, item) in _itemCache)
                if (item.Value<string>("type") == "Chroma Pack" || id.StartsWith("ChromaPack_", StringComparison.Ordinal))
                    ids.Add(id);
            foreach (var (id, info) in _catalog.Items)
                if (info.Type == "Gold" && id.StartsWith("ChromaPack_", StringComparison.Ordinal))
                    ids.Add(id);
            return JArray.FromObject(ids
                .Select(ItemBase)
                .OrderBy(i => i.Value<string>("name"), StringComparer.OrdinalIgnoreCase));
        }

        // ── areas / sorting ──────────────────────────────────────────────

        private Dictionary<string, int> BuildAreaLevels(JArray chests, JArray enemies)
        {
            var chestLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var chest in chests.OfType<JObject>())
            {
                var area = chest.Value<string>("area") ?? "";
                if (area is "" or "Unknown") continue;
                var level = chest.Value<int?>("level") ?? 1;
                chestLevels[area] = chestLevels.TryGetValue(area, out var current)
                    ? Math.Min(current, level)
                    : level;
            }

            var levels = new Dictionary<string, int>(chestLevels, StringComparer.OrdinalIgnoreCase);
            foreach (var enemy in enemies.OfType<JObject>())
            {
                var area = enemy.Value<string>("area") ?? "";
                if (area is "" or "Unknown" || chestLevels.ContainsKey(area)) continue;
                var level = enemy.Value<int?>("level") ?? 1;
                levels[area] = levels.TryGetValue(area, out var current)
                    ? Math.Min(current, level)
                    : level;
            }
            foreach (var (area, level) in KnownAreaLevels)
                if (levels.ContainsKey(area))
                    levels[area] = level;
            return levels;
        }

        private static List<string> BuildAreaOrder(Dictionary<string, int> levels)
        {
            var order = levels.Keys
                .OrderBy(area => SpecialAreas.Contains(area))
                .ThenBy(area => SpecialAreaOrder.GetValueOrDefault(area, 0))
                .ThenBy(area => SpecialAreas.Contains(area) ? 999 : levels.GetValueOrDefault(area, 999))
                .ThenBy(area => area, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var area in SpecialAreas.OrderBy(a => a, StringComparer.OrdinalIgnoreCase))
                if (!order.Contains(area, StringComparer.OrdinalIgnoreCase))
                    order.Add(area);
            return order;
        }

        private static JArray SortEnemies(JArray enemies)
        {
            var result = new JArray();
            foreach (var enemy in enemies.OfType<JObject>()
                         .OrderBy(e => e.Value<string>("area"), StringComparer.OrdinalIgnoreCase)
                         .ThenBy(e => e.Value<int?>("level") ?? 0)
                         .ThenBy(e => e.Value<string>("name"), StringComparer.OrdinalIgnoreCase))
                result.Add(enemy.DeepClone());
            return result;
        }

        private static JArray SortChests(JArray chests, List<string> areaOrder)
        {
            int AreaRank(string area)
            {
                var index = areaOrder.FindIndex(a => string.Equals(a, area, StringComparison.OrdinalIgnoreCase));
                return index >= 0 ? index : 999;
            }

            var result = new JArray();
            foreach (var chest in chests.OfType<JObject>()
                         .OrderBy(c => AreaRank(c.Value<string>("area") ?? ""))
                         .ThenBy(c => c.Value<int?>("level") ?? 0)
                         .ThenBy(c => c.Value<string>("name"), StringComparer.OrdinalIgnoreCase))
                result.Add(chest.DeepClone());
            return result;
        }

        // ── static metadata / loaders ────────────────────────────────────

        private void LoadEncounters()
        {
            foreach (var table in new[] { "DT_jRPG_Encounters", "DT_jRPG_Encounters_CleaTower", "DT_WorldMap_Encounters" })
            {
                var rows = DumpStaged(table);
                if (rows is null) continue;
                foreach (var encounter in rows.Properties())
                {
                    // Runtime battle variants are duplicates of the original
                    // encounter for guide purposes; don't multiply every enemy's
                    // encounter list by BattleVariants.
                    if (encounter.Name.StartsWith(BattleGroupKit.VariantPrefix, StringComparison.Ordinal))
                        continue;
                    if (encounter.Value["Enemies"] is not JObject roster) continue;
                    var over = encounter.Value.Value<int?>("EncounterLevelOverride") ?? 0;
                    _encounterOverridesByName[encounter.Name] = over;
                    foreach (var slot in roster.Properties())
                    {
                        var enemyId = (slot.Value as JObject)?.Value<string>("RowName") ?? "";
                        if (enemyId.Length == 0) continue;
                        if (!_encountersByEnemy.TryGetValue(enemyId, out var list))
                            _encountersByEnemy[enemyId] = list = new List<string>();
                        if (!list.Contains(encounter.Name, StringComparer.Ordinal))
                            list.Add(encounter.Name);
                        if (!_encounterOverridesByEnemy.TryGetValue(enemyId, out var overs))
                            _encounterOverridesByEnemy[enemyId] = overs = new List<int>();
                        if (!overs.Contains(over)) overs.Add(over);
                    }
                }
            }
        }

        private void LoadWeaponEffects(string repo)
        {
            var path = Path.Combine(repo, "work", "weapon_effects.json");
            if (!File.Exists(path)) return;
            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                if (root["effects"] is not JArray effects) return;
                foreach (var effect in effects.OfType<JObject>())
                {
                    var id = effect.Value<string>("id") ?? "";
                    if (id.Length == 0) continue;
                    _weaponEffectNames[id] = effect.Value<string>("displayName") ?? "";
                    _weaponEffectDescriptions[id] = effect.Value<string>("description") ?? "";
                }
            }
            catch { /* optional metadata */ }
        }

        private void LoadPictosCreated(string repo)
        {
            var path = Path.Combine(repo, "work", "pictos_created.csv");
            if (!File.Exists(path)) return;
            try
            {
                var vanillaReference = false;
                foreach (var rawLine in File.ReadLines(path))
                {
                    if (rawLine.Length == 0) continue;
                    var fields = CsvFields(rawLine);
                    if (fields.Count == 0) continue;
                    var id = fields[0].TrimStart('\uFEFF');
                    if (id.StartsWith("# Vanilla reference", StringComparison.OrdinalIgnoreCase))
                    {
                        vanillaReference = true;
                        continue;
                    }
                    if (vanillaReference || fields.Count < 3 || id is "Id" or "") continue;
                    if (!Regex.IsMatch(id, @"^[A-Za-z0-9_+\-]+$")) continue;
                    _pictosCreated[id] = new CreatedPictos(
                        fields[1],
                        fields[2],
                        fields.Count > 4 ? fields[4] : "");
                }
            }
            catch { /* optional metadata */ }
        }

        private JObject StatMultipliers(JObject? source)
        {
            var result = new JObject();
            if (source is null) return result;
            foreach (var prop in source.Properties())
            {
                var key = prop.Name.Split("::").Last();
                if (!StatLabels.TryGetValue(key, out var label)) continue;
                try { result[label] = prop.Value.Value<double>(); }
                catch { /* ignore */ }
            }
            return result;
        }

        private List<string> CategoryIds(string category) =>
            _catalog.Categories.TryGetValue(category, out var ids) ? ids : new List<string>();

        private static int TintOrder(string kind) => kind switch
        {
            "Healing" => 0, "Energy" => 1, "Revive" => 2, "Party" => 3, _ => 4,
        };

        private JObject? DumpStaged(string table)
        {
            var asset = FileCopy.FindUnder(_modDir, table + ".uasset");
            if (asset is not null) return DumpAsset(asset);
            return DumpVanilla(table);
        }

        private static JObject? DumpVanilla(string table)
        {
            var asset = FileCopy.FindTablesAsset(table + ".uasset");
            return asset is null ? null : DumpAsset(asset);
        }

        private static JObject? DumpAsset(string asset)
        {
            try { return JObject.Parse(new DataTableEditor(asset).Dump()); }
            catch { return null; }
        }

        private static Dictionary<string, string> LoadStringTable(string table)
        {
            var asset = FileCopy.FindTablesAsset(table + ".uasset");
            if (asset is null) return new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                return new Dictionary<string, string>(DataTableEditor.OpenStringTable(asset), StringComparer.Ordinal);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        private static JObject LoadJson(string path)
        {
            try { return JObject.Parse(File.ReadAllText(path)); }
            catch { return new JObject(); }
        }

        private static List<string> CsvFields(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(c);
            }
            fields.Add(current.ToString());
            return fields;
        }

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

        private static string StripRef(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var text = value.Trim();
            if (text.Contains(']')) text = text[(text.LastIndexOf(']') + 1)..].Trim();
            return text;
        }

        private static string ResolveStRef(string? value, Dictionary<string, string> table)
        {
            var key = StripRef(value);
            if (key.Length == 0) return "";
            return table.GetValueOrDefault(key)
                   ?? table.GetValueOrDefault(value ?? "")
                   ?? "";
        }

        private static string Element(string? raw)
        {
            var key = (raw ?? "").Split("::").Last();
            return Elements.GetValueOrDefault(key, string.IsNullOrEmpty(key) ? "—" : key);
        }

        private static string PrettyId(string raw)
        {
            var s = raw;
            foreach (var prefix in new[] { "WMV_", "WM_", "PETANK_", "QUEST_", "Chest_", "DA_", "BP_" })
                if (s.StartsWith(prefix, StringComparison.Ordinal))
                    s = s[prefix.Length..];
            s = Regex.Replace(s, "_ALPHA$", " Alpha");
            s = Regex.Replace(s, @"_\d+$", "");
            return Humanize(s);
        }

        private static string Humanize(string raw)
        {
            var s = raw.Replace('_', ' ').Trim();
            s = Regex.Replace(s, @"(?<=[a-z])(?=[A-Z])", " ");
            s = Regex.Replace(s, @"\s+", " ");
            return s;
        }

        private static string ChestName(string chestId, string area)
        {
            var match = Regex.Match(chestId, @"_(\d+)$");
            if (match.Success && area is not "" and not "Unknown")
                return $"{area} — Chest {match.Groups[1].Value}";
            var stem = chestId.StartsWith("Chest_", StringComparison.Ordinal) ? chestId[6..] : chestId;
            return Humanize(stem);
        }

        private static string CleanItemName(string raw)
        {
            var s = (raw ?? "").Trim();
            return Regex.Replace(
                s,
                @"\s+\((?:Pictos|Weapon|Key Item|Key|Upgrade Material|Consumable|Broken [^)]+|Cut Content|DLC Item|Inventory)\)$",
                "",
                RegexOptions.IgnoreCase);
        }

        private static readonly Dictionary<string, string> RawEnemyKeys = new()
        {
            ["1"] = "Max HP", ["2"] = "Max AP", ["3"] = "Physical Attack",
            ["13"] = "Free Aim Attack", ["10"] = "Accuracy", ["4"] = "Physical Defense",
            ["14"] = "Magical Defense", ["7"] = "Speed", ["6"] = "Critical Rate",
            ["15"] = "Mastery",
        };

        private static readonly Dictionary<string, string> StatLabels = new()
        {
            ["NewEnumerator1"] = "Max HP", ["NewEnumerator2"] = "Max AP",
            ["NewEnumerator3"] = "Physical Attack", ["NewEnumerator4"] = "Physical Defense",
            ["NewEnumerator6"] = "Critical Rate", ["NewEnumerator7"] = "Speed",
            ["NewEnumerator10"] = "Accuracy", ["NewEnumerator13"] = "Free Aim Attack",
            ["NewEnumerator14"] = "Magical Defense", ["NewEnumerator15"] = "Mastery",
        };

        private static readonly Dictionary<string, string> RawItemType = new()
        {
            ["NewEnumerator0"] = "Weapon", ["NewEnumerator7"] = "Consumable",
            ["NewEnumerator10"] = "Pictos", ["NewEnumerator11"] = "Key",
            ["NewEnumerator12"] = "Inventory", ["NewEnumerator14"] = "Shard",
            ["NewEnumerator15"] = "Gold", ["NewEnumerator16"] = "CharacterCustomization",
            ["NewEnumerator17"] = "SkillUnlocker",
        };

        private static readonly Dictionary<string, string> ItemSubtype = new()
        {
            ["NewEnumerator0"] = "Lune", ["NewEnumerator1"] = "Monoco", ["NewEnumerator2"] = "Sciel",
            ["NewEnumerator11"] = "Consumable", ["NewEnumerator14"] = "Maelle",
            ["NewEnumerator15"] = "Pictos", ["NewEnumerator16"] = "Gustave",
            ["NewEnumerator18"] = "Key", ["NewEnumerator19"] = "Inventory",
            ["NewEnumerator21"] = "Verso", ["NewEnumerator22"] = "Journal",
            ["NewEnumerator23"] = "Music Record",
        };

        private static readonly Dictionary<string, string> Elements = new()
        {
            ["NewEnumerator0"] = "Fire", ["NewEnumerator1"] = "Ice",
            ["NewEnumerator2"] = "Lightning", ["NewEnumerator3"] = "Earth",
            ["NewEnumerator4"] = "Dark", ["NewEnumerator5"] = "Light",
            ["NewEnumerator6"] = "Physical", ["NewEnumerator7"] = "Void",
            ["NewEnumerator9"] = "Invalid",
        };

        private static readonly Dictionary<string, string> Attributes = new()
        {
            ["NewEnumerator0"] = "Vitality", ["NewEnumerator1"] = "Might",
            ["NewEnumerator2"] = "Intelligence", ["NewEnumerator3"] = "Agility",
            ["NewEnumerator4"] = "Defense", ["NewEnumerator5"] = "Luck",
            ["NewEnumerator6"] = "Invalid",
        };

        private static readonly Dictionary<string, string> Grades = new()
        {
            ["NewEnumerator0"] = "Invalid", ["NewEnumerator1"] = "D",
            ["NewEnumerator2"] = "C", ["NewEnumerator3"] = "B",
            ["NewEnumerator4"] = "A", ["NewEnumerator5"] = "S",
        };

        private static readonly string[] CategoryPriority =
        {
            "Key Item", "Weapon", "Pictos", "Music Record", "Journal", "Skill Unlock",
            "Merchant Unlock", "Upgrade Material", "Consumable", "Cosmetic",
            "Lovely Foot", "Item_Pickup", "DLC Items", "Cut Content Items",
        };

        private sealed record CreatedPictos(string Name, string Description, string Group);
    }
}
