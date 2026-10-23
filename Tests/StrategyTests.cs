using System.Collections.Generic;
using System.Linq;
using EmberHUD;
using EmberHUD.Testing;
using Vorax.Sim;

namespace Vorax;

public sealed class StrategyTests
{
    static readonly SolveSettings Quick = new(60, 1500, 0.05);

    const int Pests = 201;
    const int BoneSentry = 202;
    const int Aemberon = 204;

    static Engine NewEngine() => new(Quick, new Rng(1));

    static Advice Advise(VoraxState state, Strategy strategy) => NewEngine().Advise(state, strategy, default)!;

    static VoraxMonster Group(int slot, int species, int rarity, int count, int activity) => new(slot, slot + 1, species, rarity, count, activity);

    static VoraxCard Potion(int id) => new(0, id, VoraxCardKind.Potion, 1);

    static VoraxCard Kit(int id) => new(0, id, VoraxCardKind.Bundle, 0);

    static VoraxCard InstrumentCard(int id) => new(0, id, VoraxCardKind.Instrument, 0);

    static VoraxState Hand(VoraxCard[] offered, VoraxMonster[] monsters, int selected = -1, VoraxPhase phase = VoraxPhase.Round, int round = 1, params int[] owned) =>
        new(phase, round, 13, 100, 0, 0, 0, 3, 6, monsters, [.. offered.Select((c, i) => c with { Slot = i })], [.. owned.Select(InstrumentCard)], []) { SelectedCard = selected };

    static VoraxMonster[] PestGroups(params int[] activities) => [.. activities.Select((a, i) => Group(i, Pests, 1, 36, a))];

    static void IsStrategyPick(Advice advice, int expected)
    {
        Check.Equal(AdviceSource.Strategy, advice.Source);
        Check.Equal(expected, advice.Pick.Indexes[0]);
    }

    [Test]
    public void PestsTakesChrysalisSpecimenFirst()
    {
        var offered = new[] { InstrumentCard(205), InstrumentCard(211), InstrumentCard(103) };
        IsStrategyPick(Advise(Hand(offered, PestGroups(1, 1), phase: VoraxPhase.Instrument), new PestsStrategy()), 2);
    }

    [Test]
    public void PestsFallsBackToTheSimulatorForAnUnknownSituation()
    {
        var offered = new[] { InstrumentCard(101), InstrumentCard(104) };
        var advice = Advise(Hand(offered, PestGroups(1, 1), phase: VoraxPhase.Instrument), new PestsStrategy());
        Check.Equal(AdviceSource.SimulatorFallback, advice.Source);
        Check.That(advice.Error!.Contains("no rule"), advice.Error);
        Check.Equal(1, advice.Pick.Indexes.Length);
    }

    [Test]
    public void PestsPutsQuantityOnTheMostActiveGroup()
    {
        IsStrategyPick(Advise(Hand([Potion(207)], PestGroups(2, 90, 4), selected: 0), new PestsStrategy()), 1);
    }

    [Test]
    public void PestsPutsActivityOnTheGroupWithTheMostQuantity()
    {
        var board = new[] { Group(0, Pests, 1, 36, 5), Group(1, Pests, 2, 24, 5), Group(2, Aemberon, 3, 200, 5) };
        IsStrategyPick(Advise(Hand([Potion(301)], board, selected: 0), new PestsStrategy()), 2);
    }

    [Test]
    public void PestsAddPestGroupsEarlyAndQuantityLater()
    {
        var offered = new[] { Potion(207), Potion(102) };
        IsStrategyPick(Advise(Hand(offered, PestGroups(5, 5), round: 2), new PestsStrategy()), 1);
        IsStrategyPick(Advise(Hand(offered, PestGroups(5, 5), round: 9), new PestsStrategy()), 0);
    }

    [Test]
    public void PestsKeepTheLastGroupOfARarityWhenOwningTheRestraint()
    {
        var offered = new[] { Potion(208), Potion(205) };
        var board = new[] { Group(0, Pests, 1, 36, 5), Group(1, Pests, 1, 36, 5), Group(2, Pests, 2, 24, 5), Group(3, Pests, 4, 1, 300), Group(4, Pests, 4, 1, 300) };
        IsStrategyPick(Advise(Hand(offered, board, round: 9), new PestsStrategy()), 0);
        IsStrategyPick(Advise(Hand(offered, board, round: 9, owned: 202), new PestsStrategy()), 1);
    }

