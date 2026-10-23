using System.Linq;
using EmberHUD.Testing;
using Vorax.Sim;

namespace Vorax;

public sealed class PotionTests
{
    static readonly Monster Pest = Monster.Fresh(Species.Pests, Rarity.Normal);
    static readonly Monster Sentry = Monster.Fresh(Species.BoneSentry, Rarity.Normal);
    static readonly Monster Agito = Monster.Fresh(Species.Agito, Rarity.Normal);
    static readonly Monster Aemberon = Monster.Fresh(Species.Aemberon, Rarity.Normal);

    [Test]
    public void ProbioticEnzymeAddsElevenActivityPerPestGroupToEveryPest()
    {
        var s = Board(Pest, Pest, Pest, Sentry);
        Use(s, 0);
        Check.Equal(34, s.Dishes[0]!.Value.Activity);
        Check.Equal(34, s.Dishes[2]!.Value.Activity);
        Check.Equal(1, s.Dishes[3]!.Value.Activity);
    }

    [Test]
    public void XenopurgeAddsOneFiftyFourAndRemovesTwoOtherSpeciesGroups()
    {
        var s = Board(Agito, Aemberon, Aemberon);
        Use(s, 17, 0);
        Check.Equal(1, Count(s));
        Check.Equal((1, 190), (s.Dishes[0]!.Value.Activity, s.Dishes[0]!.Value.Quantity));
    }

    [Test]
    public void PotentAndPlainXenopurgeDifferInQuantityAndRemovals()
    {
        var potent = Board(Agito, Aemberon, Aemberon, Aemberon);
        var plain = Board(Agito, Aemberon, Aemberon, Aemberon);
        Use(potent, 17, 0);
        Use(plain, 18, 0);
        Check.Equal((2, 190), (Count(potent), potent.Dishes[0]!.Value.Quantity));
        Check.Equal((3, 163), (Count(plain), plain.Dishes[0]!.Value.Quantity));
    }

    [Test]
    public void BoneSentryPotionsRemoveOnlyNonSentries()
    {
        foreach (var id in new[] { 3, 10, 37 })
        {
            var s = Board(Sentry, Pest, Agito, Aemberon);
            Use(s, id);
            Check.That(s.Dishes.Where(d => d is not null).Any(d => d!.Value.Species == Species.BoneSentry));
            Check.That(s.Dishes[0]!.Value.Species == Species.BoneSentry);
        }
        var all = Board(Sentry, Pest, Agito, Aemberon);
        Use(all, 37);
        Check.Equal(1, Count(all));
        Check.Equal((1 + 60, 36 + 117), (all.Dishes[0]!.Value.Activity, all.Dishes[0]!.Value.Quantity));
        var none = Board(Pest, Agito);
        Use(none, 37);
        Check.Equal(2, Count(none));
    }

    [Test]
    public void ChemicalBoneOintmentRemovesUpToThreeAndBuffsEveryone()
    {
        var s = Board(Sentry, Pest, Agito, Aemberon, Pest);
        Use(s, 3);
        Check.Equal(2, Count(s));
        Check.Equal((1 + 84, 36 + 84), (s.Dishes[0]!.Value.Activity, s.Dishes[0]!.Value.Quantity));
    }

    [Test]
    public void CleansingOintmentRemovesTheGroupToTheRight()
    {
        var s = Board(Sentry, Pest, Agito);
        Use(s, 4, 0);
        Check.Equal((21, 77), (s.Dishes[0]!.Value.Activity, s.Dishes[0]!.Value.Quantity));
        Check.That(s.Dishes[1] is null);
        Check.That(s.Dishes[2] is not null);
        var other = Board(Pest, Agito);
        Use(other, 4, 0);
        Check.Equal((21, 36), (other.Dishes[0]!.Value.Activity, other.Dishes[0]!.Value.Quantity));
        Check.Equal(2, Count(other));
    }

    [Test]
    public void DigestiveEnzymeRemovesTheLeftNeighbour()
    {
        var s = Board(Pest, Agito, Aemberon);
        Use(s, 15, 2);
        Check.Equal(62, s.Dishes[2]!.Value.Activity);
        Check.That(s.Dishes[1] is null);
        Check.That(s.Dishes[0] is not null);
    }

    [Test]
    public void SameRarityAndSameSpeciesLeechPotionsBoostTwoOthers()
    {
        var rarity = Board(Pest, Agito, Aemberon, Sentry with { Rarity = Rarity.Rare });
        Use(rarity, 14, 0);
        Check.Equal(2, rarity.Dishes.Count(d => d is { Activity: 32 }));
        var species = Board(Pest, Pest, Pest, Agito);
        Use(species, 19, 0);
        Check.Equal(2, species.Dishes.Count(d => d is { Activity: 32 }));
        Check.Equal(1, species.Dishes[3]!.Value.Activity);
    }

