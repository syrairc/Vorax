using System.Linq;
using EmberHUD;
using EmberHUD.Testing;
using Vorax.Sim;
using Rarity = Vorax.Sim.Rarity;

namespace Vorax;

public sealed class NotoriousTests
{
    const int Xenopurge = 207;

    static SimState Board()
    {
        var s = new SimState { Round = 3, Rerolls = 3 };
        s.Dishes[0] = new(Species.Pests, Rarity.Normal, 1000, 150);
        s.Dishes[1] = new(Species.Agito, Rarity.Magic, 5, 24);
        s.Dishes[2] = new(Species.BoneSentry, Rarity.Normal, 1, 36);
        return s;
    }

    static VoraxMonster Group(int slot, int species, int rarity, int count, int activity) => new(slot, slot + 1, species, rarity, count, activity);

    static VoraxState Hand(VoraxCard[] offered, VoraxMonster[] monsters, int rerolls = 3, int selected = -1, VoraxPhase phase = VoraxPhase.Round) =>
        new(phase, 3, 13, 100, 0, 0, 0, rerolls, 6, monsters, [.. offered.Select((c, i) => c with { Slot = i })], [], []) { SelectedCard = selected };

    static VoraxMonster[] Monsters() => [Group(0, 201, 1, 150, 1000), Group(1, 203, 2, 24, 5), Group(2, 202, 1, 36, 1)];

    [Test]
    public void ValueStaysInsideZeroAndTheCapAndRisesWithTheBoard()
    {
        var small = Board();
        var big = Board();
        big.Dishes[0] = new(Species.Pests, Rarity.Normal, 5000, 800);
        var vSmall = NotoriousModel.Value(small);
        var vBig = NotoriousModel.Value(big);
        Check.That(vSmall >= 0 && vSmall <= NotoriousModel.Cap && vBig >= 0 && vBig <= NotoriousModel.Cap);
        Check.That(vBig > vSmall, $"{vBig} vs {vSmall}");
    }

    [Test]
    public void ValueMatchesTheForkReferenceNumbers()
    {
        // numbers from running the fork's own value function on the same boards
        var early = Board();
        Check.That(System.Math.Abs(NotoriousModel.Value(early) - 848577.16) < 0.5, NotoriousModel.Value(early).ToString());

        var mid = Board();
        mid.Round = 5;
        mid.Dishes[0] = new(Species.Pests, Rarity.Normal, 20, 150);
        mid.Dishes[1] = new(Species.Pests, Rarity.Magic, 30, 90);
        mid.Dishes[2] = new(Species.Agito, Rarity.Rare, 40, 60);
        mid.Dishes[3] = new(Species.BoneSentry, Rarity.Normal, 10, 80);
        mid.Dishes[4] = new(Species.Aemberon, Rarity.Boss, 300, 5);
        mid.Instruments.AddRange([2, 19]);
        mid.Passed8000 = true;
        Check.That(System.Math.Abs(NotoriousModel.Value(mid) - 711766.79) < 0.5, NotoriousModel.Value(mid).ToString());

        var late = new SimState { Round = 11, Rerolls = 1, Passed8000 = true, Passed28000 = true };
        late.Dishes[0] = new(Species.Pests, Rarity.Normal, 200, 300);
        late.Dishes[1] = new(Species.Pests, Rarity.Magic, 300, 190);
        late.Dishes[2] = new(Species.Agito, Rarity.Rare, 140, 260);
        late.Dishes[3] = new(Species.BoneSentry, Rarity.Normal, 110, 180);
        late.Dishes[4] = new(Species.Aemberon, Rarity.Boss, 700, 15);
        late.Dishes[5] = new(Species.Pests, Rarity.Boss, 300, 3);
        late.Instruments.AddRange([6, 23, 13]);
        Check.That(System.Math.Abs(NotoriousModel.Value(late) - 300030.52) < 0.5, NotoriousModel.Value(late).ToString());

        var picking = new SimState { Round = 9, Rerolls = 0, Passed8000 = true, PendingInstrumentPicks = 1 };
        picking.Dishes[0] = new(Species.Pests, Rarity.Normal, 50, 150);
        picking.Instruments.Add(1);
        Check.Equal(0.0, NotoriousModel.Value(picking));
    }

    [Test]
    public void ValueOfAFinishedGameIsTheCappedScore()
    {
        var s = Board();
        s.Over = true;
        Check.Equal(150156.0, NotoriousModel.Value(s));
        s.Dishes[0] = new(Species.Pests, Rarity.Normal, 5000, 800);
        Check.Equal(NotoriousModel.Cap, NotoriousModel.Value(s));
    }

    [Test]
    public void TheXenopurgeOnTheBigGroupBeatsNoEffectCards()
    {
        var result = new Notorious(8).RankPotions(Board(), [17, 37, 36], false)!;
        Check.Equal(0, result.Best.Option!.Value.Card);
        Check.Equal(0, result.Best.Option!.Value.Targets[0]);
    }

