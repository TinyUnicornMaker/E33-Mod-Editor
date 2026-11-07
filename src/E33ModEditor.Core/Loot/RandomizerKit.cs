using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
namespace E33ModEditor.Core.Loot;

/// <summary>
/// Tiered chest / drop / shop replacement plus optional monster and weapon
/// stat rolls. Encounter tables are never shuffled.
/// </summary>
public static class RandomizerKit
{
    public const string ChestsTable = "DT_ChestsContent";
    public const string EnemiesTable = "DT_jRPG_Enemies";
    public const string WeaponsTable = "DT_WeaponDefinitions";

    /// <summary>Perfect (L5) Chroma Catalyst — the mod's rare endgame drop.</summary>
    public const string PerfectCatalystId = "UpgradeMaterial_Level5";
    /// <summary>
    /// Fight level at or above which a T5-area regular/elite rolls the Perfect
    /// Catalyst. 80 is the start of the T5 band, so every endgame area qualifies
    /// — including Flying Manor (83) and Verso's Drafts (86), which sit below
    /// the top endgame areas (Renoir's Drafts 93, Act 3 Lumière 99).
    /// </summary>
    public const int PerfectCatalystMinLevel = 80;

    /// <summary>
    /// Perfect Catalyst quantity for the surviving Grosse Tete variants. His
    /// dodge-only kill makes the normal 5/15 boss payload farmable without a
    /// real fight, so the row recipe is capped at 3. The world-map variant is
    /// T3 and pays no Perfect Catalyst at all.
    /// </summary>
    public const int GrosseTetePerfectCatalystQty = 3;

    private static bool IsGrosseTete(string id) =>
        id.Contains("GrossTete", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// In-game chance for the extra x1 catalyst one tier below a non-boss field
    /// drop's own catalyst (see <see cref="AddFieldDrops"/>).
    /// </summary>
    public const double CatalystOneTierBelowChance = 50;
    /// <summary>
    /// In-game chance for the extra x1 catalyst two tiers below a non-boss field
    /// drop's own catalyst. Nothing lower than this ever rolls.
    /// </summary>
    public const double CatalystTwoTiersBelowChance = 30;

    /// <summary>
    /// Shop stock for a catalyst below the shop's own tier. The merchant row's
    /// Quantity is an int32 and the UI renders it with Convert_IntToText (no
    /// 99 clamp), so 999 is safe and more convenient than 99.
    /// </summary>
    public const int LowerCatalystShopQuantity = 999;
    /// <summary>Shop stock for the shop tier's own catalyst (T5 never sells L5).</summary>
    public const int CurrentCatalystShopQuantity = 33;

    public sealed class Settings
    {
        public int Seed { get; set; } = -1;
        public bool Chests { get; set; } = true;
        public bool PaintCages { get; set; } = true;
        public bool EnemyDrops { get; set; } = true;
        public bool Merchants { get; set; } = true;
        public bool WeaponPassives { get; set; } = true;
        public bool MonsterStats { get; set; } = true;
        public bool WeaponStats { get; set; } = true;

        /// <summary>
        /// Experimental: rewrite encounter rosters with extra enemies (up to
        /// <see cref="MaxEnemiesPerBattle"/>) and patch the battle assets so
        /// slots 4+ spawn and display. Off by default; the field-side capacity
        /// is harmless when no encounter asks for more than four enemies.
        /// </summary>
        public bool BattleGroups { get; set; } = false;
        /// <summary>Largest group the randomizer may create (1–
        /// <see cref="BattleGroupKit.FieldCapacity"/>; values above four also
        /// trigger the field-capacity patch).</summary>
        public int MaxEnemiesPerBattle { get; set; } = 5;
        /// <summary>Field capacity to patch independently of
        /// <see cref="MaxEnemiesPerBattle"/> (0 = use the roster max). Lets a test
        /// patch 32 battle slots while normal fights stay small.</summary>
        public int FieldCapacityOverride { get; set; } = 0;
        /// <summary>Test hook: force this encounter row's roster (and its
        /// variants) to <see cref="BattleTestCount"/> copies of
        /// <see cref="BattleTestEnemy"/>. Empty = off.</summary>
        public string BattleTestRow { get; set; } = "";
        public string BattleTestEnemy { get; set; } = "";
        public int BattleTestCount { get; set; } = 0;
        /// <summary>Percent chance per eligible encounter to add at least one
        /// extra enemy. 0 keeps vanilla group sizes.</summary>
        public double ExtraEnemiesChance { get; set; } = 50;
        /// <summary>Shuffle the final roster so added enemies are not all appended
        /// at the end. Bosses/specials are excluded before this happens.</summary>
        public bool ShuffleBattleRosters { get; set; } = true;
        /// <summary>
        /// Experimental: pre-generate variant encounter rows and re-roll the
        /// world enemy's encounter on every battle start, so attacking the same
        /// overworld enemy does not always produce the same group. Off by
        /// default; uses <see cref="BattleVariants"/> variants per encounter.
        /// </summary>
        public bool RandomBattles { get; set; } = false;
        /// <summary>Variant encounter rows generated per eligible encounter for
        /// <see cref="RandomBattles"/> (1–8).</summary>
        public int BattleVariants { get; set; } = 3;
        /// <summary>Optional encounter-id prefix for a first-pass test area.
        /// Flying Waters encounters use <c>GO_</c>. Empty means every eligible
        /// encounter table row.</summary>
        public string BattleGroupsAreaPrefix { get; set; } = "";

        public double[] StatLo { get; set; } = (double[])DefaultStatLo.Clone();
        public double[] StatHi { get; set; } = (double[])DefaultStatHi.Clone();
        public double[] BossHp { get; set; } = (double[])DefaultBossHp.Clone();
        /// <summary>
        /// Hotter band rolled for enemy PhysicalAttack/MagicalAttack only
        /// (base band +0.25 growing +0.1/tier). All other stats use StatLo/Hi.
        /// </summary>
        public double[] AttackLo { get; set; } = (double[])DefaultAttackLo.Clone();
        public double[] AttackHi { get; set; } = (double[])DefaultAttackHi.Clone();
        /// <summary>
        /// Dedicated boss Speed multiplier applied on top of whatever factor a
        /// boss row rolled, for every tier (regular enemies unaffected).
        /// Default 2.0 => a T3 boss averages ~2.36x vanilla speed.
        /// </summary>
        public double BossSpeed { get; set; } = DefaultBossSpeed;
        /// <summary>
        /// Alternate T4–T5 defense curve: 1× at the T4 floor, ramping to 30×
        /// the regular curve at the top of T5. Off keeps the regular curve.
        /// </summary>
        public bool ExtremeBossDefense { get; set; }
        public double RegularPictosChance { get; set; } = 10;
        public double ElitePictosChance { get; set; } = 66;
        /// <summary>
        /// Chance (percent) that an endgame regular/elite enemy — a T5-area,
        /// level ≥ <see cref="PerfectCatalystMinLevel"/> non-boss — also rolls a
        /// Perfect (L5) Chroma Catalyst. 0 disables it. Rolled on top of the
        /// normal field drop; the L4 field catalyst still comes first.
        /// Default 3.125% (≈ 1/32).
        /// </summary>
        public double T5PerfectCatalystChance { get; set; } = 3.125;
        public int PetankLumina { get; set; } = 50;
        public int MerchantGearSlots { get; set; } = 4;
        /// <summary>
        /// Capacity shards placed per tint family (Healing / Energy / Revive).
        /// Each shard raises how many of that tint the party can carry.
        /// </summary>
        public int TintsPerType { get; set; } = 9;
        /// <summary>Chroma Elixir Shards placed (raises how many Elixirs you carry).</summary>
        public int ChromaElixirShards { get; set; } = 2;
        /// <summary>Potency shapes placed per type (Shape of Health / Energy / Life).</summary>
        public int ShapesPerType { get; set; } = 2;

        public bool IsOff =>
            !Chests && !PaintCages && !EnemyDrops && !Merchants && !WeaponPassives
            && !MonsterStats && !WeaponStats && !BattleGroups && !RandomBattles;
    }

    public static readonly double[] DefaultStatLo = { 0.95, 1.00, 1.10, 1.20, 1.50 };
    public static readonly double[] DefaultStatHi = { 1.05, 1.15, 1.25, 1.50, 1.80 };
    public static readonly double[] DefaultBossHp = { 1.5, 2.5, 10.0, 25.0, 50.0 };

    /// <summary>
    /// HP multiplier for flying curve rows: they dodge most base attacks, so a
    /// full curve pool lasts far longer than its number suggests.
    /// </summary>
    public const double FlyingHpFactor = 0.33;
    public static readonly double[] DefaultAttackLo = { 1.20, 1.35, 1.55, 1.75, 2.15 };
    public static readonly double[] DefaultAttackHi = { 1.30, 1.50, 1.70, 2.05, 2.45 };
    /// <summary>Boss-only Speed multiplier on top of the rolled base band (all tiers).</summary>
    public const double DefaultBossSpeed = 2.0;

    // Starting weapons – randomized within joining tier (Gustave exception T3)
    private static readonly Dictionary<string, (string Character, int JoinTier)> StartingWeapons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Noahram"] = ("Gustave", 1),
        ["Lunerim"] = ("Lune", 1),
        ["Maellum"] = ("Maelle", 1),
        ["Scieleson"] = ("Sciel", 2),
        ["Verleso"] = ("Verso", 3),
        ["Monocaro"] = ("Monoco", 3),
    };