    [Test]
    public void PestsLeaveAKitToTheSimulator()
    {
        var advice = Advise(Hand([Kit(2)], PestGroups(5, 5), round: 9), new PestsStrategy());
        Check.Equal(AdviceSource.SimulatorFallback, advice.Source);
    }

    [Test]
    public void ClawsTakesContractedClawsFirst()
    {
        var offered = new[] { InstrumentCard(203), InstrumentCard(102), InstrumentCard(204) };
        IsStrategyPick(Advise(Hand(offered, PestGroups(1, 1), phase: VoraxPhase.Instrument), new ClawsStrategy()), 1);
        IsStrategyPick(Advise(Hand(offered[..1].Append(offered[2]).ToArray(), PestGroups(1, 1), phase: VoraxPhase.Instrument), new ClawsStrategy()), 1);
    }

    [Test]
    public void ClawsPrefersRemovalAndAwakensLate()
    {
        var offered = new[] { Potion(201), Potion(210), Potion(207) };
        var board = PestGroups(5, 5, 5);
        IsStrategyPick(Advise(Hand(offered, board, round: 3), new ClawsStrategy()), 2);
        IsStrategyPick(Advise(Hand(offered, board, round: 10), new ClawsStrategy()), 1);
    }

    [Test]
    public void ClawsNeverRemovesBoneSentriesWhenAnotherTargetExists()
    {
        var board = new[] { Group(0, BoneSentry, 1, 1, 1), Group(1, Pests, 1, 36, 1), Group(2, Aemberon, 1, 36, 5) };
        IsStrategyPick(Advise(Hand([Potion(106)], board, selected: 0), new ClawsStrategy()), 1);
    }

    [Test]
    public void ClawsKeepsTheBiggestGroupWhenRemoving()
    {
        var board = new[] { Group(0, Pests, 1, 200, 50), Group(1, Pests, 1, 36, 1), Group(2, Aemberon, 1, 36, 5) };
        IsStrategyPick(Advise(Hand([Potion(105)], board, selected: 0), new ClawsStrategy()), 1);
        IsStrategyPick(Advise(Hand([Potion(207)], board, selected: 0), new ClawsStrategy()), 0);
    }

    [Test]
    public void ClawsLeavesAnAllSentryBoardToTheSimulator()
    {
        var board = new[] { Group(0, BoneSentry, 1, 1, 1), Group(1, BoneSentry, 1, 36, 1) };
        var advice = Advise(Hand([Potion(106)], board, selected: 0), new ClawsStrategy());
        Check.Equal(AdviceSource.SimulatorFallback, advice.Source);
    }

    [Test]
    public void PestsSkipPestAddersWhenTheBoardIsFull()
    {
        var offered = new[] { Potion(102), Potion(207) };
        IsStrategyPick(Advise(Hand(offered, PestGroups(1, 2, 3, 4, 5, 6), round: 2), new PestsStrategy()), 1);
    }

    [Test]
    public void PestsUseProbioticEnzymeAsActivityNotAsAPestAdder()
    {
        var offered = new[] { Potion(305), Potion(207) };
        IsStrategyPick(Advise(Hand(offered, PestGroups(5, 5), round: 2), new PestsStrategy()), 1);
        IsStrategyPick(Advise(Hand([Potion(305)], PestGroups(5, 5), round: 2), new PestsStrategy()), 0);
    }

    [Test]
    public void ClawsDoNotTreatPiaMaterAsAnAwaken()
    {
        var offered = new[] { Potion(303), Potion(210) };
        IsStrategyPick(Advise(Hand(offered, PestGroups(5, 5, 5), round: 10), new ClawsStrategy()), 1);
        var advice = Advise(Hand([Potion(303)], PestGroups(5, 5, 5), round: 10), new ClawsStrategy());
        Check.Equal(AdviceSource.SimulatorFallback, advice.Source);
    }

