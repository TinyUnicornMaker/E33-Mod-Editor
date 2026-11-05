using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core.Loot;

/// <summary>
/// Vanilla chests, enemy drops and merchants, tagged with the original
/// randomizer's item types / pictos-weapon tiers and an in-game area.
/// Frequencies are counts of cooked slots (and expected rolls for % drops).
/// </summary>
public sealed class LootCatalog
{
    public const int MaxTier = 5;
    /// <summary>Vanilla item-level cap: weapons/pictos roll at 1–33, enemy levels run to ~99.
    /// Runtime weapon drops may then be scaled by WeaponDropLevelKit (pictos never are).</summary>
    public const int MaxItemLevel = 33;

    public sealed class ItemInfo
    {
        public string CodeName { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Type { get; init; } = "Unknown";
        public bool CutContent { get; init; }
        public bool HasQuantities { get; init; }
        public int? PictosTier { get; init; }
        public int? WeaponTier { get; init; }
    }

    public sealed class EnemyInfo
    {
        public string CodeName { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Archetype { get; init; } = "Regular";
        /// <summary>Curated encounter level from enemy_data.json; 0 if unknown.</summary>
        public int Level { get; init; }
        public bool CutContent { get; init; }
    }

    public sealed class Slot
    {
        public string ItemId { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string ItemType { get; set; } = "";
        public int Quantity { get; init; } = 1;
        public double Chance { get; init; } = 100;
        public int Weight { get; init; } = 1;
        public bool LootTableRef { get; init; }
        public string? LootTable { get; init; }
        /// <summary>DataTableEditor path from the row root, e.g. LootTableSections.0.LootEntries.0.ItemID.</summary>
        public string Path { get; init; } = "";
        public string Table { get; init; } = "";
        public string RowName { get; init; } = "";
    }

    public sealed class Check
    {
        public string Source { get; init; } = ""; // Chest / Paint Cages / Enemy / Merchant
        public string Id { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Area { get; init; } = "";
        public int AreaLevel { get; init; }
        /// <summary>Normalised area bucket shared by chests and enemies of the same
        /// place (see <see cref="AreaKeyOf"/>), used to spread gear per area.</summary>
        public string AreaKey { get; init; } = "";
        public int Tier { get; init; }
        public string MerchantFile { get; init; } = "";
        public string MerchantPath { get; init; } = "";
        /// <summary>Row's IsBoss flag (enemy sources); used to mirror the
        /// realizer's boss promotion in the distribution model.</summary>
        public bool IsBoss { get; init; }
        public List<Slot> Slots { get; init; } = new();
        public string PrimaryType =>
            Slots.FirstOrDefault(s => !s.LootTableRef)?.ItemType
            ?? Slots.FirstOrDefault()?.ItemType
            ?? "";
        public string PrimaryItem =>
            Slots.FirstOrDefault(s => !s.LootTableRef)?.ItemId
            ?? Slots.FirstOrDefault()?.LootTable
            ?? "";
    }

    public sealed class FreqRow
    {
        public string Group { get; init; } = "";
        public string Label { get; init; } = "";
        public int Slots { get; init; }
        public double Expected { get; init; }
        public string Note { get; init; } = "";
    }

    public Dictionary<string, ItemInfo> Items { get; }
    private readonly Dictionary<string, EnemyInfo> _enemies;
    public IReadOnlyDictionary<string, EnemyInfo> Enemies => _enemies;
    public IReadOnlyDictionary<string, List<string>> Categories { get; }
    public ItemTiers ItemTiers { get; }
    public WeaponEffectTiers EffectTiers { get; }
    public WeaponEffectCharacters EffectCharacters { get; }
    public WeaponEffectArchetypes EffectArchetypes { get; }
    public WeaponEffectConditions EffectConditions { get; }
    public IReadOnlyDictionary<string, List<string>> Tiers => ItemTiers.Flat;
    public IReadOnlyDictionary<string, int> AreaLevels { get; }
    public List<Check> Checks { get; } = new();
    public List<FreqRow> Frequencies { get; } = new();

    public HashSet<string> BrokenItems { get; } = new(BrokenItemList, StringComparer.Ordinal);
    public HashSet<string> ProtectedItems { get; }

    public LootCatalog()
    {
        ItemTiers = ItemTiers.Load();
        EffectTiers = WeaponEffectTiers.Load();
        EffectCharacters = WeaponEffectCharacters.Load();
        EffectArchetypes = WeaponEffectArchetypes.Load();
        EffectConditions = WeaponEffectConditions.Load();
        foreach (var spec in WeaponEffectsCreateKit.Specs)
        {
            var initial = string.IsNullOrEmpty(spec.Character)
                ? WeaponEffectCharacters.Roster
                : new[] { spec.Character };
            EffectCharacters.Ensure(spec.Id, initial);
        }
        EffectCharacters.ApplyGustaveRules(EffectTiers);
        Items = LoadItems();
        _enemies = LoadEnemies();
        Categories = LoadJson<Dictionary<string, List<string>>>("item_categories.json");
        AreaLevels = LoadAreaLevels();
        ProtectedItems = BuildProtected();
    }

    /// <summary>Registers a runtime-generated enemy variant (e.g. an endgame
    /// T5 clone) so tier / archetype handling treats it like a real row.</summary>
    public void RegisterEnemy(string id, EnemyInfo info) => _enemies[id] = info;

    public static int TierFromLevel(int level)
    {
        if (level < 1) level = 1;
        var itemLevel = level / 3.0;
        return Math.Clamp((int)(itemLevel / (MaxItemLevel / (double)MaxTier)) + 1, 1, MaxTier);
    }

    /// <summary>
    /// Item-level bands per loot tier (weapons/pictos grant at L1 and upgrade
    /// with tier-appropriate catalysts; bands document the intended power:
    /// T1 1-8, T2 9-13, T3 14-20, T4 21-26, T5 27-33).
    /// </summary>
    public static (int Lo, int Hi) TierLevelRange(int tier) => tier switch
    {
        1 => (1, 8),
        2 => (9, 13),
        3 => (14, 20),
        4 => (21, 26),
        _ => (27, 33),
    };

    /// <summary>Random item level inside the tier band (T1 1-8 … T5 27-33).</summary>
    public static int TierToItemLevel(int tier, Random rng)
    {
        var (lo, hi) = TierLevelRange(Math.Clamp(tier, 1, MaxTier));
        return rng.Next(lo, hi + 1);
    }