    /// <summary>
    /// Roster gate: a weapon may only appear in a shop, chest or drop whose
    /// source tier is at or above its owner's join tier (T1 = Gustave/Lune/
    /// Maelle, T2 adds Sciel, T3+ everyone). Pre-recruitment characters' gear
    /// can't resolve in-game ("?" shop rows, silent no-grants), so sources
    /// below the join tier must never place them. Pictos are character-
    /// agnostic and always allowed.
    /// </summary>
    private static readonly Dictionary<string, int> CharacterJoinTier = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Gustave"] = 1,
        ["Lune"] = 1,
        ["Maelle"] = 1,
        ["Sciel"] = 2,
        ["Verso"] = 3,
        ["Monoco"] = 3,
    };

    /// <summary>True when <paramref name="id"/> of <paramref name="type"/> may
    /// be placed at source <paramref name="tier"/> under the roster gate.
    /// Unknown weapon owners fail closed (every pool weapon parses to a known
    /// character; see DialogueRewardKit.WeaponCharacter).</summary>
    public static bool WeaponAllowedAtTier(LootCatalog catalog, string type, string id, int tier)
    {
        if (!string.Equals(type, "Weapon", StringComparison.OrdinalIgnoreCase)) return true;
        var owner = DialogueRewardKit.WeaponCharacter(catalog, id);
        return CharacterJoinTier.TryGetValue(owner, out var join) && join <= Math.Clamp(tier, 1, 5);
    }

    /// <summary>
    /// Quest-guaranteed gear (white-nevron help rewards). Never modified by the
    /// randomizer, but pre-marked as placed so loot never duplicates them.
    /// </summary>
    private static readonly HashSet<string> GuaranteedQuestRewards = new(StringComparer.Ordinal)
    {
        "Deminerim", "AutoShell", "Recovery", "HealingParry", "ProtectingHeal", "LongerShell",
    };

    /// <summary>
    /// Per-pack placement state: every picto/weapon already handed out by an
    /// earlier chest, drop or shop roll. Shared across all three sources in
    /// call order so no ID is ever placed twice in one pack.
    /// </summary>
    public sealed class LootPlacement
    {
        public HashSet<string> Placed { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Never placeable, never repeatable: starting weapons (held at join)
        /// and quest-guaranteed gear (white-nevron rewards).
        /// </summary>
        private HashSet<string> PreSeeded { get; } = new(StringComparer.Ordinal);

        public LootPlacement()
        {
            foreach (var id in StartingWeapons.Keys) PreSeeded.Add(id);
            foreach (var id in GuaranteedQuestRewards) PreSeeded.Add(id);
            foreach (var id in PreSeeded) Placed.Add(id);
        }

        /// <summary>
        /// Uniform draw from the unplaced pool at <paramref name="tier"/>,
        /// cascading down through lower tiers (…→T2→T1) only when a tier is
        /// fully placed. Marks and returns the pick, or null when tier and
        /// cascade are exhausted (caller falls back to chroma).
        /// </summary>
        public string? TakeUnique(LootCatalog catalog, string type, int tier, Random rng)
        {
            // True cascade: prefer the requested tier and only drop to a lower
            // one when it has nothing left. Pooling every lower tier into a
            // single uniform pick diluted the tier (a T2 draw could land on T1
            // even while T2 stock remained).
            for (int t = Math.Clamp(tier, 1, 5); t >= 1; t--)
            {
                var candidates = CandidatesAtTier(catalog, type, t);
                if (candidates.Count == 0) continue;
                var pick = candidates[rng.Next(candidates.Count)];
                Placed.Add(pick);
                return pick;
            }
            return null;
        }

        /// <summary>Unplaced, unreserved ids of <paramref name="type"/> at exactly
        /// <paramref name="tier"/> (no cascade).</summary>
        private List<string> CandidatesAtTier(LootCatalog catalog, string type, int tier)
        {
            var t = Math.Clamp(tier, 1, 5);
            var reserved = ReservedFor(type, t);
            var candidates = new List<string>();
            foreach (var id in Pool(catalog, type, t))
            {
                if (Placed.Contains(id) || reserved.Contains(id)) continue;
                if (!WeaponAllowedAtTier(catalog, type, id, t)) continue;
                candidates.Add(id);
            }
            return candidates;
        }

        /// <summary>
        /// Uniform draw from exactly <paramref name="tier"/> — never cascades.
        /// Boss guarantees use this so their weapon/pictos always match the row
        /// tier, and therefore the tier-pinned catalyst on the same drop.
        /// </summary>
        public string? TakeUniqueAtTier(LootCatalog catalog, string type, int tier, Random rng)
        {
            var candidates = CandidatesAtTier(catalog, type, tier);
            if (candidates.Count == 0) return null;
            var pick = candidates[rng.Next(candidates.Count)];
            Placed.Add(pick);
            return pick;
        }

        /// <summary>
        /// Shop stock draw: a gear id of <paramref name="type"/> at
        /// <paramref name="tier"/>, distinct from what this shop already stocks
        /// (<paramref name="stock"/>). Prefers an unplaced id and only then
        /// repeats a tier-appropriate one — shops roll last, so the unique pool is
        /// normally spent, and a shop that sells nothing but chroma is a bug, not
        /// a fallback. Cascades to lower tiers only when a tier is fully spent.
        /// Returns null only when the tier pool for that type is empty.
        /// </summary>
        public string? TakeForShop(LootCatalog catalog, string type, int tier, Random rng, HashSet<string> stock)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                for (int t = Math.Clamp(tier, 1, 5); t >= 1; t--)
                {
                    var reserved = ReservedFor(type, t);
                    var candidates = new List<string>();
                    foreach (var id in Pool(catalog, type, t))
                    {
                        if (stock.Contains(id)) continue;
                        if (pass == 0 && (Placed.Contains(id) || reserved.Contains(id))) continue;
                        if (!WeaponAllowedAtTier(catalog, type, id, t)) continue;
                        // Family-identity ranked weapons (Reach*/Siren* _1/_2)
                        // render as "?" in merchant UI; never stock them.
                        if (type == "Weapon" && PictosMagnitudeKit.IsQuirkedRankedWeapon(id)) continue;
                        candidates.Add(id);
                    }
                    if (candidates.Count == 0) continue;
                    var pick = candidates[rng.Next(candidates.Count)];
                    Placed.Add(pick);
                    return pick;
                }
            }
            return null;
        }

        /// <summary>
        /// Boss-row draw: tier-locked unique while it lasts, then a same-tier
        /// repeat (boss guarantees outrank uniqueness — the game holds fewer
        /// weapons than boss rows demand them). Never drops below the row tier,
        /// so boss gear always matches its tier-pinned catalyst.
        /// </summary>
        public string? TakeUniqueOrRepeat(LootCatalog catalog, string type, int tier, Random rng)
        {
            var t0 = Math.Clamp(tier, 1, 5);
            var unique = TakeUniqueAtTier(catalog, type, t0, rng);
            if (unique is not null) return unique;
            // Repeats never touch pre-seeded gear (starters / quest rewards)
            // and never cross the roster gate.
            var pool = Pool(catalog, type, t0)
                .Where(id => !PreSeeded.Contains(id) && WeaponAllowedAtTier(catalog, type, id, t0)).ToList();
            if (pool.Count == 0) return null;
            var pick = pool[rng.Next(pool.Count)];
            Placed.Add(pick);
            return pick;
        }

        // ── area spread ────────────────────────────────────────────────────
        // Gear is pre-split per (type, tier, area) in proportion to that area's
        // drop points, so a tier's gear turns up across its areas instead of
        // draining into whichever area happens to roll first. An area's queue is
        // spent before the shared remainder, which is spent before the global
        // cascade — so a proportional share always lands, and nothing strands.
        private readonly Dictionary<(string Type, int Tier, string Area), Queue<string>> _byArea = new();
        private readonly Dictionary<(string Type, int Tier), Queue<string>> _shared = new();
        private readonly Dictionary<(string Type, int Tier, string Area), int> _allocated = new();
        /// <summary>Ids sitting in an area queue. The global cascade must not take
        /// these, or area A's draw would eat area B's share and the split would
        /// collapse back into whoever rolls first.</summary>
        private readonly Dictionary<(string Type, int Tier), HashSet<string>> _reserved = new();

        /// <summary>Allocation sizes per (type, tier, area) — tests / diagnostics.</summary>
        public IReadOnlyDictionary<(string Type, int Tier, string Area), int> Allocation => _allocated;

        /// <summary>
        /// Splits every tier's picto and weapon pools across areas in proportion
        /// to drop points, with largest-remainder rounding. A drop point is a
        /// loot source that can yield that type: each chest row counts 1 and each
        /// enemy row counts 1 (per the agreed rule). Pools are shuffled first, so
        /// <em>which</em> picto of a tier lands in an area is random but the
        /// counts are proportional to the areas' drop points.
        /// </summary>
        public void AllocateByArea(LootCatalog catalog, Random rng,
            bool includeChests = true, bool includePaintCages = true)
        {
            var points = new Dictionary<(int Tier, string Area), int>();
            foreach (var c in catalog.Checks)
            {
                if (c.Source == "Merchant") continue; // shops draw from the shared pool
                if (c.Source == "Chest" && !includeChests) continue;
                if (c.Source == LootCatalog.PaintCageSource && !includePaintCages) continue;
                if (c.AreaKey.Length == 0) continue;
                var key = (Math.Clamp(c.Tier, 1, 5), c.AreaKey);
                points[key] = points.GetValueOrDefault(key) + 1;
            }

            foreach (var type in new[] { "Pictos", "Weapon" })
            {
                for (int tier = 1; tier <= 5; tier++)
                {
                    // Roster gate at allocation time: an area's queues must
                    // never reserve a weapon its tier can't field, or the
                    // share strands (global draws skip it, the area can't
                    // spend it) and ReleaseReserved leaks it downhill.
                    var pool = Pool(catalog, type, tier)
                        .Where(id => !PreSeeded.Contains(id) && !Placed.Contains(id)
                            && WeaponAllowedAtTier(catalog, type, id, tier))
                        .ToList();
                    for (int i = pool.Count - 1; i > 0; i--)
                    {
                        var j = rng.Next(i + 1);
                        (pool[i], pool[j]) = (pool[j], pool[i]);
                    }

                    var shared = SharedFor(type, tier);
                    if (pool.Count == 0) continue;

                    var areas = points
                        .Where(kv => kv.Key.Tier == tier)
                        .OrderBy(kv => kv.Key.Area, StringComparer.Ordinal)
                        .Select(kv => (kv.Key.Area, Weight: kv.Value))
                        .ToList();
                    var total = areas.Sum(a => a.Weight);
                    if (total == 0)
                    {
                        foreach (var id in pool) shared.Enqueue(id);
                        continue;
                    }

                    var exact = areas.Select(a => (double)pool.Count * a.Weight / total).ToArray();
                    var take = exact.Select(e => (int)Math.Floor(e)).ToArray();
                    var leftover = pool.Count - take.Sum();
                    var order = Enumerable.Range(0, areas.Count)
                        .OrderByDescending(i => exact[i] - take[i])
                        .ThenBy(i => i)
                        .ToList();
                    for (int i = 0; i < leftover && order.Count > 0; i++) take[order[i % order.Count]]++;

                    var reserved = ReservedFor(type, tier);
                    int p = 0;
                    for (int i = 0; i < areas.Count; i++)
                    {
                        if (take[i] <= 0) continue;
                        var q = new Queue<string>();
                        for (int k = 0; k < take[i] && p < pool.Count; k++)
                        {
                            q.Enqueue(pool[p]);
                            reserved.Add(pool[p]);
                            p++;
                        }
                        if (q.Count == 0) continue;
                        _byArea[(type, tier, areas[i].Area)] = q;
                        _allocated[(type, tier, areas[i].Area)] = q.Count;
                    }
                    for (; p < pool.Count; p++) shared.Enqueue(pool[p]);
                }
            }
        }

        private Queue<string> SharedFor(string type, int tier)
        {
            var key = (type, Math.Clamp(tier, 1, 5));
            if (!_shared.TryGetValue(key, out var q)) _shared[key] = q = new Queue<string>();
            return q;
        }

        private HashSet<string> ReservedFor(string type, int tier)
        {
            var key = (type, Math.Clamp(tier, 1, 5));
            if (!_reserved.TryGetValue(key, out var set))
                _reserved[key] = set = new HashSet<string>(StringComparer.Ordinal);
            return set;
        }

        /// <summary>
        /// Hands one area's unspent (type, tier) share to everyone. Called when
        /// that area's chest rows are all processed, so the area got exclusive
        /// first bite at its proportional share and leftovers still scavenge
        /// instead of stranding. Queues (not the reserved set alone) are the
        /// source of truth: ids already taken are skipped via Placed.
        /// </summary>
        public void ReleaseArea(string type, int tier, string area)        {
            var t = Math.Clamp(tier, 1, 5);
            var shared = SharedFor(type, t);
            if (_byArea.TryGetValue((type, t, area), out var q))
            {
                while (q.Count > 0)
                {
                    var cand = q.Dequeue();
                    if (Placed.Contains(cand)) continue;
                    shared.Enqueue(cand);
                }
                _byArea.Remove((type, t, area));
            }
            if (_reserved.TryGetValue((type, t), out var set))
                set.RemoveWhere(id => Placed.Contains(id) || shared.Contains(id));
        }

        /// <summary>
        /// Hands every unspent area share to everyone. Called after the chest
        /// phase so enemy-only areas (no chest rows, hence no per-area release)
        /// and disabled-chest configurations can't strand pool ids; enemy and
        /// shop draws then behave exactly as the old global release.
        /// </summary>
        public void ReleaseAll()
        {
            foreach (var (type, tier, area) in _byArea.Keys.ToList())
                ReleaseArea(type, tier, area);
            foreach (var set in _reserved.Values)
                set.Clear();
        }

        /// <summary>Pops the next id a global draw hasn't already taken.</summary>
        private bool TakeUnplaced(Queue<string> q, HashSet<string>? reserved, out string id)
        {
            id = "";
            while (q.Count > 0)
            {
                var cand = q.Dequeue();
                reserved?.Remove(cand);
                if (Placed.Contains(cand)) continue;
                Placed.Add(cand);
                id = cand;
                return true;
            }
            return false;
        }

        private bool TryTakeArea(string type, int tier, string area, out string id)
        {
            // Area share first, then the shared remainder. Shares are sticky:
            // an area's queue is only released to everyone once that area's
            // own chest rows are done (see RandomizeChests) or at ReleaseAll,
            // so early areas can no longer eat late areas' shares. The global
            // cascade (TakeUnique) skips reserved ids, so unreleased shares
            // are never spent by strangers — a miss here just falls through.
            if (area.Length > 0 && _byArea.TryGetValue((type, tier, area), out var q)
                && TakeUnplaced(q, ReservedFor(type, tier), out id))
                return true;
            var shared = SharedFor(type, tier);
            return TakeUnplaced(shared, null, out id);
        }

        /// <summary>Area-first gear draw; null when the area's share and the shared
        /// remainder are both spent (caller falls back to the global cascade).</summary>
        public string? TakeForArea(LootCatalog catalog, string type, int tier, string area, Random rng)
        {
            var t = Math.Clamp(tier, 1, 5);
            return TryTakeArea(type, t, area, out var id) ? id : TakeUnique(catalog, type, t, rng);
        }

        /// <summary>Area-first gear draw that never cascades below the requested
        /// tier; null when the area share and shared remainder are both spent.</summary>
        public string? TakeForAreaStrict(string type, int tier, string area)
        {
            var t = Math.Clamp(tier, 1, 5);
            return TryTakeArea(type, t, area, out var id) ? id : null;
        }

        /// <summary>Boss-row draw: area share first, then a tier-appropriate repeat
        /// once the cascade is dry (boss guarantees outrank uniqueness).</summary>
        public string? TakeForAreaOrRepeat(LootCatalog catalog, string type, int tier, string area, Random rng)
        {
            var t = Math.Clamp(tier, 1, 5);
            return TryTakeArea(type, t, area, out var id) ? id : TakeUniqueOrRepeat(catalog, type, t, rng);
        }

        /// <summary>
        /// Character-locked guaranteed weapon draw: unplaced same-tier weapons of
        /// <paramref name="owner"/> first, then a same-tier repeat (owner
        /// guarantees outrank uniqueness). Returns null only when that character
        /// has no weapon in the tier list at all.
        /// </summary>
        public string? TakeOwnedUniqueOrRepeat(
            LootCatalog catalog, IReadOnlyDictionary<string, string> owners,
            string owner, int tier, Random rng)
        {
            var t = Math.Clamp(tier, 1, 5);
            bool Mine(string id) =>
                owners.TryGetValue(id, out var o)
                && string.Equals(o, owner, StringComparison.OrdinalIgnoreCase);

            var unique = Pool(catalog, "Weapon", t)
                .Where(id => Mine(id) && !Placed.Contains(id)
                    && !ReservedFor("Weapon", t).Contains(id)
                    && WeaponAllowedAtTier(catalog, "Weapon", id, t))
                .ToList();
            if (unique.Count > 0)
            {
                var pick = unique[rng.Next(unique.Count)];
                Placed.Add(pick);
                return pick;
            }

            var repeat = Pool(catalog, "Weapon", t)
                .Where(id => Mine(id) && !PreSeeded.Contains(id)
                    && WeaponAllowedAtTier(catalog, "Weapon", id, t))
                .ToList();
            if (repeat.Count == 0) return null;
            var rep = repeat[rng.Next(repeat.Count)];
            Placed.Add(rep);
            return rep;
        }

        /// <summary>Same-tier repeat that avoids <paramref name="exclude"/>, so a
        /// multi-picto guaranteed package stays distinct even when the unique
        /// pool of that tier is already drained.</summary>
        public string? TakeDistinctRepeatAtTier(
            LootCatalog catalog, string type, int tier, Random rng, ICollection<string> exclude)
        {
            var t = Math.Clamp(tier, 1, 5);
            var pool = Pool(catalog, type, t)
                .Where(id => !PreSeeded.Contains(id) && !exclude.Contains(id)
                    && WeaponAllowedAtTier(catalog, type, id, t))
                .ToList();
            if (pool.Count == 0) return null;
            var pick = pool[rng.Next(pool.Count)];
            Placed.Add(pick);
            return pick;
        }
    }

    private static int EffectiveWeaponTier(LootCatalog catalog, string id, int baseTier)
    {
        if (StartingWeapons.TryGetValue(id, out var info))
        {
            // Gustave alone may roll T3 on his starter (even though he joins T1)
            if (info.Character.Equals("Gustave", StringComparison.OrdinalIgnoreCase))
                return 3; // directly T3 per spec – or allow 1-3 roll? fixed T3 per clarification
            return Math.Clamp(info.JoinTier, 1, 5);
        }
        return Math.Clamp(baseTier, 1, 5);
    }

    private static int RollScalingCount(int weaponTier, Random rng) => weaponTier switch
    {
        1 => 1,
        2 => 2,
        3 => rng.NextDouble() < 0.40 ? 2 : 3,
        4 => rng.NextDouble() < 0.20 ? 2 : 3,
        _ => 3,
    };

    /// <summary>
    /// Stored AttributeScaling grade for a weapon tier. The tooltip letter the
    /// player sees is NOT this value: BP_ItemInstance_Gear_Weapon adds the
    /// weapon's unlocked attribute-upgrade count (0..3, unlocked at weapon-
    /// quality ranks 2/4/5) to the stored grade, and a sum above S (index 5)
    /// renders as a BLANK letter in the shipped build. At max level (33,
    /// quality 5) the offset is +3, so only stored D (→A) and C (→S) are safe.
    /// Design validated in-game: tiers 1-3 read A when fully leveled, tiers
    /// 4-5 read S. NewEnumerator0=Invalid also renders as missing — never use.
    /// </summary>
    private static string GradeForTier(int weaponTier)
    {
        return weaponTier switch
        {
            >= 1 and <= 3 => "EAttributeScalingGrade::NewEnumerator1", // D -> A at max
            _ => "EAttributeScalingGrade::NewEnumerator2",             // C -> S at max
        };
    }

    private static readonly string[] ScalingAttributes =
    {
        "ECharacterAttribute::NewEnumerator0",
        "ECharacterAttribute::NewEnumerator1",
        "ECharacterAttribute::NewEnumerator3",
        "ECharacterAttribute::NewEnumerator4",
        "ECharacterAttribute::NewEnumerator5",
    };


    public static bool HasLeftoverAssets(string modDir) =>
        FindAsset(modDir, ChestsTable + ".uasset") != null
        || FindAsset(modDir, EnemiesTable + ".uasset") != null
        || FindAsset(modDir, WeaponsTable + ".uasset") != null
        || FindAsset(modDir, BattleGroupKit.EncountersTable + ".uasset") != null;

    public static int Apply(string modDir, Settings s, LootCatalog catalog, IReadOnlyList<string>? extraPictos)
    {
        _ = extraPictos; // pools come only from the Tier Editor now
        RestoreVanilla(modDir, "DT_jRPG_Encounters");
        RestoreVanilla(modDir, "DT_jRPG_Encounters_CleaTower");
        // DT_WorldMap_Encounters is staged and repointed by WorldMapVariantKit
        // (once the enemy table is in place), so no vanilla restore here.

        if (s.IsOff)
        {
            RestoreVanilla(modDir, ChestsTable);
            RestoreVanilla(modDir, EnemiesTable);
            RestoreVanilla(modDir, WeaponsTable);
            RestoreMerchantTables(modDir);
            return 0;
        }

        var seed = s.Seed >= 0 ? s.Seed : Environment.TickCount % 99_999_999;
        s.Seed = seed;
        var rng = new Random(seed);
        // One shared placement state across chests → drops → shops so no
        // picto/weapon is ever handed out twice in a pack.
        var placement = new LootPlacement();

        // Refresh the defense-picto ban from the packed table so created pictos
        // that grant Defense (e.g. Blighted Power III) are excluded from the
        // gear-banned areas too — the hardcoded list only covers vanilla ids.
        DefensePictos.Clear();
        DefensePictos.UnionWith(DefensePictosFrom(modDir));

        // Only stock ids the pack actually contains: created content whose kit
        // did not run (e.g. a magnitude variant with its toggle off) would
        // otherwise be handed out as a phantom shop/chest entry.
        _packedItems = PackedItemIds(modDir);

        // White-nevron dialogue grants draw first so their replacements are
        // reserved before anything else rolls; swapped originals go back to
        // pools so they stay obtainable somewhere.
        if (s.EnemyDrops)
        {
            var swaps = DialogueRewardKit.Apply(modDir, catalog, rng, placement);
            foreach (var original in swaps.Keys) placement.Placed.Remove(original);
        }

        if (!s.Chests && !s.PaintCages) RestoreVanilla(modDir, ChestsTable);
        if (!s.EnemyDrops && !s.MonsterStats) RestoreVanilla(modDir, EnemiesTable);
        if (!s.WeaponPassives && !s.WeaponStats) RestoreVanilla(modDir, WeaponsTable);
        if (!s.Merchants) RestoreMerchantTables(modDir);

        int n = 0;
        string? enemiesPath = null;
        string? weaponsPath = null;
        if (s.EnemyDrops || s.MonsterStats)
            enemiesPath = CopyTable(EnemiesTable, modDir);
        // Tutorials disabled: the Monoco's Station Stalact/Pelerin fights keep
        // their tutorial rows (and therefore their tutorial stats and drops),
        // but spawn the plain native actor instead of the tutorial blueprint.
        if (enemiesPath != null && TutorialDisableKit.IsDisabled(modDir))
            n += TutorialDisableKit.ApplyActorSwaps(modDir);
        // Design fight levels for story/venue bosses whose area level differs
        // from the level their targets were derived at. Must run before the
        // venue kits so clones are created at the corrected level.
        if (s.MonsterStats)
            StoryLevelKit.Apply(modDir);
        // Split the shared native rows out of the world-map fights before the
        // loot rolls, so a T4+ native no longer leaks its tier into them.
        if (enemiesPath != null)
            WorldMapVariantKit.Build(modDir);
        // Endgame areas borrow low-tier rows (Flying Manor is a gauntlet of
        // them): clone those into T5-only variants so they roll T5 loot + the
        // rare Perfect Catalyst without leaking down into their native areas.
        if (enemiesPath != null)
            EndgameVariantKit.Build(modDir, catalog);
        // Reused bosses: a row fought in an override-bearing world fight would
        // otherwise pay its native band there. Clone it to a venue-pinned variant.
        if (enemiesPath != null)
            BossVenueKit.Build(modDir, catalog);
        // Neutral 1.0 defense bases on the staged archetype tables so the
        // per-row boss-curve defense pins land exactly (vanilla bases are 0).
        if (s.MonsterStats && enemiesPath != null)
            EnemyDefenseKit.StageArchetypeDefense(modDir);
        if (s.WeaponPassives || s.WeaponStats)
            // WeaponPassiveSlotKit stages the definitions table with the fifth
            // slot already added; keep it instead of clobbering it with vanilla.
            weaponsPath = FindAsset(modDir, WeaponsTable + ".uasset") ?? CopyTable(WeaponsTable, modDir);

        // Spread each tier's pictos/weapons across areas in proportion to their
        // drop points before anything draws, so a tier's gear turns up across all
        // its areas instead of draining into whichever area rolls first. Own RNG
        // so the main draw stream is unaffected.
        if (s.Chests || s.PaintCages || s.EnemyDrops)
            placement.AllocateByArea(catalog, new Random(seed ^ 0x51ED2701),
                includeChests: s.Chests || s.EnemyDrops,
                includePaintCages: s.PaintCages || s.EnemyDrops);

        // Priority order: Boss > Chests / Paint Cages > Regular. Bosses claim
        // their guaranteed gear first, so encounter guarantees get first pick of
        // tier-correct unique gear; chests then take the remainder; the
        // regular/elite/overworld draws happen last (below).
        var reservedBossGear = (s.EnemyDrops && enemiesPath != null)
            ? ReserveBossGear(enemiesPath, catalog, rng, placement)
            : new Dictionary<string, List<(string ItemId, int Qty, double Chance)>>(StringComparer.Ordinal);

        if (s.Chests || s.PaintCages)
            n += RandomizeChests(modDir, catalog, rng, s, placement);

        // Chest rows are done: release every unspent area share (per-area
        // releases already fired as areas completed; this catches enemy-only
        // areas and disabled-chest configurations) so nothing strands and
        // enemy/shop draws see the full remainder.
        if (s.Chests || s.PaintCages || s.EnemyDrops)
            placement.ReleaseAll();

        if (s.EnemyDrops && enemiesPath != null)
            n += RandomizeEnemyDrops(enemiesPath, catalog, rng, s, placement, reservedBossGear);
        if (s.MonsterStats && enemiesPath != null)
            n += RandomizeMonsterStats(enemiesPath, catalog, rng, s);
        if (s.Merchants) n += RandomizeMerchants(modDir, catalog, rng, s, placement);
        if ((s.WeaponPassives || s.WeaponStats) && weaponsPath != null)
            n += RandomizeWeapons(weaponsPath, catalog, rng, s);

        // Experimental battle groups: this runs last so loot/stat passes see the
        // vanilla rosters and variant repoints are already in place. The field
        // capacity patch composes with Party of Five, which stages the same
        // BattleManager/FL/CharacterBattleBase assets later in the pack.
        if (s.BattleGroups || s.RandomBattles)
            n += BattleGroupKit.ApplyCapacity(modDir,
                s.FieldCapacityOverride > 0 ? s.FieldCapacityOverride : s.MaxEnemiesPerBattle);
        if (s.BattleGroups)
            n += BattleGroupKit.RandomizeEncounters(modDir, catalog, rng, s);
        if (s.RandomBattles)
        {
            n += BattleGroupKit.BuildRuntimeBattles(modDir, catalog, rng, s);
            // Fail loudly rather than shipping a pack whose world enemies never
            // re-roll (e.g. an area filter that matched nothing).
            BattleGroupKit.VerifyRuntimeBattles(modDir);
        }
        return n;
    }

    // ───────────────────── bands / elements ─────────────────────

    public static (double Lo, double Hi) StatBand(int tier, Settings? s = null)
    {
        var i = Math.Clamp(tier, 1, 5) - 1;
        var lo = s?.StatLo is { Length: >= 5 } a ? a[i] : DefaultStatLo[i];
        var hi = s?.StatHi is { Length: >= 5 } b ? b[i] : DefaultStatHi[i];
        return (lo, hi);
    }

    public static (double Lo, double Hi) AttackBand(int tier, Settings? s = null)
    {
        var i = Math.Clamp(tier, 1, 5) - 1;
        var lo = s?.AttackLo is { Length: >= 5 } a ? a[i] : DefaultAttackLo[i];
        var hi = s?.AttackHi is { Length: >= 5 } b ? b[i] : DefaultAttackHi[i];
        return (lo, hi);
    }

    public static double BossHpFactor(int tier, Settings? s = null)
    {
        var i = Math.Clamp(tier, 1, 5) - 1;
        return s?.BossHp is { Length: >= 5 } a ? a[i] : DefaultBossHp[i];
    }

    public static IReadOnlyList<string> WeaponElementPool(int tier)
    {
        // EAttackType: Fire 0, Ice 1, Lightning 2, Earth 3, Dark 4, Light 5, Physical 6, Void 7
        var list = new List<string>
        {
            "NewEnumerator6", "NewEnumerator3", "NewEnumerator0", "NewEnumerator2", "NewEnumerator1",
        };
        if (tier >= 4)
        {
            list.Add("NewEnumerator5");
            list.Add("NewEnumerator4");
        }
        if (tier >= 5)
            list.Add("NewEnumerator7");
        return list;
    }

    private static double RollBand(int tier, Random rng, Settings s)
    {
        var (lo, hi) = StatBand(tier, s);
        if (hi < lo) (lo, hi) = (hi, lo);
        return lo + rng.NextDouble() * (hi - lo);
    }

    private static double RollAttackBand(int tier, Random rng, Settings s)
    {
        var (lo, hi) = AttackBand(tier, s);
        if (hi < lo) (lo, hi) = (hi, lo);
        return lo + rng.NextDouble() * (hi - lo);
    }

    private static readonly string[] EnemyScaleSkip = { "Chroma", "Experience" };
    private static readonly HashSet<string> BossArchetypes =
        new(StringComparer.OrdinalIgnoreCase) { "Boss", "Alpha", "SuperBoss" };

    /// <summary>HP, Experience and Chroma all use this factor (vanilla × N).</summary>
    private static readonly Dictionary<string, double> StoryHpOverride = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OL_MirrorRenoir_FirstFight"] = 21,
        ["MM_MirrorRenoir"] = 12,
        ["L_Creation"] = 100,
        ["L_Boss_Curator"] = 100,
        // Visages Axon pair (decoy Visages + true Mask Keeper): flat x21 on
        // HP, Experience and Chroma (the StoryRewardFields), on top of their T3
        // stat band.
        ["MF_Axon_Visages"] = 21,
        ["MF_Axon_MaskKeeper_VisagesPhase2"] = 21,
        // Sirene boss: 30% above the Visages Axon pair, HP + Experience +
        // Chroma (no area curve-lift applies — Sirene is not an Act 3 /
        // Frozen Hearts remap). Other stats take StatBonusOverride below.
        ["SI_Axon_Sirene"] = 27.3,
    };

    /// <summary>
    /// Absolute effective-HP pins. Unlike <see cref="StoryHpOverride"/> (a
    /// vanilla-relative factor), these fix the row's in-game HP by deriving the
    /// <c>EnemyScaling.HP</c> that, at the row's encounter level, multiplies the
    /// archetype HP to the target. Experience and Chroma follow the same factor
    /// so the rewards track the health pool. Deterministic: computed from the
    /// archetype curves, adds no RNG draws.
    /// </summary>
    private static readonly Dictionary<string, double> StoryAbsoluteHp = new(StringComparer.OrdinalIgnoreCase)
    {
        // The Paintress: pinned at exactly 33,333,333 HP (archetype[43] × scaling).
        ["MO_Boss_Paintress"] = 33_333_333,
        // Act 3 Lumière finale trio (see AbsoluteStatPins for their ATK/SPD).
        // Echassier: fast, very high HP, low damage.
        ["LU_EchassierAlpha"] = 900_000_000,
        ["L_Creation"] = 1_300_000_000,
        ["L_Boss_Curator"] = 2_000_000_000,
        // Endless Tower Eveque alphas: the floors are multi-enemy encounters,
        // so the encounter level stays at its venue level (85/86/87) and the
        // venue clone's scaling is pinned to the documented T4 target.
        ["BV_SM_Boss_Eveque_Earth_ALPHA_85"] = 532_557_103,
        ["BV_SM_Boss_Eveque_Lightning_ALPHA_86"] = 532_557_103,
        ["BV_SM_Boss_Eveque_Ice_ALPHA_CleaTower_87"] = 532_557_103,
        // Heart of the Canvas narrative finale: both phases pinned to ~333M.
        ["FinalBoss_Verso"] = 333_000_000,
        ["FinalBoss_Maelle"] = 333_000_000,
    };

    /// <summary>
    /// White-nevron T1/T2 durability floor. These optional bosses kept their
    /// vanilla scaling and only received the generic tier boss factor, so they
    /// ended up below the regular boss in the same area (While Rocher was 0.07x
    /// Lampmaster; White Portier 0.11x Painted Renoir). Each entry pins effective
    /// HP to that nearest regular boss at the row's real venue level; HP,
    /// Experience and Chroma follow the same factor. T4+ white nevrons are on
    /// the absolute curve already and are deliberately absent. The Troubadour
    /// dodge-trial row is intentionally low and is also absent.
    /// Deterministic: no RNG draws.
    /// </summary>
    private static readonly Dictionary<string, (int Level, double Hp)> WhiteNevronHpTargets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["QUEST_DemineurWithoutMine"] = (8, 18_252),   // Goblu, Flying Waters
            ["QUEST_DemineurWithMine"] = (8, 18_252),
            ["QUEST_JarNeedLight"] = (3, 3_627),           // Eveque, Spring Meadows
            ["QUEST_HexgaLuster"] = (19, 56_890),          // Lampmaster, Stone Wave Cliffs
            ["QUEST_Rocher_HexgaQuest"] = (19, 56_890),
            ["QUEST_TroubadourCantPlay_Kill"] = (30, 404_560), // Dualliste P1, nearest story boss
            ["Portier_DoorSoul"] = (28, 1_570_023),        // Painted Renoir, same level
        };

    /// <summary>
    /// Absolute effective-combat-stat pins for individual rows (like
    /// <see cref="StoryAbsoluteHp"/> but per stat). Each entry fixes the final
    /// in-game value: the staged <c>EnemyScaling</c> is set so
    /// <c>archetype[encounter level] x scaling</c> equals the target. Values are
    /// keyed by <c>EnemyScaling</c> field name. Deterministic: no RNG draws.
    /// </summary>
    /// Act 3 finale trio (Speedster / Bruiser / Apex): the specialist stat runs
    /// at <see cref="FinaleSpecialistShare"/> × the level-99 curve maximum and
    /// the other two at <see cref="FinaleSupportShare"/> × it — ATK
    /// <see cref="CurveAtkMax"/> (72k/39.6k), Speed <see cref="CurveSpdMax"/>
    /// (18k/9.9k), DEF <see cref="EnemyDefenseKit.MaxDefense"/> (20.78M/11.43M).
    /// Derived from the curves so a refit flows through (the old literals were
    /// left behind by the ATK/SPD refits and the defense reshape). Echassier
    /// scales to the level-99 curves (its HP pin stays derived at its true
    /// spawn level 48). HP stays absolutely pinned (see StoryAbsoluteHp).
    private const double FinaleSpecialistShare = 1.2;
    private const double FinaleSupportShare = 0.66;
    private static readonly Dictionary<string, Dictionary<string, double>> AbsoluteStatPins =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Echassier (Speedster): speed 1.2×, ATK/DEF 0.66×.
            ["LU_EchassierAlpha"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PhysicalAttack"] = FinaleSupportShare * CurveAtkMax(99),
                ["Speed"] = FinaleSpecialistShare * CurveSpdMax(99),
                ["PhysicalDefense"] = FinaleSupportShare * EnemyDefenseKit.MaxDefense(99),
                ["MagicalDefense"] = FinaleSupportShare * EnemyDefenseKit.MaxDefense(99),
            },
            // Creation (Bruiser): defense 1.2×, ATK/SPD 0.66×.
            ["L_Creation"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PhysicalAttack"] = FinaleSupportShare * CurveAtkMax(99),
                ["Speed"] = FinaleSupportShare * CurveSpdMax(99),
                ["PhysicalDefense"] = FinaleSpecialistShare * EnemyDefenseKit.MaxDefense(99),
                ["MagicalDefense"] = FinaleSpecialistShare * EnemyDefenseKit.MaxDefense(99),
            },
            // Renoir (Apex): attack 1.2×, SPD/DEF 0.66×.
            ["L_Boss_Curator"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PhysicalAttack"] = FinaleSpecialistShare * CurveAtkMax(99),
                ["Speed"] = FinaleSupportShare * CurveSpdMax(99),
                ["PhysicalDefense"] = FinaleSupportShare * EnemyDefenseKit.MaxDefense(99),
                ["MagicalDefense"] = FinaleSupportShare * EnemyDefenseKit.MaxDefense(99),
            },
            // Ono Puncho (the 9,999-damage Gestral Village quest): a deliberately
            // slow, paper-thin target for the one-hit objective. HP and attack
            // keep the normal T5 boss curve; only Speed and both defense stats
            // are pinned, and the curve below skips any field listed here.
            ["Quest_GestralSumo9999"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Speed"] = 500,
                ["PhysicalDefense"] = 500,
                ["MagicalDefense"] = 500,
            },
            // The Paintress (story, Monolith L43): the T3 story bosses were
            // all 0-DEF glass cannons. Give her a real but not wall-like
            // defense: 975 ≈ the expected level-111 weapon Attack
            // (DT_EquipmentScalingTable_Weapons Attack[111] = 974.4), so a
            // weapon-only hit is roughly halved by A²/(A+2D).
            ["MO_Boss_Paintress"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PhysicalDefense"] = 975,
                ["MagicalDefense"] = 975,
            },
        };

    /// <summary>
    /// Fight level used to derive an absolute pin when it differs from the
    /// row's table/encounter level. Ono Puncho's row starts at Level 0 and is
    /// raised to 100 later in the same pass, so the pin must derive at 100
    /// (the runtime level) rather than 0.
    /// </summary>
    private static readonly Dictionary<string, int> AbsolutePinLevels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Quest_GestralSumo9999"] = 100,
        };

    // ── boss / chromatic-alpha target curves ─────────────────────────────
    // HP follows a gently accelerating curve (L21 = 11M -> L99 = 900M soft cap);
    // Attack, Speed and Defense are max curves split through the shared pool
    // (see ResolveShares: atk²+spd²+def² ≤ 0.75, balanced 50/50/50).
    private const double CurveP = 1.8;
    private static double CurveT(int level) => Math.Clamp((level - 21) / 78.0, 0, 1);
    public static double CurveHp(int level) =>
        Math.Min(900_000_000, 11_000_000 + 889_000_000 * Math.Pow(CurveT(level), CurveP));
    // ATK curve refit: base tripled (2,000 -> 6,000 at L21) and top ×5
    // (12,000 -> 60,000 at L99), same 1.8 shape, so the range grows from
    // 10,000 to 54,000. Everything downstream multiplies the row's attack
    // share into this max, so the pool split is unchanged.
    private static double CurveAtkMax(int level) => 6_000 + 54_000 * Math.Pow(CurveT(level), CurveP);
    private static double CurveSpdMax(int level) => 5_000 + 10_000 * Math.Pow(CurveT(level), CurveP);

    // Reward rule: boss/alpha XP is expressed directly in level-ups, rising
    // steadily from 1 level at the T4 floor (level 60) to 3 levels at level 99.
    private const int RewardLmin = 60;
    private const double RewardXpPerLevel = 33_333_333; // the flat level cost
    public static double RewardLevels(int level) =>
        Math.Clamp(1 + 2.0 * (level - RewardLmin) / (99 - RewardLmin), 1, 3);

    /// <summary>Attack focus of the shared pool per boss/alpha (see
    /// <see cref="ResolveShares"/>). Default 0.70 when unlisted.</summary>
    private static readonly Dictionary<string, double> BossAtkShare = new(StringComparer.OrdinalIgnoreCase)
    {
        // role-spread rewrite: full ternary coverage (see work/plan_shares.py)
        // NUKER
        ["Boss_LampMaster_ALPHA"] = 0.70,
        ["CT_Boss_Paintress_CleaTower"] = 0.73,
        ["CW_LampMasterAlpha"] = 0.75,
        ["Duolliste_A"] = 0.78,
        ["Duolliste_B"] = 0.81,
        ["FB_Dualliste_CleasTower"] = 0.84,
        ["QUEST_JudgeOfMercy"] = 0.86,
        ["RD_Creation_Alpha"] = 0.89,
        ["SC_LampMaster_CleasTower"] = 0.92,
        ["YF_Chevaliere_A_Alpha"] = 0.95,
        ["YF_Chevaliere_B_Alpha"] = 0.97,
        ["YF_Chevaliere_C_Alpha"] = 1.00,
        // GLASS
        ["GO_Demineur_ALPHA"] = 0.55,
        ["MF_Moissonneuse_ALPHA"] = 0.59,
        ["MM_Danseuse_Alpha"] = 0.63,
        ["MM_Danseuse_CloneAlpha"] = 0.67,
        ["RE_OrphelinHammer_Alpha"] = 0.71,
        ["RE_OrphelinNail_Alpha"] = 0.74,
        ["RE_OrphelinSpoon_Alpha"] = 0.78,
        ["SM_Abbest_Alpha"] = 0.82,
        ["WM_Petank_Alpha"] = 0.86,
        ["YF_Glaise_ALPHA"] = 0.90,
        // BRUISER
        ["Boss_Clea_ALPHA"] = 0.55,
        ["CFH_Dualliste"] = 0.58,
        ["CT_Boss_Curator_CleaTower"] = 0.61,
        ["CT_MaskKeeper_NoMask_CleaTwoer"] = 0.64,
        ["MF_Chapelier_ALPHA"] = 0.67,
        ["MM_Braseleur_ALPHA"] = 0.70,
        ["QUEST_Golgra_DarkArena"] = 0.73,
        ["QUEST_Golgra_SacredRiver"] = 0.76,
        ["RC_Alicia"] = 0.79,
        ["RD_Creation"] = 0.82,
        ["RE_Veilleur_Alpha"] = 0.85,
        // BALANCED
        ["CFH_Boss_Eveque_Fire_ALPHA"] = 0.35,
        ["CFH_Boss_Eveque_Ice_ALPHA"] = 0.38,
        ["CFH_Boss_Eveque_Lightning_ALPHA"] = 0.41,
        ["FB_Benisseur_Alpha"] = 0.45,
        ["GO_Goblu_ALPHA"] = 0.48,
        ["GO_Luster_Alpha_CleasTower"] = 0.51,
        ["SM_Boss_Eveque_Earth_ALPHA"] = 0.54,
        ["SM_Boss_Eveque_Fire_ALPHA_CleaTower"] = 0.57,
        ["SM_Boss_Eveque_Ice_ALPHA_CleaTower"] = 0.60,
        ["SM_Boss_Eveque_Lightning_ALPHA"] = 0.64,
        ["WM_Serpenphare"] = 0.67,
        ["WM_Sprong"] = 0.70,
        // WALL
        ["AS_PotatoBag_Tank_ALPHA"] = 0.25,
        ["Boss_Simon"] = 0.27,
        ["Boss_Simon_Phase2"] = 0.29,
        ["CFH_Boss_Clea"] = 0.31,
        ["CFH_Gargant"] = 0.34,
        ["CFH_Goblu"] = 0.36,
        ["CFH_LampMaster"] = 0.38,
        ["GO_Cruler_ALPHA"] = 0.40,
        ["MF_GrossTete"] = 0.42,
        ["ML_Gargant_Gold"] = 0.44,
        ["Portier_DoorSoul"] = 0.46,
        ["SM_Portier_ALPHA"] = 0.49,
        ["TS_PotatoBag_Boss_Twilight"] = 0.51,
        ["VD_Osquio"] = 0.53,
        ["YF_Jar_ALPHA"] = 0.55,
        // SKIRMISH
        ["CP_Benisseur"] = 0.22,
        ["CP_Contorsionniste"] = 0.25,
        ["CP_Glaise"] = 0.28,
        ["CP_Jar"] = 0.31,
        ["CP_Stalact"] = 0.34,
        ["QUEST_SleepingBenisseur"] = 0.38,
        ["VD_Barbasucette_Alpha"] = 0.41,
        ["VD_Franctale_Alpha"] = 0.44,
        ["VD_Licorne_Alpha"] = 0.47,
        ["VD_Machinapieds_Alpha"] = 0.50,
        // SPEEDSTER
        ["AS_PotatoBag_Mage_ALPHA"] = 0.24,
        ["AS_PotatoBag_Ranger_ALPHA"] = 0.28,
        ["Boss_Simon_ALPHA"] = 0.32,
        ["QUEST_Danseuse_DanceClass_Kill"] = 0.36,
        ["VD_Licornapieds"] = 0.40,
        // SOLO
        ["CFH_GrossTete"] = 0.20,
        ["WM_Mime_Bald"] = 0.20,
        // FIXED
        ["Duolliste_P2"] = 1.00,
        ["MF_Boucheclier_ALPHA"] = 0.70,
        ["MM_Gargant"] = 0.64,
        ["SM_Boss_Eveque_Ice_ALPHA"] = 0.72,
    };

    /// <summary>Default defense focus for curve rows without an entry below.</summary>
    public const double DefaultDefShare = 0.30;

    /// <summary>Per-stat floor of the pool (see <see cref="ResolveShares"/>).</summary>
    public const double ShareFloor = 0.20;

    /// <summary>Squared-norm budget of the pool: 50/50/50 uses it exactly.</summary>
    public const double ShareBudgetSq = 0.75;

    /// <summary>
    /// Defense focus of the shared pool per boss/alpha. Walls and bruisers take
    /// 0.50–0.60, the balanced middle 0.40, skirmishers the 0.30 default and
    /// glass evasives 0.20. The Act 3 finale trio is absent: absolute pins set
    /// their stats exactly, so shares never apply to them.
    /// </summary>
    private static readonly Dictionary<string, double> BossDefShare = new(StringComparer.OrdinalIgnoreCase)
    {
        // NUKER
        ["Boss_LampMaster_ALPHA"] = 0.35,
        ["CT_Boss_Paintress_CleaTower"] = 0.34,
        ["CW_LampMasterAlpha"] = 0.32,
        ["Duolliste_A"] = 0.31,
        ["Duolliste_B"] = 0.30,
        ["FB_Dualliste_CleasTower"] = 0.28,
        ["QUEST_JudgeOfMercy"] = 0.27,
        ["RD_Creation_Alpha"] = 0.25,
        ["SC_LampMaster_CleasTower"] = 0.24,
        ["YF_Chevaliere_A_Alpha"] = 0.23,
        ["YF_Chevaliere_B_Alpha"] = 0.21,
        ["YF_Chevaliere_C_Alpha"] = 0.20,
        // GLASS
        ["GO_Demineur_ALPHA"] = 0.28,
        ["MF_Moissonneuse_ALPHA"] = 0.27,
        ["MM_Danseuse_Alpha"] = 0.26,
        ["MM_Danseuse_CloneAlpha"] = 0.25,
        ["RE_OrphelinHammer_Alpha"] = 0.24,
        ["RE_OrphelinNail_Alpha"] = 0.24,
        ["RE_OrphelinSpoon_Alpha"] = 0.23,
        ["SM_Abbest_Alpha"] = 0.22,
        ["WM_Petank_Alpha"] = 0.21,
        ["YF_Glaise_ALPHA"] = 0.20,
        // BRUISER
        ["Boss_Clea_ALPHA"] = 0.38,
        ["CFH_Dualliste"] = 0.41,
        ["CT_Boss_Curator_CleaTower"] = 0.43,
        ["CT_MaskKeeper_NoMask_CleaTwoer"] = 0.46,
        ["MF_Chapelier_ALPHA"] = 0.49,
        ["MM_Braseleur_ALPHA"] = 0.52,
        ["QUEST_Golgra_DarkArena"] = 0.54,
        ["QUEST_Golgra_SacredRiver"] = 0.57,
        ["RC_Alicia"] = 0.60,
        ["RD_Creation"] = 0.62,
        ["RE_Veilleur_Alpha"] = 0.65,
        // BALANCED
        ["CFH_Boss_Eveque_Fire_ALPHA"] = 0.30,
        ["CFH_Boss_Eveque_Ice_ALPHA"] = 0.33,
        ["CFH_Boss_Eveque_Lightning_ALPHA"] = 0.35,
        ["FB_Benisseur_Alpha"] = 0.38,
        ["GO_Goblu_ALPHA"] = 0.41,
        ["GO_Luster_Alpha_CleasTower"] = 0.44,
        ["SM_Boss_Eveque_Earth_ALPHA"] = 0.46,
        ["SM_Boss_Eveque_Fire_ALPHA_CleaTower"] = 0.49,
        ["SM_Boss_Eveque_Ice_ALPHA_CleaTower"] = 0.52,
        ["SM_Boss_Eveque_Lightning_ALPHA"] = 0.55,
        ["WM_Serpenphare"] = 0.57,
        ["WM_Sprong"] = 0.60,
        // WALL
        ["AS_PotatoBag_Tank_ALPHA"] = 0.50,
        ["Boss_Simon"] = 0.53,
        ["Boss_Simon_Phase2"] = 0.56,
        ["CFH_Boss_Clea"] = 0.59,
        ["CFH_Gargant"] = 0.61,
        ["CFH_Goblu"] = 0.64,
        ["CFH_LampMaster"] = 0.67,
        ["GO_Cruler_ALPHA"] = 0.70,
        ["MF_GrossTete"] = 0.73,
        ["ML_Gargant_Gold"] = 0.76,
        ["Portier_DoorSoul"] = 0.79,
        ["SM_Portier_ALPHA"] = 0.81,
        ["TS_PotatoBag_Boss_Twilight"] = 0.84,
        ["VD_Osquio"] = 0.87,
        ["YF_Jar_ALPHA"] = 0.90,
        // SKIRMISH
        ["CP_Benisseur"] = 0.35,
        ["CP_Contorsionniste"] = 0.38,
        ["CP_Glaise"] = 0.42,
        ["CP_Jar"] = 0.45,
        ["CP_Stalact"] = 0.48,
        ["QUEST_SleepingBenisseur"] = 0.52,
        ["VD_Barbasucette_Alpha"] = 0.55,
        ["VD_Franctale_Alpha"] = 0.58,
        ["VD_Licorne_Alpha"] = 0.62,
        ["VD_Machinapieds_Alpha"] = 0.65,
        // SPEEDSTER
        ["AS_PotatoBag_Mage_ALPHA"] = 0.24,
        ["AS_PotatoBag_Ranger_ALPHA"] = 0.27,
        ["Boss_Simon_ALPHA"] = 0.31,
        ["QUEST_Danseuse_DanceClass_Kill"] = 0.34,
        ["VD_Licornapieds"] = 0.38,
        // SOLO
        ["CFH_GrossTete"] = 1.00,
        ["WM_Mime_Bald"] = 0.20,
        // FIXED
        ["Duolliste_P2"] = 0.20,
        ["MF_Boucheclier_ALPHA"] = 0.50,
        ["MM_Gargant"] = 0.60,
        ["SM_Boss_Eveque_Ice_ALPHA"] = 0.40,
        // Off-sheet curve rows (unchanged).
        ["MM_Braseleur"] = 0.50,
        ["Quest_GestralSumo9999"] = 0.40,
        ["VD_GestralBalloon"] = 0.40,
    };

    /// <summary>
    /// Resolves the (attack, speed, defense) pool shares for one row from its
    /// configured attack/defense focuses. Feasible by construction: floors are
    /// clamped first, speed fills the quadratic budget
    /// (atk²+spd²+def² ≤ 0.75, exact at 50/50/50), and over-budget requests
    /// scale attack/defense down to the floor-pinned remainder. Deterministic:
    /// no RNG draws.
    /// </summary>
    public static (double Atk, double Spd, double Def) ResolveShares(double atkCfg, double defCfg)
    {
        var a = Math.Clamp(atkCfg, ShareFloor, 1.0);
        var d = Math.Clamp(defCfg, ShareFloor, 1.0);
        if (a * a + d * d + ShareFloor * ShareFloor <= ShareBudgetSq)
            return (a, Math.Sqrt(ShareBudgetSq - a * a - d * d), d);
        var k = Math.Sqrt((ShareBudgetSq - ShareFloor * ShareFloor) / (a * a + d * d));
        a *= k; d *= k;
        if (d < ShareFloor) { d = ShareFloor; a = Math.Sqrt(ShareBudgetSq - 2 * ShareFloor * ShareFloor); }
        else if (a < ShareFloor) { a = ShareFloor; d = Math.Sqrt(ShareBudgetSq - 2 * ShareFloor * ShareFloor); }
        return (a, ShareFloor, d);
    }

    /// <summary>Rows that keep their own story treatment / absolute pins instead
    /// of the curve (Painted Renoirs, the Paintress, the Act 3 finale trio).</summary>
    private static readonly HashSet<string> CurveExceptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "OL_MirrorRenoir_FirstFight", "MM_MirrorRenoir",
        "MO_Boss_Paintress",
        "LU_EchassierAlpha", "L_Creation", "L_Boss_Curator",
        // Endless Tower Eveque venue clones: multi-enemy floors keep their
        // venue level (85/86/87), so their HP is an absolute pin rather than a
        // curve value (see StoryAbsoluteHp).
        "BV_SM_Boss_Eveque_Earth_ALPHA_85",
        "BV_SM_Boss_Eveque_Lightning_ALPHA_86",
        "BV_SM_Boss_Eveque_Ice_ALPHA_CleaTower_87",
    };

    /// <summary>Clone rows follow their main (HP 10%, ATK/SPD 50%).</summary>
    private static readonly Dictionary<string, string> BossCloneOf = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MM_Danseuse_CloneAlpha"] = "MM_Danseuse_Alpha",
        // White-nevron dance-class clone: a mid-fight re-summon, so it takes the
        // same 10% HP / 50% ATK-SPD-DEF clone treatment as the Danseuse alpha.
        ["QUEST_Danseuse_DanceClass_Clone"] = "QUEST_Danseuse_DanceClass_Kill",
    };

    /// <summary>
    /// A re-summoned clone is 10% HP / 50% offences, so it also pays 10% XP and
    /// chroma. Leaving the clone on the main body's reward rate turned one
    /// overworld Danseuse into a ~16M-XP-per-summon farm (200 kills ≈ 3.2B,
    /// nearly 100 levels). Deterministic: no RNG draws.
    /// </summary>
    private const double CloneRewardFactor = 0.1;

    /// <summary>Fight level used for the curve and the archetype lookup: an
    /// explicit encounter override wins, then the curated enemy level, then the
    /// row's table level.</summary>
    private static int CurveLevel(string id, int tableLevel, LootCatalog.EnemyInfo? enemy)
    {
        if (LootCatalog.EncounterLevelOverrides.TryGetValue(id, out var lvl) && lvl > 0) return lvl;
        if (enemy is { Level: > 0 }) return enemy.Level;
        return tableLevel > 0 ? tableLevel : 1;
    }

    /// <summary>Absolute HP/ATK/SPD/DEF targets for a boss/alpha row, or false
    /// when the row keeps its story treatment. Attack/speed/defense shares come
    /// resolved from the pool (see <see cref="ResolveShares"/>).</summary>
    private static bool TryCurveTarget(
        string id, LootCatalog.EnemyInfo? enemy, int tableLevel, LootCatalog catalog,
        double atkShare, double spdShare, double defShare, bool extremeDefense,
        out double hp, out double atk, out double spd, out double def)
    {
        hp = atk = spd = def = 0;
        if (CurveExceptions.Contains(id) || StoryFixedDrops.ContainsKey(id)) return false;
        if (BossCloneOf.TryGetValue(id, out var main))
        {
            catalog.Enemies.TryGetValue(main, out var mainInfo);
            int mlvl = CurveLevel(main, tableLevel, mainInfo);
            double mshare = BossAtkShare.GetValueOrDefault(main, 0.70);
            var (_, sMain, dMain) = ResolveShares(mshare,
                BossDefShare.GetValueOrDefault(main, DefaultDefShare));
            hp = CurveHp(mlvl) * 0.1;
            atk = mshare * CurveAtkMax(mlvl) * 0.5;
            spd = sMain * CurveSpdMax(mlvl) * 0.5;
            def = dMain * EnemyDefenseKit.MaxDefense(mlvl, extremeDefense) * 0.5;
            return true;
        }
        int level = CurveLevel(id, tableLevel, enemy);
        hp = CurveHp(level);
        atk = atkShare * CurveAtkMax(level);
        spd = spdShare * CurveSpdMax(level);
        def = defShare * EnemyDefenseKit.MaxDefense(level, extremeDefense);
        return true;
    }

    /// <summary>
    /// Extra multiplier applied on top of the normal per-field randomization for
    /// every combat stat except <see cref="StoryRewardFields"/> (HP/Experience/
    /// Chroma, which follow their own override). Deterministic: multiplies the
    /// computed factor, adding no RNG draws.
    /// </summary>
    private static readonly Dictionary<string, double> StatBonusOverride = new(StringComparer.OrdinalIgnoreCase)
    {
        // Sirene boss: +11% on top of the rolled T3 bands.
        ["SI_Axon_Sirene"] = 1.11,
    };

    /// <summary>
    /// Per-row outgoing-damage multiplier: scales the row's attack factor so the
    /// enemy hits N× harder. Applied only to PhysicalAttack/MagicalAttack, on
    /// top of the attack band roll and any <see cref="StatBonusOverride"/>; never
    /// touches HP, Speed, defences or rewards. Deterministic: no RNG draws.
    /// </summary>
    private static readonly Dictionary<string, double> DamageMultiplierOverride = new(StringComparer.OrdinalIgnoreCase)
    {
        // Story bosses: Painted Renoir ×3 (triple), The Paintress ×2 (double).
        ["MM_MirrorRenoir"] = 3,
        ["MO_Boss_Paintress"] = 2,
    };

    /// <summary>
    /// Absolute-HP tuning for the Dualliste story fight and its two phase
    /// presentation rows. Each bar is pinned to an absolute scaling of 8.0
    /// (~404.6K HP), not the old 15.0/16.0: the LR boss refills to max HP for
    /// phase 2, so two bars total ~809K, the intended ~800K. The factors are
    /// vanilla-relative (8.0 / vanilla scaling), i.e. ×20 over P1's 0.4,
    /// ×2 over P2's 4.0 and ×10.667 over LR's 0.75.
    /// The R/L elite solos instead take <see cref="DuallisteHpFactor"/> below;
    /// everything else keeps the rows' normal T2 boss treatment.
    /// Deterministic lookup: no RNG draws.
    /// </summary>
    private static readonly Dictionary<string, double> BossHpOverride = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FB_Dualliste_Phase1"] = 8.0 / 0.4,
        ["FB_Dualliste_Phase2"] = 8.0 / 4.0,
        ["FB_DuallisteLR"] = 8.0 / 0.75,
    };

    /// <summary>
    /// Flat-HP rule for the T2 Dualliste solos: these rows multiply HP by
    /// <see cref="DuallisteHpFactor"/> and then by the normal randomizer boss
    /// factor, and take flat <see cref="DuallisteAttackFactor"/> attacks instead
    /// of the T2 band roll. Explicit row set (not substring matching) so
    /// CFH/GDC/CleasTower/ALPHA and endgame Duolliste rows can never match.
    /// LR and P1/P2 take the absolute per-bar pin in <see cref="BossHpOverride"/>.
    /// Deterministic: no net RNG draws (dummy draws hold stream position).
    /// </summary>
    private static readonly HashSet<string> DuallisteStoryRows = new(StringComparer.OrdinalIgnoreCase)
    {
        "FB_DuallisteR", "FB_DuallisteL", "FB_DuallisteLR",
    };

    /// <summary>
    /// Dualliste solo HP base, for the R/L elite rows only. Back to the
    /// original ×8 after loot testing (the ×0.1 test value let the fight end
    /// in a hit or two); the generic randomizer boss factor still multiplies
    /// on top, so the BossHp setting stays meaningful. LR uses the absolute
    /// 8.0 per-bar pin instead, because it is a two-bar story fight.
    /// </summary>
    private const double DuallisteHpFactor = 8.0;

    /// <summary>
    /// Vanilla-relative XP/chroma rate for the Dualliste solos. Held at the
    /// original 8 so the HP test value doesn't nerf the payout.
    /// </summary>
    private const double DuallisteRewardRate = 8.0;

    /// <summary>Flat attack factor for <see cref="DuallisteStoryRows"/> (no band roll).</summary>
    private const double DuallisteAttackFactor = 5.0;

    /// <summary>
    /// A per-enemy loot package. By default it is drawn in addition to the
    /// row's normal archetype recipe; <see cref="Replace"/> makes it the whole
    /// package instead (protected key items are still preserved).
    /// <see cref="Tier"/> is the package's own tier (it does not have to match
    /// the row tier); <see cref="WeaponOwner"/> locks one weapon to a single
    /// character's family ("" = any character); <see cref="Weapons"/> draws that
    /// many free (any-character) weapons, distinct while the pool allows;
    /// <see cref="Pictos"/> is a count of distinct pictos; the catalyst is
    /// UpgradeMaterial_Level&lt;CatalystLevel&gt;, drawn uniformly in
    /// [CatalystQty, CatalystQtyHi] when CatalystQtyHi is higher (0 = fixed
    /// CatalystQty); <see cref="Lumina"/> is Consumable_LuminaPoint at that
    /// quantity.
    /// </summary>
    public sealed record LootOverride(
        int Tier,
        string? WeaponOwner = null,
        int Pictos = 0,
        int CatalystLevel = 0,
        int CatalystQty = 0,
        int Weapons = 0,
        int Lumina = 0,
        bool Replace = false,
        int CatalystQtyHi = 0);

    /// <summary>
    /// Boss rows with a hand-authored package that always applies when
    /// EnemyDrops is on (independent of the BossHp/stat settings). Reserved
    /// before chests/shops so the guarantee can never be stranding-dependent.
    /// </summary>
    private static readonly Dictionary<string, LootOverride> LootOverrides =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Forgotten Battlefield Dualliste: on top of the normal boss
            // package, a T3 package — one Verso-only T3 weapon, two distinct
            // T3 pictos and one T3 catalyst.
            ["FB_DuallisteLR"] = new LootOverride(
                Tier: 3, WeaponOwner: "Verso", Pictos: 2, CatalystLevel: 3, CatalystQty: 1),
            // Old Lumiere Renoir: his whole package is hand-authored — three
            // distinct T3 pictos, two random T3 weapons, 21 T3 catalysts and 99
            // lumina — so the normal boss recipe is suppressed for this row.
            ["OL_MirrorRenoir_FirstFight"] = new LootOverride(
                Tier: 3, Weapons: 2, Pictos: 3, CatalystLevel: 3, CatalystQty: 21,
                Lumina: 99, Replace: true),
            // Sirene boss ("Sirène's Coliseum"): her whole package is
            // hand-authored — three distinct T3 pictos, two random T3 weapons,
            // 3-5 T3 catalysts and 5 lumina (the generic boss amount, matching
            // the Visages Axon) — so the normal boss recipe is suppressed.
            ["SI_Axon_Sirene"] = new LootOverride(
                Tier: 3, Weapons: 2, Pictos: 3, CatalystLevel: 3, CatalystQty: 3,
                CatalystQtyHi: 5, Lumina: 5, Replace: true),
            // The Paintress (Monolith peak): whole package hand-authored — one
            // random T4 weapon, three distinct T4 pictos and 33 Grandiose
            // (UpgradeMaterial_Level4) Chroma Catalysts. The normal boss recipe
            // (bonus pictos, lumina) is suppressed.
            ["MO_Boss_Paintress"] = new LootOverride(
                Tier: 4, Weapons: 1, Pictos: 3, CatalystLevel: 4, CatalystQty: 33,
                Replace: true),
            // Painted Renoir (inside the Monolith): one random T4 weapon,
            // three distinct T4 pictos and 33 Grandiose (UpgradeMaterial_Level4)
            // Chroma Catalysts, replacing the normal boss recipe. His pinned
            // Ultima Power is preserved separately (PictosMagnitudeKit.
            // PinExclusiveDrops / the protected-item list).
            ["MM_MirrorRenoir"] = new LootOverride(
                Tier: 4, Weapons: 1, Pictos: 3, CatalystLevel: 4, CatalystQty: 33,
                Replace: true),
            // Act 3 Lumière finale trio: one T5 weapon + three T5 pictos and
            // 21 Perfect (L5) Chroma Catalysts each (T5-high boss rate),
            // replacing the normal boss recipe. Renoir's pinned Infinite picto
            // is protected and preserved.
            ["LU_EchassierAlpha"] = new LootOverride(
                Tier: 5, Weapons: 1, Pictos: 3, CatalystLevel: 5, CatalystQty: 21,
                Replace: true),
            ["L_Creation"] = new LootOverride(
                Tier: 5, Weapons: 1, Pictos: 3, CatalystLevel: 5, CatalystQty: 21,
                Replace: true),
            ["L_Boss_Curator"] = new LootOverride(
                Tier: 5, Weapons: 1, Pictos: 3, CatalystLevel: 5, CatalystQty: 21,
                Replace: true),
        };

    /// <summary>
    /// Battle-reward tuning for the story Dualliste phases: vanilla-relative
    /// factor for Experience and Chroma only (vanilla × 12). HP keeps
    /// BossHpOverride, everything else keeps normal T2 boss treatment.
    /// Deterministic: no RNG draws.
    /// </summary>
    private static readonly Dictionary<string, double> BossXpChromaOverride = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FB_Dualliste_Phase1"] = 12,
        ["FB_Dualliste_Phase2"] = 12,
    };

    private static readonly HashSet<string> StoryFinaleOverride = new(StringComparer.OrdinalIgnoreCase)
    {
        "L_Creation", "L_Boss_Curator",
    };

    /// <summary>
    /// Area-true table levels for gestral arena rows. Vanilla uses inflated
    /// template numbers (QUEST quartet 60, GA warriors 35, Dark Golgra 48)
    /// which the runtime feeds into encounter level → difficulty curve row,
    /// loot-context item levels and XP/chroma. Village Golgra stays 80 by
    /// design; Ono Puncho is forced to 100. Written to the staged row's
    /// Level AFTER the tier is derived from the vanilla number, so loot
    /// pools, catalysts and area tiers are unchanged — only runtime inputs.
    /// </summary>
    private static readonly Dictionary<string, int> EnemyLevelOverride = new(StringComparer.OrdinalIgnoreCase)
    {
        ["QUEST_MatthieuTheColossus"] = 15,
        ["QUEST_BertrandBigHands"] = 15,
        ["QUEST_DominiqueGiantFeet"] = 15,
        ["QUEST_JulienTinyHead"] = 15,
        ["GV_GestralScielTuto"] = 15,
        ["GV_Sciel_Arena"] = 15,
        ["GA_GestralChad"] = 15,
        ["GA_GestralSumo"] = 65,
        ["GA_GestralSkinny"] = 65,
        ["GA_GestralBuff"] = 65,
        ["GA_GestralTall"] = 65,
        ["GA_GestralBaby"] = 65,
        ["QUEST_Golgra_DarkArena"] = 81,
        ["Quest_GestralSumo9999"] = 100,
        // Act 3 finale chromatic: table Level is 0; it spawns at the zone level
        // (48), so both the DT write and the absolute-pin derivation use 48.
        ["LU_EchassierAlpha"] = 48,
        // Reworked fight levels (curve + runtime + audit all derive here):
        // Alicia fights at 69 in the Reacher; Chromatic Glissando at 65.
        ["RC_Alicia"] = 69,
        ["SI_Glissando_Alpha"] = 65,
        // Painted Renoir (Old Lumiere) fights at 28: the vanilla template
        // level is 60, and the 21x story HP factor multiplies whatever base
        // the staged level points at (626,366 x 21 = 13.1M at 60 vs the
        // intended 74,763 x 21 = 1.57M at 28).
        ["OL_MirrorRenoir_FirstFight"] = 28,
    };

    /// <summary>
    /// Gestral arena fights pay through post-fight dialogue, not battle
    /// drops: these rows stage Lumina only (boss rows ×5, others rated),
    /// never gear or catalysts. XP/Chroma scale untouched. Ono Puncho
    /// (<c>Quest_GestralSumo9999</c>) is the exception: he is undefeatable, so
    /// the drop pass gives him no loot at all.
    /// </summary>
    private static readonly HashSet<string> ArenaNoDrops = new(StringComparer.OrdinalIgnoreCase)
    {
        "QUEST_MatthieuTheColossus", "QUEST_BertrandBigHands",
        "QUEST_DominiqueGiantFeet", "QUEST_JulienTinyHead",
        "GV_GestralScielTuto", "GV_Sciel_Arena", "GA_GestralChad",
        "GA_GestralSumo", "GA_GestralSkinny", "GA_GestralBuff",
        "GA_GestralTall", "GA_GestralBaby", "GV_Golgra",
        "QUEST_Golgra_DarkArena", "Quest_GestralSumo9999",
    };

    /// <summary>
    /// Boss rows that respawn mid-fight. A fixed catalyst/lumina payout is an
    /// infinite farm, so these rows pay neither; their XP/chroma are also 10%
    /// (see <see cref="CloneRewardFactor"/>). The Chromatic Danseuse clone
    /// (re-summoned by the main alpha) and the Danseuse teacher clone.
    /// </summary>
    private static readonly HashSet<string> RespawningNoRewards = new(StringComparer.OrdinalIgnoreCase)
    {
        "MM_Danseuse_CloneAlpha",
        "QUEST_Danseuse_DanceClass_Clone",
    };

    /// <summary>
    /// White-nevron optional fights. They read as Regular/Strong/Elite in the
    /// archetype column but are fought as bosses, so they go through the full
    /// boss recipe (guaranteed weapon + pictos, tier-pinned catalysts, boss
    /// stats) instead of the Elite drop recipe.
    /// </summary>
    public static readonly HashSet<string> WhiteNevronBoss = new(StringComparer.OrdinalIgnoreCase)
    {
        "QUEST_DemineurWithoutMine", "QUEST_DemineurWithMine",
        "QUEST_JarNeedLight",
        "QUEST_SleepingBenisseur",
        "QUEST_TroubadourCantPlay", "QUEST_TroubadourCantPlay_Kill",
        "QUEST_Danseuse_DanceClass", "QUEST_Danseuse_DanceClass_Kill",
        "QUEST_Danseuse_DanceClass_Clone",
        "Portier_DoorSoul",
        "QUEST_JudgeOfMercy",
        "QUEST_HexgaLuster", "QUEST_Rocher_HexgaQuest",
        "GO_Bourgeon_Quest",
        "FC_ChalierSword", "FC_ChalierHammer", "FC_ChalierSpear",
    };

    /// <summary>
    /// The Floating Cemetery white-nevron fight spawns only one of these three
    /// Chalier variants, so only one row ever pays out. They share a single
    /// rolled package (see <see cref="ReserveBossGear"/>) instead of each
    /// drawing their own — the two unfought rows would otherwise strand pool
    /// gear (marked placed, never obtainable), retiring those pictos for the
    /// whole pack.
    /// </summary>
    public static readonly HashSet<string> ChalierTrio = new(StringComparer.OrdinalIgnoreCase)
    {
        "FC_ChalierSword", "FC_ChalierHammer", "FC_ChalierSpear",
    };

    /// <summary>
    /// The Flying Waters white Demineur has two rows (with and without the
    /// mine) and only one is ever fought. Share a single rolled package so the
    /// unfought row cannot strand pool gear — same arrangement as
    /// <see cref="ChalierTrio"/>.
    /// </summary>
    public static readonly HashSet<string> DemineurPair = new(StringComparer.OrdinalIgnoreCase)
    {
        "QUEST_DemineurWithoutMine", "QUEST_DemineurWithMine",
    };

    /// <summary>
    /// Scripted story fights excluded from the randomizer: stats stay vanilla
    /// and drops are exactly the fixed list (no pool draws, no preserved
    /// entries). White-haired Man (Stone Wave Cliffs) is unwinnable-adjacent
    /// story staging — vanilla drops nothing; he now drops exactly 1
    /// Grandiose Chroma Catalyst and nothing else.
    /// </summary>
    private static readonly Dictionary<string, List<(string ItemId, int Qty, double Chance)>> StoryFixedDrops =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SC_MirrorRenoir_GustaveEnd"] = new() { ("UpgradeMaterial_Level4", 1, 100) },
        };

    private static readonly HashSet<string> StoryRewardFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "HP", "Experience", "Chroma",
    };

    // ───────────────────── chests ─────────────────────

    private static readonly string[] ShapeIds =
    {
        "ConsumableUpgradeMaterial_Health",
        "ConsumableUpgradeMaterial_AP",
        "ConsumableUpgradeMaterial_Revive",
    };

    /// <summary>
    /// Capacity shards: each one raises how many of that tint the party can
    /// carry (restocked at flags/camp). Vanilla scatters these as world
    /// pickups; the randomizer places them as chest loot instead.
    /// </summary>
    private static readonly string[] TintShardIds =
    {
        "HealingTint_Shard",
        "EnergyTint_Shard",
        "ReviveTint_Shard",
    };

    /// <summary>Chroma Elixir Shard — the capacity shard for the party heal.</summary>
    private const string ChromaElixirShardId = "PartyHealShard";

    private static int RandomizeChests(string modDir, LootCatalog catalog, Random rng, Settings s, LootPlacement placement)
    {
        var path = CopyTable(ChestsTable, modDir);
        var ed = new DataTableEditor(path);
        StructPropertyData? dummyEntry = null;
        StructPropertyData? dummySection = null;
        foreach (var row in ed.Table.Table.Data)
        {
            if (Named(row.Value, "LootTableSections") is not ArrayPropertyData secs) continue;
            foreach (var sec in secs.Value.OfType<StructPropertyData>())
            {
                dummySection ??= (StructPropertyData)sec.Clone();
                if (Named(sec.Value, "LootEntries") is ArrayPropertyData ents
                    && ents.Value.OfType<StructPropertyData>().FirstOrDefault() is { } e)
                {
                    dummyEntry = (StructPropertyData)e.Clone();
                    break;
                }
            }
            if (dummyEntry != null) break;
        }
        if (dummyEntry is null || dummySection is null) return 0;

        // Paint Cage rows are ordinary DT_ChestsContent rows but form their own
        // randomizer group. Selecting the group decides whether they run; the
        // normal Chests toggle never touches them.
        bool IsPaintCage(string chestId) => LootCatalog.PaintCageChests.Contains(chestId);
        bool Active(string chestId) => IsPaintCage(chestId) ? s.PaintCages : s.Chests;
        bool IsChestSource(string source) => source == "Chest" || source == LootCatalog.PaintCageSource;

        int changed = 0;
        // (tier, the row's LootTableSections array, a single-entry section's
        // LootEntries array, asset) — one slot per drop.
        var inject = new List<(int Tier, ArrayPropertyData Sections, ArrayPropertyData Entries, UAsset Asset)>();

        // Sticky shares: an area's allocated gear is owner-only until its own
        // chest rows are all processed, then the leftovers release to everyone.
        // Countdown every row with a cataloged area — including key-only
        // vanilla skips that never draw — so nothing holds a release hostage.
        var rowsLeft = new Dictionary<(int Tier, string Area), int>();
        foreach (var row in ed.Table.Table.Data)
        {
            var rid = DataTableEditor.CleanName(row.Name);
            var rcheck = catalog.Checks.FirstOrDefault(c => IsChestSource(c.Source) && c.Id == rid);
            if (rcheck is null || !Active(rid) || rcheck.AreaKey.Length == 0) continue;
            var rkey = (Tier: Math.Clamp(rcheck.Tier, 1, 5), Area: rcheck.AreaKey);
            rowsLeft[rkey] = rowsLeft.GetValueOrDefault(rkey) + 1;
        }

        foreach (var row in ed.Table.Table.Data)
        {
            var id = DataTableEditor.CleanName(row.Name);
            var check = catalog.Checks.FirstOrDefault(c => IsChestSource(c.Source) && c.Id == id);
            if (!Active(id)) continue;
            var isPaintCage = IsPaintCage(id) || check?.Source == LootCatalog.PaintCageSource;
            var tier = check?.Tier ?? 1;
            if (check is not null && check.AreaKey.Length > 0)
            {
                var rkey = (Tier: Math.Clamp(check.Tier, 1, 5), Area: check.AreaKey);
                if (rowsLeft.TryGetValue(rkey, out var left))
                {
                    left--;
                    if (left <= 0)
                    {
                        rowsLeft.Remove(rkey);
                        placement.ReleaseArea("Pictos", rkey.Tier, rkey.Area);
                        placement.ReleaseArea("Weapon", rkey.Tier, rkey.Area);
                    }
                    else rowsLeft[rkey] = left;
                }
            }
            var refs = Named(row.Value, "LootTableReferences") as ArrayPropertyData;
            var sections = Named(row.Value, "LootTableSections") as ArrayPropertyData;
            if (sections is null) continue;

            if (isPaintCage)
            {
                // Paint Cages are their own group: exactly one guaranteed
                // pictos/weapon from the cage's tier, never a catalyst, chroma
                // or shard. The gear-banned-area rule intentionally does not
                // apply here — the cage guarantee wins.
                var gear = TakePaintCageGear(catalog, placement, tier, check?.AreaKey ?? "", rng);
                if (refs is not null)
                    refs.Value = Array.Empty<PropertyData>();
                var sec = (StructPropertyData)dummySection.Clone();
                var entries = Named(sec.Value, "LootEntries") as ArrayPropertyData;
                if (entries is null) continue;
                if (Named(sec.Value, "ChancePercent") is DoublePropertyData dc)
                    dc.Value = 100;
                else if (Named(sec.Value, "ChancePercent") is FloatPropertyData fc)
                    fc.Value = 100f;
                entries.Value = new PropertyData[] { MakeChestEntry(ed.Asset, dummyEntry, gear) };
                sections.Value = new PropertyData[] { sec };
                changed++;
                continue;
            }

            if (refs is { Value.Length: > 0 })
            {
                // Wipe non-key references (catalyst tables) – they are not key items
                refs.Value = Array.Empty<PropertyData>();
                // Ensure at least one section exists for filling
                var sec = EnsureSection(ed.Asset, sections, dummySection);
                var entries = Named(sec.Value, "LootEntries") as ArrayPropertyData;
                if (entries is null) continue;
                // Mark this chest as open for gear/lumina injection – don't pre-fill catalyst, let pool handle it
                // We will handle this section in the per-section loop below, so create an empty entries and continue
                entries.Value = Array.Empty<PropertyData>();
                // fall-through to per-section handling
            }

            // Snapshot: the loop below replaces this row's section array.
            foreach (var secObj in sections.Value.OfType<StructPropertyData>().ToList())
            {
                var entries = Named(secObj.Value, "LootEntries") as ArrayPropertyData;
                if (entries?.Value is null) continue;
                var kept = new List<StructPropertyData>();
                var open = false;
                var areaLevel = check?.AreaLevel ?? 1;
                foreach (var ent in entries.Value.OfType<StructPropertyData>())
                {
                    var old = ChestItemId(ent);
                    if (old is null)
                    {
                        open = true;
                        continue;
                    }
                    if (KeepFixedForChest(catalog, old, tier, areaLevel))
                        kept.Add((StructPropertyData)ent.Clone());
                    else
                        open = true;
                }
                // If no kept and not open but entries empty, treat as open to avoid empty chest
                if (entries.Value.Length == 0) open = true;
                if (!open && kept.Count > 0)
                    continue; // key-only chest stays vanilla

                // One drop per SECTION. The game rolls each section once and
                // returns one entry from it (S_LootTableEntry weights are the
                // weighted pick among a section's entries, not extra drops), so
                // packing several entries into one section collapses a
                // multi-drop chest into a single weighted pick. Give every drop
                // its own section instead.
                var newSections = new List<PropertyData>();
                var slots = new List<ArrayPropertyData>();

                void AddDrop(StructPropertyData entry)
                {
                    var sec = (StructPropertyData)secObj.Clone();
                    var es = Named(sec.Value, "LootEntries") as ArrayPropertyData;
                    if (es is null) return;
                    es.Value = new PropertyData[] { entry };
                    newSections.Add(sec);
                    slots.Add(es);
                }

                foreach (var key in kept) AddDrop(key);
                var n = RollChestCount(rng);
                var areaKey = check?.AreaKey ?? "";
                var gearBanned = ChestGearBanned(areaKey);
                for (int i = 0; i < n; i++)
                {
                    var drop = RollChestDrop(catalog, tier, rng, placement, areaKey);
                    // Defense in depth: no future roll-table edit may sneak a
                    // weapon or catalyst into a gear-banned area chest.
                    if (gearBanned && IsChestBannedDrop(catalog, drop))
                        drop = ChromaForTier(tier, rng);
                    AddDrop(MakeChestEntry(ed.Asset, dummyEntry, drop));
                }
                if (newSections.Count == 0) continue;

                sections.Value = newSections.ToArray();
                changed++;
                // Collect for guaranteed injections (all tiers 1-5, equally distributed)
                if (tier is >= 1 and <= 5)
                    foreach (var es in slots)
                        inject.Add((tier, sections, es, ed.Asset));
            }
        }

        // Tint / shape quotas are satisfied by overriding rolled lumina/chroma
        // drops, not by adding extra entries.
        changed += PlaceShardsAndShapes(inject, dummyEntry, s, rng);
        ed.Save(path);
        return changed;
    }

    /// <summary>
    /// One guaranteed tier-appropriate pictos or weapon for a Paint Cage.
    /// Area share → global cascade → same-tier repeat, for both types; the
    /// 21-cage count is far below the pool sizes, so one of these always lands.
    /// </summary>
    private static string TakePaintCageGear(LootCatalog catalog, LootPlacement placement,
        int tier, string areaKey, Random rng)
    {
        var first = rng.Next(2) == 0 ? "Pictos" : "Weapon";
        var second = first == "Pictos" ? "Weapon" : "Pictos";
        var id = placement.TakeForAreaStrict(first, tier, areaKey)
                 ?? placement.TakeForAreaStrict(second, tier, areaKey)
                 ?? placement.TakeUniqueOrRepeat(catalog, first, tier, rng)
                 ?? placement.TakeUniqueOrRepeat(catalog, second, tier, rng)
                 ?? placement.TakeDistinctRepeatAtTier(catalog, first, tier, rng, Array.Empty<string>())
                 ?? placement.TakeDistinctRepeatAtTier(catalog, second, tier, rng, Array.Empty<string>());
        return id ?? throw new InvalidDataException(
            $"[paint-cage] no tier-{tier} pictos or weapon left for {areaKey}");
    }

    private static int PlaceShardsAndShapes(
        List<(int Tier, ArrayPropertyData Sections, ArrayPropertyData Entries, UAsset Asset)> inject,
        StructPropertyData dummy, Settings s, Random rng)
    {
        if (inject.Count == 0) return 0;
        int n = 0;
        var byTier = inject.GroupBy(x => x.Tier).ToDictionary(g => g.Key, g => g.ToList());

        // Override one rolled lumina/chroma drop somewhere in the target tier
        // so quotas carve the requirement out of the normal distribution.
        bool Swap(string itemId, int tier)
        {
            foreach (var t in new[] { tier, 1, 2, 3, 4, 5 }.Distinct())
            {
                if (!byTier.TryGetValue(t, out var list)) continue;
                var options = new List<(ArrayPropertyData Ents, int Idx, UAsset Asset)>();
                foreach (var (_, _, entries, asset) in list)
                {
                    var arr = entries.Value.OfType<StructPropertyData>().ToList();
                    for (int i = 0; i < arr.Count; i++)
                        if (IsSwappable(ChestItemId(arr[i]))) options.Add((entries, i, asset));
                }
                if (options.Count == 0) continue;
                var pick = options[rng.Next(options.Count)];
                ReplaceEntryItem(pick.Ents, pick.Idx, pick.Asset, itemId);
                return true;
            }
            return false;
        }

        void Place(string itemId, int tier)
        {
            if (Swap(itemId, tier)) { n++; return; }
            // Nothing to override – give it its own section so it lands as a
            // real drop instead of becoming an alternative in a weighted pick.
            var tierList = byTier.GetValueOrDefault(Math.Clamp(tier, 1, 5)) ?? byTier.GetValueOrDefault(1);
            if (tierList is null || tierList.Count == 0) return;
            var slot = tierList[0];
            var template = slot.Sections.Value.OfType<StructPropertyData>().FirstOrDefault();
            if (template is null) return;
            var sec = (StructPropertyData)template.Clone();
            if (Named(sec.Value, "LootEntries") is not ArrayPropertyData es) return;
            es.Value = new PropertyData[] { MakeChestEntry(slot.Asset, dummy, itemId) };
            var list = slot.Sections.Value.ToList();
            list.Add(sec);
            slot.Sections.Value = list.ToArray();
            n++;
        }

        int shards = Math.Max(0, s.TintsPerType);
        int elixirShards = Math.Max(0, s.ChromaElixirShards);
        int shapes = Math.Max(0, s.ShapesPerType);

        // Capacity shards — Healing / Energy / Revive. These are what raise
        // how many of each tint the party can carry; the tint items themselves
        // are baseline (you start with one of each) and are never dropped.
        for (int si = 0; si < TintShardIds.Length; si++)
        {
            for (int i = 0; i < shards; i++)
                Place(TintShardIds[si], 1 + ((si + i) % 4));
        }

        // Chroma Elixir Shard — capacity for the party heal.
        for (int i = 0; i < elixirShards; i++)
            Place(ChromaElixirShardId, 1 + (i % 4));

        // Potency shapes — Shape of Health / Energy / Life. Each applied shape
        // raises the matching tint one grade (Small → normal → Strong).
        for (int si = 0; si < ShapeIds.Length; si++)
        {
            for (int i = 0; i < shapes; i++)
                Place(ShapeIds[si], 1 + ((si * shapes + i) % 4));
        }
        return n;
    }

    private static bool IsSwappable(string? id) =>
        id is "Consumable_LuminaPoint"
            or "ChromaPack_Regular" or "ChromaPack_Large" or "ChromaPack_ExtraLarge";

    private static void ReplaceEntryItem(ArrayPropertyData entries, int index, UAsset asset, string itemId)
    {
        var arr = entries.Value.OfType<StructPropertyData>().ToList();
        asset.AddNameReference(FString.FromString(itemId));
        if (Named(arr[index].Value, "ItemID") is NamePropertyData nm)
            nm.Value = new FName(asset, itemId);
        entries.Value = arr.Cast<PropertyData>().ToArray();
    }

    private static string CatalystForTier(int tier) =>
        $"UpgradeMaterial_Level{Math.Clamp(tier, 1, 5)}";

    private static string ChromaForTier(int tier, Random rng)
    {
        // Remaining chests after hitting distribution targets can be chroma.
        // Weight by tier: T1 Regular, T2 Large, T3-4 ExtraLarge, T5 ExtraLarge/Regular mix
        return tier switch
        {
            1 => "ChromaPack_Regular",
            2 => "ChromaPack_Large",
            3 => rng.Next(2) == 0 ? "ChromaPack_Large" : "ChromaPack_ExtraLarge",
            4 => "ChromaPack_ExtraLarge",
            _ => rng.Next(3) == 0 ? "ChromaPack_Regular" : "ChromaPack_ExtraLarge",
        };
    }


    /// <summary>
    /// Shop gear bounds. A shop keeps its vanilla gear count clamped to this
    /// range (MerchantGearSlots acts as the floor), so a picto-heavy vendor stays
    /// picto-heavy instead of being cut down to the setting.
    /// </summary>
    public const int MinShopGear = 4, MaxShopGear = 12;

    private static readonly string[] ChromaPackIds =
        { "ChromaPack_Regular", "ChromaPack_Large", "ChromaPack_ExtraLarge" };

    /// <summary>
    /// Normal chest areas reachable without combat (early Dark Shores / Frozen
    /// Hearts access). Their regular chests may never yield weapons or catalysts
    /// — pictos, chroma, lumina, tints and shapes are fine. Paint Cages are
    /// deliberately exempt: their independent group always rolls tier gear.
    /// Keys are normalised AreaKeys (see LootCatalog.AreaKeyOf).
    /// </summary>
    private static readonly HashSet<string> ChestNoGearAreas = new(StringComparer.OrdinalIgnoreCase)
        { "darkshores", "frozenhearts" };

    private static bool ChestGearBanned(string areaKey) => ChestNoGearAreas.Contains(areaKey);

    /// <summary>
    /// True for item ids that must never land in a gear-banned area chest:
    /// catalysts of any level, anything classified as a weapon, and any
    /// picto carrying a Defense stat (see <see cref="DefensePictos"/>).
    /// </summary>
    private static bool IsChestBannedDrop(LootCatalog catalog, string id) =>
        id.StartsWith("UpgradeMaterial_", StringComparison.Ordinal) ||
        string.Equals(catalog.TypeOf(id), "Weapon", StringComparison.OrdinalIgnoreCase) ||
        DefensePictos.Contains(id);

    /// <summary>
    /// Pictos carrying an equip Defense stat boost (DT_PictosDefinitions
    /// BaseStatMultipliers[E_jRPG_StatType::NewEnumerator4] greater than 0).
    /// Seeded with the 93 vanilla ids, then refreshed in <see cref="Apply"/>
    /// from the packed table via <see cref="DefensePictosFrom"/> so created
    /// pictos (e.g. Blighted Power III) are covered too. Banned from Dark
    /// Shores / Frozen Hearts chests alongside weapons and catalysts —
    /// reachable without combat, so no free defense there. No other area is
    /// affected.
    /// </summary>
    public static readonly HashSet<string> DefensePictos = new(StringComparer.OrdinalIgnoreCase)
    {
        "APOnBurn",
        "APOnPowerful",
        "APOnRush",
        "APOnShell",
        "AegisRevival",
        "AntiBlight",
        "AntiBurn",
        "AntiCharm",
        "AntiFrozen",
        "AntiShock",
        "AntiSilence",
        "AntiStun",
        "AugmentedAttack",
        "AutoDispelEnergy",
        "AutoRegen",
        "BeneficialContamination",
        "BestDefense",
        "BigEnergyTint",
        "BigHealingTint",
        "BurnDurationIncrease",
        "BurningMark",
        "CleansingTint",
        "CritChanceOnDefenseless",
        "CriticalBreak",
        "DamageShare",
        "DefenselessOnBreak",
        "DefensiveMode",
        "DispelOnAPConsume",
        "DodgeSpecialist",
        "EffectiveHeal",
        "EmpoweringJumps",
        "Energy",
        "EnergyDeath",
        "EvasiveHealer",
        "ExhaustAffinity",
        "FastAttacker",
        "FasterThanStrong",
        "FirstLife",
        "FreeAimPowerful",
        "FreeAimShell",
        "FreeAimSpeed",
        "Frenzy",
        "GradientCounterCharge",
        "GradientOnBuff",
        "GradientOnCrit",
        "GradientTint",
        "GreatHealingTint",
        "GreaterPowerless",
        "GreaterShell",
        "GreaterSlow",
        "HealOnBuff",
        "HealingFire",
        "HealingMark",
        "HealingParry",
        "HealingTintEnergy",
        "InMediasRes",
        "LastStand",
        "LastStandCritical",
        "LastStandShell",
        "LongerBreak",
        "LongerDodge",
        "LongerShell",
        "ParryDamageNoDodge",
        "PhysicalFighter",
        "PowerOfPain",
        "PowerfulOnShell",
        "PowerlessStrike",
        "Recovery",
        "ReviveCheer",
        "ReviveTintEnergy",
        "ReviveWithRegen",
        "RewardingMark",
        "Roulette",
        "SOSHealingTint",
        "SharedCare",
        "ShellOnRush",
        "ShellStrike",
        "ShellTint",
        "ShieldBreaker",
        "ShieldingDeath",
        "ShieldingTint",
        "SlowOnBreak",
        "Solidifying",
        "SolidifyingMeditation",
        "SoloFighter",
        "SosRush",
        "SosShell",
        "Stand",
        "Tainted",
        "Teamwork",
        "TimeTint",
        "VersatileHealer",
        "WeakeningMark",
    };

    /// <summary>
    /// Ids whose packed <c>DT_PictosDefinitions</c> grants a Defense stat
    /// (<c>BaseStatMultipliers[E_jRPG_StatType::NewEnumerator4] &gt; 0</c>).
    /// Reads the staged mod table when present — so created pictos are covered —
    /// else the vanilla <c>work/tables</c> copy. Mirrors the rando-smoke guard.
    /// </summary>
    public static HashSet<string> DefensePictosFrom(string? modDir)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = (!string.IsNullOrEmpty(modDir) ? FileCopy.FindUnder(modDir, "DT_PictosDefinitions.uasset") : null)
                   ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), "DT_PictosDefinitions.uasset");
        if (path is null) return set;
        var json = Newtonsoft.Json.Linq.JObject.Parse(new DataTableEditor(path).Dump());
        foreach (var prop in json.Properties())
        {
            if (prop.Value["BaseStatMultipliers"] is not Newtonsoft.Json.Linq.JObject mults) continue;
            foreach (var m in mults.Properties())
                if (m.Name.EndsWith("NewEnumerator4", StringComparison.Ordinal) && m.Value.ToObject<double>() > 0)
                {
                    set.Add(prop.Name);
                    break;
                }
        }
        return set;
    }

    /// <summary>
    /// Row ids present in the packed <c>DT_jRPG_Items</c> (staged mod tree when
    /// available, else the vanilla copy). Loot only stocks ids the pack actually
    /// contains, so created content whose kit did not run (e.g. a magnitude
    /// variant with its toggle off) is never handed out as a phantom entry.
    /// </summary>
    public static HashSet<string> PackedItemIds(string? modDir)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = (!string.IsNullOrEmpty(modDir) ? FileCopy.FindUnder(modDir, "DT_jRPG_Items.uasset") : null)
                   ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), "DT_jRPG_Items.uasset");
        if (path is null) return set;
        var json = Newtonsoft.Json.Linq.JObject.Parse(new DataTableEditor(path).Dump());
        foreach (var prop in json.Properties()) set.Add(prop.Name);
        return set;
    }

    /// <summary>Rolls per randomized chest: 1 @70%, 2 @20%, 3 @10%.</summary>
    private static int RollChestCount(Random rng)
    {
        var u = rng.NextDouble();
        if (u < 0.70) return 1;
        if (u < 0.90) return 2;
        return 3;
    }

    /// <summary>
    /// One chest drop: pictos 25% · weapon 3% · lumina point 36% ·
    /// chroma pack 36% (equal chance of every size). Gear draws are unique
    /// per pack (LootPlacement cascade); exhausted tiers fall back to chroma.
    /// In gear-banned areas (ChestNoGearAreas) the weapon share folds into
    /// chroma instead of drawing from the weapon pool, and defense pictos
    /// are released back to the pool (still obtainable elsewhere) with the
    /// slot folding to chroma.
    /// </summary>
    private static string RollChestDrop(LootCatalog catalog, int tier, Random rng, LootPlacement placement, string areaKey)
    {
        var u = rng.NextDouble();
        if (u < 0.25)
        {
            var pick = placement.TakeForArea(catalog, "Pictos", tier, areaKey, rng);
            if (pick is not null && ChestGearBanned(areaKey) && DefensePictos.Contains(pick))
            {
                placement.Placed.Remove(pick);
                return ChromaForTier(tier, rng);
            }
            return pick ?? ChromaForTier(tier, rng);
        }
        if (u < 0.28)
        {
            if (ChestGearBanned(areaKey)) return ChromaForTier(tier, rng);
            return placement.TakeForArea(catalog, "Weapon", tier, areaKey, rng) ?? ChromaForTier(tier, rng);
        }
        if (u < 0.64) return "Consumable_LuminaPoint";
        return ChromaPackIds[rng.Next(ChromaPackIds.Length)];
    }

    private static bool KeepFixed(LootCatalog catalog, string id)
    {
        // Only key items stay vanilla – everything else (Gold, Shard, Lumina, catalysts, pictos/weapons) is overwritable.
        if (catalog.IsProtected(id) || catalog.BrokenItems.Contains(id)) return true;
        return false;
    }

    private static bool KeepFixedForChest(LootCatalog catalog, string id, int tier, int areaLevel)
    {
        // Wipe all non-key chest contents that aren't key items then fill ensuring no chest is empty.
        // Cut content never survives, even when broken-listed (e.g. placeholder rows).
        if (catalog.IsProtected(id)) return true;
        if (catalog.IsCut(id)) return false;
        return catalog.BrokenItems.Contains(id);
    }

    private static string? ChestItemId(StructPropertyData ent) =>
        Named(ent.Value, "ItemID") is NamePropertyData n ? n.Value?.ToString() : null;

    private static StructPropertyData EnsureSection(UAsset asset, ArrayPropertyData sections,
        StructPropertyData dummySection)
    {
        if (sections.Value.OfType<StructPropertyData>().FirstOrDefault() is { } existing)
            return existing;
        var neu = (StructPropertyData)dummySection.Clone();
        sections.Value = new PropertyData[] { neu };
        return neu;
    }

    private static StructPropertyData MakeChestEntry(UAsset asset, StructPropertyData dummy, string itemId)
    {
        // Ranked weapons go in by family name, matching vanilla loot (see
        // PictosMagnitudeKit.LootReferenceFor).
        itemId = PictosMagnitudeKit.LootReferenceFor(itemId);
        asset.AddNameReference(FString.FromString(itemId));
        var ent = (StructPropertyData)dummy.Clone();
        if (Named(ent.Value, "ItemID") is NamePropertyData nm)
            nm.Value = new FName(asset, itemId);
        if (Named(ent.Value, "Quantity") is IntPropertyData q)
            q.Value = 1;
        if (Named(ent.Value, "Weight") is IntPropertyData w)
            w.Value = 1;
        return ent;
    }

    // ───────────────────── enemy drops ─────────────────────

    /// <summary>
    /// Boss gear reservation: Boss/Alpha/SuperBoss rows claim their
    /// guaranteed gear BEFORE chests roll, so the boss promise holds no
    /// matter how small the pools run. Returns row id → drop specs
    /// (item id, qty, chance); RandomizeEnemyDrops stamps them in. Boss gear
    /// draws may repeat a tier-appropriate item once the cascade is exhausted.
    /// </summary>
    public static Dictionary<string, List<(string ItemId, int Qty, double Chance)>> ReserveBossGear(
        string enemiesPath, LootCatalog catalog, Random rng, LootPlacement placement)
    {
        var reserved = new Dictionary<string, List<(string, int, double)>>(StringComparer.Ordinal);
        var ed = new DataTableEditor(enemiesPath);
        IReadOnlyDictionary<string, string>? owners = null;
        // Single rolled package shared by the Chalier trio (only one variant is
        // ever fought — see ChalierTrio).
        List<(string, int, double)>? chalierShared = null;
        // Same treatment for the two white Demineur rows (see DemineurPair).
        List<(string, int, double)>? demineurShared = null;
        List<(string, int, double)> BuildGear(
            StructPropertyData row, LootCatalog.EnemyInfo? enemy, string arch, bool bossRow,
            LootOverride? over, int tier, string areaKey)
        {
            var gear = new List<(string, int, double)>();
            var rowId = DataTableEditor.CleanName(row.Name);
            void Take(string type, int count, double chance)
            {
                if (chance < 100 && rng.NextDouble() * 100 >= chance) return;
                for (int i = 0; i < count; i++)
                    gear.Add((placement.TakeForAreaOrRepeat(catalog, type, tier, areaKey, rng)
                        ?? ChromaForTier(tier, rng), 1, 100));
            }

            var isBoss = arch is "Boss" or "Alpha" or "SuperBoss"
                || (over is null && WhiteNevronBoss.Contains(DataTableEditor.CleanName(row.Name)))
                || (over is null && bossRow && IsRealBossRow(DataTableEditor.CleanName(row.Name), enemy, arch));
            // A Replace override IS the package, so the archetype recipe is
            // skipped for that row (otherwise it would double up).
            if (isBoss && over?.Replace != true)
            {
                if (arch is "SuperBoss")
                {
                    // 2 weapons + 2 pictos, 66% third picto, 33% fourth.
                    Take("Weapon", 2, 100);
                    Take("Pictos", 2, 100);
                    Take("Pictos", 1, 66);
                    Take("Pictos", 1, 33);
                }
                else
                {
                    // 1 weapon + 1 picto, 66% second picto, 33% third.
                    Take("Weapon", 1, 100);
                    Take("Pictos", 1, 100);
                    Take("Pictos", 1, 66);
                    Take("Pictos", 1, 33);
                }
                // Catalysts scale by band, in addition to the regular gear
                // (~10 levels per band):
                //   T4 low  (L 60–69) : 5×L5 + 3–5×L4
                //   T4 high (L 70–79) : 10×L5 + 3–5×L4
                //   T5 low  (L 80–89) : 15×L5 + 6–10×L4
                //   T5 high (L 90–99) : 21×L5 + 11–25×L4
                //   T1–T3             : 3–5 of their own tier (unchanged)
                // Farmable adds (respawning rows) pay no catalysts or lumina:
                // the clone can be re-summoned, so any fixed payout is an
                // infinite farm. Its weapon/pictos remain, but XP/chroma are
                // 10% (see CloneRewardFactor).
                var respawningClone = RespawningNoRewards.Contains(rowId);
                if (!respawningClone)
                {
                    if (tier >= 4)
                    {
                        var bossLevel = EnemyRowLevel(row, enemy);
                        int l5, l4Lo, l4Hi;
                        if (tier >= 5 && bossLevel >= 90) { l5 = 21; l4Lo = 11; l4Hi = 25; }
                        else if (tier >= 5)               { l5 = 15; l4Lo = 6;  l4Hi = 10; }
                        else if (bossLevel >= 70)         { l5 = 10; l4Lo = 3;  l4Hi = 5;  }
                        else                              { l5 = 5;  l4Lo = 3;  l4Hi = 5;  }
                        // Grosse Tete can be killed by dodging his attacks without
                        // ever hitting him, so the surviving variants cap the
                        // Perfect Catalyst payload at 3 (vanilla recipe: 5/15).
                        // The WM variant is T3 and never reaches this branch.
                        if (IsGrosseTete(rowId)) l5 = Math.Min(l5, GrosseTetePerfectCatalystQty);
                        if (l5 > 0)
                            gear.Add(("UpgradeMaterial_Level5", l5, 100));
                        gear.Add(("UpgradeMaterial_Level4", rng.Next(l4Lo, l4Hi + 1), 100));
                    }
                    else
                    {
                        gear.Add((CatalystForTier(tier), rng.Next(3, 6), 100));
                    }
                    gear.Add(("Consumable_LuminaPoint", 5, 100));
                }
            }

            if (over is not null)
                AppendOverride(catalog, placement, owners ??= WeaponOwners(), over, areaKey, rng, gear);

            return gear;
        }
        foreach (var row in ed.Table.Table.Data)
        {
            var id = DataTableEditor.CleanName(row.Name);
            if (Named(row.Value, "PossibleLoot") is not ArrayPropertyData) continue;
            catalog.Enemies.TryGetValue(id, out var enemy);
            var arch = enemy?.Archetype ?? "Regular";
            // The row's own IsBoss flag counts as a boss too, so encounters the
            // archetype column mislabels (François, the FinalBoss rows, arena
            // fights) still get their guarantee.
            var bossRow = Named(row.Value, "IsBoss") is BoolPropertyData ib && ib.Value;

            // Rows with a hand-authored override always reserve (independent of
            // the skips below), so the package cannot depend on the archetype
            // column or on chest ordering.
            LootOverrides.TryGetValue(id, out var over);
            if (over is null)
            {
                if (arch == "Merchant") continue;
                if (enemy is { CutContent: true }) continue;
                if (StoryFixedDrops.ContainsKey(id)) continue; // scripted story fight: fixed drops, no pool draws
                // Petank rows take their own drop branch below; reserving here would
                // draw gear out of the pool that never gets stamped on the row.
                if (arch == "Petank") continue;
                // Arena rows pay Lumina only (dialogue carries the rewards); white-
                // nevron fights take the boss recipe via the gate below — either
                // way reserving here would strand pool gear for arenas only.
                if (ArenaNoDrops.Contains(id)) continue;
                if (arch is not ("Boss" or "Alpha" or "SuperBoss") && !WhiteNevronBoss.Contains(id)
                    && !(bossRow && IsRealBossRow(id, enemy, arch)))
                    continue;
            }

            var tier = catalog.AreaTierForEnemy(id, enemy,
                LootCatalog.TierFromLevel(EnemyRowLevel(row, enemy)), isBossRow: true);
            var areaKey = catalog.AreaKeyFor(id);
            if (ChalierTrio.Contains(id))
            {
                chalierShared ??= BuildGear(row, enemy, arch, bossRow, over, tier, areaKey);
                reserved[id] = new List<(string, int, double)>(chalierShared);
                continue;
            }
            if (DemineurPair.Contains(id))
            {
                demineurShared ??= BuildGear(row, enemy, arch, bossRow, over, tier, areaKey);
                reserved[id] = new List<(string, int, double)>(demineurShared);
                continue;
            }
            reserved[id] = BuildGear(row, enemy, arch, bossRow, over, tier, areaKey);
        }
        return reserved;
    }

    /// <summary>
    /// Draws a <see cref="LootOverride"/> package and appends it to
    /// <paramref name="gear"/>. The weapon is locked to its owning character,
    /// pictos stay distinct, and a drained pool repeats a same-tier id rather
    /// than dropping the guaranteed slot to chroma.
    /// </summary>
    private static void AppendOverride(
        LootCatalog catalog, LootPlacement placement, IReadOnlyDictionary<string, string> owners,
        LootOverride over, string areaKey, Random rng, List<(string ItemId, int Qty, double Chance)> gear)
    {
        var t = Math.Clamp(over.Tier, 1, 5);

        if (over.WeaponOwner is { Length: > 0 })
        {
            var wid = placement.TakeOwnedUniqueOrRepeat(catalog, owners, over.WeaponOwner, t, rng);
            if (wid is not null) gear.Add((wid, 1, 100));
        }

        // Free weapons: any character's family, distinct while the pool allows.
        // A hand-authored package is a guarantee, so fall back to a tier-wide
        // distinct draw when the area share is spent or the pick duplicates.
        if (over.Weapons > 0)
        {
            var taken = new List<string>();
            for (int i = 0; i < over.Weapons; i++)
            {
                var wid = placement.TakeForAreaOrRepeat(catalog, "Weapon", t, areaKey, rng);
                if (wid is null || taken.Contains(wid, StringComparer.Ordinal))
                    wid = placement.TakeDistinctRepeatAtTier(catalog, "Weapon", t, rng, taken) ?? wid;
                if (wid is null) break;
                taken.Add(wid);
                gear.Add((wid, 1, 100));
            }
        }

        var pictos = new List<string>();
        for (int i = 0; i < Math.Max(0, over.Pictos); i++)
        {
            var pid = placement.TakeForAreaOrRepeat(catalog, "Pictos", t, areaKey, rng);
            if (pid is null || pictos.Contains(pid, StringComparer.Ordinal))
                pid = placement.TakeDistinctRepeatAtTier(catalog, "Pictos", t, rng, pictos) ?? pid;
            if (pid is null) break;
            pictos.Add(pid);
            gear.Add((pid, 1, 100));
        }

        if (over.CatalystLevel > 0 && (over.CatalystQty > 0 || over.CatalystQtyHi > 0))
        {
            // Uniform count over the package's range bis (0 upper bound = fixed).
            var lo = Math.Max(0, over.CatalystQty);
            var qty = over.CatalystQtyHi > lo ? rng.Next(lo, over.CatalystQtyHi + 1) : lo;
            if (qty > 0)
                gear.Add(($"UpgradeMaterial_Level{Math.Clamp(over.CatalystLevel, 1, 5)}", qty, 100));
        }

        if (over.Lumina > 0)
            gear.Add(("Consumable_LuminaPoint", over.Lumina, 100));
    }

    /// <summary>
    /// The table's IsBoss flag is set on ~180 rows — tutorial fights, level-2
    /// regulars and the Petank lumina enemies among them — so an IsBoss row only
    /// counts as a boss when it also reads like one. Rows the archetype column
    /// already covers never reach this check.
    /// </summary>
    public static bool IsRealBossRow(string id, LootCatalog.EnemyInfo? enemy, string arch)
    {
        if (WhiteNevronBoss.Contains(id)) return true;
        if (arch is "Regular" or "Petank" or "Merchant") return false;
        if (id.Contains("Tutorial", StringComparison.OrdinalIgnoreCase)) return false;
        if (enemy?.Level is > 0 and <= 5) return false;
        return true;
    }

    // ─────────────── rare Perfect Catalyst (endgame regulars) ───────────────

    /// <summary>
    /// Named quest / DLC fights that sit in an endgame area but are not farmable
    /// regulars, so they must never carry the rare Perfect Catalyst.
    /// </summary>
    private static readonly HashSet<string> PerfectCatalystExcluded = new(StringComparer.OrdinalIgnoreCase)
    {
        "Quest_FeintFight",          // Monsieur Frappe
        "VD_Quest_GestralSeesaw",    // Half-baked Gestral — Seesaw Quest
    };

    /// <summary>
    /// True when <paramref name="id"/> should also roll the rare Perfect (L5)
    /// Chroma Catalyst: an endgame non-boss — a T5-area row at fight level ≥
    /// <see cref="PerfectCatalystMinLevel"/> — that does not also spawn in a
    /// lower-band encounter (see <see cref="T5CatalystBarred"/>). Elites are
    /// included; bosses, alphas, superbosses, pets, merchants, overworld,
    /// scripted/arena rows and named quest fights are not.
    /// </summary>
    public static bool QualifiesForT5PerfectCatalyst(
        string id, string arch, int level, int tier, bool isBoss, bool overworld)
    {
        if (isBoss || overworld) return false;
        if (arch is "Boss" or "Alpha" or "SuperBoss" or "Merchant" or "Petank") return false;
        if (WhiteNevronBoss.Contains(id) || StoryFixedDrops.ContainsKey(id) || ArenaNoDrops.Contains(id)
            || PerfectCatalystExcluded.Contains(id))
            return false;
        if (level < PerfectCatalystMinLevel || tier != LootCatalog.MaxTier) return false;
        return !T5CatalystBarred().Contains(id);
    }

    private static HashSet<string>? _t5CatalystBarred;

    /// <summary>
    /// Enemy rows that must not carry the rare Perfect Catalyst because they
    /// also spawn in a lower-band fight: loot lives on the enemy row, so a row
    /// shared with a sub-endgame encounter would hand the catalyst out there
    /// too. A row is barred when a non-tower encounter references it with a
    /// pinned <c>EncounterLevelOverride</c> below
    /// <see cref="PerfectCatalystMinLevel"/> (an override of 0 inherits the
    /// row's own level, so it never bars). Endless Tower fights
    /// (<c>DT_jRPG_Encounters_CleaTower</c>) are deliberately exempt, so
    /// tower-reused endgame rows such as <c>L_Aberration</c> keep the drop.
    /// </summary>
    private static HashSet<string> T5CatalystBarred()
    {
        if (_t5CatalystBarred != null) return _t5CatalystBarred;
        var barred = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in new[] { "DT_jRPG_Encounters", "DT_WorldMap_Encounters" })
        {
            var path = FileCopy.FindTablesAsset(table + ".uasset");
            if (path is null)
            {
                EnsureExtracted(table);
                path = FileCopy.FindTablesAsset(table + ".uasset");
            }
            if (path is null) continue;
            try
            {
                var ed = new DataTableEditor(path);
                foreach (var row in ed.Table.Table.Data)
                {
                    var over = Named(row.Value, "EncounterLevelOverride") is IntPropertyData ip ? ip.Value : 0;
                    if (over is <= 0 or >= PerfectCatalystMinLevel) continue;
                    if (Named(row.Value, "Enemies") is not MapPropertyData map) continue;
                    foreach (var kv in map.Value)
                        if (kv.Value is StructPropertyData sv
                            && Named(sv.Value, "RowName") is NamePropertyData nm
                            && nm.Value?.ToString() is { Length: > 0 } enemyId)
                            barred.Add(enemyId);
                }
            }
            catch (Exception e)
            {
                // A missing/broken extract must not stop a pack; rando-smoke
                // verifies the bar geometry against the real tables.
                Console.Error.WriteLine($"[perfect-catalyst] {table} guard skipped: {e.Message}");
            }
        }
        _t5CatalystBarred = barred;
        return barred;
    }

    private static int RandomizeEnemyDrops(string path, LootCatalog catalog, Random rng, Settings s,
        LootPlacement placement, Dictionary<string, List<(string ItemId, int Qty, double Chance)>> reservedBossGear)
    {
        var ed = new DataTableEditor(path);
        StructPropertyData? dummy = null;
        foreach (var row in ed.Table.Table.Data)
        {
            if (Named(row.Value, "PossibleLoot") is ArrayPropertyData arr && arr.Value.Length > 0
                && arr.Value[0] is StructPropertyData st)
            {
                dummy = (StructPropertyData)st.Clone();
                break;
            }
        }
        if (dummy is null) return 0;

        // Per-row entry template. A generated drop must inherit the row's own
        // item-handle DataTable and LootContextLevelOffset (which sets the rolled
        // level the game compares against owned gear), not a table-wide dummy.
        // The dummy is only the fallback for rows whose vanilla loot was empty.
        StructPropertyData Template(StructPropertyData row)
        {
            if (Named(row.Value, "PossibleLoot") is ArrayPropertyData ra
                && ra.Value.OfType<StructPropertyData>().FirstOrDefault() is { } firstEntry)
                return (StructPropertyData)firstEntry.Clone();
            return dummy!;
        }

        int changed = 0;
        foreach (var row in ed.Table.Table.Data)
        {
            var id = DataTableEditor.CleanName(row.Name);
            var loot = Named(row.Value, "PossibleLoot") as ArrayPropertyData;
            if (loot is null) continue;
            catalog.Enemies.TryGetValue(id, out var enemy);
            var arch = enemy?.Archetype ?? "Regular";
            if (arch == "Merchant") continue;
            if (enemy is { CutContent: true }) continue; // cut content never enters the randomizer

            // Preserve this row's own entry shape: the item handle's DataTable
            // and the LootContextLevelOffset that decides the rolled level.
            var template = Template(row);

            var preserved = new List<StructPropertyData>();
            foreach (var ent in loot.Value.OfType<StructPropertyData>())
            {
                var old = DropItemId(ent);
                if (old != null && catalog.IsProtected(old))
                    preserved.Add((StructPropertyData)ent.Clone());
            }

            var level = EnemyRowLevel(row, enemy);
            var rowTier = LootCatalog.TierFromLevel(level);
            var tier = catalog.AreaTierForEnemy(id, enemy, rowTier,
                isBossRow: reservedBossGear.ContainsKey(id));
            var drops = new List<StructPropertyData>(preserved);
            var overworld = IsOverworld(id, enemy);
            var areaKey = catalog.AreaKeyFor(id);

            if (arch == "Petank")
            {
                drops.Add(MakeDrop(ed.Asset, template, "Consumable_LuminaPoint", Math.Max(1, s.PetankLumina), 100));
            }
            else if (StoryFixedDrops.TryGetValue(id, out var fixedDrops))
            {
                // Scripted story fight: exactly the fixed list, nothing else.
                // Deterministic: no RNG draws, seed stream untouched.
                foreach (var (itemId, qty, chance) in fixedDrops)
                    drops.Add(MakeDrop(ed.Asset, template, itemId, qty, chance));
            }
            else if (id.Equals("Quest_GestralSumo9999", StringComparison.OrdinalIgnoreCase))
            {
                // Ono Puncho cannot be defeated: his 9,999-damage objective is
                // paid through dialogue, so the row carries no battle drops at
                // all (not even the arena Lumina).
            }
            else if (ArenaNoDrops.Contains(id))
            {
                // Dialogue carries the rewards: Lumina only, never gear or
                // catalysts. Boss-shaped rows pay ×5, the rest the rated one.
                var arenaBoss = arch is "Boss" or "Alpha" or "SuperBoss"
                    || ((Named(row.Value, "IsBoss") is BoolPropertyData ab && ab.Value)
                        && IsRealBossRow(id, enemy, arch));
                if (arenaBoss)
                    drops.Add(MakeDrop(ed.Asset, template, "Consumable_LuminaPoint", 5, 100));
                else
                    drops.Add(MakeDrop(ed.Asset, template, "Consumable_LuminaPoint", 1, 4 * tier));
            }
            else if (reservedBossGear.TryGetValue(id, out var gear))
            {
                // Reserved up front (ReserveBossGear) so the guarantee holds;
                // repeats allowed here once the cascade ran dry.
                foreach (var (itemId, qty, chance) in gear)
                {
                    // The chromatic Petank's signature payout is 1000 lumina,
                    // not the standard boss 5 plus 1000. Drop the generic boss
                    // lumina here and add the full amount once below.
                    if (id == "WM_Petank_Alpha" && itemId == "Consumable_LuminaPoint") continue;
                    drops.Add(MakeDrop(ed.Asset, template, itemId, qty, chance));
                }
                if (id == "WM_Petank_Alpha")
                    drops.Add(MakeDrop(ed.Asset, template, "Consumable_LuminaPoint", 1000, 100));
            }
            else if (overworld)
            {
                AddPictosOrWeapon(ed.Asset, template, drops, catalog, placement, tier, areaKey, rng);
                AddFieldDrops(ed.Asset, template, drops, tier, rng);
            }
            else if (arch == "Elite")
            {
                AddRolls(ed.Asset, template, drops, catalog, placement, "Pictos", tier, areaKey, rng, 1, s.ElitePictosChance);
                AddFieldDrops(ed.Asset, template, drops, tier, rng);
            }
            else
            {
                AddRolls(ed.Asset, template, drops, catalog, placement, "Pictos", tier, areaKey, rng, 1, s.RegularPictosChance);
                AddFieldDrops(ed.Asset, template, drops, tier, rng);
            }

            // Rare endgame drop: an endgame regular/elite (T5 area, level >=
            // PerfectCatalystMinLevel) also rolls one Perfect Chroma Catalyst at
            // T5PerfectCatalystChance%. Rows borrowed by a lower-band fight are
            // barred (T5CatalystBarred) so the catalyst can't leak out of the
            // endgame areas; the Endless Tower is exempt. Appended after the
            // recipe so the L4 field catalyst stays first.
            var isBossFlag = Named(row.Value, "IsBoss") is BoolPropertyData bossFlag && bossFlag.Value;
            if (s.T5PerfectCatalystChance > 0
                && QualifiesForT5PerfectCatalyst(id, arch, level, tier, isBossFlag, overworld))
                drops.Add(MakeDrop(ed.Asset, template, PerfectCatalystId, 1, s.T5PerfectCatalystChance));

            loot.Value = drops.Cast<PropertyData>().ToArray();
            changed++;
        }
        ed.Save(path);
        return changed;
    }

    /// <summary>
    /// Per-fight level for loot/stat tiers. See
    /// <see cref="LootCatalog.ResolveEnemyLevel"/>.
    /// </summary>
    private static int EnemyRowLevel(StructPropertyData row, LootCatalog.EnemyInfo? enemy)
    {
        var table = Named(row.Value, "Level") is IntPropertyData lvl ? lvl.Value : 0;
        return LootCatalog.ResolveEnemyLevel(table, enemy);
    }

    private static string? DropItemId(StructPropertyData drop)
    {
        var item = Named(drop.Value, "Item") as StructPropertyData;
        var nameProp = item is null ? null : Named(item.Value, "RowName") as NamePropertyData;
        return nameProp?.Value?.ToString();
    }

    private static bool IsOverworld(string id, LootCatalog.EnemyInfo? enemy) =>
        id.StartsWith("WM_", StringComparison.Ordinal)
        || (enemy?.DisplayName.Contains("(World Map)", StringComparison.OrdinalIgnoreCase) ?? false);

    private static void AddPictosOrWeapon(UAsset asset, StructPropertyData dummy,
        List<StructPropertyData> drops, LootCatalog catalog, LootPlacement placement, int tier, string areaKey, Random rng)
    {
        // Coin flip prefers weapons, then falls back to the other type, then
        // chroma when both are fully placed.
        string? id = rng.Next(2) == 0
            ? placement.TakeForArea(catalog, "Weapon", tier, areaKey, rng) ?? placement.TakeForArea(catalog, "Pictos", tier, areaKey, rng)
            : placement.TakeForArea(catalog, "Pictos", tier, areaKey, rng) ?? placement.TakeForArea(catalog, "Weapon", tier, areaKey, rng);
        drops.Add(MakeDrop(asset, dummy, id ?? ChromaForTier(tier, rng), 1, 100));
    }

    private static void AddRolls(UAsset asset, StructPropertyData dummy, List<StructPropertyData> drops,
        LootCatalog catalog, LootPlacement placement, string type, int tier, string areaKey, Random rng, int count, double chance)
    {
        if (count <= 0 || chance <= 0) return;
        for (int i = 0; i < count; i++)
        {
            // Sub-100% chances are rolled here, not in game: a won lottery
            // consumes a unique ID and lands as a guaranteed drop, so placed
            // gear is always actually findable; a lost lottery leaves no entry.
            if (chance < 100 && rng.NextDouble() * 100 >= chance) continue;
            drops.Add(MakeDrop(asset, dummy,
                placement.TakeForArea(catalog, type, tier, areaKey, rng) ?? ChromaForTier(tier, rng), 1, 100));
        }
    }

    /// <summary>
    /// Field economy for non-boss rows: 30% in-game chance at 1–3 catalysts
    /// of the row's level (capped at L4 — regulars never drop L5), plus one
    /// lumina at 4%-per-tier in-game chance. T5 rows pay that capped L4
    /// catalyst at double the rate (60% chance, 2–6) so they stay a step above
    /// T4. On top of the row's own catalyst, higher-tier rows also roll the
    /// catalysts below it: x1 one tier down at 50% and x1 two tiers down at
    /// 30%, never below L1. With T5's field catalyst capped at L4, an endgame
    /// row therefore gets L3 at 50% and L2 at 30% (see the T5-relative rule).
    /// Fungible drops keep their chance in game; only the count is rolled at
    /// pack time.
    /// </summary>
    private static void AddFieldDrops(UAsset asset, StructPropertyData dummy,
        List<StructPropertyData> drops, int tier, Random rng)
    {
        var t = Math.Clamp(tier, 1, 5);
        var catalystTier = Math.Min(t, 4);
        var catalystChance = t >= 5 ? 60 : 30;
        var catalystQty = t >= 5 ? rng.Next(2, 7) : rng.Next(1, 4);
        drops.Add(MakeDrop(asset, dummy, CatalystForTier(catalystTier), catalystQty, catalystChance));

        // Lower-tier catalysts stay relevant as you climb: one tier below at
        // 50% and two tiers below at 30%, one each. T2 only reaches L1; T1 has
        // nothing below it. T4/T5 share the same relative pair because a T5
        // field drop's own catalyst is the capped L4.
        for (int delta = 1; delta <= 2; delta++)
        {
            var lowerTier = catalystTier - delta;
            if (lowerTier < 1) break;
            var chance = delta == 1 ? CatalystOneTierBelowChance : CatalystTwoTiersBelowChance;
            drops.Add(MakeDrop(asset, dummy, CatalystForTier(lowerTier), 1, chance));
        }

        drops.Add(MakeDrop(asset, dummy, "Consumable_LuminaPoint", 1, 4 * t));
    }

    private static StructPropertyData MakeDrop(UAsset asset, StructPropertyData dummy,
        string itemId, int qty, double chance)
    {
        // Ranked weapons (Reach*/Siren*_1/2) must be referenced by their bare
        // family name, as vanilla loot does; the suffixed row id renders "?"
        // and never grants (see PictosMagnitudeKit.LootReferenceFor).
        itemId = PictosMagnitudeKit.LootReferenceFor(itemId);
        asset.AddNameReference(FString.FromString(itemId));
        var drop = (StructPropertyData)dummy.Clone();
        var item = Named(drop.Value, "Item") as StructPropertyData;
        if (item != null && Named(item.Value, "RowName") is NamePropertyData nm)
            nm.Value = new FName(asset, itemId);
        if (Named(drop.Value, "Quantity") is IntPropertyData q)
            q.Value = Math.Max(qty, 1);
        if (Named(drop.Value, "LootPercentageChance") is DoublePropertyData d)
            d.Value = chance;
        else if (Named(drop.Value, "LootPercentageChance") is FloatPropertyData f)
            f.Value = (float)chance;
        return drop;
    }

    // ───────────────────── merchants ─────────────────────

    private static int RandomizeMerchants(string modDir, LootCatalog catalog, Random rng, Settings s, LootPlacement placement)
    {
        EnsureExtracted("DT_Merchant_");
        var root = Path.Combine(EditorPaths.RepoRoot, "work", "tables");
        var files = Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "DT_Merchant_*.uasset", SearchOption.AllDirectories).ToList()
            : new List<string>();
        int changed = 0;
        foreach (var src in files)
        {
            var dest = CopyPairIntoMod(src, modDir);
            var ed = new DataTableEditor(dest);
            var stem = Path.GetFileNameWithoutExtension(src);
            if (!LootCatalog.MerchantTiers.TryGetValue(stem, out var tier))
                tier = 2;
            var table = ed.Table.Table.Data;
            if (table.Count == 0) continue;

            // Cosmetic / key-item shops (customization merchants, record
            // vendors) sell only their vanilla stock — every row being
            // customization gear or a protected key/cosmetic item marks one,
            // and it is left completely vanilla. Read the item's own type via
            // Describe(), not TypeOf(): that returns the *category* first
            // ("Cosmetic"), which would let these shops through.
            bool protectedShop = true;
            foreach (var row in table)
            {
                var code = DataTableEditor.CleanName(row.Name);
                if (catalog.IsProtected(code)
                    || catalog.Describe(code).Type == "CharacterCustomization")
                    continue;
                protectedShop = false;
                break;
            }
            if (protectedShop) continue;

            var dummy = (StructPropertyData)table[0].Clone();

            // Kept vanilla rows carry their own struct (clone) so per-row
            // PriceOverride/LevelOverride/Quantity/Condition survive the
            // rebuild below. Cloning the row-0 template instead clobbered
            // them (e.g. Pinabby's 1000-chroma Wavy haircut collapsed to
            // its 10-chroma BuyValue via PriceOverride -1).
            var keptRows = new Dictionary<string, StructPropertyData>(StringComparer.Ordinal);
            var vanillaGear = 0;
            foreach (var row in table)
            {
                var old = DataTableEditor.CleanName(row.Name);
                if (catalog.IsProtected(old) && !keptRows.ContainsKey(old))
                    keptRows[old] = (StructPropertyData)row.Clone();
                var oldType = catalog.Describe(old).Type;
                if (oldType is "Pictos" or "Weapon") vanillaGear++;
            }

            var newItems = new List<string>(keptRows.Keys)
            {
                "Consumable_LuminaPoint", "Consumable_Respec",
            };
            // T5 shops keep their no-L5 restriction: they stock L1–L4 only.
            var maxCatalystTier = Math.Min(tier, 4);
            for (int t = 1; t <= maxCatalystTier; t++)
                newItems.Add($"UpgradeMaterial_Level{t}");

            // Guarantee real stock: at least one weapon and one pictos, then fill
            // the remaining gear slots with either type. Never chroma — shops roll
            // last, so the unique pool is normally spent, and because the table is
            // keyed by item name repeated picks collapse into a single row (which
            // is how shops ended up selling nothing but currency).
            var stock = new HashSet<string>(StringComparer.Ordinal);
            string? Stock(string type)
            {
                var id = placement.TakeForShop(catalog, type, tier, rng, stock);
                if (id is null && string.Equals(type, "Weapon", StringComparison.OrdinalIgnoreCase))
                {
                    // Dry weapon pool (often the roster gate on early shops):
                    // fill the slot with a tier-appropriate pictos, repeats
                    // allowed, rather than a cross-cast weapon or an empty slot.
                    id = placement.TakeForShop(catalog, "Pictos", tier, rng, stock);
                    if (id is not null)
                        Console.WriteLine($"[shop] {stem}: weapon pool dry, stocked pictos {id} instead");
                }
                if (id is null) return null;
                stock.Add(id);
                newItems.Add(id);
                return id;
            }
            Stock("Weapon");
            Stock("Pictos");
            var slots = Math.Clamp(Math.Max(s.MerchantGearSlots, vanillaGear), MinShopGear, MaxShopGear);
            for (int i = stock.Count; i < slots; i++)
                Stock(rng.Next(2) == 0 ? "Weapon" : "Pictos");

            table.Clear();
            var miscQty = tier == 5 ? 99 : 5;
            foreach (var item in newItems)
            {
                ed.Asset.AddNameReference(FString.FromString(item));
                var isKept = keptRows.TryGetValue(item, out var vanillaRow);
                var neu = isKept ? (StructPropertyData)vanillaRow.Clone()
                                 : (StructPropertyData)dummy.Clone();
                neu.Name = new FName(ed.Asset, item);
                if (Named(neu.Value, "ItemRowName") is NamePropertyData nr)
                    nr.Value = new FName(ed.Asset, item);
                if (!isKept && Named(neu.Value, "Quantity") is IntPropertyData q)
                {
                    if (item.StartsWith("UpgradeMaterial_", StringComparison.Ordinal))
                    {
                        // The shop tier's own catalyst is a x33 convenience;
                        // every catalyst below it is bulk stock (x999). A T5
                        // shop has no L5 row, so all four of its catalysts are
                        // "below" and get the bulk quantity.
                        var level = int.TryParse(item["UpgradeMaterial_Level".Length..], out var lv)
                            ? lv
                            : 0;
                        q.Value = level < tier
                            ? LowerCatalystShopQuantity
                            : CurrentCatalystShopQuantity;
                    }
                    else if (item is "Consumable_LuminaPoint" or "Consumable_Respec")
                        q.Value = miscQty;
                    else
                        q.Value = 1;
                }
                if (!isKept)
                {
                    // The row-0 template carries its own sale terms (e.g.
                    // Kasumi's 5000-chroma Obscur skin, GrandisStation's level
                    // 21): new stock must sell at base BuyValue and natural
                    // level instead of inheriting them. Kept rows preserve
                    // their own overrides above; Condition stays inherited
                    // (row-0 conditions are shop-level checkers).
                    if (Named(neu.Value, "PriceOverride") is IntPropertyData po)
                        po.Value = -1;
                    if (Named(neu.Value, "LevelOverride") is IntPropertyData lo)
                        lo.Value = -1;
                }
                table.Add(neu);
                changed++;
            }
            ed.Save(dest);
        }
        return changed;
    }

    // ───────────────────── monster stats ─────────────────────

    private static int RandomizeMonsterStats(string path, LootCatalog catalog, Random rng, Settings s)
    {
        var ed = new DataTableEditor(path);
        int changed = 0;
        foreach (var row in ed.Table.Table.Data)
        {
            var id = DataTableEditor.CleanName(row.Name);
            catalog.Enemies.TryGetValue(id, out var enemy);
            if (enemy is { CutContent: true })
                continue; // cut content never enters the randomizer
            if (StoryFixedDrops.ContainsKey(id))
                continue; // scripted story fight: stats stay vanilla (see RandomizeEnemyDrops)

            var tableLevel = Named(row.Value, "Level") is IntPropertyData lvlProp ? lvlProp.Value : 0;
            var catalogLevel = LootCatalog.CatalogFightLevel(tableLevel, enemy);
            var level = LootCatalog.RemapLootLevel(enemy, catalogLevel);
            var rowTier = LootCatalog.TierFromLevel(level);
            var isBoss = IsBossRow(row, enemy);
            // Drops and stats both follow the area band unless explicitly
            // overridden (AreaTierOverrides, Act 3 / Frozen Hearts remaps, or
            // boss rows holding their row tier so guarantees never downgrade).
            var tier = catalog.AreaTierForEnemy(id, enemy, rowTier, isBoss);
            var storyHp = StoryHpOverride.GetValueOrDefault(id);
            var finale = StoryFinaleOverride.Contains(id);
            // Flying rows dodge most base attacks (see the curve-HP rule below).
            var flyingRow = Named(row.Value, "IsFlying") is BoolPropertyData flyRow && flyRow.Value;

            // Archetype for the stat maths below. World-variant clones (WMV_)
            // have no enemy_data entry of their own, so borrow the donor row's
            // archetype: a null archetype resolves to the Regular table, which
            // silently mistargets every absolute pin by an order of magnitude
            // (a T4 clone pinned 5526.92 HP scaling instead of 425.15).
            var statArch = enemy?.Archetype;
            if (statArch is null
                && id.StartsWith(WorldMapVariantKit.VariantPrefix, StringComparison.Ordinal)
                && catalog.Enemies.TryGetValue(
                    id.Substring(WorldMapVariantKit.VariantPrefix.Length), out var donorEnemy))
                statArch = donorEnemy.Archetype;
            // Real boss rows only for the T1-T3 attack-band uplift. The raw
            // IsBoss flag is also set on tutorials, Petanks and quest mobs.
            // Keep low-level story bosses (Eveque L3, Goblu L8) in scope, but
            // exclude tutorial rows and the Regular/Petank/Merchant archetypes.
            var isRealBoss = isBoss
                && !id.Contains("Tutorial", StringComparison.OrdinalIgnoreCase)
                // Hand-authored story fights (Renoir, Dualliste) keep their
                // own attack values; Dualliste phase rows especially must not
                // inherit the generic T2 band uplift.
                && !id.Contains("Dualliste", StringComparison.OrdinalIgnoreCase)
                && !id.Contains("Renoir", StringComparison.OrdinalIgnoreCase)
                && (WhiteNevronBoss.Contains(id)
                    || statArch is not ("Regular" or "Petank" or "Merchant"));

            // Curve targets for T4/T5 bosses and chromatic alphas: HP from the
            // gently accelerating curve and ATK/SPD/DEF from the shared pool
            // (see ResolveShares). Story rows and the Act 3 finale trio keep
            // their own absolute pins. This includes the T4 world-variant
            // clones (WMV_): they are chromatic alphas, so they curve at their
            // world tier like any other alpha.
            var (aShare, sShare, dShare) = ResolveShares(
                BossAtkShare.GetValueOrDefault(id, 0.70),
                BossDefShare.GetValueOrDefault(id, DefaultDefShare));
            double curveHp = 0, curveAtk = 0, curveSpd = 0, curveDef = 0;
            bool curve = isBoss && tier >= 4
                && TryCurveTarget(id, enemy, tableLevel, catalog, aShare, sShare, dShare,
                    s.ExtremeBossDefense,
                    out curveHp, out curveAtk, out curveSpd, out curveDef);
            // Flying curve rows dodge most base attacks, so a full curve pool
            // lasts far longer than its number suggests: stage a third of the
            // curve HP. Loot, XP (level-linked) and ATK/SPD/DEF are untouched.
            // (Flying ladder bosses take the same cut in the HP branch below.)
            if (curve && flyingRow)
                curveHp *= FlyingHpFactor;

            // Area-true table levels for gestral arena rows (see
            // EnemyLevelOverride): written after the tier derivation above so
            // loot pools/catalysts/tiers are untouched — only runtime inputs.
            if (EnemyLevelOverride.TryGetValue(id, out var forcedLevel)
                && Named(row.Value, "Level") is IntPropertyData lvlProp2)
                lvlProp2.Value = forcedLevel;

            if (Named(row.Value, "EnemyScaling") is not StructPropertyData scaling)
                continue;

            // Absolute-HP pins (see StoryAbsoluteHp): derive the factor that
            // sets this row's HP scaling so archetype[encounter level] × scaling
            // equals the target. Experience/Chroma then follow the same factor
            // below, so rewards track the health pool. Deterministic: reads the
            // archetype curves, adds no RNG draws.
            if (StoryAbsoluteHp.TryGetValue(id, out var targetHp) && targetHp > 0)
            {
                var encounterLevel = LootCatalog.EncounterLevelForEnemy(id, tableLevel);
                var archetypeHp = EnemyEffectiveStats.ArchetypeHp(enemy?.Archetype, encounterLevel);
                var curHp = scaling.Value
                    .Where(f => DataTableEditor.CleanName(f.Name) == "HP")
                    .Select(ReadNumber).FirstOrDefault();
                if (archetypeHp > 0 && curHp > 0)
                    storyHp = targetHp / archetypeHp / curHp;
            }

            // White-nevron T1/T2 durability floor (see WhiteNevronHpTargets):
            // use the row's real venue level, not its DT template Level (Jar's
            // template is 60, Troubadour's 25, Portier's 30).
            bool whitePin = WhiteNevronHpTargets.TryGetValue(id, out var whiteTarget)
                && whiteTarget.Hp > 0;
            double whiteHpRatio = 1;
            if (whitePin)
            {
                var archetypeHp = EnemyEffectiveStats.ArchetypeHp(statArch, whiteTarget.Level);
                var curHp = scaling.Value
                    .Where(f => DataTableEditor.CleanName(f.Name) == "HP")
                    .Select(ReadNumber).FirstOrDefault();
                if (archetypeHp > 0 && curHp > 0)
                {
                    storyHp = whiteTarget.Hp / archetypeHp / curHp;
                    // The old HP path used BossHpFactor(tier); scale XP/Chroma by
                    // the same HP growth so the payout follows the longer fight
                    // and never drops below the pre-pin reward.
                    var oldHpFactor = BossHpFactor(tier, s);
                    if (oldHpFactor > 0)
                        whiteHpRatio = Math.Max(1, storyHp / oldHpFactor);
                }
            }

            // Area curve-lift (Act 3 → 99, Frozen Hearts +16) except named story rows.
            if (storyHp == 0 && (LootCatalog.IsAct3Lumiere(enemy) || LootCatalog.IsFrozenHearts(enemy))
                && catalogLevel != level)
                BakeScaling(scaling, catalogLevel, level, isBoss || BossArchetypes.Contains(enemy?.Archetype ?? ""));

            foreach (var field in scaling.Value)
            {
                var name = DataTableEditor.CleanName(field.Name);
                double factor;
                if (storyHp > 0 && StoryRewardFields.Contains(name))
                {
                    if (whitePin && name is "Experience" or "Chroma")
                    {
                        // Preserve the normal boss reward rate and multiply it by
                        // the HP growth; HP itself still uses the absolute pin.
                        var cur = ReadNumber(field);
                        if (cur == 0) continue;
                        var hpRate = BossHpFactor(tier, s);
                        factor = Math.Max(1, (1 + hpRate / 3) / cur) * whiteHpRatio;
                    }
                    else
                    {
                        factor = storyHp;
                    }
                }
                else if (BossXpChromaOverride.TryGetValue(id, out var xcfactor)
                    && name is "Experience" or "Chroma")
                {
                    // Story duel payout: flat vanilla multiplier, no formula.
                    factor = xcfactor;
                }
                else if (isBoss && name is "Experience" or "Chroma")
                {
                    // Boss XP/Chroma follow 1/3 of the HP rate: 1 + HpRate/3.
                    // Dualliste solos use the flat ×8 rate; everything else the
                    // normal T2 boss rate. Story rows are handled by the
                    // storyHp branch above; zeros (scripted fights paid via
                    // dialogue) are preserved; the max keeps vanilla innates
                    // and never nerfs. ScaleNumber multiplies, so divide the
                    // target out of the current value.
                    // Deterministic: no RNG draws, seed stream untouched.
                    var cur = ReadNumber(field);
                    if (cur == 0)
                        continue;
                    var hpRate = DuallisteStoryRows.Contains(id) ? DuallisteRewardRate : BossHpFactor(tier, s);
                    factor = Math.Max(1, (1 + hpRate / 3) / cur);
                }
                else if (EnemyScaleSkip.Contains(name, StringComparer.Ordinal))
                {
                    continue;
                }
                else if (finale)
                {
                    // Same number of RollBand draws as a normal boss row so the
                    // rest of the seed (later enemies, weapons) stays put.
                    _ = RollBand(tier, rng, s);
                    factor = name is "PhysicalAttack" or "MagicalAttack" ? 3 : 5;
                }
                else if (name == "HP" && DuallisteStoryRows.Contains(id))
                {
                    // LR is the two-bar story fight: pin each bar to 8.0
                    // (~404.6K) so the two-phase total is ~809K. The R/L
                    // elite solos keep the flat ×8 base × boss factor.
                    factor = BossHpOverride.TryGetValue(id, out var hpOverride)
                        ? hpOverride
                        : DuallisteHpFactor * BossHpFactor(tier, s);
                }
                else if (name == "HP" && isBoss)
                {
                    factor = BossHpOverride.GetValueOrDefault(id, BossHpFactor(tier, s));
                    // Flying ladder bosses: same third-HP rule as the curve rows.
                    if (flyingRow)
                        factor *= FlyingHpFactor;
                }
                else if (DuallisteStoryRows.Contains(id) && name is "PhysicalAttack" or "MagicalAttack")
                {
                    // T2 Dualliste solos: flat attacks instead of the band roll.
                    // Dummy draw keeps the seed stream position identical.
                    _ = RollAttackBand(tier, rng, s);
                    factor = DuallisteAttackFactor;
                }
                else
                {
                    // Attacks roll the hotter attack band; one draw per field
                    // either way so the RNG stream position never shifts.
                    var isAttack = name is "PhysicalAttack" or "MagicalAttack";
                    factor = isAttack ? RollAttackBand(tier, rng, s) : RollBand(tier, rng, s);
                    // T1-T3 bosses: double the bonus above 1.0, so the same
                    // rolls become T1 1.40-1.60 (was 1.20-1.30),
                    // T2 1.70-2.00 (was 1.35-1.50), T3 2.10-2.40 (was 1.55-1.70).
                    // Only the band-rolled branch is affected: Dualliste's flat
                    // attacks, finale rows and absolute stat pins keep their own
                    // values. No extra RNG draws, so the seed/loot stream is
                    // unchanged.
                    if (isRealBoss && tier <= 3 && isAttack && factor > 1)
                        factor = 1 + (factor - 1) * 2;
                }
                // Dedicated boss Speed multiplier (every tier, every boss): x2 on
                // top of the band/flat factor the row rolled, so bosses act about
                // twice as often. Regular enemies are untouched and this consumes
                // no RNG draws, so the seed stream stays identical.
                if (isBoss && name == "Speed")
                    factor *= s.BossSpeed;
                // Per-row extra multiplier (e.g. the Sirene boss +11%): applies
                // to combat stats only — HP/Experience/Chroma have their own
                // override and must not be scaled twice.
                if (!StoryRewardFields.Contains(name)
                    && StatBonusOverride.TryGetValue(id, out var statBonus))
                    factor *= statBonus;
                // Per-row outgoing-damage multiplier (triple the story bosses'
                // attacks): scales only the attack fields, after the band roll.
                // Deterministic: no RNG draws.
                if (DamageMultiplierOverride.TryGetValue(id, out var damageMult)
                    && name is "PhysicalAttack" or "MagicalAttack")
                    factor *= damageMult;
                // Absolute per-stat pins (the Act 3 finale trio) run last: they
                // override the band/override factor so the effective value lands
                // exactly on the target. The normal branch already consumed its
                // RNG draws, so the seed stream is unchanged. Defense pins assign
                // directly (staged archetype base 1.0; vanilla 0.0 could never
                // divide into a factor).
                if (AbsoluteStatPins.TryGetValue(id, out var pins)
                    && pins.TryGetValue(name, out var pinTarget) && pinTarget > 0)
                {
                    if (name is "PhysicalDefense" or "MagicalDefense")
                    {
                        SetNumber(field, pinTarget / EnemyDefenseKit.StagedDefenseBase);
                        continue;
                    }
                    var pinLevel = AbsolutePinLevels.TryGetValue(id, out var forcedPinLevel)
                        ? forcedPinLevel
                        : LootCatalog.EncounterLevelForEnemy(id, tableLevel);
                    var archVal = EnemyEffectiveStats.ArchetypeStat(statArch, pinLevel, name);
                    if (name == "Speed")
                    {
                        // Speed pins assign the staged scaling directly and skip
                        // the curve pass: the boss-speed multiplier and boss curve
                        // must not overwrite the target (Ono Puncho 500).
                        if (archVal > 0) SetNumber(field, pinTarget / archVal);
                        continue;
                    }
                    var cur = ReadNumber(field);
                    if (archVal > 0 && cur > 0) factor = pinTarget / archVal / cur;
                }
                // Curve targets (HP / PhysicalAttack / Speed / defenses) run last
                // too, on top of any band/bake, so the effective value lands on
                // the target. Defenses assign the absolute pin directly: vanilla
                // enemy scaling is 0.0, so multiplying could never stage one —
                // and the band draw above already held the seed-stream position.
                // A field with its own AbsoluteStatPins entry is skipped here so
                // the pin stays exact (Ono Puncho's Speed/DEF).
                if (curve
                    && !(AbsoluteStatPins.TryGetValue(id, out var pinnedFields)
                        && pinnedFields.ContainsKey(name)))
                {
                    double curveTarget = name switch
                    {
                        "HP" => curveHp,
                        "PhysicalAttack" => curveAtk,
                        "Speed" => curveSpd,
                        "PhysicalDefense" or "MagicalDefense" => curveDef,
                        _ => 0,
                    };
                    if (curveTarget > 0)
                    {
                        var lvl = CurveLevel(id, tableLevel, enemy);
                        var cur = ReadNumber(field);
                        if (name is "PhysicalDefense" or "MagicalDefense")
                        {
                            // Staged archetype defense bases are uniformly 1.0,
                            // so the staged scaling IS the effective defense:
                            // assign the absolute pin whatever vanilla scaling
                            // was (0.0 or otherwise — consulting the vanilla
                            // table here would mistarget rows whose vanilla
                            // scaling is nonzero). Smoke asserts the staged
                            // tables carry the 1.0 bases.
                            SetNumber(field, curveTarget / EnemyDefenseKit.StagedDefenseBase);
                            continue;
                        }
                        else
                        {
                            var archVal = EnemyEffectiveStats.ArchetypeStat(statArch, lvl, name);
                            if (archVal > 0 && cur > 0) factor = curveTarget / archVal / cur;
                        }
                    }
                }
                // XP / Chroma: level-linked reward for every tuned boss/alpha
                // (the curve rows plus the Act 3 trio): 1 level at L60 -> 3 at L99,
                // at the flat level cost. Same multiplier for both.
                // Story-absolute rows already chose their reward factor from the
                // HP pin in the StoryRewardFields branch above; do not replace it
                // with the level-linked rate (L_Creation / L_Boss_Curator /
                // LU_EchassierAlpha would otherwise pay XP/Chroma at a fraction
                // of their HP factor).
                if (storyHp <= 0
                    && (curve || AbsoluteStatPins.ContainsKey(id))
                    && name is "Experience" or "Chroma")
                {
                    var lvl = CurveLevel(id, tableLevel, enemy);
                    var archXp = EnemyEffectiveStats.ArchetypeStat(statArch, lvl, "Experience");
                    var cur = ReadNumber(field);
                    if (archXp > 0 && cur > 0)
                        factor = RewardLevels(lvl) * RewardXpPerLevel / archXp / cur;
                }
                // Re-summoned clones pay 10% XP/chroma (see CloneRewardFactor)
                // regardless of which reward branch above selected the rate.
                if (BossCloneOf.ContainsKey(id) && name is "Experience" or "Chroma")
                    factor *= CloneRewardFactor;
                ScaleNumber(field, factor);
            }
            changed++;
        }
        ed.Save(path);
        return changed;
    }

    private static void BakeScaling(StructPropertyData scaling, int fromLevel, int toLevel, bool bossAtk)
    {
        foreach (var field in scaling.Value)
        {
            var name = DataTableEditor.CleanName(field.Name);
            var cur = ReadNumber(field);
            if (cur == 0) continue;
            var ratio = EnemyStatCurves.Ratio(fromLevel, toLevel, name, bossAtk);
            if (Math.Abs(ratio - 1) < 1e-9) continue;
            ScaleNumber(field, ratio);
        }
    }

    private static double ReadNumber(PropertyData field) => field switch
    {
        DoublePropertyData d => d.Value,
        FloatPropertyData f => f.Value,
        IntPropertyData i => i.Value,
        _ => 0,
    };

    private static bool IsBossRow(StructPropertyData row, LootCatalog.EnemyInfo? enemy)
    {
        if (Named(row.Value, "IsBoss") is BoolPropertyData b && b.Value)
            return true;
        var id = DataTableEditor.CleanName(row.Name);
        if (WhiteNevronBoss.Contains(id)) return true;
        var arch = enemy?.Archetype ?? "";
        return BossArchetypes.Contains(arch);
    }

    private static void ScaleNumber(PropertyData field, double factor)
    {
        switch (field)
        {
            case DoublePropertyData d: d.Value *= factor; break;
            case FloatPropertyData f: f.Value = (float)(f.Value * factor); break;
            case IntPropertyData i: i.Value = Math.Max(0, (int)Math.Round(i.Value * factor)); break;
        }
    }

    /// <summary>Writes an absolute value (used for defense pins on rows whose
    /// vanilla scaling is 0.0, where multiplying could never land a target).</summary>
    private static void SetNumber(PropertyData field, double value)
    {
        switch (field)
        {
            case DoublePropertyData d: d.Value = value; break;
            case FloatPropertyData f: f.Value = (float)value; break;
            case IntPropertyData i: i.Value = Math.Max(0, (int)Math.Round(value)); break;
        }
    }

    // ───────────────────── weapon stats / elements ─────────────────────

    private const string StatAttack = "E_jRPG_StatType::NewEnumerator3";
    private const string StatFreeAim = "E_jRPG_StatType::NewEnumerator13";

    private static int RandomizeWeapons(string path, LootCatalog catalog, Random rng, Settings s)
    {
        var ed = new DataTableEditor(path);
        // The fifth slot only exists once WeaponPassiveSlotKit has staged the
        // table; standalone runs (smoke tests) fall back to four slots.
        bool hasSlot5 = ed.Table.Table.Data.Count > 0
            && ed.Table.Table.Data[0].Value.Any(p => DataTableEditor.CleanName(p.Name) == WeaponPassiveSlotKit.Field);
        var edits = new Newtonsoft.Json.Linq.JObject();
        // Pre-roll passives to keep rng order identical to previous two-pass (passives first)
        var loadoutMap = new Dictionary<string, WeaponPassiveLoadout>(StringComparer.OrdinalIgnoreCase);
        if (s.WeaponPassives)
        {
            var owners = WeaponOwners();
            var loadouts = RollWeaponPassives(catalog, owners, rng, hasSlot5 ? 5 : 4);
            foreach (var w in loadouts) loadoutMap[w.WeaponId] = w;
        }

        int tierRows = 0, passiveHits = 0;
        foreach (var row in ed.Table.Table.Data)
        {
            var id = DataTableEditor.CleanName(row.Name);
            var tier = WeaponItemTier(catalog, id);
            if (tier is null) continue;
            tierRows++;
            var effTier = EffectiveWeaponTier(catalog, id, tier.Value);
            var obj = new Newtonsoft.Json.Linq.JObject();
            if (s.WeaponStats)
            {
                var atkRoll = RollBand(effTier, rng, s);
                var aimRoll = RollBand(effTier, rng, s);
                var chromaRoll = RollBand(effTier, rng, s);
                var elements = WeaponElementPool(effTier);
                // Reuse the melee element rolled with the passives (it gated the
                // Lune stain pool); fall back to a fresh draw when passives are off.
                var meleeEl = s.WeaponPassives && loadoutMap.TryGetValue(id, out var statLoadout)
                              && !string.IsNullOrEmpty(statLoadout.MeleeElement)
                    ? statLoadout.MeleeElement
                    : PickOne(elements, rng);
                var aimEl = PickOne(elements, rng);
                obj[$"BaseStatsMultipliers.{StatAttack}"] = atkRoll;
                obj[$"BaseStatsMultipliers.{StatFreeAim}"] = aimRoll;
                obj["ChromaValueMultiplier"] = chromaRoll;
                obj["BaseDamageType"] = meleeEl;
                obj["FreeAimDamageType"] = aimEl;
                var nScaling = RollScalingCount(effTier, rng);
                var shuffledAttrs = ScalingAttributes.OrderBy(_ => rng.Next()).Take(nScaling).ToList();
                var scalingMap = new Newtonsoft.Json.Linq.JObject();
                foreach (var attr in shuffledAttrs)
                    scalingMap[attr] = GradeForTier(effTier);
                obj["AttributeScaling"] = scalingMap;
            }
            if (s.WeaponPassives && loadoutMap.TryGetValue(id, out var w))
            {
                passiveHits++;
                obj["Level1Lumina"] = string.IsNullOrEmpty(w.Extra) ? "None" : w.Extra;
                obj["Level2Lumina"] = string.IsNullOrEmpty(w.Level2) ? "None" : w.Level2;
                obj["Level3Lumina"] = string.IsNullOrEmpty(w.Level3) ? "None" : w.Level3;
                obj["Level4Lumina"] = string.IsNullOrEmpty(w.Level4) ? "None" : w.Level4;
                if (hasSlot5)
                    obj[WeaponPassiveSlotKit.Field] = string.IsNullOrEmpty(w.Level5) ? "None" : w.Level5;
            }
            if (obj.Count > 0) edits[id] = obj;
        }
        if (s.WeaponPassives && passiveHits * 20 < tierRows * 19)
            throw new InvalidOperationException(
                $"Weapon passive coverage collapsed ({passiveHits}/{tierRows} tiered weapons got loadouts). "
                + "Owner derivation failed — check the work/tables DT_jRPG_Items "
                + "extraction and work/weapon_owner_overrides.json, then re-pack.");
        if (edits.Count > 0)
        {
            ed.Apply(edits.ToString());
            ed.Save(path);
        }
        return edits.Count;
    }

    private static int? WeaponItemTier(LootCatalog catalog, string id)
    {
        var col = catalog.ItemTiers.FindColumn(ItemTiers.Weapon, id);
        if (col is null || !col.StartsWith("Tier", StringComparison.Ordinal))
            return null;
        return int.TryParse(col["Tier".Length..], out var t) && t >= 1 ? t : null;
    }

    // ───────────────────── pools ─────────────────────


    /// <summary>Ids the current pack actually contains (see <see cref="PackedItemIds"/>).</summary>
    private static HashSet<string>? _packedItems;

    private static IEnumerable<string> Pool(LootCatalog catalog, string type, int tier) =>
        catalog.PoolFor(type, tier).Where(id => _packedItems is null || _packedItems.Contains(id));

    private static string PickOne(IReadOnlyList<string> pool, Random rng) =>
        pool[rng.Next(pool.Count)];


    // ───────────────────── weapon passives ─────────────────────

    public sealed class WeaponPassiveLoadout
    {
        public required string WeaponId { get; init; }
        public required string Character { get; init; }
        public required int WeaponTier { get; init; }
        /// <summary>Melee element (EAttackType enumerator) rolled with the passives;
        /// RandomizeWeapons reuses it for BaseDamageType so the Lune stain gate
        /// and the stat roll cannot disagree.</summary>
        public string MeleeElement { get; init; } = "";
        /// <summary>Archetype the weapon's archetype slots drew from (diagnostics).</summary>
        public string PrimaryArchetype { get; init; } = "";
        public string Extra { get; init; } = "";
        public string Level2 { get; init; } = "";
        public string Level3 { get; init; } = "";
        public string Level4 { get; init; } = "";
        public string Level5 { get; init; } = "";

        public IEnumerable<string> Filled()
        {
            if (Level2.Length > 0) yield return Level2;
            if (Level3.Length > 0) yield return Level3;
            if (Level4.Length > 0) yield return Level4;
            if (Level5.Length > 0) yield return Level5;
            if (Extra.Length > 0) yield return Extra;
        }
    }

    /// <summary>
    /// Derived fresh on every call. Deliberately not cached: this runs inside a
    /// long-lived editor process whose work/tables tree can be re-extracted or
    /// restored underneath it, and a stale owner map silently collapses weapon
    /// passive coverage at pack time.
    /// </summary>
    public static IReadOnlyDictionary<string, string> WeaponOwners() => LoadWeaponOwners();

    public static List<WeaponPassiveLoadout> RollWeaponPassives(
        LootCatalog catalog, IReadOnlyDictionary<string, string> owners, Random rng, int maxSlots = 5)
    {
        // Ensure deterministic order and fill any weapons missing from ItemTiers (defensive)
        var complete = new Dictionary<string, string>(owners, StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "Weapon_Tier1", "Weapon_Tier2", "Weapon_Tier3", "Weapon_Tier4", "Weapon_Tier5" })
        {
            if (!catalog.ItemTiers.Flat.TryGetValue(key, out var list2)) continue;
            foreach (var wid in list2)
            {
                if (complete.ContainsKey(wid)) continue;
                Console.Error.WriteLine(
                    $"[RandomizerKit] skipping weapon without owner: {wid} (add it to work/weapon_owner_overrides.json)");
            }
        }

        var list = new List<WeaponPassiveLoadout>();
        foreach (var (id, character) in complete.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var baseTier = WeaponItemTier(catalog, id);
            if (baseTier is null) continue;
            if (character.Length == 0) continue;
            var weaponTier = EffectiveWeaponTier(catalog, id, baseTier.Value);

            // The melee element drives the Lune stain gate, and RandomizeWeapons
            // assigns the same draw to BaseDamageType, so the passives and the
            // weapon element can never disagree.
            var elementPool = WeaponElementPool(weaponTier);
            var meleeElement = elementPool[rng.Next(elementPool.Count)];
            var meleeName = StainsMechanicKit.ElementNames.TryGetValue(meleeElement, out var name)
                ? name : "";

            // Fill ladder: one slot per weapon tier — T1–T5 fill 1/2/3/4/5 slots.
            // The wildcard occupies the weakest slot (Extra); archetype picks are
            // ranked strongest-first into the higher slots.
            int slotCount = Math.Min(WeaponEffectTiers.SlotCount(weaponTier), Math.Clamp(maxSlots, 1, 5));
            var picks = WeaponEffectRoller.Roll(catalog.EffectTiers, catalog.EffectCharacters,
                catalog.EffectArchetypes, catalog.EffectConditions, weaponTier, character,
                meleeName, rng, slotCount);
            string Slot(int i) => i < picks.Slots.Count ? picks.Slots[i] : "";

            list.Add(new WeaponPassiveLoadout
            {
                WeaponId = id,
                Character = character,
                WeaponTier = weaponTier,
                MeleeElement = meleeElement,
                PrimaryArchetype = picks.PrimaryArchetype,
                Extra = Slot(0),
                Level2 = Slot(1),
                Level3 = Slot(2),
                Level4 = Slot(3),
                Level5 = Slot(4),
            });
        }
        return list;
    }

    private static Dictionary<string, string> LoadWeaponOwners()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        EnsureExtracted("DT_jRPG_Items");
        var src = FileCopy.FindTablesAsset("DT_jRPG_Items.uasset")
            ?? throw new FileNotFoundException(
                "DT_jRPG_Items.uasset not found under work/tables — cannot derive weapon owners (table extraction failed?).");
        var ed = new DataTableEditor(src);
        int totalRows = 0, weaponRows = 0;
        foreach (var row in ed.Table.Table.Data)
        {
            totalRows++;
            var dump = ed.DumpRow(row);
            if (dump["Item_Type"]?.ToString() != "NewEnumerator0") continue;
            weaponRows++;
            var id = DataTableEditor.CleanName(row.Name);
            var ch = WeaponEffectCharacters.CharacterFromItem(
                dump["Pictos_Data"]?.ToString(),
                dump["Item_Subtype"]?.ToString());
            if (ch.Length > 0) map[id] = ch;
        }
        if (totalRows == 0)
            throw new InvalidDataException(
                $"{src} parsed 0 rows — the extraction is broken. Delete the work/tables/{Path.GetFileNameWithoutExtension(src)} folder and re-pack to re-extract.");
        if (weaponRows == 0)
            throw new InvalidDataException(
                $"{src} parsed {totalRows} rows but none are weapons — the shared UE schema cache was poisoned by an earlier broken parse. Restart the editor (packs now reset it automatically) and re-pack.");
        Console.Error.WriteLine($"[RandomizerKit] derived {map.Count} weapon owners from DT_jRPG_Items ({weaponRows} weapon rows)");
        // Gustave starter weapons share Verso pictos (Verso inherits Gustave) but must stay Gustave for T3 roll
        foreach (var gid in new[] { "Noahram", "Lanceram", "Nosaram", "Velokan" })
            if (map.ContainsKey(gid)) map[gid] = "Gustave";
            else map[gid] = "Gustave";
        // Manual overrides win last (work/weapon_owner_overrides.json: { weaponId: character })
        var overridePath = Path.Combine(EditorPaths.RepoRoot, "work", "weapon_owner_overrides.json");
        if (File.Exists(overridePath))
        {
            try
            {
                var o = Newtonsoft.Json.JsonConvert.DeserializeObject<
                    Dictionary<string, string>>(File.ReadAllText(overridePath));
                foreach (var (wid, ch) in o ?? new())
                {
                    if (string.IsNullOrWhiteSpace(wid) || string.IsNullOrWhiteSpace(ch)) continue;
                    if (!WeaponEffectCharacters.Roster.Contains(ch, StringComparer.OrdinalIgnoreCase))
                    {
                        Console.Error.WriteLine($"[RandomizerKit] bad owner override '{wid}={ch}'");
                        continue;
                    }
                    map[wid] = ch;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[RandomizerKit] owner overrides unreadable: {ex.Message}");
            }
        }
        return map;
    }

    /// <summary>
    /// Final eligibility sweep of the packed weapons table against a freshly
    /// loaded catalog. Returns one line per forbidden (weapon, passive) pair;
    /// empty list means clean.
    /// </summary>
    public static List<string> SweepWeaponEligibility(string modDir)
    {
        var problems = new List<string>();
        try
        {
            var path = FindAsset(modDir, WeaponsTable + ".uasset");
            if (path is null) return problems;
            var catalog = new LootCatalog();
            var owners = LoadWeaponOwners();
            var ed = new DataTableEditor(path);
            foreach (var row in ed.Table.Table.Data)
            {
                var id = DataTableEditor.CleanName(row.Name);
                if (!owners.TryGetValue(id, out var owner)) continue;
                // Vanilla developer test weapons (DebugSciel, DebugVerso, ...) ship in the
                // table with combos that violate the real character-allocation rule (e.g.
                // DebugVerso is Gustave-owned with AutoDeath). They are not randomized loot
                // and are never assigned to a player, so they must not gate packing.
                if (id.StartsWith("Debug", StringComparison.Ordinal)) continue;
                // Same scope as RandomizeWeapons: only active T1-T5 weapons are
                // randomized; DoNotUse/untiered rows keep their vanilla passives.
                var tierCol = catalog.ItemTiers.FindColumn(ItemTiers.Weapon, id);
                if (tierCol is null || !tierCol.StartsWith("Tier", StringComparison.Ordinal)) continue;
                var d = ed.DumpRow(row);
                var slots = new[]
                {
                    d["Level1Lumina"]?.ToString(), d["Level2Lumina"]?.ToString(),
                    d["Level3Lumina"]?.ToString(), d["Level4Lumina"]?.ToString(),
                    d[WeaponPassiveSlotKit.Field]?.ToString(),
                };
                // Element gate applies to every character now (stain families
                // plus element skill / AP families), so read the melee element
                // for all owners, not just Lune.
                var meleeName = "";
                var melee = d["BaseDamageType"]?.ToString() ?? "";
                if (StainsMechanicKit.ElementNames.TryGetValue(melee, out var meleeEl)) meleeName = meleeEl;
                // Condition conflicts: no two mutually exclusive build
                // conditions on one weapon (Full Strength vs Self Burn, …).
                var pickedTags = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in slots)
                {
                    if (string.IsNullOrEmpty(p) || p == "None") continue;
                    if (!catalog.EffectTiers.Allows(catalog.EffectCharacters, owner, p))
                    {
                        problems.Add($"{id} ({owner}) <- {p}");
                        continue;
                    }
                    // Element-locked effects must match the weapon's melee element
                    // (stain families plus the element skill / AP families).
                    var el = catalog.EffectArchetypes.ElementOf(p)
                             ?? (catalog.EffectConditions.ElementGateEnabled
                                 ? catalog.EffectConditions.ElementOf(p) : null);
                    if (!string.IsNullOrEmpty(el) && !string.IsNullOrEmpty(meleeName)
                        && !el.Equals(meleeName, StringComparison.OrdinalIgnoreCase))
                        problems.Add($"{id} ({owner}) <- {p} (element {el} vs melee {meleeName})");
                    if (!catalog.EffectConditions.SelfConsistent(p))
                        problems.Add($"{id} ({owner}) <- {p} (internally conflicting tags)");
                    if (catalog.EffectConditions.Conflicts(pickedTags, p))
                    {
                        var with = slots.Where(x => !string.IsNullOrEmpty(x) && x != "None")
                            .SelectMany(x => catalog.EffectConditions.TagsOf(x))
                            .Distinct(StringComparer.Ordinal);
                        problems.Add($"{id} ({owner}) <- {p} conflicts with "
                                     + string.Join("/", with));
                    }
                    pickedTags.UnionWith(catalog.EffectConditions.TagsOf(p));
                }
                // Exclusive groups: at most one member per weapon (stain-echo,
                // status-consume, ...).
                var groups = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var p in slots)
                {
                    if (string.IsNullOrEmpty(p) || p == "None") continue;
                    if (WeaponEffectTiers.ExclusiveGroup(p) is { } g)
                        groups[g] = groups.GetValueOrDefault(g) + 1;
                }
                foreach (var (g, n) in groups)
                    if (n > 1)
                        problems.Add($"{id} ({owner}) has {n} x {g}");
            }
        }
        catch (Exception ex)
        {
            problems.Add("sweep error: " + ex.Message);
        }
        return problems;
    }

    // ───────────────────── files ─────────────────────

    private static string CopyTable(string table, string modDir)
    {
        var src = FileCopy.FindTablesAsset(table + ".uasset");
        if (src is null)
        {
            EnsureExtracted(table);
            src = FileCopy.FindTablesAsset(table + ".uasset")
                  ?? throw new FileNotFoundException(table + ".uasset");
        }
        return CopyPairIntoMod(src, modDir);
    }

    private static void RestoreVanilla(string modDir, string table)
    {
        var dest = FindAsset(modDir, table + ".uasset");
        if (dest is null) return;
        var src = FileCopy.FindTablesAsset(table + ".uasset");
        if (src is null) return;
        FileCopy.AssetPair(src, dest);
    }

    private static void RestoreMerchantTables(string modDir)
    {
        if (!Directory.Exists(modDir)) return;
        foreach (var dest in Directory.EnumerateFiles(modDir, "DT_Merchant_*.uasset", SearchOption.AllDirectories))
        {
            var src = FileCopy.FindTablesAsset(Path.GetFileName(dest));
            if (src is null) continue;
            FileCopy.AssetPair(src, dest);
        }
    }

    private static string CopyPairIntoMod(string srcUasset, string modDir)
    {
        var idx = srcUasset.IndexOf("Sandfall", StringComparison.Ordinal);
        var dest = idx >= 0
            ? Path.Combine(modDir, srcUasset[idx..])
            : Path.Combine(modDir, Path.GetFileName(srcUasset));
        FileCopy.AssetPair(srcUasset, dest);
        return dest;
    }

    private static void EnsureExtracted(string filter)
    {
        if (filter.EndsWith("_") && Directory.Exists(Path.Combine(EditorPaths.RepoRoot, "work", "tables")))
        {
            var have = Directory.EnumerateFiles(
                Path.Combine(EditorPaths.RepoRoot, "work", "tables"),
                filter + "*.uasset", SearchOption.AllDirectories).Take(5).Count();
            if (have >= 5) return;
        }
        else if (FileCopy.FindTablesAsset(filter + ".uasset") != null)
            return;
        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", filter.Trim('_'));
        Directory.CreateDirectory(dir);
        new Retoc().ToLegacy(dir, filter);
    }

    private static string? FindAsset(string root, string fileName) =>
        FileCopy.FindUnder(root, fileName);

    private static PropertyData? Named(IList<PropertyData> data, string name) =>
        data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == name);

    /// <summary>
    /// Act 3 Lumiere is the endgame final area: loot must roll at the vanilla
    /// item cap (pictos 33). Loot level is
    /// (EncounterLevel + LootContextLevelOffset)/3 (zone 48 + vanilla 0-3 =
    /// 16-17). Pin every Act 3 drop's offset to 51 (48+51=99, /3=33).
    /// WeaponDropLevelKit then scales weapon drops from that vanilla 33 to its
    /// configured MaxDropLevel (222 by default); pictos stay at 33.
    /// Runs after PinExclusiveDrops so SuperSonic/RuptureShot are covered.
    /// </summary>
    public static int PinAct3LootLevels(string modDir, LootCatalog catalog)
    {
        var path = FindAsset(modDir, EnemiesTable + ".uasset");
        if (path is null) return 0;
        var ed = new DataTableEditor(path);
        int changed = 0;
        foreach (var row in ed.Table.Table.Data)
        {
            var id = DataTableEditor.CleanName(row.Name);
            if (!catalog.Enemies.TryGetValue(id, out var enemy)) continue;
            if (!LootCatalog.IsAct3Lumiere(enemy)) continue;
            if (enemy.Archetype is "Merchant") continue;
            if (enemy.CutContent) continue;
            if (Named(row.Value, "PossibleLoot") is not ArrayPropertyData loot) continue;
            foreach (var ent in loot.Value.OfType<StructPropertyData>())
            {
                if (Named(ent.Value, "LootContextLevelOffset") is IntPropertyData ip)
                {
                    if (ip.Value != 51) { ip.Value = 51; changed++; }
                }
            }
        }
        if (changed > 0) ed.Save(path);
        return changed;
    }
}
