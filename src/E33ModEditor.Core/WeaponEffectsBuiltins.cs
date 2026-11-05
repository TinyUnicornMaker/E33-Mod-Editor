namespace E33ModEditor.Core;

/// <summary>
/// Generated stain-start (2/4) and stain-echo matrix (6×6 without same-type).
/// Same-type echoes (Fire→Fire etc.) are intentionally omitted — they cause an
/// infinite loop because the generated stain re-triggers its own echo.
/// Light/Dark start graphs do not exist; those stains still appear in the echo matrix.
/// </summary>
internal static class WeaponEffectsBuiltins
{
    private static readonly (string Name, string Enum, string Bp, string Passive, string AmountKey)[] Starts =
    {
        ("Fire", "NewEnumerator0", "BP_LU_Stains_FireStart", "Stains_FireStart", "Stains_FireStart_StainsAmount"),
        ("Ice", "NewEnumerator1", "BP_LU_Stains_IceStart", "Stains_IceStart", "Stains_IceStart_StainsAmount"),
        ("Lightning", "NewEnumerator2", "BP_LU_Stains_LightningStart", "Stains_LightningStart", "Stains_LightningStart_StainsAmount"),
        ("Earth", "NewEnumerator3", "BP_LU_Stains_EarthStart", "Stains_EarthStart", "Stains_EarthStart_StainsAmount"),
    };

    private static readonly (string Name, string Enum)[] EchoStains =
    {
        ("Fire", "NewEnumerator0"),
        ("Ice", "NewEnumerator1"),
        ("Lightning", "NewEnumerator2"),
        ("Earth", "NewEnumerator3"),
        ("Dark", "NewEnumerator4"),
        ("Light", "NewEnumerator5"),
    };

    public static IEnumerable<WeaponEffectsCreateKit.EffectSpec> All()
    {
        foreach (var s in Starts)
        {
            foreach (var amount in new[] { 2, 4 })
            {
                var id = $"WepFx_{s.Name}Start{amount}";
                var key = id + "_StainsAmount";
                yield return new WeaponEffectsCreateKit.EffectSpec(
                    id,
                    $"{s.Name} Start {amount}",
                    $"Start battle with {amount} {s.Name} Stain{(amount == 1 ? "" : "s")}.",
                    WeaponEffectsCreateKit.FamilyStainStart,
                    s.Bp,
                    s.Passive,
                    null,
                    "Lune",
                    s.Name,
                    new[] { (s.AmountKey, key) },
                    new[] { new WeaponEffectsCreateKit.BalancingKey(key, s.AmountKey, amount) },
                    s.Enum,
                    null,
                    null);
            }
        }

        foreach (var from in EchoStains)
        foreach (var to in EchoStains)
        {
            if (from.Name == to.Name) continue; // same-type → infinite loop, skip
            var id = $"WepFx_Echo{from.Name}{to.Name}";
            yield return new WeaponEffectsCreateKit.EffectSpec(
                id,
                $"{from.Name} → {to.Name}",
                $"When a {from.Name} Stain is generated, a {to.Name} Stain is also generated.",
                WeaponEffectsCreateKit.FamilyStainEcho,
                "BP_LU_Stains_Stains_+StainOnSpecificStainGenerated",
                "Stains_LightningOnFire",
                null,
                "Lune",
                from.Name,
                Array.Empty<(string, string)>(),
                Array.Empty<WeaponEffectsCreateKit.BalancingKey>(),
                from.Enum,
                to.Enum,
                null);
        }
    }

}
