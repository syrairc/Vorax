using System.Linq;
using EmberHUD.Testing;
using Vorax.Sim;

namespace Vorax;

public sealed class InstrumentTests
{
    static readonly Monster Pest = Monster.Fresh(Species.Pests, Rarity.Normal);
    static readonly Monster Sentry = Monster.Fresh(Species.BoneSentry, Rarity.Normal);
    static readonly Monster Agito = Monster.Fresh(Species.Agito, Rarity.Normal);

    [Test]
    public void KnownAndEventDrivenTables()
    {
        Check.That(Enumerable.Range(1, 25).All(Instruments.Known));
        Check.That(!Instruments.Known(0) && !Instruments.Known(26));
        Check.Equal("2,5,6,7,8,12,16,22", string.Join(',', Enumerable.Range(1, 25).Where(Instruments.EventDriven)));
    }

    [Test]
    public void ChrysalisAddsEightQuantityXTimesToXRandomGroups()
    {
        var s = Board(Pest, Pest);
        End(s, 1);
        Check.Equal(36 + 16, s.Dishes[0]!.Value.Quantity);
        Check.Equal(36 + 16, s.Dishes[1]!.Value.Quantity);
    }

    [Test]
    public void IncubationSacNeedsTwoPestsAndAddsFortyFiveOnEveryAdd()
    {
        var two = Board(Pest, Pest);
        two.Add(Pest);
        two.Add(Agito);
        End(two, 2);
        Check.Equal(36 + 90, two.Dishes[0]!.Value.Quantity);
        Check.Equal(36 + 90, two.Dishes[3]!.Value.Quantity);

        var one = Board(Pest, Agito);
        one.Add(Agito);
        End(one, 2);
        Check.Equal(36, one.Dishes[0]!.Value.Quantity);
    }

    [Test]
    public void ContractedClawsGiveOneFiftyOnNonSentryRemoves()
    {
        var removed = Board(Pest, Agito, Sentry);
        removed.Remove(1);
        End(removed, 6);
        Check.Equal(150, Quantity(removed) - (36 + 36));

        var sentryGone = Board(Pest, Agito, Sentry);
        sentryGone.Remove(2);
        End(sentryGone, 6);
        Check.Equal(0, Quantity(sentryGone) - (36 + 36));
    }

    [Test]
    public void ContractedClawsCountFuseAndDevourAsRemoves()
    {
        var fused = Board(Pest, Pest, Agito);
        fused.Fuse([0, 1], Species.Pests, Rarity.Magic);
        End(fused, 6);
        Check.Equal(300, Quantity(fused) - (36 * 3));

        var devoured = Board(Pest, Agito, Agito);
        devoured.Devour(0, 1);
        End(devoured, 6);
        Check.Equal(150, Quantity(devoured) - (36 * 3));
    }

    [Test]
    public void AdherentMetatarsalsNeedTwoSentries()
    {
        var two = Board(Sentry, Sentry, Pest);
        two.Remove(2);
        End(two, 5);
        Check.Equal(180, Quantity(two) - 72);

        var one = Board(Sentry, Pest, Agito);
        one.Remove(1);
        End(one, 5);
        Check.Equal(0, Quantity(one) - 72);
    }

    [Test]
    public void ChainedEventsOnlyReachEventDrivenInstruments()
    {
        var mutated = 0;
        for (ulong seed = 0; seed < 40; seed++)
        {
            var s = Board(Pest);
            s.Add(Pest);
            s.Dishes[0] = s.Dishes[0]!.Value with { Quantity = 276 };
            s.Instruments.AddRange([7, 12, 17]);
            Round.EndRound(s, new Rng(seed));
            var a = s.Dishes[1]!.Value;
            Check.That(a.Species == Species.Aemberon ? a.Activity == 1 + 80 + 35 : a.Activity == 1);
            Check.Equal(20 + 1 + (a.Species == Species.Aemberon ? 35 : 0), s.Dishes[0]!.Value.Activity);
            if (a.Species == Species.Aemberon) mutated++;
        }
        Check.That(mutated > 0 && mutated < 40);
    }

    [Test]
    public void EventDrivenCascadeTerminatesWithBoundedEvents()
    {
        var s = Board(Pest);
        s.Add(Pest);
        s.Instruments.AddRange([2, 12]);
        Round.EndRound(s, new Rng(3));
        var eventsAfter = s.Events.Count;
        Round.EndRound(s, new Rng(3));
        Check.That(s.Events.Count >= eventsAfter);
        Check.That(s.Events.Count < eventsAfter + 40);
    }