    /// <summary>
    /// Explicit per-enemy tier overrides: these win over the area tier for
    /// both drops and monster stats. High-level fights placed in low-level
    /// geography (Ono Puncho 86 in the village, Sakapatate alphas 65 filed
    /// under Ancient Sanctuary) keep their row tier instead of dropping to
    /// the area band.
    /// </summary>
    public static readonly Dictionary<string, int> AreaTierOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Quest_GestralSumo9999"] = 5,
        ["SM_Abbest_Alpha"] = 4, // Chromatic Abbest easter egg: T4 loot/stats, T4 (x10) boss HP
        ["AS_PotatoBag_Tank_ALPHA"] = 4,
        ["AS_PotatoBag_Ranger_ALPHA"] = 4,
        ["AS_PotatoBag_Mage_ALPHA"] = 4,
        ["QUEST_Golgra_SacredRiver"] = 4,
        // Chromatic Reaper Cultist: solo world-map fight (WM_CultistFlying_Alpha)
        // held at T3 over its T2 Stone Wave Cliffs band — stats and rewards.
        ["SC_CultistFlying_ALPHA"] = 3,
        // Chromatic Greatsword Cultist (Sirene): SC_ row filed under the T2
        // Stone Wave Cliffs band, fought at Sirene level — pin to T3.
        ["SC_CultistHeavy_Alpha"] = 3,
        // Visages island is a T3 area: every MF_ row — the mask trio, the two
        // Axon fights (decoy Visages + true Mask Keeper) and the island regulars
        // (including their chromatic variants) — is pinned to T3 for stats and
        // drops. The actor folders these rows live in (manyfaces/axons) also hold
        // Renoir's Drafts / Chosen Path / Frozen Hearts rows, so this stays an
        // explicit row list instead of an area-key floor.
        // The island's chromatic BOSSES are the exception: they are fought in
        // T4/T5 endgame areas, not on the T3 island, so they are deliberately
        // NOT pinned and keep their row tier — which puts them on the T4/T5 boss
        // curve like every other chromatic alpha:
        //   MF_Boucheclier_ALPHA  L61 Isle of the Eyes (T4)
        //   MF_Chapelier_ALPHA    L65 The Crows        (T4)
        //   MF_Moissonneuse_ALPHA L99 Endless Tower     (T5)
        // (Cut-content MF_Contorsionniste_Alpha stays pinned with the island.)
        ["MF_Axon_MaskKeeper_VisagesPhase2"] = 3, ["MF_Axon_Visages"] = 3,
        ["MF_Boucheclier"] = 3, ["MF_BoucheclierAnger"] = 3,
        ["MF_Boucheclier_MaskAnger"] = 3,
        ["MF_Chapelier"] = 3, ["MF_ChapelierSadness"] = 3,
        ["MF_Chapelier_MaskSadness"] = 3,
        ["MF_Contorsionniste"] = 3, ["MF_Contorsionniste_Alpha"] = 3,
        ["MF_Mime"] = 3,
        ["MF_MaskAnger"] = 3, ["MF_MaskJoy"] = 3, ["MF_MaskSadness"] = 3,
        ["MF_Moissonneuse"] = 3, ["MF_MoissonneuseJoy"] = 3, ["MF_MoissonneuseMaskJoy"] = 3,
        // Native Grosse Tete keeps its own T4 band (tagged L55 is mid-T3 by the
        // row-tier formula, so it needs the pin). Its world-map venue clone is
        // deliberately NOT pinned: WM_GrossTete runs at level 50 and must pay
        // T3, because the fight is a dodge-only no-hit farm.
        ["MF_GrossTete"] = 4,
        // Sirene arena ("Sirène's Coliseum") is a T3 area: every Sirene row —
        // both bosses, the regulars and the chromatic variants — is pinned to T3
        // for stats and drops, with one exception: Chromatic Glissando fights
        // at L65, so it keeps its T4 row tier (boss rows beat the area band)
        // and takes the T4 boss curve like any other chromatic alpha.
        // The actor folders these rows live in (axons,
        // forgottenbattlefield, gestrals, sirene) also hold other areas' rows,
        // so this stays an explicit row list instead of an area-key floor.
        // SI_Ballet/SI_Benisseur are also used by world-map fights; they are
        // released from those curated lists (worldmap_encounter_tiers.json) so
        // this pin is what the encounter-tier check sees.
        ["SI_Axon_Sirene"] = 3, ["SI_Tisseur"] = 3,
        ["SI_Ballet"] = 3, ["SI_Ballet_Melee"] = 3,
        ["SI_Glissando"] = 3,
        ["SI_Chorale"] = 3, ["SI_Benisseur"] = 3, ["SI_Mime"] = 3,
        ["SI_Ballet_ALPHA_Charm"] = 3, ["SI_Ballet_ALPHA_Range"] = 3, ["SI_Ballet_ALPHA_Melee"] = 3,
        // Dark Shores is a T4 area, but Noire is also used by the T2 world-map
        // fight WM_Noire_Danseuse_Benisseur. Released from that curated list
        // (worldmap_encounter_tiers.json) and pinned here so its native area
        // tier is what the encounter-tier check sees.
        ["DS_Noire"] = 4,
        // Shared-actor-folder rows in T4/T5 areas: the actor path (sirene,
        // forgottenbattlefield, reacher, lumiere, manyfaces, ancientsanctuary,
        // chevaliere) would otherwise hand a region row the lower band of
        // whatever area folder it physically lives in. Pin each row to its own
        // region so its drops sit on the area tier.
        // Flying Cemetery (T4 — forgottenbattlefield folder):
        ["FC_ChalierHammer"] = 4, ["FC_ChalierSpear"] = 4, ["FC_ChalierSword"] = 4,
        // Renoir's Drafts (T5 — sirene / forgottenbattlefield / reacher / lumiere folders):
        ["RD_Aberration"] = 5, ["RD_Ballet"] = 5, ["RD_Benisseur"] = 5,
        ["RD_Echassier"] = 5, ["RD_OrphelinHammer"] = 5, ["RD_OrphelinSpoon"] = 5,
        ["RD_Veilleur"] = 5,
        // Endless Night Sanctuary (T4 — ancientsanctuary folder):
        ["TS_PotatoBag_Mage_Twilight"] = 4, ["TS_PotatoBag_Ranger_Twilight"] = 4,
        ["TS_PotatoBag_Tank_Twilight"] = 4,
        // Crimson Forest (T4 — chevaliere folder): the Gold Chevaliere and its
        // chromatic variant live under …/Enemies/Chevaliere/… beside the Yellow
        // Harvest / Old Lumiere rows, which would hand them the T2 band. The
        // regular row is also borrowed by two world-map fights, so it is listed
        // in WorldMapVariantKit.DoubleDipped (the world fights get a T3 clone)
        // and was released from worldmap_encounter_tiers.json; without both the
        // curated fight tier (checked first) pins it back to T2.
        ["YF_Chevaliere_C"] = 4, ["YF_Chevaliere_C_Alpha"] = 4,
        // Gradient-attack tutorial Stalact: fought in Monoco's Station (25, T2),
        // but its actor folder (…/Enemies/MonocoMountain/…) buckets it to
        // Monoco Mountain (63, T4), so as an IsBoss row it took the T4 boss
        // curve (~53M HP). Pin to T2 for T2 boss stats instead.
        ["MM_Stalact_GradientAttackTutorial"] = 2,
        // White-nevron bosses below T4 use their tier's original boss HP
        // scaling, never the T4/T5 curve. Their actor folders live in other
        // regions (Jar in YellowForest, Rocher in Sea_Cliff), which would
        // otherwise lift them a tier. Pin each to the area it is fought in.
        ["QUEST_JarNeedLight"] = 1,
        ["QUEST_Rocher_HexgaQuest"] = 1,
        ["QUEST_HexgaLuster"] = 1,
        // White Portier (Esoteric Ruins, level 28) is a regular T2 boss, not a
        // T5 one: the old curated level 80 lifted it onto the boss curve.
        ["Portier_DoorSoul"] = 2,
        // Venue clones for world-map fights that are deliberately T4 despite
        // their venue level (50 is T3 by the band formula). BossVenueKit
        // clones these rows; the pin makes the clone curve at its venue level.
        ["BV_SM_Boss_Eveque_Ice_ALPHA_50"] = 4,
        // Grosse Tete's WM fight is intentionally T3: he can be defeated by
        // repeatedly dodging his attacks without landing a hit, so it must not
        // pay a T4 package. Native MF_GrossTete / CFH_GrossTete keep their own
        // bands (their perfect-catalyst quantity is capped in RandomizerKit).
        // ML Gargant's design level 41 is T3 by the band formula but the fight
        // is a T4 boss; pin the venue clone so it keeps the T4 curve.
        ["BV_ML_Gargant_Gold_41"] = 4,
    };

    /// <summary>
    /// Explicit fight levels for rows whose <c>DT_jRPG_Enemies.Level</c> is a
    /// template rather than the level the encounter runs at. The Monolith
    /// interior's tainted/gold rows carry their source-area level (3/8/13/…)
    /// and its bosses are inflated (Painted Renoir 28, Paintress 60), but the
    /// whole interior resolves to its <c>LevelScaling</c> of 43. The runtime
    /// builds stats as <c>archetype[EncounterLevel] × EnemyScaling</c>, so both
    /// the effective-stat helper and the absolute-HP override read this.
    /// </summary>
    /// <summary>Act 3 Lumière is overridden to this level (loot band, stat band
    /// and encounter level for its finale bosses).</summary>
    public const int Act3LumiereLevel = 99;

    public static readonly Dictionary<string, int> EncounterLevelOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MO_Boss_Paintress"] = 43,
        // Act 3 Lumière finale trio: the mod treats Act 3 as the level-99 band.
        // Echassier is the exception: its table level is 0 and, unlike the
        // instanced finale fights, its world fight spawns at the zone level
        // (48 — observed ~61M HP against a 99-derived pin, exactly
        // Alpha[48] × 223.99), so its absolute pins derive at 48.
        ["LU_EchassierAlpha"] = 48,
        ["L_Creation"] = Act3LumiereLevel,
        ["L_Boss_Curator"] = Act3LumiereLevel,
        // Heart of the Canvas narrative finale is a level-55 area; the rows
        // carry a level-45 template, so pin the derivation to the arena level.
        ["FinalBoss_Verso"] = 55,
        ["FinalBoss_Maelle"] = 55,
    };