    [Test]
    public void ClawsNeverRemoveTheBiggestGroupOrTheOnlyOne()
    {
        var board = new[] { Group(0, BoneSentry, 1, 1, 1), Group(1, BoneSentry, 1, 1, 2), Group(2, Pests, 1, 200, 50) };
        var target = Advise(Hand([Potion(106)], board, selected: 0), new ClawsStrategy());
        Check.Equal(AdviceSource.SimulatorFallback, target.Source);
        var potion = Advise(Hand([Potion(106), Potion(201)], board), new ClawsStrategy());
        Check.Equal(AdviceSource.SimulatorFallback, potion.Source);
        var lone = Advise(Hand([Potion(105)], [Group(0, Pests, 1, 36, 1)], selected: 0), new ClawsStrategy());
        Check.Equal(AdviceSource.SimulatorFallback, lone.Source);
    }

    [Test]
    public void ClawsFuseOnlyLateAndCleanseOnlyWithABoneSentry()
    {
        var pests = PestGroups(5, 5, 5);
        var fuse = new[] { Potion(302), Potion(106) };
        IsStrategyPick(Advise(Hand(fuse, pests, round: 2), new ClawsStrategy()), 1);
        IsStrategyPick(Advise(Hand(fuse, pests, round: 10), new ClawsStrategy()), 0);
        var cleanse = new[] { Potion(307), Potion(106) };
        IsStrategyPick(Advise(Hand(cleanse, pests, round: 2), new ClawsStrategy()), 1);
        var withSentry = new[] { Group(0, BoneSentry, 1, 36, 1), Group(1, Pests, 1, 36, 1), Group(2, Pests, 1, 36, 9) };
        IsStrategyPick(Advise(Hand(cleanse, withSentry, round: 2), new ClawsStrategy()), 0);
    }

    [Test]
    public void BothStrategiesHaveNamesAndDescriptions()
    {
        foreach (var (strategy, name) in new (Strategy, string)[] { (new PestsStrategy(), "Pests"), (new ClawsStrategy(), "Claws") })
        {
            Check.Equal(name, strategy.Name);
            Check.That(strategy.Description.Length > 0, name);
            Check.That(Registry.ByName(name) is not null, name);
        }
    }

    [Test]
    public void BothStrategiesReturnValidPicksOnTheBundledSampleStates()
    {
        var mixed = new[] { Group(0, Pests, 1, 36, 5), Group(1, BoneSentry, 2, 24, 9), Group(2, 203, 3, 12, 40), Group(3, Aemberon, 4, 1, 300) };
        var states = new List<VoraxState>
        {
            Hand([InstrumentCard(103), InstrumentCard(102), InstrumentCard(204)], mixed, phase: VoraxPhase.Instrument),
            Hand([InstrumentCard(101), InstrumentCard(108)], mixed, phase: VoraxPhase.Instrument),
            Hand([Potion(102), Potion(207), Potion(210)], mixed),
            Hand([Potion(301), Potion(307), Kit(2)], mixed, round: 11, owned: 202),
            Hand([Kit(2), Kit(6)], mixed),
        };
        foreach (var potion in new[] { 105, 106, 205, 207, 208, 210, 213, 214, 301, 302, 303 })
            states.Add(Hand([Potion(potion)], mixed, selected: 0, round: 6));
        foreach (var strategy in new Strategy[] { new PestsStrategy(), new ClawsStrategy() })
        foreach (var state in states)
        {
            var advice = Advise(state, strategy);
            var label = $"{strategy.Name} {advice.Stage}";
            Check.That(advice.Error is null || advice.Error.Contains("no rule"), $"{label}: {advice.Error}");
            Check.That(advice.Pick.IsReroll || advice.Pick.Indexes.Length > 0, label);
            var limit = advice.Stage == Stage.Target ? 6 : state.Offered.Length;
            Check.That(advice.Pick.Indexes.All(i => i >= 0 && i < limit), label);
            if (advice.Stage == Stage.Target) Check.That(advice.Pick.Indexes.All(i => mixed.Any(m => m.Slot == i)), label);
        }
    }
}