    [Test]
    public void SutureFusesEveryThreeSameRarityIntoTheNextAndIgnoresBoss()
    {
        var s = Board(Pest, Agito, Sentry, Monster.Fresh(Species.Pests, Rarity.Boss));
        End(s, 14);
        Check.Equal(2, Count(s));
        var fused = s.Dishes.First(d => d is { Rarity: Rarity.Magic })!.Value;
        Check.Equal((Species.Pests, 3, 108), (fused.Species, fused.Activity, fused.Quantity));

        var bosses = Board(Monster.Fresh(Species.Pests, Rarity.Boss), Monster.Fresh(Species.Agito, Rarity.Boss), Monster.Fresh(Species.Aemberon, Rarity.Boss));
        End(bosses, 14);
        Check.Equal(3, Count(bosses));
    }

    [Test]
    public void FouledScraperAddsTwentyActivityAboveTwoSeventyFiveQuantity()
    {
        var s = Board(new Monster(Species.Pests, Rarity.Normal, 1, 276), new Monster(Species.Pests, Rarity.Normal, 1, 275));
        End(s, 17);
        Check.Equal(21, s.Dishes[0]!.Value.Activity);
        Check.Equal(1, s.Dishes[1]!.Value.Activity);
    }

    [Test]
    public void CanineFileAddsThirtyQuantityAboveTwoFiftyFiveActivity()
    {
        var s = Board(new Monster(Species.Pests, Rarity.Normal, 256, 10), new Monster(Species.Pests, Rarity.Normal, 255, 10));
        End(s, 18);
        Check.Equal(40, s.Dishes[0]!.Value.Quantity);
        Check.Equal(10, s.Dishes[1]!.Value.Quantity);
    }

    [Test]
    public void PigIronSawNeedsBothStatsOverOneFifty()
    {
        var s = Board(new Monster(Species.Pests, Rarity.Normal, 151, 151), new Monster(Species.Pests, Rarity.Normal, 151, 150), new Monster(Species.Pests, Rarity.Normal, 150, 151));
        End(s, 15);
        Check.Equal((166, 166), (s.Dishes[0]!.Value.Activity, s.Dishes[0]!.Value.Quantity));
        Check.Equal((151, 150), (s.Dishes[1]!.Value.Activity, s.Dishes[1]!.Value.Quantity));
        Check.Equal((150, 151), (s.Dishes[2]!.Value.Activity, s.Dishes[2]!.Value.Quantity));
    }

    [Test]
    public void TannedBeltNeedsNormalMagicAndBoss()
    {
        var yes = Board(Pest, Monster.Fresh(Species.Pests, Rarity.Magic), Monster.Fresh(Species.Pests, Rarity.Boss));
        End(yes, 21);
        Check.Equal((21, 25, 320), (yes.Dishes[0]!.Value.Activity, yes.Dishes[1]!.Value.Activity, yes.Dishes[2]!.Value.Activity));

        var no = Board(Pest, Monster.Fresh(Species.Pests, Rarity.Magic), Monster.Fresh(Species.Pests, Rarity.Rare));
        End(no, 21);
        Check.Equal(1, no.Dishes[0]!.Value.Activity);
    }

    [Test]
    public void BeakAddsAtTwoGroupsAndDevoursAtSix()
    {
        var two = Board(Pest, Pest);
        End(two, 23);
        Check.Equal(3, Count(two));

        var one = Board(Pest);
        End(one, 23);
        Check.Equal(1, Count(one));

        var six = Board(
            new Monster(Species.Pests, Rarity.Normal, 1, 10),
            new Monster(Species.Pests, Rarity.Normal, 2, 10),
            new Monster(Species.Pests, Rarity.Normal, 3, 10),
            new Monster(Species.Pests, Rarity.Normal, 4, 10),
            new Monster(Species.Pests, Rarity.Normal, 5, 10),
            new Monster(Species.Pests, Rarity.Normal, 6, 10));
        End(six, 23);
        Check.Equal(5, Count(six));
        Check.Equal((7, 20), (six.Dishes[5]!.Value.Activity, six.Dishes[5]!.Value.Quantity));
    }

    [Test]
    public void LadyAwakensEveryTwoRoundsFromAcquisition()
    {
        var boss = Monster.Fresh(Species.Pests, Rarity.Boss);
        foreach (var (round, bonus) in new[] { (2, 0), (3, 120), (4, 0), (5, 120) })
        {
            var s = Board(boss);
            s.Round = round;
            s.AcquiredRound[19] = 1;
            End(s, 19);
            Check.Equal(boss.Activity + bonus, s.Dishes[0]!.Value.Activity);
        }
    }

    [Test]
    public void ButterflyFusesTwoPestsEveryThreeRounds()
    {
        foreach (var (round, fused) in new[] { (3, false), (4, true), (6, false), (7, true) })
        {
            var s = Board(Pest, Pest, Agito);
            s.Round = round;
            s.AcquiredRound[25] = 1;
            End(s, 25);
            Check.Equal(fused ? 2 : 3, Count(s));
            if (fused) Check.Equal((2, 72), (Pests(s).Activity, Pests(s).Quantity));
        }

        var single = Board(Pest, Agito);
        single.Round = 4;
        single.AcquiredRound[25] = 1;
        End(single, 25);
        Check.Equal(2, Count(single));
    }

