using System;
using System.Linq;
using EmberHUD;

namespace Vorax;

public sealed class PestsStrategy : Strategy
{
    static readonly int[] InstrumentPriority = [103, 211, 202, 106, 205];
    static readonly int[] PestAdders = [102, 104, 211, 305, 403];
    static readonly int[] QuantityPotions = [207, 214, 208];
    static readonly int[] ActivityPotions = [205, 206, 301];
    static readonly int[] PotionsThatCanLoseARarity = [207, 214, 208, 213, 216];

    const int TannedLeatherRestraint = 202;
    const int EarlyRounds = 4;

    public override string Name => "Pests";

    public override string Description => "Add Pest groups early, Quantity on the most active group, Activity on the biggest one.";

    public override Pick? PickInstrument(StrategyContext c) => StrategyHelpers.PickFirstOffered(c, InstrumentPriority, "Pest build instrument");

    public override Pick? PickPotion(StrategyContext c)
    {
        if (StrategyHelpers.SimulatorWantsAKit(c)) return null;
        var early = c.State.Round <= EarlyRounds ? Take(c, PestAdders, "adds Pest groups early") : null;
        return early ?? Take(c, QuantityPotions, "Quantity for the most active group") ?? Take(c, ActivityPotions, "Activity for the group with most Quantity");
    }

    public override Pick? PickTarget(StrategyContext c)
    {
        var potion = c.Chosen!.Value.Id;
        if (QuantityPotions.Contains(potion)) return Target(c, m => m.Activity, "most Activity gets the Quantity");
        if (ActivityPotions.Contains(potion)) return Target(c, m => m.Count, "most Quantity gets the Activity");
        return null;
    }

    static Pick? Take(StrategyContext c, int[] potions, string reason)
    {
        var allowed = BeltAtRisk(c) ? potions.Except(PotionsThatCanLoseARarity).ToArray() : potions;
        return StrategyHelpers.PickFirstOffered(c, allowed, reason);
    }

    static bool BeltAtRisk(StrategyContext c) => StrategyHelpers.Owns(c, TannedLeatherRestraint) && (Groups(c, 1) == 1 || Groups(c, 2) == 1 || Groups(c, 3, 4) == 1);

    static int Groups(StrategyContext c, params int[] rarities) => c.State.Monsters.Count(m => rarities.Contains(m.Rarity));

    static Pick? Target(StrategyContext c, Func<VoraxMonster, int> by, string reason)
    {
        if (c.State.Monsters.Length == 0) return null;
        return Pick.One(c.State.Monsters.MaxBy(by).Slot, reason);
    }
}