    [Test]
    public void ARerollIsOnlyEverRankedWithRerollsLeft()
    {
        var none = Board();
        none.Rerolls = 0;
        Check.That(new Notorious(8).RankPotions(none, [37, 38, 39], true)!.Ranking.All(e => !e.IsReroll));
        Check.That(new Notorious(8).RankPotions(Board(), [37, 38, 39], true)!.Ranking.Any(e => e.IsReroll));
    }

    [Test]
    public void TheSearchIsDeterministicForTheSameBoard()
    {
        var a = new Notorious(8).RankPotions(Board(), [17, 21, 10])!;
        var b = new Notorious(8).RankPotions(Board(), [17, 21, 10])!;
        Check.Equal(a.Best.Goal, b.Best.Goal);
        Check.Equal(string.Join("|", a.Ranking.Select(e => e.Goal)), string.Join("|", b.Ranking.Select(e => e.Goal)));
    }

    [Test]
    public void InstrumentRankingCoversEveryOffer()
    {
        var result = new Notorious(8).RankInstruments(Board(), [18, 15, 23]);
        Check.Equal(3, result.Ranking.Count);
        Check.That(result.Ranking.All(e => e.Goal <= NotoriousModel.Cap));
        Check.That(result.Ranking.Select(e => e.Goal).SequenceEqual(result.Ranking.Select(e => e.Goal).OrderByDescending(g => g)));
    }

    [Test]
    public void OptionsListEveryTargetChoice()
    {
        var options = Notorious.Options(Board(), [21, 17, 37]);
        Check.Equal(3 + 3, options.Count(o => o.Card == 0));
        Check.Equal(3, options.Count(o => o.Card == 1));
        Check.Equal(1, options.Count(o => o.Card == 2));
    }

    [Test]
    public void ThePlayedKitOpensAHandInsteadOfSpendingTheRound()
    {
        var result = new Notorious(16).RankPotions(Board(), [33, 37, 36], false)!;
        var kit = result.Ranking.First(e => e.Option is { Card: 0 });
        var peat = result.Ranking.First(e => e.Option is { Card: 1 });
        Check.That(kit.Goal > peat.Goal * 1.5, $"kit {kit.Goal} peat {peat.Goal}");
        Check.That(kit.Quick > peat.Quick);
    }

    [Test]
    public void ItIsRegisteredUnderItsName()
    {
        Check.That(Registry.ByName("Notorious v1") is NotoriousStrategy);
    }

    [Test]
    public void TheStrategyAdvisesAllThreeStages()
    {
        var engine = new Engine(new SolveSettings(60, 1500, 0.05), new Rng(1));
        var strategy = Registry.ByName("Notorious v1")!;

        var potion = engine.Advise(Hand([new(0, Xenopurge, VoraxCardKind.Potion, 1), new(0, 401, VoraxCardKind.Potion, 1)], Monsters()), strategy, default)!;
        Check.That(potion.Error is null, potion.Error);
        Check.Equal(AdviceSource.Strategy, potion.Source);
        Check.That(potion.Pick.IsReroll || potion.Pick.Indexes is [0 or 1]);

        var target = engine.Advise(Hand([new(0, Xenopurge, VoraxCardKind.Potion, 1), new(0, 401, VoraxCardKind.Potion, 1)], Monsters(), selected: 0), strategy, default)!;
        Check.That(target.Error is null, target.Error);
        Check.Equal(Stage.Target, target.Stage);
        Check.Equal(AdviceSource.Strategy, target.Source);
        Check.Equal(1, target.Pick.Indexes.Length);

        var instrument = engine.Advise(Hand([new(0, 202, VoraxCardKind.Instrument, 0), new(0, 205, VoraxCardKind.Instrument, 0)], Monsters(), phase: VoraxPhase.Instrument), strategy, default)!;
        Check.That(instrument.Error is null, instrument.Error);
        Check.Equal(Stage.Instrument, instrument.Stage);
        Check.Equal(AdviceSource.Strategy, instrument.Source);
        Check.That(instrument.Pick.Indexes is [0 or 1]);
    }

    [Test]
    public void TliOverlayPlusIsRegisteredAndDealsKitsLessOftenThanTheFork()
    {
        Check.That(Registry.ByName("TLI Overlay Plus") is not null);
        int Kits(DrawProfile profile)
        {
            var rng = new Rng(7);
            var kits = 0;
            for (var i = 0; i < 4000; i++) kits += DrawModel.DrawHand(rng, 3, null, profile).Count(DrawModel.IsKit);
            return kits;
        }
        Check.That(Kits(DrawModel.OverlayPlus) * 2 < Kits(DrawModel.Standard));
    }
}