    [Test]
    public void ThresholdCrossingSetsPendingInstrumentPicks()
    {
        var first = Board(new Monster(Species.Pests, Rarity.Normal, 28, 276));
        first.Round = 1;
        first.Instruments.Add(17);
        var afterFirst = Round.Play(first, -1, [], new Rng(1));
        Check.Equal((1, true, false, 2), (afterFirst.PendingInstrumentPicks, afterFirst.Passed8000, afterFirst.Passed28000, afterFirst.Round));

        var second = Board(new Monster(Species.Pests, Rarity.Normal, 82, 276));
        second.Round = 5;
        second.Passed8000 = true;
        second.Instruments.Add(17);
        var afterSecond = Round.Play(second, -1, [], new Rng(1));
        Check.Equal((1, true, true), (afterSecond.PendingInstrumentPicks, afterSecond.Passed8000, afterSecond.Passed28000));

        var both = Board(new Monster(Species.Pests, Rarity.Magic, 300, 1), Monster.Fresh(Species.Pests, Rarity.Magic), Monster.Fresh(Species.Pests, Rarity.Magic));
        both.Round = 1;
        both.Instruments.Add(13);
        var afterBoth = Round.Play(both, -1, [], new Rng(1));
        Check.Equal((2, true, true), (afterBoth.PendingInstrumentPicks, afterBoth.Passed8000, afterBoth.Passed28000));

        var again = Round.Play(afterBoth, -1, [], new Rng(1));
        Check.Equal(2, again.PendingInstrumentPicks);
    }

    [Test]
    public void ThresholdsTriggerAtExactlyEightAndTwentyEightThousand()
    {
        foreach (var (total, passed1, picks1) in new[] { (7999, false, 0), (8000, true, 1), (8001, true, 1) })
        {
            var s = Board(new Monster(Species.Pests, Rarity.Normal, 1, total));
            s.Round = 1;
            var next = Round.Play(s, -1, [], new Rng(1));
            Check.Equal((passed1, picks1, false), (next.Passed8000, next.PendingInstrumentPicks, next.Passed28000));
        }

        foreach (var (total, passed2, picks2) in new[] { (27999, false, 0), (28000, true, 1) })
        {
            var s = Board(new Monster(Species.Pests, Rarity.Normal, 1, total));
            s.Round = 5;
            s.Passed8000 = true;
            var next = Round.Play(s, -1, [], new Rng(1));
            Check.Equal((passed2, picks2), (next.Passed28000, next.PendingInstrumentPicks));
        }
    }

    [Test]
    public void PlayLeavesTheInputStateUntouched()
    {
        var s = Board(new Monster(Species.Pests, Rarity.Normal, 28, 276), Pest);
        s.Round = 1;
        s.Instruments.Add(17);
        s.Events.Add(new(EventKind.Add, 1, Species.Pests, Species.Pests));
        var dishes = s.Dishes.ToArray();
        var next = Round.Play(s, 0, [], new Rng(1));
        Check.That(next.PendingInstrumentPicks == 1 && next.Passed8000, "the play crossed the threshold");
        Check.That(s.Dishes.SequenceEqual(dishes), "dishes");
        Check.Equal(1, s.Events.Count);
        Check.Equal(0, s.PendingInstrumentPicks);
        Check.That(!s.Passed8000, "passed flag");
        Check.Equal(1, s.Round);
    }

    [Test]
    public void PlayAppliesPotionAdvancesRoundAndClearsEvents()
    {
        var s = Board(Pest, Pest);
        s.Round = 1;
        var next = Round.Play(s, 0, [], new Rng(1));
        Check.Equal(2, next.Round);
        Check.Equal(23, next.Dishes[0]!.Value.Activity);
        Check.Equal(0, next.Events.Count);
        Check.Equal(1, s.Round);

        s.Round = Round.Rounds;
        Check.Equal(Round.Rounds, Round.Play(s, -1, [], new Rng(1)).Round);
    }

    [Test]
    public void TotalActivityIsSumOfDishTotals()
    {
        var s = Board(Pest, Monster.Fresh(Species.Agito, Rarity.Boss));
        Check.Equal(36L + 300L, s.TotalActivity);
    }

    static void End(SimState s, int instrument)
    {
        s.Instruments.Add(instrument);
        Round.EndRound(s, new Rng(5));
    }

    static int Count(SimState s) => s.Dishes.Count(d => d is not null);

    static long Quantity(SimState s) => s.Dishes.Where(d => d is not null).Sum(d => (long)d!.Value.Quantity);

    static Monster Pests(SimState s) => s.Dishes.First(d => d is { Species: Species.Pests })!.Value;

    static SimState Board(params Monster[] monsters)
    {
        var s = new SimState();
        foreach (var m in monsters) s.Add(m);
        s.Events.Clear();
        return s;
    }
}