    /// <summary>Fight level for effective-stat maths: an explicit encounter
    /// override wins, else the row's own table level.</summary>
    public static int EncounterLevelForEnemy(string id, int tableLevel) =>
        EncounterLevelOverrides.TryGetValue(id, out var lvl) && lvl > 0 ? lvl : tableLevel;

    /// <summary>
    /// Curated per-encounter tiers for world-map fights whose rosters mix
    /// regions (Data/worldmap_encounter_tiers.json): a fight spawns one enemy
    /// from The Reacher, one from Frozen Hearts and one from the Forgotten
    /// Battlefield, so tiering each by its own region hands out several tiers
    /// from one fight. The curation pins the whole roster to the fight's tier.
    /// Loot is keyed by enemy row, not by encounter, so a row used by several
    /// curated fights takes the majority tier (tie → lowest) and the rows that
    /// disagree are listed in <see cref="EncounterTierConflicts"/>.
    /// </summary>
    public static IReadOnlyList<EncounterTierEntry> EncounterTiers { get; } = LoadEncounterTiers();

    private static readonly (Dictionary<string, int> ByEnemy,
        List<EncounterTierConflict> Conflicts) EncounterTierMap = BuildEncounterTierByEnemy();

    public static IReadOnlyDictionary<string, int> EncounterTierByEnemy => EncounterTierMap.ByEnemy;

    public static IReadOnlyList<EncounterTierConflict> EncounterTierConflicts => EncounterTierMap.Conflicts;

    public sealed record EncounterTierEntry(string Encounter, int Tier, IReadOnlyList<string> Enemies);

    /// <summary>An enemy row used by curated fights that disagree on the tier.</summary>
    public sealed record EncounterTierConflict(
        string Enemy, IReadOnlyList<(string Encounter, int Tier)> Votes, int Tier);

    private static IReadOnlyList<EncounterTierEntry> LoadEncounterTiers()
    {
        var list = new List<EncounterTierEntry>();
        var root = JObject.Parse(ReadEmbedded("worldmap_encounter_tiers.json"));
        if (root["encounters"] is not JObject encs) return list;
        foreach (var enc in encs.Properties())
        {
            var tier = enc.Value.Value<int?>("tier") ?? 0;
            if (tier < 1 || tier > MaxTier) continue;
            var enemies = (enc.Value["enemies"] as JArray)?
                .Select(t => t.ToString()).Where(s => s.Length > 0).ToList() ?? new List<string>();
            if (enemies.Count == 0) continue;
            list.Add(new EncounterTierEntry(enc.Name, tier, enemies));
        }
        return list;
    }

    /// <summary>Flattens the curation to enemy row → tier (majority wins, tie → highest).</summary>
    private static (Dictionary<string, int> ByEnemy,
        List<EncounterTierConflict> Conflicts) BuildEncounterTierByEnemy()
    {
        var votes = new Dictionary<string, List<(string Encounter, int Tier)>>(StringComparer.Ordinal);
        foreach (var e in EncounterTiers)
            foreach (var id in e.Enemies)
            {
                if (!votes.TryGetValue(id, out var v)) votes[id] = v = new();
                v.Add((e.Encounter, e.Tier));
            }

        var byEnemy = new Dictionary<string, int>(StringComparer.Ordinal);
        var conflicts = new List<EncounterTierConflict>();
        foreach (var (id, v) in votes)
        {
            // Majority wins; on a tie keep the higher tier so a row shared with a
            // higher-tier fight is never nerfed below that fight's intent.
            var chosen = v.GroupBy(x => x.Tier)
                .OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key;
            byEnemy[id] = chosen;
            if (v.Select(x => x.Tier).Distinct().Count() > 1)
                conflicts.Add(new EncounterTierConflict(id, v, chosen));
        }
        return (byEnemy, conflicts);
    }

    /// <summary>
    /// Area level for a normalised area bucket: chest-area mapping first,
    /// then location LevelScaling minimum, else 0 (unknown).
    /// </summary>
    public int AreaLevelForKey(string areaKey)
    {
        var best = 0;
        foreach (var (key, mapped) in ChestAreas)
        {
            if (AreaKeyOf(key) == areaKey || AreaKeyOf(mapped.Area) == areaKey)
            {
                if (best == 0 || mapped.Level < best) best = mapped.Level;
            }
        }
        foreach (var (token, lv) in AreaLevels)
        {
            if (AreaKeyOf(token) == areaKey && (best == 0 || lv < best)) best = lv;
        }
        foreach (var c in Checks)
        {
            if ((c.Source == "Chest" || c.Source == PaintCageSource)
                && c.AreaKey == areaKey && (best == 0 || c.AreaLevel < best))
                best = c.AreaLevel;
        }
        return best;
    }

    /// <summary>
    /// Minimum enemy tier per area bucket (normalised key). The enemy area
    /// band resolves to the MINIMUM of the chest-area mapping, location
    /// LevelScaling and chest checks, so a low spawn-scaling floor (e.g.
    /// SeaCliff 19) can pin enemies a tier below their own chests/shops
    /// (SeaCliff chests are T2 at area level 24). Floors keep the two in
    /// sync without touching runtime levels, XP/chroma or difficulty.
    /// </summary>
    public static readonly Dictionary<string, int> AreaTierFloors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["seacliff"] = 2,
        // Reacher: spawn/level scaling is 58 (→ T3) while the area's chests were
        // lifted to the T4 band; the floor keeps the monsters in sync.
        ["reacher"] = 4,
    };

    /// <summary>
    /// Tier controlling an enemy's drops and monster stats: biome remap rows
    /// (Act 3 Lumiere 99, Frozen Hearts +16) keep their remapped row tier; then
    /// a curated world-map fight tier or an explicit override wins; otherwise
    /// the area band. Boss rows never drop below their row tier so encounter
    /// guarantees (ReserveBossGear) hold.
    /// </summary>
    public int AreaTierForEnemy(string id, EnemyInfo? enemy, int rowTier, bool isBossRow = false)
    {
        // Biome remaps (Act 3 Lumiere 99, Frozen Hearts +16) are native tiers
        // and win over the world-map curation: a Frozen Hearts / Act 3 mob keeps
        // its remapped row tier even when the same row also appears in a curated
        // mixed-region world-map fight. Otherwise the native area would hand out
        // the fight's lower tier (e.g. T2 catalysts in the T4 Frozen Hearts).
        if (enemy is not null && (IsAct3Lumiere(enemy) || IsFrozenHearts(enemy)))
            return Math.Clamp(rowTier, 1, MaxTier);
        // A curated world-map fight tier wins over the area band: those fights
        // mix enemy regions, so the per-row region tier would hand out several
        // tiers from a single fight (see EncounterTiers).
        if (EncounterTierByEnemy.TryGetValue(id, out var encounterTier))
            return Math.Clamp(encounterTier, 1, MaxTier);
        if (AreaTierOverrides.TryGetValue(id, out var forced))
            return Math.Clamp(forced, 1, MaxTier);
        var areaKey = AreaKeyFor(id);
        var areaLevel = string.IsNullOrEmpty(areaKey) ? 0 : AreaLevelForKey(areaKey);
        int tier;
        if (areaLevel <= 0) tier = rowTier;
        else
        {
            var areaTier = TierFromLevel(areaLevel);
            tier = (isBossRow && rowTier > areaTier) ? rowTier : areaTier;
        }
        if (!string.IsNullOrEmpty(areaKey) && AreaTierFloors.TryGetValue(areaKey, out var floor))
            tier = Math.Max(tier, floor);
        return Math.Clamp(tier, 1, MaxTier);
    }

