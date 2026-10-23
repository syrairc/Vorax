using System.Linq;
using EmberHUD.Testing;
using Vorax.Sim;
using Rarity = Vorax.Sim.Rarity;

namespace Vorax;

// The engine rules that were brought in line with the Notorious fork: inline reactions, fitted weights, caps, handler fixes
public sealed class AlignmentTests
{
    static readonly Monster Pest = Monster.Fresh(Species.Pests, Rarity.Normal);

    static SimState Board(params Monster[] monsters)
    {
        var s = new SimState();
        foreach (var m in monsters) s.Add(m);
        s.Events.Clear();
        return s;
    }

    [Test]
    public void ReactiveInstrumentsAnswerTheMomentAnEventHappens()
    {
        var s = Board(Pest, Pest);
        s.Instruments.Add(2);
        s.Rng = new Rng(1);
        s.Add(Pest);
        Check.Equal(36 + 45, s.Dishes[0]!.Value.Quantity);
        Check.Equal(36 + 45, s.Dishes[2]!.Value.Quantity);
        s.Add(Pest);
        Check.Equal(36 + 90, s.Dishes[0]!.Value.Quantity);
        Check.Equal(36 + 90, s.Dishes[2]!.Value.Quantity);
        Check.Equal(36 + 45, s.Dishes[3]!.Value.Quantity);
    }

    [Test]
    public void RoundEndDoesNotReplayEventsThatAlreadyReacted()
    {
        var s = Board(Pest, Pest);
        s.Instruments.Add(2);
        s.Rng = new Rng(1);
        s.Add(Pest);
        Round.EndRound(s, new Rng(1));
        Check.Equal(36 + 45, s.Dishes[0]!.Value.Quantity);
    }

    [Test]
    public void LadyAwakensEachRaritySnapshotOnceSoAGroupNeverHopsTwice()
    {
        for (ulong seed = 0; seed < 20; seed++)
        {
            var s = Board(Pest);
            s.Round = 3;
            s.Instruments.Add(19);
            s.AcquiredRound[19] = 1;
            Round.EndRound(s, new Rng(seed));
            Check.Equal(Rarity.Magic, s.Dishes[0]!.Value.Rarity);
        }
    }

    [Test]
    public void SloughFusesIntoARandomRarityPest()
    {
        var rarities = Enumerable.Range(0, 60).Select(seed =>
        {
            var s = Board(Pest, Pest);
            Potions.Apply(s, 1, [0, 1], new Rng((ulong)seed));
            return s.Dishes.First(d => d is not null)!.Value.Rarity;
        }).ToHashSet();
        Check.That(rarities.Contains(Rarity.Normal) && rarities.Count >= 3, string.Join(",", rarities));
    }

    [Test]
    public void AwakenNeverLowersARarity()
    {
        var s = Board(Monster.Fresh(Species.Agito, Rarity.Boss));
        s.Awaken(0, Rarity.Rare);
        Check.Equal(Rarity.Boss, s.Dishes[0]!.Value.Rarity);
        Check.Equal(300 + 120, s.Dishes[0]!.Value.Activity);
    }

    [Test]
    public void ThresholdPicksStopAtThreeInstruments()
    {
        var s = Board(new Monster(Species.Pests, Rarity.Normal, 1, 8000));
        s.Round = 2;
        s.Instruments.AddRange([13, 15, 17]);
        var next = Round.Play(s, -1, [], new Rng(1));
        Check.Equal((true, 0), (next.Passed8000, next.PendingInstrumentPicks));
    }

    [Test]
    public void TheLastRoundEndsTheGame()
    {
        var s = Board(Pest);
        s.Round = Round.Rounds;
        var next = Round.Play(s, -1, [], new Rng(1));
        Check.That(next.Over && next.Round == Round.Rounds);
        Check.That(!s.Over);
    }

    [Test]
    public void SpawnRaritiesFollowTheForkWeights()
    {
        var rng = new Rng(3);
        var counts = new int[5];
        for (var i = 0; i < 40000; i++) counts[(int)SimState.RandomRarity(rng)]++;
        Check.That(System.Math.Abs(counts[1] / 40000.0 - 0.398) < 0.015, $"normal {counts[1]}");
        Check.That(System.Math.Abs(counts[4] / 40000.0 - 0.189) < 0.015, $"boss {counts[4]}");
    }

    [Test]
    public void HandsHoldDistinctCardsWithKitsAndRareSupremes()
    {
        var rng = new Rng(4);
        var supreme = 0;
        var kits = 0;
        var cards = 0;
        for (var i = 0; i < 6000; i++)
        {
            var hand = DrawModel.DrawHand(rng, 3);
            Check.Equal(3, hand.Distinct().Count());
            foreach (var id in hand)
            {
                cards++;
                if (id is 35 or 36 or 37 or 38 or 39 or 41) supreme++;
                if (DrawModel.IsKit(id)) kits++;
            }
        }
        Check.That(supreme > 0 && supreme < cards * 0.03, $"supreme {supreme}/{cards}");
        Check.That(kits > cards * 0.03, $"kits {kits}/{cards}");
    }

    [Test]
    public void AnOpenedKitDealsItsOwnCountWithoutKits()
    {
        var rng = new Rng(5);
        var kit = DrawModel.KitOf(33)!.Value;
        var hand = DrawModel.DrawHand(rng, kit.Amount, kit);
        Check.Equal(5, hand.Length);
        Check.That(hand.All(id => !DrawModel.IsKit(id)));
        Check.Equal(5, hand.Distinct().Count());
    }

    [Test]
    public void InstrumentOffersAreThreeDistinctFromTheRightPool()
    {
        var rng = new Rng(6);
        var basic = DrawModel.DrawInstruments(rng, []);
        Check.That(basic.Length == 3 && basic.Distinct().Count() == 3 && basic.All(id => id is >= 1 and <= 8));
        var advanced = DrawModel.DrawInstruments(rng, [6, 19]);
        Check.That(advanced.Length == 3 && advanced.All(id => id is >= 9 and <= 25 && id != 19));
    }
}