    [Test]
    public void TwinHormonesMutateAndDuplicate()
    {
        var pests = Board(Agito);
        Use(pests, 24, 0);
        Check.Equal(2, Count(pests));
        Check.That(pests.Dishes.All(d => d is null || d.Value.Species == Species.Pests));
        var sentries = Board(Agito, Pest);
        Use(sentries, 28, 0);
        var m = sentries.Dishes[0]!.Value;
        Check.Equal((Species.BoneSentry, 36 + 62), (m.Species, m.Quantity));
    }

    [Test]
    public void AddPotionsOnAFullBoardOnlyOverflow()
    {
        foreach (var id in new[] { 7, 23, 27, 30, 38 })
        {
            var s = Board(Pest, Pest, Pest, Pest, Pest, Pest);
            var before = s.Dishes.ToArray();
            Use(s, id);
            Check.That(s.Events.Any(e => e.Kind == EventKind.AddOverflow));
            if (id != 38) Check.That(s.Dishes.SequenceEqual(before));
        }
        var lure = Board(Pest, Pest, Pest, Pest, Pest, Pest);
        Use(lure, 38);
        Check.That(lure.Dishes.All(d => d!.Value.Activity == 41));
        Check.That(lure.Dishes.All(d => d!.Value.Quantity == 96));
    }

    [Test]
    public void AddPotionsAddTheirSpeciesWithBonuses()
    {
        var s = Board();
        Use(s, 23);
        Check.Equal(Species.Pests, s.Dishes[0]!.Value.Species);
        Check.Equal(true, s.Dishes[0]!.Value.Quantity >= 73);
        var t = Board();
        Use(t, 25);
        Check.Equal(Species.Agito, t.Dishes[0]!.Value.Species);
        Check.Equal(true, t.Dishes[0]!.Value.Activity >= 32);
        var u = Board();
        Use(u, 30);
        Check.Equal(Species.Aemberon, u.Dishes[0]!.Value.Species);
    }

    [Test]
    public void OneOrTwoTargetPotionsWorkWithOneTarget()
    {
        foreach (var id in new[] { 1, 13, 21, 31 })
        {
            var s = Board(Pest, Agito);
            Use(s, id, 1);
            Check.That(Count(s) >= 1);
        }
        var fuse = Board(Pest, Agito);
        Use(fuse, 1, 0, 1);
        Check.Equal(1, Count(fuse));
        Check.Equal((2, 72), (fuse.Dishes[0]!.Value.Activity, fuse.Dishes[0]!.Value.Quantity));
        Check.Equal(Species.Pests, fuse.Dishes[0]!.Value.Species);
        var magic = Board(Pest);
        Use(magic, 13, 0);
        Check.Equal((Rarity.Magic, 32), (magic.Dishes[0]!.Value.Rarity, magic.Dishes[0]!.Value.Activity));
        var rare = Board(Pest);
        Use(rare, 21, 0);
        Check.Equal((Rarity.Rare, 88), (rare.Dishes[0]!.Value.Rarity, rare.Dishes[0]!.Value.Quantity));
    }

    [Test]
    public void XenoHormoneRemovesTargetsThenAddsFourRandomWithFivePlus()
    {
        var s = Board(Pest, Agito);
        Use(s, 31, 0, 1);
        Check.Equal(4, Count(s));
        Check.That(s.Dishes.All(d => d is null || d.Value.Activity >= 6));
        var targeted = Board(Pest, Agito);
        Use(targeted, 32, 0);
        Check.Equal(3, Count(targeted));
        Check.Equal(2, targeted.Dishes.Count(d => d is { Rarity: Rarity.Magic, Quantity: 49 }));
    }

    [Test]
    public void SupremePotionsDoWhatTheyClaim()
    {
        var heart = Board(new Monster(Species.Pests, Rarity.Normal, 10, 10), new Monster(Species.Pests, Rarity.Normal, 2, 2));
        Use(heart, 36);
        Check.Equal((10, 10), (heart.Dishes[0]!.Value.Activity, heart.Dishes[0]!.Value.Quantity));
        Check.Equal((44, 44), (heart.Dishes[1]!.Value.Activity, heart.Dishes[1]!.Value.Quantity));
        var water = Board(Agito, Agito with { Rarity = Rarity.Magic }, Agito with { Rarity = Rarity.Boss }, Pest);
        Use(water, 39);
        Check.Equal((Rarity.Rare, 121), (water.Dishes[0]!.Value.Rarity, water.Dishes[0]!.Value.Activity));
        Check.Equal((Rarity.Boss, 121), (water.Dishes[1]!.Value.Rarity, water.Dishes[1]!.Value.Activity));
        Check.Equal((Rarity.Boss, 121), (water.Dishes[2]!.Value.Rarity, water.Dishes[2]!.Value.Activity));
        Check.Equal(1, water.Dishes[3]!.Value.Activity);
        var pill = Board(Pest, Agito with { Rarity = Rarity.Magic }, Sentry with { Rarity = Rarity.Rare });
        Use(pill, 35);
        Check.That(pill.Dishes.All(d => d is null || d.Value.Species == Species.Aemberon));
    }

