namespace E33ModEditor.Core.Loot;

/// <summary>
/// Archetype-aware weapon passive draw. One primary archetype is chosen per
/// weapon; every archetype slot draws an archetype first (the primary with
/// <see cref="PrimaryShare"/>, otherwise a uniformly random sibling link) and
/// then an effect inside it. Repeats are allowed, the same effect never twice.
/// One wildcard fills the weakest slot. The existing effect-tier mix, character
/// rules, exclusive groups and the Lune melee-element stain gate all still
/// apply.
///
/// The condition layer (<see cref="WeaponEffectConditions"/>) additionally
/// rejects any pick whose conditions fight an effect already on the weapon
/// (Full Strength vs Self Burn, counter vs no-damage, …). The wildcard prefers
/// *neutral* effects so it can never break the committed build; archetype
/// effects are picked before the wildcard, so the build is seeded first.
/// </summary>
public static class WeaponEffectRoller
{
    /// <summary>Chance an archetype slot takes the primary archetype instead of
    /// one of its siblings. 0.5 makes a T4/T5 weapon visibly stack its primary
    /// while sibling picks still vary the build.</summary>
    public const double PrimaryShare = 0.5;

    public sealed class Picks
    {
        public string PrimaryArchetype { get; init; } = "";
        /// <summary>Slot order: index 0 = Extra (weakest), then Level2..Level5.</summary>
        public IReadOnlyList<string> Slots { get; init; } = Array.Empty<string>();
        /// <summary>Condition tags committed by the picked effects.</summary>
        public IReadOnlyList<string> ConditionTags { get; init; } = Array.Empty<string>();
    }

    public static Picks Roll(
        WeaponEffectTiers tiers,
        WeaponEffectCharacters chars,
        WeaponEffectArchetypes archetypes,
        WeaponEffectConditions conditions,
        int weaponTier,
        string character,
        string? meleeElement,
        Random rng,
        int slotCount)
    {
        slotCount = Math.Clamp(slotCount, 1, 5);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var pickedTags = new HashSet<string>(StringComparer.Ordinal);

        string? ElementOf(string id) =>
            archetypes.ElementOf(id)
            ?? (conditions.ElementGateEnabled ? conditions.ElementOf(id) : null);

        bool ElementOk(string id)
        {
            var el = ElementOf(id);
            if (string.IsNullOrEmpty(el)) return true;
            return !string.IsNullOrEmpty(meleeElement)
                   && el.Equals(meleeElement, StringComparison.OrdinalIgnoreCase);
        }

        bool Allowed(string id) =>
            ElementOk(id) && tiers.Allows(chars, character, id)
            && !conditions.Conflicts(pickedTags, id);

        // Eligible archetypes: at least one member this character may roll and
        // (for Lune) the weapon's melee element allows.
        var eligible = new List<(WeaponEffectArchetypes.Archetype Arch, int Weight)>();
        foreach (var arch in archetypes.All)
        {
            var weight = 0;
            foreach (var m in arch.Members)
                if (Allowed(m)) weight++;
            if (weight > 0) eligible.Add((arch, weight));
        }

        // One primary per weapon, weighted by eligible member count, so the
        // archetype draw is proportional to the pool the old uniform roll saw.
        var primary = "";
        var siblings = new List<WeaponEffectArchetypes.Archetype>();
        if (eligible.Count > 0)
        {
            primary = PickWeighted(eligible, rng);
            var eligibleById = new Dictionary<string, WeaponEffectArchetypes.Archetype>(StringComparer.Ordinal);
            foreach (var (arch, _) in eligible) eligibleById[arch.Id] = arch;
            var siblingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var l in archetypes.Links(primary)) siblingIds.Add(l);
            // Lune: the melee element's stain family and the neutral Stain
            // family always stay in reach — the element gate removes the other
            // families, mirroring "remove the wrong-element stain effects"
            // instead of hiding the matching ones behind the link graph.
            if (string.Equals(character, "Lune", StringComparison.OrdinalIgnoreCase))
            {
                siblingIds.Add("stain");
                var elArch = "stain_" + (meleeElement ?? "").ToLowerInvariant();
                if (archetypes.Get(elArch) is not null) siblingIds.Add(elArch);
            }
            foreach (var id in siblingIds)
                if (eligibleById.TryGetValue(id, out var arch))
                    siblings.Add(arch);
        }

