using System.Collections.Generic;
using System.Linq;

namespace Vorax.Sim;

public static class DrawModel
{
    const double SupremeChance = 0.01;
    static readonly HashSet<int> Boxes = [33, 34, 40];
    static readonly IReadOnlyList<(int SimId, double Weight)> Default = BuildDefault();

    public static IReadOnlyList<(int SimId, double Weight)> PotionPool(IReadOnlyDictionary<int, int>? realWeights)
    {
        if (realWeights is not { Count: > 0 }) return Default;
        return realWeights.Where(kv => kv.Value > 0 && kv.Key >= 0 && kv.Key < Potions.All.Count && !Boxes.Contains(kv.Key)).Select(kv => (kv.Key, (double)kv.Value)).ToList();
    }

    // Normal, Magic, Rare, Supreme - the order of KitDef.RarityWeights
    public static int RarityClass(int simId) => simId switch { <= 6 => 2, <= 22 => 1, <= 32 => 0, _ => 3 };

    public static IReadOnlyList<(int SimId, double Weight)> KitPool(KitDef kit, IReadOnlyList<(int SimId, double Weight)> pool)
    {
        var result = new List<(int, double)>();
        for (var rarity = 0; rarity < 4 && rarity < kit.RarityWeights.Length; rarity++)
        {
            var members = pool.Where(p => RarityClass(p.SimId) == rarity).ToList();
            var total = members.Sum(p => p.Weight);
            if (kit.RarityWeights[rarity] <= 0 || total <= 0) continue;
            result.AddRange(members.Select(p => (p.SimId, kit.RarityWeights[rarity] * p.Weight / total)));
        }
        return result;
    }

    public static int[] Draw(IReadOnlyList<(int SimId, double Weight)> pool, int count, Rng rng)
    {
        var left = new List<(int SimId, double Weight)>(pool);
        var hand = new int[System.Math.Min(count, left.Count)];
        for (var n = 0; n < hand.Length; n++)
        {
            var roll = rng.NextDouble() * left.Sum(p => p.Weight);
            var pick = 0;
            for (; pick < left.Count - 1; pick++)
            {
                roll -= left[pick].Weight;
                if (roll < 0) break;
            }
            hand[n] = left[pick].SimId;
            left.RemoveAt(pick);
        }
        return hand;
    }

    static List<(int, double)> BuildDefault()
    {
        var common = Enumerable.Range(0, 33).ToList();
        var supreme = Enumerable.Range(35, 5).ToList();
        return [.. common.Select(id => (id, (1 - SupremeChance) / common.Count)), .. supreme.Select(id => (id, SupremeChance / supreme.Count))];
    }
}