    [Test]
    public void RandomSingleTargetPotionsHitOneGroup()
    {
        var qty = Board(Pest, Pest, Pest);
        Use(qty, 8);
        Check.Equal(2, qty.Dishes.Count(d => d is { Quantity: 61 }));
        Check.Equal(4, Count(qty));
        var brain = Board(Pest, Pest);
        Use(brain, 9);
        Check.Equal(1, brain.Dishes.Count(d => d is { Activity: 42 }));
        var four = Board(Pest, Agito with { Rarity = Rarity.Magic }, Sentry with { Rarity = Rarity.Rare }, Aemberon with { Rarity = Rarity.Boss }, Pest);
        Use(four, 16);
        Check.Equal(180 + 128, four.Dishes.Where(d => d is not null).Sum(d => d!.Value.Quantity));
    }

    [Test]
    public void MutationPotionsKeepTheirSpecies()
    {
        var shell = Board(Pest);
        Use(shell, 6, 0);
        var m = shell.Dishes[0]!.Value;
        Check.Equal((Species.Aemberon, 51), (m.Species, m.Activity));
        var sticky = Board(Sentry with { Rarity = Rarity.Rare }, Pest);
        Use(sticky, 22, 0);
        Check.Equal(32, sticky.Dishes[0]!.Value.Activity);
        Check.Equal((Species.BoneSentry, Rarity.Rare, 32, 36), (sticky.Dishes[1]!.Value.Species, sticky.Dishes[1]!.Value.Rarity, sticky.Dishes[1]!.Value.Activity, sticky.Dishes[1]!.Value.Quantity));
        var soft = Board(Agito with { Rarity = Rarity.Magic }, Agito with { Rarity = Rarity.Boss }, Pest);
        Use(soft, 2, 0);
        Use(soft, 2, 1);
        Use(soft, 2, 2);
        Check.Equal(61, soft.Dishes[0]!.Value.Activity);
        Check.Equal(91, soft.Dishes[1]!.Value.Activity);
        Check.Equal(31, soft.Dishes[2]!.Value.Activity);
    }

    [Test]
    public void FreshMarrowMutatesUpToThreeNonAemberonsAndBoostsAemberons()
    {
        var one = Board(Pest);
        Use(one, 5);
        Check.Equal((Species.Aemberon, 1, 36 + 42), (one.Dishes[0]!.Value.Species, one.Dishes[0]!.Value.Activity, one.Dishes[0]!.Value.Quantity));

        var many = Board(Pest, Pest, Pest, Aemberon);
        Use(many, 5);
        Check.That(many.Dishes.Where(d => d is not null).All(d => d!.Value.Species == Species.Aemberon));
        Check.Equal(4 * 36 + (2 + 3 + 3) * 42, many.Dishes.Sum(d => d?.Quantity ?? 0));

        var already = Board(Aemberon);
        Use(already, 5);
        Check.Equal((1, 36), (already.Dishes[0]!.Value.Activity, already.Dishes[0]!.Value.Quantity));
    }

    [Test]
    public void BoneGrowthAddsFortyAndASentryAlsoEatsANonSentry()
    {
        var pest = Board(Pest);
        Use(pest, 10);
        Check.Equal((1, 36 + 40), (pest.Dishes[0]!.Value.Activity, pest.Dishes[0]!.Value.Quantity));

        var sentry = Board(Sentry);
        Use(sentry, 10);
        Check.Equal(36 + 40 + 127, sentry.Dishes[0]!.Value.Quantity);

        var both = Board(Sentry, Pest);
        Use(both, 10);
        var sentryHit = both.Dishes[1] is null;
        Check.Equal(sentryHit ? 36 + 40 + 127 : 36, both.Dishes[0]!.Value.Quantity);
        Check.Equal(sentryHit ? 1 : 2, Count(both));
    }

    [Test]
    public void GrayMatterAddsFortyOneThenMutatesAndMaybeBecomesAemberonPlusThirty()
    {
        var s = Board(Pest);
        Use(s, 11);
        var m = s.Dishes[0]!.Value;
        Check.Equal(36, m.Quantity);
        Check.That(m.Activity == 42 || (m.Activity == 72 && m.Species == Species.Aemberon), $"activity {m.Activity} {m.Species}");
    }

