using System.Linq;
using EmberHUD;

namespace Vorax;

public sealed class ClawsStrategy : Strategy
{
    static readonly int[] InstrumentPriority = [102, 204, 203];
    static readonly int[] Removers = [207, 214, 213, 302, 307, 105, 106];
    static readonly int[] SacrificePotions = [105, 106];
    static readonly int[] FunnelPotions = [207, 214, 213];
    static readonly int[] Awakeners = [210, 303];

    const int BoneSentry = 202;

    public override string Name => "Claws";

    public override string Description => "Remove, fuse and devour groups to funnel everything into one big group, Awaken it late.";

    public override Pick? PickInstrument(StrategyContext c) => StrategyHelpers.PickFirstOffered(c, InstrumentPriority, "Claws build instrument");

    public override Pick? PickPotion(StrategyContext c)
    {
        if (StrategyHelpers.SimulatorWantsAKit(c)) return null;
        var awaken = IsLate(c) ? StrategyHelpers.PickFirstOffered(c, Awakeners, "Awaken the big group") : null;
        return awaken ?? (CanRemove(c) ? StrategyHelpers.PickFirstOffered(c, Removers, "removes a group to funnel into one") : null);
    }

    public override Pick? PickTarget(StrategyContext c)
    {
        var potion = c.Chosen!.Value.Id;
        if (SacrificePotions.Contains(potion)) return Sacrifice(c);
        if (FunnelPotions.Contains(potion) || Awakeners.Contains(potion)) return Biggest(c);
        return null;
    }

    static bool IsLate(StrategyContext c) => c.State.Round > c.State.Rounds / 2;

    static bool CanRemove(StrategyContext c) => c.State.Monsters.Length >= 2 && c.State.Monsters.Any(m => m.Species != BoneSentry);

    static Pick? Biggest(StrategyContext c) => c.State.Monsters.Length == 0 ? null : Pick.One(c.State.Monsters.MaxBy(m => m.Total).Slot, "biggest group");

    static Pick? Sacrifice(StrategyContext c)
    {
        var candidates = c.State.Monsters.Where(m => m.Species != BoneSentry).ToArray();
        return candidates.Length == 0 ? null : Pick.One(candidates.MinBy(m => m.Total).Slot, "smallest group that is not a Bone Sentry");
    }
}