    public static bool IsAct3Lumiere(EnemyInfo? enemy) =>
        enemy?.DisplayName.Contains("(Lumiere - Act 3)", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsFrozenHearts(EnemyInfo? enemy) =>
        enemy?.DisplayName.Contains("(Frozen Hearts)", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Catalog fight level before area retarget (Act 3 → 99, Frozen Hearts +16).</summary>
    public static int CatalogFightLevel(int tableLevel, EnemyInfo? enemy)
    {
        if (enemy?.DisplayName.Contains("(Lumiere - Act 1)", StringComparison.OrdinalIgnoreCase) == true)
            return 1;
        if (enemy is { Level: > 0 })
            return enemy.Level;
        return tableLevel > 0 ? tableLevel : 1;
    }

    /// <summary>Loot / randomizer-band level. Act 3 Lumière is T5 (99); Frozen Hearts sits at the top of T4.</summary>
    public static int RemapLootLevel(EnemyInfo? enemy, int catalogLevel)
    {
        if (IsAct3Lumiere(enemy)) return Act3LumiereLevel;
        if (IsFrozenHearts(enemy)) return Math.Clamp(catalogLevel + 16, 1, 79);
        return catalogLevel;
    }

    /// <summary>
    /// Encounter level used for loot/stat tiers. <c>DT_jRPG_Enemies.Level</c>
    /// is the enemy-type template: copies of the same Nevron share one number,
    /// and arena/boss rows are often inflated (Gestral Village Golgra is 80 in
    /// the table, 15 in the village). Per-row levels in enemy_data.json are
    /// tagged to the fight they appear in, so they win when present. Prologue
    /// fights stay level 1. Act 3 Lumière remaps to 99 (T5); Frozen Hearts
    /// shifts +16 so the area caps at 79 (high T4).
    /// </summary>
    public static int ResolveEnemyLevel(int tableLevel, EnemyInfo? enemy) =>
        RemapLootLevel(enemy, CatalogFightLevel(tableLevel, enemy));
    public bool IsCut(string code) =>
        Items.TryGetValue(code, out var i) && i.CutContent;

    public IEnumerable<string> PoolFor(string type, int tier)
    {
        var t = Math.Clamp(tier, 1, MaxTier);
        if (type is "Pictos")
        {
            if (Tiers.TryGetValue($"Pictos_Tier{t}", out var p))
                return FilterPool(p);
        }
        else if (type is "Weapon")
        {
            if (Tiers.TryGetValue($"Weapon_Tier{t}", out var w))
                return FilterPool(w);
        }

        return Items.Values
            .Where(i => TypeOf(i.CodeName) == type
                        && !BrokenItems.Contains(i.CodeName)
                        && !ProtectedItems.Contains(i.CodeName))
            .Select(i => i.CodeName);
    }

    // Tier-list membership is the only content gate: cut pictos/weapons are
    // distributable when the user sorts them into a tier (cut ENEMIES stay
    // excluded in RandomizerKit).
    private IEnumerable<string> FilterPool(IEnumerable<string> ids) =>
        ids.Where(id =>
            !BrokenItems.Contains(id) && !ProtectedItems.Contains(id));

    public ItemInfo Describe(string code)
    {
        if (Items.TryGetValue(code, out var i)) return i;
        return new ItemInfo
        {
            CodeName = code,
            DisplayName = Humanize(code),
            Type = InferType(code),
        };
    }

    public string TypeOf(string code)
    {
        if (string.IsNullOrEmpty(code)) return "";
        foreach (var (cat, ids) in Categories)
        {
            if (ids.Contains(code))
            {
                if (cat is "Key Item" or "Music Record" or "Journal" or "Skill Unlock"
                    or "Merchant Unlock" or "Lovely Foot" or "Cosmetic" or "DLC Items"
                    or "Cut Content Items")
                    return cat;
            }
        }
        if (Items.TryGetValue(code, out var i) && !string.IsNullOrEmpty(i.Type) && i.Type != "Invalid")
            return NormalizeType(i.Type, code);
        return InferType(code);
    }

    /// <summary>
    /// Keys, journals, records, skill/merchant unlocks, cosmetics and quest
    /// items stay vanilla in chests, drops and shops.
    /// </summary>
    public bool IsProtected(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;
        if (ProtectedItems.Contains(code)) return true;
        return TypeOf(code) is "Key Item" or "Music Record" or "Journal"
            or "Skill Unlock" or "Merchant Unlock" or "Lovely Foot" or "Cosmetic";
    }

    public void Rebuild(JObject chests, JObject enemies, IReadOnlyList<(string File, JObject Rows)> merchants)
    {
        Checks.Clear();
        _areaKeyById = null;
        AddChests(chests);
        AddEnemies(enemies);
        foreach (var (file, rows) in merchants)
            AddMerchant(file, rows);
        RebuildFrequencies();
    }

    public IEnumerable<string> PoolFor(string type, int tier, bool includeCut)
    {
        var t = Math.Clamp(tier, 1, MaxTier);
        if (type is "Pictos")
        {
            if (Tiers.TryGetValue($"Pictos_Tier{t}", out var p))
                return FilterPool(p, includeCut);
        }
        else if (type is "Weapon")
        {
            if (Tiers.TryGetValue($"Weapon_Tier{t}", out var w))
                return FilterPool(w, includeCut);
        }

        return Items.Values
            .Where(i => TypeOf(i.CodeName) == type
                        && !BrokenItems.Contains(i.CodeName)
                        && (includeCut || !i.CutContent)
                        && !ProtectedItems.Contains(i.CodeName))
            .Select(i => i.CodeName);
    }

    private IEnumerable<string> FilterPool(IEnumerable<string> ids, bool includeCut) =>
        ids.Where(id =>
        {
            if (BrokenItems.Contains(id) || ProtectedItems.Contains(id)) return false;
            if (!Items.TryGetValue(id, out var i)) return true;
            return includeCut || !i.CutContent;
        });

    public void RegisterExtraPictos(IEnumerable<(string Id, string Display)> extras)
    {
        foreach (var (id, display) in extras)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (!Items.ContainsKey(id))
            {
                Items[id] = new ItemInfo
                {
                    CodeName = id,
                    DisplayName = display,
                    Type = "Pictos",
                    PictosTier = 2,
                };
            }
        }
    }

    // ───────────────────── loaders ─────────────────────

    private static T LoadJson<T>(string file)
    {
        var json = ReadEmbedded(file);
        return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json)
               ?? throw new InvalidDataException("bad " + file);
    }

    private static string ReadEmbedded(string file)
    {
        var asm = typeof(LootCatalog).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(file, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException(file);
        using var s = asm.GetManifestResourceStream(name)
                      ?? throw new FileNotFoundException(file);
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    private Dictionary<string, ItemInfo> LoadItems()
    {
        var arr = JArray.Parse(ReadEmbedded("item_data.json"));
        var tiers = FlattenTiers(LoadJson<Dictionary<string, Dictionary<string, List<string>>>>("item_tiers.json"));
        var pictos = new Dictionary<string, int>(StringComparer.Ordinal);
        var weapons = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int t = 1; t <= MaxTier; t++)
        {
            if (tiers.TryGetValue($"Pictos_Tier{t}", out var p))
                foreach (var id in p) pictos[id] = t;
            if (tiers.TryGetValue($"Weapon_Tier{t}", out var w))
                foreach (var id in w) weapons[id] = t;
        }

        var map = new Dictionary<string, ItemInfo>(StringComparer.Ordinal);
        foreach (var o in arr.OfType<JObject>())
        {
            var code = o.Value<string>("CodeName") ?? "";
            if (code.Length == 0) continue;
            map[code] = new ItemInfo
            {
                CodeName = code,
                DisplayName = o.Value<string>("CustomName") ?? code,
                Type = o.Value<string>("Type") ?? "Unknown",
                CutContent = o.Value<bool?>("IsCutContent") ?? false,
                HasQuantities = o.Value<bool?>("HasQuantities") ?? false,
                PictosTier = pictos.TryGetValue(code, out var pt) ? pt : null,
                WeaponTier = weapons.TryGetValue(code, out var wt) ? wt : null,
            };
        }
        return map;
    }

    private static Dictionary<string, EnemyInfo> LoadEnemies()
    {
        var arr = JArray.Parse(ReadEmbedded("enemy_data.json"));
        var map = new Dictionary<string, EnemyInfo>(StringComparer.Ordinal);
        foreach (var o in arr.OfType<JObject>())
        {
            var code = o.Value<string>("CodeName") ?? "";
            if (code.Length == 0) continue;
            map[code] = new EnemyInfo
            {
                CodeName = code,
                DisplayName = o.Value<string>("CustomName") ?? code,
                Archetype = o.Value<string>("Archetype") ?? "Regular",
                Level = o.Value<int?>("Level") ?? 0,
                CutContent = o.Value<bool?>("IsCutContent") ?? false,
            };
        }
        return map;
    }

    private static Dictionary<string, List<string>> FlattenTiers(
        Dictionary<string, Dictionary<string, List<string>>> raw)
    {
        var d = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (cat, tiers) in raw)
        foreach (var (tier, items) in tiers)
            d[$"{cat}_{tier}"] = items;
        return d;
    }

