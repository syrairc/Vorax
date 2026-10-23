using System.Collections.Generic;
using System.Linq;

namespace Vorax.Sim;

public static class Round
{
    public const int Rounds = 13;
    public const int FirstThreshold = 8000;
    public const int SecondThreshold = 28000;
    const int MaxCascadePasses = 4;

    public static void EndRound(SimState state, Rng rng)
    {
        var owned = state.Instruments.Order().ToList();
        var seen = state.Events.Count;
        var firstPass = state.Events.ToList();
        foreach (var id in owned) Instruments.Apply(state, id, firstPass, rng);
        for (var pass = 0; pass < MaxCascadePasses; pass++)
        {
            var fresh = state.Events.Skip(seen).ToList();
            if (fresh.Count == 0) break;
            seen = state.Events.Count;
            foreach (var id in owned.Where(Instruments.EventDriven)) Instruments.Apply(state, id, fresh, rng);
        }
    }

    public static SimState Play(SimState state, int potionSimId, IReadOnlyList<int> targets, Rng rng)
    {
        var next = state.Clone();
        next.Events.Clear();
        if (potionSimId >= 0) Potions.Apply(next, potionSimId, targets, rng);
        EndRound(next, rng);
        if (next.Round >= Rounds) return next;
        next.Round++;
        next.Events.Clear();
        var total = next.TotalActivity;
        if (!next.Passed8000 && total >= FirstThreshold)
        {
            next.Passed8000 = true;
            next.PendingInstrumentPicks++;
        }
        if (!next.Passed28000 && total >= SecondThreshold)
        {
            next.Passed28000 = true;
            next.PendingInstrumentPicks++;
        }
        return next;
    }
}
