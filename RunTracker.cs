using System.Collections.Generic;
using EmberHUD;
using Vorax.Sim;

namespace Vorax;

public sealed class RunTracker
{
    int lastRound;
    int startActivity;
    bool passed8000;
    bool passed28000;
    readonly Dictionary<int, int> acquiredRounds = [];
    MapOptions options;

    public MapOptions Update(VoraxState state)
    {
        if (state.Phase == VoraxPhase.Other) return options;
        var newRun = lastRound == 0 || (state.Round == 1 && lastRound > 1) || state.Activity < startActivity;
        if (newRun)
        {
            startActivity = state.Activity;
            passed8000 = passed28000 = false;
            acquiredRounds.Clear();
        }

        lastRound = state.Round;
        passed8000 |= state.Activity >= 8000;
        passed28000 |= state.Activity >= 28000;
        foreach (var card in state.Instruments) acquiredRounds.TryAdd(card.Id, state.Round);
        return options = new(passed8000, passed28000, new Dictionary<int, int>(acquiredRounds));
    }
}