        // Archetype effects first: they seed the build's condition tags, so the
        // later wildcard can be checked against them.
        var picks = new List<string>();
        for (int i = 1; i < slotCount; i++)
        {
            // Synergy bias: once a trigger is on the weapon (e.g. Shield
            // Spender / Burn applier), later picks are weighted toward its
            // partners (Shield Maker / Burn scaling). Re-evaluated per slot as
            // the build grows.
            var p = PickArchetypeEffect(tiers, chars, archetypes, conditions, primary, siblings,
                weaponTier, character, used, rng, Allowed, pickedTags);
            if (string.IsNullOrEmpty(p))
                p = tiers.PickEffect(weaponTier, character, used, chars, rng, Allowed);
            if (string.IsNullOrEmpty(p)) break;
            used.Add(p);
            picks.Add(p);
            pickedTags.UnionWith(conditions.TagsOf(p));
        }

        // Wildcard owns the weakest slot. Prefer a synergy partner when one is
        // active, else a neutral (condition-free) one so it cannot fight the
        // committed build; fall back to any conflict-free effect.
        var wildcard = tiers.PickEffect(weaponTier, character, used, chars, rng,
                           id => Allowed(id) && conditions.SynergyWeight(id, pickedTags) > 0)
                       ?? tiers.PickEffect(weaponTier, character, used, chars, rng,
                           id => Allowed(id) && conditions.IsNeutral(id))
                       ?? tiers.PickEffect(weaponTier, character, used, chars, rng, Allowed);
        if (!string.IsNullOrEmpty(wildcard))
        {
            used.Add(wildcard);
            pickedTags.UnionWith(conditions.TagsOf(wildcard));
        }

        // Rank archetype picks strongest-first into the higher slots; the
        // wildcard already owns Extra.
        picks.Sort((a, b) =>
        {
            var cmp = (tiers.TierOf(a) ?? 0).CompareTo(tiers.TierOf(b) ?? 0);
            return cmp != 0 ? cmp : string.CompareOrdinal(a, b);
        });
        var slots = new string[slotCount];
        if (!string.IsNullOrEmpty(wildcard)) slots[0] = wildcard;
        if (string.IsNullOrEmpty(slots[0]) && picks.Count > 0)
        {
            slots[0] = picks[0];
            picks.RemoveAt(0);
        }
        for (int i = 0; i < picks.Count && i + 1 < slotCount; i++)
            slots[i + 1] = picks[i];