    private static Dictionary<string, int> LoadAreaLevels()
    {
        var arr = JArray.Parse(ReadEmbedded("location_data.json"));
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in arr.OfType<JObject>())
        {
            var code = o.Value<string>("CodeName") ?? "";
            var parts = code.Split('.');
            if (parts.Length < 3) continue;
            var area = parts[2];
            if (area == "WorldMap") continue;
            var sc = o.Value<int?>("LevelScaling") ?? 0;
            if (sc <= 0) continue;
            if (!map.TryGetValue(area, out var cur) || sc < cur)
                map[area] = sc;
        }
        return map;
    }

    private HashSet<string> BuildProtected()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cat in new[] { "Key Item", "Music Record", "Journal", "Skill Unlock", "Merchant Unlock" })
        {
            if (Categories.TryGetValue(cat, out var ids))
                foreach (var id in ids) set.Add(id);
        }
        set.Add("MusicRecord");
        set.Add(PictosMagnitudeKit.InfiniteId);
        set.Add(PictosMagnitudeKit.UltimaPowerId);
        set.Add(SpecialPictosKit.AliciaId);
        set.Add(SpecialPictosKit.LayeredId);
        set.Add(SpecialPictosKit.SuperSonicId);
        set.Add(SpecialPictosKit.RuptureId);
        set.Add(SpecialPictosKit.UnoPunchoId);
        foreach (var kv in Items)
        {
            if (kv.Value.Type is "Key" or "Key Item") set.Add(kv.Key);
            if (kv.Key.StartsWith("Quest_", StringComparison.Ordinal)) set.Add(kv.Key);
        }
        return set;
    }

    // ───────────────────── checks ─────────────────────

    private void AddChests(JObject table)
    {
        foreach (var prop in table.Properties())
        {
            if (prop.Value is not JObject row) continue;
            var id = prop.Name;
            var (area, level) = ResolveChestArea(id);
            var areaKey = AreaKeyOf(ChestToken(id));
            var slots = new List<Slot>();
            if (row["LootTableReferences"] is JArray refs)
            {
                foreach (var r in refs.OfType<JObject>())
                {
                    var tableName = r.Value<string>("LootTable") ?? "";
                    slots.Add(new Slot
                    {
                        LootTableRef = true,
                        LootTable = tableName,
                        ItemId = tableName,
                        ItemName = ShortTable(tableName),
                        ItemType = "Upgrade roll",
                        Chance = 100,
                        Table = "DT_ChestsContent",
                        RowName = id,
                    });
                }
            }
            if (row["LootTableSections"] is JArray secs)
            {
                for (int si = 0; si < secs.Count; si++)
                {
                    if (secs[si] is not JObject sec) continue;
                    var chance = sec.Value<double?>("ChancePercent") ?? 100;
                    if (sec["LootEntries"] is not JArray entries) continue;
                    for (int ei = 0; ei < entries.Count; ei++)
                    {
                        if (entries[ei] is not JObject e) continue;
                        var item = e.Value<string>("ItemID") ?? "";
                        var info = Describe(item);
                        slots.Add(new Slot
                        {
                            ItemId = item,
                            ItemName = info.DisplayName,
                            ItemType = TypeOf(item),
                            Quantity = e.Value<int?>("Quantity") ?? 1,
                            Weight = e.Value<int?>("Weight") ?? 1,
                            Chance = chance,
                            Path = $"LootTableSections.{si}.LootEntries.{ei}.ItemID",
                            Table = "DT_ChestsContent",
                            RowName = id,
                        });
                    }
                }
            }

            Checks.Add(new Check
            {
                Source = PaintCageChests.Contains(id) ? PaintCageSource : "Chest",
                Id = id,
                DisplayName = Humanize(id.StartsWith("Chest_") ? id["Chest_".Length..] : id),
                Area = area,
                AreaKey = areaKey,
                AreaLevel = level,
                Tier = TierFromLevel(level),
                Slots = slots,
            });
        }
    }

    private void AddEnemies(JObject table)
    {
        foreach (var prop in table.Properties())
        {
            if (prop.Value is not JObject row) continue;
            var id = prop.Name;
            Enemies.TryGetValue(id, out var info);
            var tableLevel = row.Value<int?>("Level") ?? 0;
            var level = ResolveEnemyLevel(tableLevel, info);
            var arch = info?.Archetype ?? "Regular";
            var display = info?.DisplayName ?? (row.Value<string>("EnemyHardcodedName") ?? id);
            var slots = new List<Slot>();
            if (row["PossibleLoot"] is JArray loot)
            {
                for (int i = 0; i < loot.Count; i++)
                {
                    if (loot[i] is not JObject e) continue;
                    var item = e["Item"]?["RowName"]?.ToString() ?? "";
                    var inf = Describe(item);
                    slots.Add(new Slot
                    {
                        ItemId = item,
                        ItemName = inf.DisplayName,
                        ItemType = TypeOf(item),
                        Quantity = e.Value<int?>("Quantity") ?? 1,
                        Chance = e.Value<double?>("LootPercentageChance") ?? 100,
                        Path = $"PossibleLoot.{i}.Item.RowName",
                        Table = "DT_jRPG_Enemies",
                        RowName = id,
                    });
                }
            }

            var area = AreaFromEnemyName(display, id);
            Checks.Add(new Check
            {
                Source = "Enemy",
                Id = id,
                DisplayName = display + (arch.Length > 0 ? $" ({arch})" : ""),
                Area = area,
                AreaKey = EnemyAreaKey(row.Value<string>("EnemyActorClassSoft"), display, id),
                AreaLevel = level,
                Tier = TierFromLevel(level),
                IsBoss = row.Value<bool?>("IsBoss") ?? false,
                Slots = slots,
            });
        }
    }

    private void AddMerchant(string file, JObject rows)
    {
        var stem = Path.GetFileNameWithoutExtension(file);
        if (stem.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            stem = Path.GetFileNameWithoutExtension(stem);
        var display = MerchantNames.GetValueOrDefault(stem, Humanize(stem.Replace("DT_Merchant_", "")));
        var tier = MerchantTiers.GetValueOrDefault(stem, 2);
        var slots = new List<Slot>();
        foreach (var prop in rows.Properties())
        {
            if (prop.Value is not JObject row) continue;
            var item = row.Value<string>("ItemRowName") ?? prop.Name;
            var inf = Describe(item);
            slots.Add(new Slot
            {
                ItemId = item,
                ItemName = inf.DisplayName,
                ItemType = TypeOf(item),
                Quantity = row.Value<int?>("Quantity") ?? 1,
                Chance = 100,
                Path = "ItemRowName",
                Table = stem,
                RowName = prop.Name,
            });
        }
        Checks.Add(new Check
        {
            Source = "Merchant",
            Id = stem,
            DisplayName = display,
            Area = display,
            AreaLevel = (tier - 1) * 20 + 10,
            Tier = tier,
            MerchantFile = stem,
            MerchantPath = file,
            Slots = slots,
        });
    }

    private void RebuildFrequencies()
    {
        Frequencies.Clear();
        void Add(string group, string label, int slots, double expected, string note = "") =>
            Frequencies.Add(new FreqRow
            {
                Group = group, Label = label, Slots = slots, Expected = expected, Note = note,
            });

        var chests = Checks.Where(c => c.Source == "Chest").ToList();
        var paintCages = Checks.Where(c => c.Source == PaintCageSource).ToList();
        var enemies = Checks.Where(c => c.Source == "Enemy").ToList();
        var merchants = Checks.Where(c => c.Source == "Merchant").ToList();

        if (paintCages.Count > 0)
        {
            Add("Paint Cages", "Paint Cage rows", paintCages.Count, paintCages.Count,
                "Locked cage rewards. The randomizer rolls exactly one tier-appropriate pictos or weapon per cage.");
            var cageSlots = paintCages.SelectMany(c => c.Slots).ToList();
            Add("Paint Cages", "Vanilla reward slots", cageSlots.Count, cageSlots.Count);
            foreach (var g in cageSlots.GroupBy(s => s.ItemType).OrderByDescending(g => g.Count()))
                Add("Paint Cage types", g.Key, g.Count(), g.Count(),
                    $"~{100.0 * g.Count() / Math.Max(1, cageSlots.Count):0.#}% of vanilla cage rewards");
        }

        Add("Chests", "Chest rows", chests.Count, chests.Count,
            "One row in DT_ChestsContent per world chest / pickup.");
        var direct = chests.SelectMany(c => c.Slots.Where(s => !s.LootTableRef)).ToList();
        var refs = chests.SelectMany(c => c.Slots.Where(s => s.LootTableRef)).ToList();
        Add("Chests", "Direct item slots", direct.Count, direct.Count,
            "LootTableSections entries. Vanilla is almost always one guaranteed item.");
        Add("Chests", "Upgrade-table references", refs.Count, refs.Count,
            "LootTableReferences → DT_LootTable_UpgradeItems_Exploration (catalysts). Not a unique item.");

        foreach (var g in direct.GroupBy(s => s.ItemType).OrderByDescending(g => g.Count()))
            Add("Chest types", g.Key, g.Count(), g.Count(),
                $"~{100.0 * g.Count() / Math.Max(1, direct.Count):0.#}% of direct chest slots");

        Add("Enemies", "Enemy rows", enemies.Count, enemies.Count, "DT_jRPG_Enemies.");
        var withLoot = enemies.Where(e => e.Slots.Count > 0).ToList();
        Add("Enemies", "Enemies with PossibleLoot", withLoot.Count, withLoot.Count);
        var drops = enemies.SelectMany(e => e.Slots).ToList();
        Add("Enemies", "Drop lines", drops.Count, drops.Sum(s => s.Chance / 100.0),
            "Expected = sum of LootPercentageChance. Vanilla is 100% on most lines, 20% on a few.");
        foreach (var g in drops.GroupBy(s => s.Chance).OrderByDescending(g => g.Key))
            Add("Enemy chances", $"{g.Key:0.#}% chance", g.Count(), g.Count() * g.Key / 100.0);
        foreach (var g in drops.GroupBy(s => s.ItemType).OrderByDescending(g => g.Count()))
            Add("Enemy types", g.Key, g.Count(), g.Sum(s => s.Chance / 100.0));

        if (merchants.Count > 0)
        {
            Add("Merchants", "Shops", merchants.Count, merchants.Count);
            var goods = merchants.SelectMany(m => m.Slots).ToList();
            Add("Merchants", "Shop slots", goods.Count, goods.Count);
            foreach (var g in goods.GroupBy(s => s.ItemType).OrderByDescending(g => g.Count()))
                Add("Merchant types", g.Key, g.Count(), g.Count());
        }

        foreach (var src in new[] { "Chest", PaintCageSource, "Enemy", "Merchant" })
        {
            var group = Checks.Where(c => c.Source == src).GroupBy(c => c.Tier);
            foreach (var g in group.OrderBy(x => x.Key))
                Add($"{src} tiers", $"Tier {g.Key} (Lv {Math.Max(1, (g.Key - 1) * 20)}–{(g.Key == MaxTier ? 99 : g.Key * 20 - 1)})",
                    g.Count(), g.Count());
        }
    }

    // ───────────────────── names / areas ─────────────────────

    private (string Area, int Level) ResolveChestArea(string chestId)
    {
        var key = ChestToken(chestId);
        if (ChestAreas.TryGetValue(key, out var mapped))
            return mapped;
        var token = key.Contains('_') ? key.Split('_').Last() : key;
        if (AreaLevels.TryGetValue(token, out var lv))
            return (Humanize(token), lv);
        return ("Unknown", 1);
    }

    /// <summary>Chest id with the "Chest_" prefix and trailing index stripped
    /// ("Chest_AncientSanctuary_4" → "AncientSanctuary").</summary>
    private static string ChestToken(string chestId)
    {
        var key = chestId.StartsWith("Chest_", StringComparison.Ordinal) ? chestId[6..] : chestId;
        return System.Text.RegularExpressions.Regex.Replace(key, @"_\d+$", "");
    }

    /// <summary>
    /// The area bucket gear is spread over: letters and digits only, lowercased,
    /// so an enemy's level folder ("Esquies_Nest", "AncientSanctuary") lands in
    /// the same bucket as the matching chest token ("EsquiesNest").
    /// </summary>
    public static string AreaKeyOf(string token)
    {
        var sb = new System.Text.StringBuilder(token.Length);
        foreach (var ch in token)
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    /// <summary>
    /// Enemy area bucket. The display-name parenthetical wins: every gestral
    /// fighter model lives under …/Enemies/Gestrals/… regardless of venue
    /// (village QUEST bosses even sit in Gestral_Arena/), so the actor folder
    /// cannot discriminate village from dark arena. Otherwise the level
    /// folder in EnemyActorClassSoft (…/Enemies/AncientSanctuary/…) is the
    /// only marker that lines up with the chest tokens; enemies without one
    /// fall back to the display-name parenthetical, then the id prefix.
    /// </summary>
    private static string EnemyAreaKey(string? actorPath, string display, string id)
    {
        if (display.Contains("Dark Gestral Arena", StringComparison.OrdinalIgnoreCase))
            return AreaKeyOf("DarkGestralArena");
        if (display.Contains("Gestral Village", StringComparison.OrdinalIgnoreCase))
            return AreaKeyOf("GestralVillage");
        var m = System.Text.RegularExpressions.Regex.Match(actorPath ?? "", @"/Enemies/([A-Za-z0-9_]+)/");
        if (m.Success) return AreaKeyOf(m.Groups[1].Value);
        return AreaKeyOf(AreaFromEnemyName(display, id));
    }

    private Dictionary<string, string>? _areaKeyById;

    /// <summary>Area bucket for a chest or enemy row id (loot-spread lookups).</summary>
    public string AreaKeyFor(string id)
    {
        if (_areaKeyById is null)
        {
            _areaKeyById = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var c in Checks) _areaKeyById.TryAdd(c.Id, c.AreaKey);
        }
        return _areaKeyById.GetValueOrDefault(id, "");
    }

    private static string AreaFromEnemyName(string display, string id)
    {
        var open = display.LastIndexOf('(');
        var close = display.LastIndexOf(')');
        if (open >= 0 && close > open)
            return display[(open + 1)..close];
        var prefix = id.Split('_')[0];
        return Humanize(prefix);
    }

    private static string InferType(string code)
    {
        if (code.StartsWith("ChromaPack_", StringComparison.Ordinal)) return "Gold";
        if (code is "Consumable_LuminaPoint" or "Consumable_Respec") return "Inventory";
        if (code.StartsWith("UpgradeMaterial_", StringComparison.Ordinal)) return "Upgrade Material";
        if (code.Contains("Tint_Shard", StringComparison.Ordinal) || code.EndsWith("Shard", StringComparison.Ordinal))
            return "Shard";
        if (code.StartsWith("Quest_", StringComparison.Ordinal)) return "Key Item";
        if (code is "MusicRecord") return "Music Record";
        if (code.EndsWith("Foot", StringComparison.Ordinal)) return "Lovely Foot";
        return "Unknown";
    }

    private static string NormalizeType(string type, string code)
    {
        if (type is "Gold") return "Gold";
        if (type is "Inventory" && code.Contains("Lumina", StringComparison.Ordinal)) return "Inventory";
        if (type is "Pictos" or "Weapon") return type;
        return InferType(code) is var inferred && inferred != "Unknown" ? inferred : type;
    }

    private static string ShortTable(string path)
    {
        var n = path.Replace('\\', '/');
        var i = n.LastIndexOf('/');
        return i >= 0 ? n[(i + 1)..] : n;
    }

    public static string Humanize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        var s = raw.Replace('_', ' ').Replace("SmallLevel ", "").Replace("SideLevel ", "");
        return s;
    }

    /// <summary>Original randomizer merchant display names.</summary>
    public static readonly Dictionary<string, string> MerchantNames = new(StringComparer.Ordinal)
    {
        ["DT_Merchant_CleaIsland"] = "Fusoka (Flying Manor)",
        ["DT_Merchant_CoastalCave_Bruler"] = "Bruler (Coastal Cave)",
        ["DT_Merchant_CoastalCave_Cruler"] = "Cruler (Coastal Cave)",
        ["DT_Merchant_FH_Custo_Danseuse"] = "Verogo (Frozen Hearts)",
        ["DT_Merchant_ForgottenBattlefield"] = "Kasumi (Forgotten Battlefield)",
        ["DT_Merchant_GestralVillage1"] = "Jujubree (Gestral Village)",
        ["DT_Merchant_GestralVillage2"] = "Eesda (Gestral Village)",
        ["DT_Merchant_GestralVillage3"] = "Gestral Merchant (Sacred River)",
        ["DT_Merchant_GoblusLair"] = "Noco (Flying Waters)",
        ["DT_Merchant_GrandisStation"] = "Grandis (Monoco Station)",
        ["DT_Merchant_GV_1_CustoSuits_Guys"] = "Delsitra (Gestral Village)",
        ["DT_Merchant_GV_1_CustoSuits_Ladies"] = "Alexcyclo (Gestral Village)",
        ["DT_Merchant_Lumiere"] = "Cribappa (Lumiere Act III)",
        ["DT_Merchant_MonocosMountain"] = "Melosh (The Monolith)",
        ["DT_Merchant_Monolith"] = "Mistra (The Monolith)",
        ["DT_Merchant_OldLumiere"] = "Mandelgo (Old Lumiere)",
        ["DT_Merchant_Optional3"] = "Grour (Renoir's Drafts)",
        ["DT_Merchant_OrangeForest"] = "Persik (Falling Leaves)",
        ["DT_Merchant_Reacher"] = "Eragol (The Reacher)",
        ["DT_Merchant_SeaCliff"] = "Jerijeri (Stone Wave Cliffs)",
        ["DT_Merchant_Sirene"] = "Klaudiso (Sirene)",
        ["DT_Merchant_TwilightSanctuary"] = "Anthonypo (Endless Night Sanctuary)",
        ["DT_Merchant_Visages"] = "Blooraga (Visages)",
        ["DT_Merchant_YellowForest"] = "Pinabby (Yellow Harvest)",
        ["DT_Merchant_WM_1"] = "Appla (World Map)",
        ["DT_Merchant_WM_2"] = "Colaro (World Map)",
        ["DT_Merchant_WM_3_GustaveSuit"] = "Carrabi (World Map)",
        ["DT_Merchant_WM_4"] = "Strabami (World Map)",
        ["DT_Merchant_WM_5"] = "Pecha (World Map)",
        ["DT_Merchant_WM_6"] = "Blakora (World Map)",
        ["DT_Merchant_WM_7"] = "Papasso (World Map)",
        ["DT_Merchant_WM_8"] = "Rederi (World Map)",
        ["DT_Merchant_WM_9"] = "Sodasso (World Map)",
        ["DT_Merchant_WM_9_Sirene"] = "Pearo (World Map)",
        ["DT_Merchant_WM_10"] = "Carnovi (World Map)",
        ["DT_Merchant_WM_11"] = "Blabary (World Map)",
        ["DT_Merchant_WM_12"] = "Geranjo (World Map)",
        ["DT_Merchant_WM_13"] = "Granasori (World Map)",
        ["DT_Merchant_WM_14"] = "Lucaroparfe (World Map)",
        ["DT_Merchant_WM_15"] = "Jumeliba (World Map)",
        ["DT_Merchant_WM_16"] = "Rubiju (World Map)",
        ["DT_Merchant_WM_17"] = "Citrelo (World Map)",
        ["DT_Merchant_VersosDraft"] = "Najabla (Verso's Drafts)",
        ["DT_Merchant_WM_Chic"] = "Chic Merchant (World Map)",
        ["DT_Merchant_Osquio"] = "Osquio Merchant (Root of All Evil)",
    };

    public static readonly Dictionary<string, int> MerchantTiers = new(StringComparer.Ordinal)
    {
        ["DT_Merchant_GoblusLair"] = 1,
        ["DT_Merchant_SeaCliff"] = 2,
        ["DT_Merchant_GestralVillage1"] = 1, ["DT_Merchant_GestralVillage2"] = 1,
        ["DT_Merchant_WM_11"] = 1,
        ["DT_Merchant_GrandisStation"] = 2, ["DT_Merchant_OldLumiere"] = 2,
        ["DT_Merchant_ForgottenBattlefield"] = 2, ["DT_Merchant_CoastalCave_Bruler"] = 2,
        ["DT_Merchant_CoastalCave_Cruler"] = 2, ["DT_Merchant_Lumiere"] = 5,
        ["DT_Merchant_WM_3_GustaveSuit"] = 2, ["DT_Merchant_WM_15"] = 2,
        ["DT_Merchant_WM_2"] = 2, ["DT_Merchant_WM_1"] = 2, ["DT_Merchant_WM_12"] = 2,
        ["DT_Merchant_WM_4"] = 2, ["DT_Merchant_WM_5"] = 2, ["DT_Merchant_WM_10"] = 2,
        ["DT_Merchant_WM_6"] = 2,
        ["DT_Merchant_YellowForest"] = 2, ["DT_Merchant_OrangeForest"] = 3,
        ["DT_Merchant_Reacher"] = 4, ["DT_Merchant_MonocosMountain"] = 4,
        ["DT_Merchant_Monolith"] = 3, ["DT_Merchant_WM_7"] = 3, ["DT_Merchant_WM_8"] = 3,
        ["DT_Merchant_WM_13"] = 3,
        ["DT_Merchant_FH_Custo_Danseuse"] = 4, ["DT_Merchant_Optional3"] = 5,
        ["DT_Merchant_CleaIsland"] = 5, ["DT_Merchant_TwilightSanctuary"] = 4,
        ["DT_Merchant_Visages"] = 3, ["DT_Merchant_Sirene"] = 2, ["DT_Merchant_WM_9"] = 4,
        ["DT_Merchant_WM_9_Sirene"] = 4, ["DT_Merchant_WM_14"] = 4, ["DT_Merchant_WM_16"] = 4,
        ["DT_Merchant_WM_17"] = 4, ["DT_Merchant_WM_Chic"] = 4,
        ["DT_Merchant_VersosDraft"] = 5, ["DT_Merchant_GestralVillage3"] = 4,
        ["DT_Merchant_GV_1_CustoSuits_Guys"] = 5, ["DT_Merchant_GV_1_CustoSuits_Ladies"] = 5,
        ["DT_Merchant_Osquio"] = 5,
    };

    /// <summary>Chest-source label for the Paint Cage group.</summary>
    public const string PaintCageSource = "Paint Cages";

    /// <summary>
    /// The reward chests that sit inside Paint Cages. The cage actor itself has
    /// no chest/loot link — each reward is an ordinary BP_Chest_Regular placed
    /// at the cage — so this list is the reliable join. It was derived from the
    /// 21 cage locations: exclusive rewards identify the weapon/pictos cages,
    /// the five PartyHealShard rows are the Chroma Elixir cages, and the
    /// remaining Revive/Chroma/catalyst cages were matched to their nearest
    /// chest actor in the level files.
    /// </summary>
    public static readonly HashSet<string> PaintCageChests = new(StringComparer.Ordinal)
    {
        // Stone Wave Cliffs (internal SeaCliff)
        "Chest_SeaCliff_5",              // Delaram
        "Chest_SeaCliff_12",             // Duenum
        // Old Lumiere
        "Chest_OldLumiere_20",           // Redalim
        // Verso's Drafts (VD_Lune is the Esquim family row)
        "Chest_SideLevel_VersosDraft_15", // Esquim
        "Chest_SideLevel_VersosDraft_9",  // Chroma Elixir Shard
        // Sirene
        "Chest_Sirene_3",                // Shield Affinity
        "Chest_Sirene_19",               // Revive Tint Shard
        // Frozen Hearts
        "Chest_FrozenHearts_7",          // Anti-Freeze
        "Chest_FrozenHearts_9",          // Revive Tint Shard
        // The Monolith
        "Chest_Monolith_3",              // Revive Tint Shard
        "Chest_Monolith_13",             // Random Defense pictos
        "Chest_Monolith_31",             // Empowering Parry
        // Flying Manor
        "Chest_FlyingManor_6",           // Powerful Revive
        "Chest_FlyingManor_41",          // Energising Stun
        // Flying Waters (internal GoblusLair)
        "Chest_GoblusLair_24",           // Chroma Elixir Shard
        "Chest_GoblusLair_26",           // Revive Tint Shard
        // Ancient Sanctuary
        "Chest_AncientSanctuary_17",     // Chroma Elixir Shard
        // Stone Wave Cliffs Cave
        "Chest_SmallLevel_StonewaveCliffsCave_4", // Chroma Elixir Shard
        // Forgotten Battlefield
        "Chest_ForgottenBattlefield_25", // Chroma Elixir Shard
        // Esoteric Ruins (4,990 chroma)
        "Chest_SmallLevel_DoorMaze_2",
        // Endless Night Sanctuary (Grandiose Chroma Catalyst)
        "Chest_TwilightSanctuary_7",
    };

    private static readonly Dictionary<string, (string Area, int Level)> ChestAreas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SpringMeadows"] = ("Spring Meadows", 3),
            ["SmallLevel_SpringMeadows"] = ("Spring Meadows", 3),
            ["GoblusLair"] = ("Flying Waters", 8),
            ["SmallLevel_GoblusLair"] = ("Flying Waters", 8),
            ["SmallLevel_GoblusLair_02"] = ("Flying Waters", 8),
            ["AncientSanctuary"] = ("Ancient Sanctuary", 12),
            ["GestralVillage"] = ("Gestral Village", 15),
            ["DarkGestralArena"] = ("Dark Gestral Arena", 75),
            ["EsquiesNest"] = ("Esquie's Nest", 16),
            ["SeaCliff"] = ("Stone Wave Cliffs", 24),
            ["YellowForest"] = ("Yellow Harvest", 21),
            ["SmallLevel_StonewaveCliffsCave"] = ("Stone Wave Cliffs Cave", 22),
            ["ForgottenBattlefield"] = ("Forgotten Battlefield", 23),
            ["GrandisStation"] = ("Monoco's Station", 25),
            ["SmallLevel_CavernCrusher"] = ("Crushing Cavern", 27),
            ["SmallLevel_DoorMaze"] = ("Esoteric Ruins", 28),
            ["OldLumiere"] = ("Old Lumiere", 28),
            ["SmallLevel_TheCarrousel"] = ("The Carousel", 30),
            ["Visages"] = ("Visages", 32),
            ["SmallLevel_Visages"] = ("Isle of the Eyes", 65),
            ["SmallLevel_CoastalCave"] = ("Coastal Cave", 36),
            // Sirene is a T3 area: chest tier comes straight off this level
            // (37 → T2, so lifted to the 40-59 / T3 band). Enemy rows are pinned
            // separately in AreaTierOverrides.
            ["Sirene"] = ("Sirene", 41),
            // Sinister Cave: game scaling is 38 (→ T2), but it holds the T3
            // Chromatic Chalier boss, so the area is lifted to the 40-59 / T3
            // band (matching its neighbour Sirene) and its chests hand out T3.
            ["SmallLevel_SinisterCave"] = ("Sinister Cave", 41),
            ["Monolith"] = ("The Monolith", 40),
            ["FallingLeaves"] = ("Falling Leaves", 41),
            ["Lumiere"] = ("Lumiere", 99),
            ["Lumiere_ACT1"] = ("Lumiere", 1),
            // Crimson Forest is a T4 area: game scaling is 52 (→ T3), but the
            // level is built around the level-61 Gold Chevalieres, so the area is
            // lifted to the 60-79 / T4 band and its chests hand out T4. Enemy
            // rows are pinned separately in AreaTierOverrides.
            ["CrimsonForest"] = ("Crimson Forest", 60),
            ["SmallLevel_Cemetery"] = ("Floating Cemetery", 55),
            ["SideLevel_CleasTower"] = ("Endless Tower", 55),
            ["SmallLevel_WhiteSands"] = ("White Sands", 55),
            ["Reacher"] = ("The Reacher", 60),
            ["SmallLevel_Reacher"] = ("The Reacher", 60),
            ["SmallLevel_RedWoods"] = ("Red Woods", 60),
            ["SmallLevel_SacredRiver"] = ("Sacred River", 60),
            ["SmallLevel_ChosenPath"] = ("The Chosen Path", 60),
            ["SmallLevel_FloatingIsland"] = ("Sky Island", 60),
            ["FrozenHearts"] = ("Frozen Hearts", 79),
            ["SmallLevel_RockTrailing"] = ("The Crows", 65),
            ["DarkShores"] = ("Dark Shores", 68),
            ["TwilightSanctuary"] = ("Endless Night Sanctuary", 73),
            ["FlyingManor"] = ("Flying Manor", 83),
            ["SideLevel_VersosDraft"] = ("Verso's Drafts", 86),
            ["RenoirSDraft"] = ("Renoir's Drafts", 93),
            ["Manor"] = ("The Manor", 1),
            ["WorldMap"] = ("World Map", 25),
            ["MiniLevels"] = ("Mini levels", 10),
            ["Generic_Chroma"] = ("Generic templates", 1),
            ["Generic_LuminaPoint"] = ("Generic templates", 1),
            ["Generic_5xLuminaPoint"] = ("Generic templates", 1),
            ["Generic_Respec"] = ("Generic templates", 1),
            ["Placeholder"] = ("Placeholder", 1),
            ["SmallLevel_FlyingCasinoEntrance"] = ("Flying Casino", 10),
        };

    private static readonly string[] BrokenItemList =
    {
        "MitigatedPerfection", "Chroma", "04_Key_Placeholder",
        "Gold_Small", "Gold_Medium", "Gold_Big", "Consumable_SkillPoint",
        "VeilleurFoot", "PetankFoot", "NoireFoot", "BourgeonFoot",
        "RiskSeeker", "LimonsolPictos", "02_ArmPicto_Placeholder",
        "AntiShock", "NullPhysical", "NullFire", "NullIce", "NullEarth",
        "NullThunder", "NullDark", "NullLight", "AbsorbPhysical", "AbsorbFire",
        "AbsorbIce", "AbsorbEarth", "AbsorbLight", "AbsorbThunder", "AbsorbDark",
        "Speedster", "Blitz", "AngelGrace", "AngelPresent", "AngelicChance",
        "RiskTaker", "HighOnPerfect", "FasterThanHisShadow", "BrambleSkin",
        "SpreadingBrambleSkin", "BrambleParry", "FastAttacker",
                "ReviveWithPrecision", "FireSkin", "FrozenSkin", "InvertedSkin",
        "OverConfident", "Fugitive", "TurboKiller", "FlashDodge",
        "PhysicalModifier", "MakeItQuick", "AutoPrecision", "BramblePerformer",
        "CriticalCursedPower", "SpeedsterDodge", "PowerBreakerShot",
        "CritChanceOnWeak", "Auto1Hp", "AntiSilence", "PostGradient",
        "PerfectHeal", "ProtectingFire", "PatientFighter",
        "DefenseBreakerDodge", "PowerBreakerDodge",
        // Redundant cut-content twin of CriticalBreak: it borrows the same
        // display name/description ("Critical Stun") and the same buff
        // (BP_LU_CritChanceOnStunned), so distributing it produces two
        // indistinguishable pictos. Never place it; CriticalBreak is canonical.
        "CritChanceOnStunned",
    };

    public static readonly HashSet<string> BrokenEncounters = new(StringComparer.Ordinal)
    {
        "Quest_WeaponlessChalier*1", "FB_Dualliste_Phase1*1",
        "JarAlternativeA*1", "JarAlternativeA*1_Gault*2", "JarAlternativeB*1",
        "VolesterAlternativA*1", "VolesterAlternativB*1", "VolesterAlternativC*1",
        "WM_Potier*1_Glaise*1_Volester*1",
    };

    public static readonly HashSet<string> BrokenEnemies = new(StringComparer.Ordinal)
    {
        "QUEST_WeaponlessChalier", "FB_Dualliste_Phase1",
        "YF_Jar_AlternativeA", "YF_Jar_AlternativeB",
        "SM_Volester_AlternativA", "SM_Volester_AlternativB", "SM_Volester_AlternativC",
        "Petank_Parent",
    };
}
