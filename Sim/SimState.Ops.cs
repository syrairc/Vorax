using System;
using System.Collections.Generic;

namespace Vorax.Sim;

public sealed partial class SimState
{
    static readonly Species[] AllSpecies = [Species.Pests, Species.BoneSentry, Species.Agito, Species.Aemberon];
    static readonly (Rarity Rarity, int Weight)[] RarityWeights = [(Rarity.Normal, 30), (Rarity.Magic, 40), (Rarity.Rare, 20), (Rarity.Boss, 10)];

    public int Add(Monster m)
    {
        var dish = Array.FindIndex(Dishes, d => d is null);
        if (dish < 0)
        {
            Events.Add(new(EventKind.AddOverflow, -1, m.Species, m.Species));
            return -1;
        }
        Dishes[dish] = m;
        Events.Add(new(EventKind.Add, dish, m.Species, m.Species));
        return dish;
    }

    public Monster? Remove(int dish)
    {
        var m = Dishes[dish];
        if (m is null) return null;
        Dishes[dish] = null;
        Events.Add(new(EventKind.Remove, dish, m.Value.Species, m.Value.Species));
        return m;
    }

    public void Mutate(int dish, Species? to, Rarity? rarity, Rng rng)
    {
        if (Dishes[dish] is not { } m) return;
        var species = to ?? rng.Pick(AllSpecies);
        var newRarity = rarity ?? RandomRarity(rng);
        Dishes[dish] = m with { Species = species, Rarity = newRarity };
        Events.Add(new(EventKind.Mutate, dish, m.Species, species));
    }

    public void Awaken(int dish, Rarity? to = null)
    {
        if (Dishes[dish] is not { } m) return;
        var next = to ?? (m.Rarity == Rarity.Boss ? Rarity.Boss : m.Rarity + 1);
        var bonus = next == Rarity.Magic ? 60 : 120;
        Dishes[dish] = m with { Rarity = next, Activity = m.Activity + bonus };
        Events.Add(new(EventKind.Awaken, dish, m.Species, m.Species));
    }

    public int Fuse(IReadOnlyList<int> dishes, Species species, Rarity rarity)
    {
        int activity = 0, quantity = 0;
        foreach (var i in dishes)
            if (Dishes[i] is { } m)
            {
                activity += m.Activity;
                quantity += m.Quantity;
            }
        foreach (var i in dishes) Remove(i);
        var dish = Array.FindIndex(Dishes, d => d is null);
        if (dish < 0) return -1;
        Dishes[dish] = new Monster(species, rarity, activity, quantity);
        Events.Add(new(EventKind.Fuse, dish, species, species));
        Events.Add(new(EventKind.Add, dish, species, species));
        return dish;
    }

    public void Devour(int eater, int prey)
    {
        if (Dishes[eater] is not { } e || Dishes[prey] is not { } p) return;
        Dishes[eater] = e with { Activity = e.Activity + p.Activity, Quantity = e.Quantity + p.Quantity };
        Remove(prey);
        Events.Add(new(EventKind.Devour, eater, e.Species, p.Species));
    }

    static Rarity RandomRarity(Rng rng)
    {
        var roll = rng.NextDouble() * 100;
        var cumulative = 0;
        foreach (var (rarity, weight) in RarityWeights)
        {
            cumulative += weight;
            if (roll < cumulative) return rarity;
        }
        return Rarity.Normal;
    }
}