        return new Picks
        {
            PrimaryArchetype = primary,
            Slots = slots,
            ConditionTags = pickedTags.OrderBy(x => x, StringComparer.Ordinal).ToList(),
        };
    }

    private static string PickArchetypeEffect(
        WeaponEffectTiers tiers, WeaponEffectCharacters chars, WeaponEffectArchetypes archetypes,
        WeaponEffectConditions conditions, string primary,
        IReadOnlyList<WeaponEffectArchetypes.Archetype> siblings,
        int weaponTier, string character, ISet<string> used, Random rng, Func<string, bool> allowed,
        IReadOnlySet<string> pickedTags)
    {
        var gustave = WeaponEffectTiers.IsGustave(character);
        var mixTier = gustave ? 3 : Math.Clamp(weaponTier, 1, 5);
        for (int i = 0; i < 24; i++)
        {
            var t = WeaponEffectTiers.PickEffectTier(mixTier, rng);
            if (gustave && t > 3) t = 3;
            var p = AtTier(t);
            if (p.Length > 0) return p;
        }
        // Walk the tiers high-to-low as a fallback, capped at the weapon's own
        // tier (so a T4 weapon never grabs a T5 archetype effect here) and at
        // T3 for Gustave.
        var cap = gustave ? 3 : Math.Clamp(weaponTier, 1, 5);
        for (int t = cap; t >= 1; t--)
        {
            var p = AtTier(t);
            if (p.Length > 0) return p;
        }
        return "";

        string AtTier(int tier)
        {
            var primaryCandidates = primary.Length == 0
                ? new List<string>()
                : Candidates(tiers, chars, archetypes.Get(primary)!, tier, character, used, allowed);
            var siblingCandidates = new List<(WeaponEffectArchetypes.Archetype Arch, List<string> Effects)>();
            foreach (var s in siblings)
            {
                var c = Candidates(tiers, chars, s, tier, character, used, allowed);
                if (c.Count > 0) siblingCandidates.Add((s, c));
            }

            List<string>? chosen = null;
            if (primaryCandidates.Count > 0 && siblingCandidates.Count > 0)
                chosen = rng.NextDouble() < PrimaryShare
                    ? primaryCandidates
                    : PickSiblingGroup(siblingCandidates, conditions, pickedTags, rng);
            else if (primaryCandidates.Count > 0)
                chosen = primaryCandidates;
            else if (siblingCandidates.Count > 0)
                chosen = PickSiblingGroup(siblingCandidates, conditions, pickedTags, rng);
            return chosen is { Count: > 0 }
                ? PickWeightedEffect(chosen, conditions, pickedTags, rng) : "";
        }
    }

    /// <summary>Pick a sibling archetype first, weighting groups that contain a
    /// synergy effect so a trigger can actually find its partner.</summary>
    private static List<string> PickSiblingGroup(
        List<(WeaponEffectArchetypes.Archetype Arch, List<string> Effects)> groups,
        WeaponEffectConditions conditions, IReadOnlySet<string> pickedTags, Random rng)
    {
        if (pickedTags.Count == 0) return groups[rng.Next(groups.Count)].Effects;
        var weights = new int[groups.Count];
        var total = 0;
        for (int i = 0; i < groups.Count; i++)
        {
            var best = 0;
            foreach (var id in groups[i].Effects)
                best = Math.Max(best, conditions.SynergyWeight(id, pickedTags));
            weights[i] = 1 + Math.Max(0, best);
            total += weights[i];
        }
        var roll = rng.Next(total);
        for (int i = 0; i < groups.Count; i++)
        {
            roll -= weights[i];
            if (roll < 0) return groups[i].Effects;
        }
        return groups[^1].Effects;
    }

    /// <summary>Uniform draw, or weighted toward synergy partners (and away
    /// from synergy `avoid` tags).</summary>
    private static string PickWeightedEffect(
        IReadOnlyList<string> ids, WeaponEffectConditions conditions,
        IReadOnlySet<string> pickedTags, Random rng)
    {
        if (ids.Count == 0) return "";
        if (pickedTags.Count == 0) return ids[rng.Next(ids.Count)];
        var total = 0;
        foreach (var id in ids) total += Weight(id);
        var roll = rng.Next(total);
        foreach (var id in ids)
        {
            roll -= Weight(id);
            if (roll < 0) return id;
        }
        return ids[^1];

        int Weight(string id) => Math.Max(1, 1 + conditions.SynergyWeight(id, pickedTags));
    }

    private static List<string> Candidates(
        WeaponEffectTiers tiers, WeaponEffectCharacters chars, WeaponEffectArchetypes.Archetype arch,
        int tier, string character, ISet<string> used, Func<string, bool> allowed)
    {
        var blocked = WeaponEffectTiers.BlockedGroups(used);
        var pool = tiers.Pool(tier);
        var list = new List<string>();
        foreach (var id in arch.Members)
        {
            if (!pool.Contains(id, StringComparer.Ordinal) || used.Contains(id)) continue;
            if (WeaponEffectTiers.ExclusiveGroup(id) is { } g && blocked.Contains(g)) continue;
            if (!allowed(id) || !tiers.Allows(chars, character, id)) continue;
            list.Add(id);
        }
        return list;
    }

    private static string PickWeighted(
        IReadOnlyList<(WeaponEffectArchetypes.Archetype Arch, int Weight)> items, Random rng)
    {
        var total = 0;
        foreach (var (_, w) in items) total += Math.Max(1, w);
        var roll = rng.Next(total);
        foreach (var (a, w) in items)
        {
            roll -= Math.Max(1, w);
            if (roll < 0) return a.Id;
        }
        return items[^1].Arch.Id;
    }
}
