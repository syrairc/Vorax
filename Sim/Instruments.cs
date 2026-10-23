using System;
using System.Collections.Generic;
using System.Linq;

namespace Vorax.Sim;

// Port of the community Vorax simulator instrument handlers (MIT, see LICENSE-voraxsim.txt). Sim id 25 is new, not in the JS
public static class Instruments
{
    delegate void Handler(SimState s, IReadOnlyList<SimEvent> events, Rng rng);

    public const int Butterfly = 25;

    static readonly HashSet<int> EventDrivenIds = [2, 5, 6, 7, 8, 12, 16, 22];

    static readonly Dictionary<int, Handler> Handlers = Build();

    public static bool Known(int simId) => Handlers.ContainsKey(simId);

    public static bool EventDriven(int simId) => EventDrivenIds.Contains(simId);

    internal static void Apply(SimState state, int simId, IReadOnlyList<SimEvent> events, Rng rng)
    {
        if (Handlers.TryGetValue(simId, out var handler)) handler(state, events, rng);
    }

    static Dictionary<int, Handler> Build() => new()
    {
        [1] = (s, _, rng) =>
        {
            var pests = BySpecies(s, Species.Pests).Count;
            for (var rep = 0; rep < pests; rep++)
            {
                var all = NonEmpty(s);
                foreach (var i in rng.Sample(all, Math.Min(pests, all.Count))) Boost(s, i, 0, 8);
            }
        },
        [2] = (s, events, _) =>
        {
            if (BySpecies(s, Species.Pests).Count < 2) return;
            for (var n = events.Count(e => e.Kind == EventKind.Add); n > 0; n--)
                foreach (var i in NonEmpty(s)) Boost(s, i, 0, 45);
        },
        [3] = (s, _, rng) =>
        {
            if (BySpecies(s, Species.Agito).Count < 2) return;
            var strong = NonEmpty(s).Where(i => s.Dishes[i]!.Value.Rarity >= Rarity.Rare).ToList();
            if (strong.Count > 0) Boost(s, rng.Pick(strong), 80);
        },
        [4] = (s, _, rng) =>
        {
            if (!BySpecies(s, Species.Agito).Any(i => s.Dishes[i]!.Value.Rarity >= Rarity.Rare)) return;
            Boost(s, rng.Pick(NonEmpty(s)), 80);
        },
        [5] = (s, events, rng) =>
        {
            if (BySpecies(s, Species.BoneSentry).Count < 2) return;
            for (var n = events.Count(e => e.Kind == EventKind.Remove); n > 0; n--) BoostRandom(s, rng, 0, 180);
        },
        [6] = (s, events, rng) =>
        {
            for (var n = events.Count(e => e.Kind == EventKind.Remove && e.Species != Species.BoneSentry); n > 0; n--) BoostRandom(s, rng, 0, 150);
        },
        [7] = (s, events, _) =>
        {
            for (var n = events.Count(e => e.Kind == EventKind.Mutate && e.ToSpecies == Species.Aemberon); n > 0; n--)
                foreach (var i in NonEmpty(s)) Boost(s, i, 35);
        },
        [8] = (s, events, _) =>
        {
            if (BySpecies(s, Species.Aemberon).Count < 2) return;
            for (var n = events.Count(e => e.Kind == EventKind.Mutate); n > 0; n--)
                foreach (var i in NonEmpty(s)) Boost(s, i, 20);
        },
        [9] = (s, _, rng) =>
        {
            if (BySpecies(s, Species.Pests).Count < 3) return;
            var added = s.Add(Monster.Fresh(Species.Pests, SimState.RandomRarity(rng)));
            if (added >= 0) Boost(s, added, 0, 100);
        },
        [10] = (s, _, rng) =>
        {
            var magic = NonEmpty(s).Where(i => s.Dishes[i]!.Value.Rarity == Rarity.Magic).ToList();
            if (magic.Count >= 2) s.Fuse(rng.Sample(magic, 2), Species.Agito, Rarity.Rare);
        },
        [11] = (s, _, rng) =>
        {
            var sentries = BySpecies(s, Species.BoneSentry);
            if (sentries.Count < 2) return;
            var others = NonEmpty(s).Where(i => s.Dishes[i]!.Value.Species != Species.BoneSentry).ToList();
            if (others.Count == 0) return;
            var removed = s.Remove(others.MinBy(i => s.Dishes[i]!.Value.Total));
            if (removed is { } m) Boost(s, rng.Pick(sentries), m.Activity, m.Quantity);
        },
        [12] = (s, events, rng) =>
        {
            foreach (var e in events.Where(e => e.Kind == EventKind.Add))
            {
                if (s.Dishes[e.Dish] is null || rng.NextDouble() >= 0.75) continue;
                s.Mutate(e.Dish, Species.Aemberon, null, rng);
                Boost(s, e.Dish, 80);
            }
        },
        [13] = (s, _, _) =>
        {
            if (NonEmpty(s).Count(i => s.Dishes[i]!.Value.Rarity == Rarity.Magic) < 3) return;
            var highest = NonEmpty(s).MaxBy(i => s.Dishes[i]!.Value.Total);
            Boost(s, highest, 0, 100);
        },
        [14] = (s, _, _) =>
        {
            foreach (var rarity in new[] { Rarity.Normal, Rarity.Magic, Rarity.Rare })
            {
                var same = NonEmpty(s).Where(i => s.Dishes[i]!.Value.Rarity == rarity).ToList();
                while (same.Count >= 3)
                {
                    var three = same.Take(3).ToList();
                    same.RemoveRange(0, 3);
                    s.Fuse(three, s.Dishes[three[0]]!.Value.Species, rarity + 1);
                }
            }
        },
        [15] = (s, _, _) =>
        {
            foreach (var i in NonEmpty(s).Where(i => s.Dishes[i]!.Value is { Quantity: > 150, Activity: > 150 })) Boost(s, i, 15, 15);
        },
        [16] = (s, events, _) =>
        {
            for (var n = events.Count(e => e.Kind == EventKind.AddOverflow); n > 0; n--)
            {
                var all = NonEmpty(s);
                if (all.Count > 0) Boost(s, all.MinBy(i => s.Dishes[i]!.Value.Total), 25, 25);
            }
        },
        [17] = (s, _, _) =>
        {
            foreach (var i in NonEmpty(s).Where(i => s.Dishes[i]!.Value.Quantity > 275)) Boost(s, i, 20);
        },
        [18] = (s, _, _) =>
        {
            foreach (var i in NonEmpty(s).Where(i => s.Dishes[i]!.Value.Activity > 255)) Boost(s, i, 0, 30);
        },
        [19] = (s, _, rng) =>
        {
            if (!EveryNthRound(s, 19, 2)) return;
            foreach (var rarity in Enum.GetValues<Rarity>())
            {
                var candidates = NonEmpty(s).Where(i => s.Dishes[i]!.Value.Rarity == rarity).ToList();
                if (candidates.Count > 0) s.Awaken(rng.Pick(candidates));
            }
        },
        [20] = (s, _, rng) =>
        {
            if (!EveryNthRound(s, 20, 3)) return;
            foreach (var i in NonEmpty(s)) s.Mutate(i, null, null, rng);
        },
        [21] = (s, _, _) =>
        {
            var rarities = NonEmpty(s).Select(i => s.Dishes[i]!.Value.Rarity).ToHashSet();
            if (!rarities.IsSupersetOf([Rarity.Normal, Rarity.Magic, Rarity.Boss])) return;
            foreach (var i in NonEmpty(s)) Boost(s, i, 20);
        },
        [22] = (s, events, _) =>
        {
            for (var n = events.Count(e => e.Kind == EventKind.Remove); n > 0; n--)
            {
                var all = NonEmpty(s);
                if (all.Count > 0) Boost(s, all.MaxBy(i => s.Dishes[i]!.Value.Total), 20, 20);
            }
        },
        [23] = (s, _, rng) =>
        {
            var all = NonEmpty(s);
            if (all.Count is >= 2 and <= 5) s.Add(Monster.Fresh(rng.Pick(SimState.AllSpecies), SimState.RandomRarity(rng)));
            else if (all.Count == SimState.DishCount)
            {
                var highest = all.MaxBy(i => s.Dishes[i]!.Value.Total);
                var lowest = all.MinBy(i => s.Dishes[i]!.Value.Total);
                if (highest != lowest) s.Devour(highest, lowest);
            }
        },
        [24] = (s, _, _) =>
        {
            if (NonEmpty(s).Count(i => s.Dishes[i]!.Value.Rarity == Rarity.Normal) < 3) return;
            foreach (var i in NonEmpty(s)) Boost(s, i, 0, 30);
        },
        [Butterfly] = (s, _, rng) =>
        {
            if (!EveryNthRound(s, Butterfly, 3)) return;
            var pests = BySpecies(s, Species.Pests);
            if (pests.Count >= 2) s.Fuse(rng.Sample(pests, 2), Species.Pests, SimState.RandomRarity(rng));
        },
    };

    static bool EveryNthRound(SimState s, int simId, int n)
    {
        var elapsed = s.Round - s.AcquiredRound.GetValueOrDefault(simId, 1);
        return elapsed > 0 && elapsed % n == 0;
    }

    static List<int> NonEmpty(SimState s) => Enumerable.Range(0, SimState.DishCount).Where(i => s.Dishes[i] is not null).ToList();

    static List<int> BySpecies(SimState s, Species species) => NonEmpty(s).Where(i => s.Dishes[i]!.Value.Species == species).ToList();

    static void Boost(SimState s, int dish, int activity, int quantity = 0)
    {
        var m = s.Dishes[dish]!.Value;
        s.Dishes[dish] = m with { Activity = m.Activity + activity, Quantity = m.Quantity + quantity };
    }

    static void BoostRandom(SimState s, Rng rng, int activity, int quantity)
    {
        var all = NonEmpty(s);
        if (all.Count > 0) Boost(s, rng.Pick(all), activity, quantity);
    }
}