    [Test]
    public void HollowSpinalAddsFortyOneToTheTargetAndMaybeMakesItAemberon()
    {
        var s = Board(Pest, Agito);
        Use(s, 12, 1);
        Check.Equal(1, s.Dishes[0]!.Value.Activity);
        Check.Equal(Species.Pests, s.Dishes[0]!.Value.Species);
        var m = s.Dishes[1]!.Value;
        Check.Equal((42, 36), (m.Activity, m.Quantity));
        Check.That(m.Species is Species.Agito or Species.Aemberon, $"{m.Species}");
    }

    [Test]
    public void EntrancingTinctureAwakensTheTargetOneRank()
    {
        var s = Board(Pest, Monster.Fresh(Species.Agito, Rarity.Magic), Monster.Fresh(Species.Aemberon, Rarity.Boss));
        Use(s, 20, 0);
        Use(s, 20, 1);
        Use(s, 20, 2);
        Check.Equal((Rarity.Magic, 61), (s.Dishes[0]!.Value.Rarity, s.Dishes[0]!.Value.Activity));
        Check.Equal((Rarity.Rare, 125), (s.Dishes[1]!.Value.Rarity, s.Dishes[1]!.Value.Activity));
        Check.Equal((Rarity.Boss, 420), (s.Dishes[2]!.Value.Rarity, s.Dishes[2]!.Value.Activity));
    }

    [Test]
    public void AnestheticTincturesMutateTheTarget()
    {
        var agito = Board(Pest, Aemberon);
        Use(agito, 26, 0);
        var m = agito.Dishes[0]!.Value;
        Check.Equal((Species.Agito, Rarity.Magic, 61, 36), (m.Species, m.Rarity, m.Activity, m.Quantity));
        Check.Equal(Species.Aemberon, agito.Dishes[1]!.Value.Species);

        var aemberon = Board(Pest, Agito, Sentry);
        Use(aemberon, 29, 0);
        Check.Equal(Species.Aemberon, aemberon.Dishes[0]!.Value.Species);
        Check.That(aemberon.Dishes[1]!.Value.Species is Species.Agito or Species.Aemberon);
        Check.That(aemberon.Dishes[2]!.Value.Species is Species.BoneSentry or Species.Aemberon);
        Check.That(aemberon.Dishes.Count(d => d is { Species: Species.Aemberon }) <= 2, "at most one extra group");
        Check.That(aemberon.Dishes.All(d => d is null || d.Value.Activity == 1));
    }

    [Test]
    public void RousingSaltsAddsOneElevenToBothStats()
    {
        var s = Board(Pest, Pest);
        Use(s, 41);
        Check.Equal(1, s.Dishes.Count(d => d is { Activity: 112, Quantity: 147 }));
        Check.That(Potions.Get(41).Targeting == Targeting.None);
    }

    [Test]
    public void TargetsOnEmptyDishesAreIgnored()
    {
        var s = Board(Pest);
        Use(s, 4, 3);
        Use(s, 17, 5);
        Use(s, 31, 2, 4);
        Check.Equal((1, 36), (s.Dishes[0]!.Value.Activity, s.Dishes[0]!.Value.Quantity));
        Check.Equal(1, Count(s));
    }

    [Test]
    public void PotionsOnAnEmptyBoardDoNothing()
    {
        foreach (var def in Potions.All.Where(p => p.SimId is not (7 or 23 or 25 or 27 or 30 or 38)))
        {
            var s = Board();
            Potions.Apply(s, def.SimId, [0, 1], new Rng(3));
            Check.Equal(0, Count(s));
        }
    }

    [Test]
    public void TableCoversAllIdsAndBoxesDoNothing()
    {
        Check.Equal(true, Enumerable.Range(0, 42).All(i => Potions.Get(i).SimId == i));
        Check.Equal(42, Potions.All.Count);
        foreach (var id in new[] { 33, 34, 40 })
        {
            Check.That(Potions.Get(id).Targeting == Targeting.None);
            var s = Board(Pest, Agito);
            var before = s.Dishes.ToArray();
            Use(s, id);
            Check.That(s.Dishes.SequenceEqual(before));
        }
        Check.That(Potions.Get(1).Targeting == Targeting.OneOrTwo);
        Check.That(Potions.Get(4).Targeting == Targeting.One);
        Check.That(Potions.Get(0).Targeting == Targeting.None);
    }

    static int Count(SimState s) => s.Dishes.Count(d => d is not null);

    static void Use(SimState s, int id, params int[] targets) => Potions.Apply(s, id, targets, new Rng(11));

    static SimState Board(params Monster[] monsters)
    {
        var s = new SimState();
        foreach (var m in monsters) s.Add(m);
        s.Events.Clear();
        return s;
    }
}
