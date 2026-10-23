using System;
using System.Collections.Generic;
using System.Linq;
using EmberHUD;
using EmberHUD.Testing;
using Vorax.Sim;

namespace Vorax;

public sealed class EngineTests
{
    static readonly SolveSettings Quick = new(60, 1500, 0.05);

    static readonly SolveSettings OnlyXenopurge = new(60, 1500, 0.05) { RealWeights = new Dictionary<int, int> { [17] = 1 } };

    const int Xenopurge = 207;
    const int Spawning = 211;
    const int SentryLimb = 108;
    const int MoltingSkin = 302;

    static Engine NewEngine(SolveSettings? settings = null) => new(settings ?? Quick, new Rng(1));

    static VoraxMonster Group(int slot, int species, int rarity, int count, int activity) => new(slot, slot + 1, species, rarity, count, activity);

    static VoraxCard Potion(int id) => new(0, id, VoraxCardKind.Potion, 1);

    static VoraxCard Kit(int id) => new(0, id, VoraxCardKind.Bundle, 0);

    static VoraxCard InstrumentCard(int id) => new(0, id, VoraxCardKind.Instrument, 0);

    static VoraxState Hand(VoraxCard[] offered, VoraxMonster[]? monsters = null, int rerolls = 3, int selected = -1, VoraxPhase phase = VoraxPhase.Round) =>
        new(phase, 1, 13, 100, 0, 0, 0, rerolls, 6, monsters ?? [], [.. offered.Select((c, i) => c with { Slot = i })], [], []) { SelectedCard = selected };

    static VoraxMonster[] PestGroups(params int[] activities) => [.. activities.Select((a, i) => Group(i, 201, 1, 36, a))];

    static VoraxMonster[] XenopurgeBoard() => [Group(0, 203, 1, 36, 1), Group(1, 204, 1, 36, 1), Group(2, 201, 3, 12, 15)];

    sealed class Scripted(Func<StrategyContext, Pick?>? instrument = null, Func<StrategyContext, Pick?>? potion = null, Func<StrategyContext, Pick?>? target = null, Func<StrategyContext, bool?>? reroll = null) : Strategy
    {
        public override string Name => "Scripted";

        public override Pick? PickInstrument(StrategyContext c) => instrument?.Invoke(c);

        public override Pick? PickPotion(StrategyContext c) => potion?.Invoke(c);

        public override Pick? PickTarget(StrategyContext c) => target?.Invoke(c);

        public override bool? ShouldReroll(StrategyContext c) => reroll?.Invoke(c);
    }

    public sealed class AlphaTestStrategy : Strategy
    {
        public override string Name => "Alpha test";
    }

    public sealed class ThrowingTestStrategy : Strategy
    {
        public ThrowingTestStrategy() => throw new InvalidOperationException("no");

        public override string Name => "Throwing test";
    }

    public sealed class BetaTestStrategy : Strategy
    {
        public override string Name => "Beta test";

        public override string Description => "second";
    }

    [Test]
    public void SimulatorAdviceNamesTheBestPotion()
    {
        var advice = NewEngine().Advise(Hand([Potion(Xenopurge), Potion(204), Potion(203)], XenopurgeBoard()), null, default)!;
        Check.Equal(Stage.Potion, advice.Stage);
        Check.That(advice.Error is null, advice.Error);
        Check.Equal(0, advice.Pick.Indexes[0]);
        Check.Equal(1, advice.Pick.Indexes.Length);
        Check.Equal(3, advice.Values!.Length);
        Check.That(!advice.Pick.IsReroll);
    }

    [Test]
    public void NoStrategyUsesTheSimulator()
    {
        var advice = NewEngine().Advise(Hand([Potion(Xenopurge), Potion(204)], XenopurgeBoard()), null, default)!;
        Check.Equal(AdviceSource.Simulator, advice.Source);
        Check.Equal("", advice.StrategyName);
    }

    [Test]
    public void StrategyPickWinsAndIsLabelledStrategy()
    {
        var strategy = new Scripted(potion: _ => Pick.One(1, "because"));
        var advice = NewEngine().Advise(Hand([Potion(Xenopurge), Potion(204)], XenopurgeBoard()), strategy, default)!;
        Check.Equal(AdviceSource.Strategy, advice.Source);
        Check.Equal("Scripted", advice.StrategyName);
        Check.Equal(1, advice.Pick.Indexes[0]);
        Check.Equal("because", advice.Pick.Reason);
        Check.That(advice.Error is null);
    }

    [Test]
    public void NullStrategyPickFallsBackToTheSimulator()
    {
        var advice = NewEngine().Advise(Hand([Potion(Xenopurge), Potion(204), Potion(203)], XenopurgeBoard()), new Scripted(), default)!;
        Check.Equal(AdviceSource.SimulatorFallback, advice.Source);
        Check.Equal("Scripted", advice.StrategyName);
        Check.Equal(0, advice.Pick.Indexes[0]);
        Check.That(advice.Error is not null);
    }

    [Test]
    public void AStrategyThatThrowsFallsBack()
    {
        var strategy = new Scripted(potion: _ => throw new InvalidOperationException("boom"));
        var advice = NewEngine().Advise(Hand([Potion(Xenopurge), Potion(204)], XenopurgeBoard()), strategy, default)!;
        Check.Equal(AdviceSource.SimulatorFallback, advice.Source);
        Check.That(advice.Error!.Contains("boom"), advice.Error);
        Check.Equal(1, advice.Pick.Indexes.Length);
    }

    [Test]
    public void OutOfRangeIndexFallsBack()
    {
        foreach (var index in new[] { 2, -1 })
        {
            var strategy = new Scripted(potion: _ => Pick.One(index, "bad"));
            var advice = NewEngine().Advise(Hand([Potion(Xenopurge), Potion(204)], XenopurgeBoard()), strategy, default)!;
            Check.Equal(AdviceSource.SimulatorFallback, advice.Source);
            Check.That(advice.Error is not null);
        }
    }

    [Test]
    public void TwoTargetsForAOneTargetPotionFallsBack()
    {
        var strategy = new Scripted(target: _ => new Pick([0, 1], "both"));
        var advice = NewEngine().Advise(Hand([Potion(Xenopurge)], PestGroups(2, 90, 4), selected: 0), strategy, default)!;
        Check.Equal(Stage.Target, advice.Stage);
        Check.Equal(AdviceSource.SimulatorFallback, advice.Source);
        Check.Equal(1, advice.Pick.Indexes.Length);
        Check.Equal(1, advice.Pick.Indexes[0]);
    }

    [Test]
    public void ATargetPickOnAnEmptySlotFallsBack()
    {
        var strategy = new Scripted(target: _ => Pick.One(5, "empty"));
        var advice = NewEngine().Advise(Hand([Potion(Xenopurge)], PestGroups(2, 90, 4), selected: 0), strategy, default)!;
        Check.Equal(AdviceSource.SimulatorFallback, advice.Source);
    }

    [Test]
    public void ATargetStrategyPickIsUsedAsTheMonsterSlot()
    {
        var strategy = new Scripted(target: _ => Pick.One(2, "third"));
        var advice = NewEngine().Advise(Hand([Potion(Xenopurge)], PestGroups(2, 90, 4), selected: 0), strategy, default)!;
        Check.Equal(AdviceSource.Strategy, advice.Source);
        Check.Equal(2, advice.Pick.Indexes[0]);
    }

    [Test]
    public void ATwoTargetPotionAcceptsOneOrTwoSlots()
    {
        var board = PestGroups(2, 9, 4);
        var two = NewEngine().Advise(Hand([Potion(MoltingSkin)], board, selected: 0), new Scripted(target: _ => new Pick([0, 2], "two")), default)!;
        Check.Equal(AdviceSource.Strategy, two.Source);
        var three = NewEngine().Advise(Hand([Potion(MoltingSkin)], board, selected: 0), new Scripted(target: _ => new Pick([0, 1, 2], "three")), default)!;
        Check.Equal(AdviceSource.SimulatorFallback, three.Source);
        var twin = NewEngine().Advise(Hand([Potion(MoltingSkin)], board, selected: 0), new Scripted(target: _ => new Pick([1, 1], "same")), default)!;
        Check.Equal(AdviceSource.SimulatorFallback, twin.Source);
    }

    [Test]
    public void RerollPickWithZeroRerollsFallsBack()
    {
        var offered = new[] { Potion(Xenopurge), Potion(204) };
        var byPick = NewEngine().Advise(Hand(offered, XenopurgeBoard(), rerolls: 0), new Scripted(potion: _ => Pick.Reroll("again")), default)!;
        Check.Equal(AdviceSource.SimulatorFallback, byPick.Source);
        Check.That(!byPick.Pick.IsReroll);
        var byFlag = NewEngine().Advise(Hand(offered, XenopurgeBoard(), rerolls: 0), new Scripted(reroll: _ => true), default)!;
        Check.Equal(AdviceSource.SimulatorFallback, byFlag.Source);
        Check.That(!byFlag.Pick.IsReroll);
    }

    [Test]
    public void AStrategyCanRerollWhenItHasRerolls()
    {
        var offered = new[] { Potion(Xenopurge), Potion(204) };
        var byPick = NewEngine().Advise(Hand(offered, XenopurgeBoard()), new Scripted(potion: _ => Pick.Reroll("again")), default)!;
        Check.Equal(AdviceSource.Strategy, byPick.Source);
        Check.That(byPick.Pick.IsReroll);
        var byFlag = NewEngine().Advise(Hand(offered, XenopurgeBoard()), new Scripted(reroll: _ => true), default)!;
        Check.Equal(AdviceSource.Strategy, byFlag.Source);
        Check.That(byFlag.Pick.IsReroll);
    }

    [Test]
    public void ShouldRerollFalseStillAsksForThePotion()
    {
        var strategy = new Scripted(potion: _ => Pick.One(1, "second"), reroll: _ => false);
        var advice = NewEngine().Advise(Hand([Potion(Xenopurge), Potion(204)], XenopurgeBoard()), strategy, default)!;
        Check.Equal(AdviceSource.Strategy, advice.Source);
        Check.Equal(1, advice.Pick.Indexes[0]);
    }

    [Test]
    public void SimulateInsideAStrategyReturnsTheSimulatorBest()
    {
        var offered = new[] { Potion(Xenopurge), Potion(204), Potion(203) };
        var strategy = new Scripted(potion: c => c.Simulate(null, false));
        var advice = NewEngine().Advise(Hand(offered, XenopurgeBoard()), strategy, default)!;
        Check.Equal(AdviceSource.Strategy, advice.Source);
        Check.Equal(0, advice.Pick.Indexes[0]);
    }

    [Test]
    public void SimulateWithCustomScoreChangesTheAnswer()
    {
        var offered = new[] { Potion(Xenopurge), Potion(Spawning) };
        var board = new[] { Group(0, 201, 1, 36, 1), Group(1, 203, 1, 36, 1) };
        double PestsOnly(SimState s) => s.Dishes.Count(d => d is { Species: Species.Pests });
        var plain = NewEngine().Advise(Hand(offered, board), new Scripted(potion: c => c.Simulate(null, false)), default)!;
        var custom = NewEngine().Advise(Hand(offered, board), new Scripted(potion: c => c.Simulate(PestsOnly, false)), default)!;
        Check.Equal(0, plain.Pick.Indexes[0]);
        Check.Equal(1, custom.Pick.Indexes[0]);
    }

    [Test]
    public void SimulateWithAllowRerollFalseNeverReturnsAReroll()
    {
        var offered = new[] { Potion(Xenopurge), Potion(204), Potion(203) };
        double FewerRerollsIsBetter(SimState s) => -s.Rerolls;
        var allowed = new Scripted(potion: c => c.Simulate(FewerRerollsIsBetter));
        var denied = new Scripted(potion: c => c.Simulate(FewerRerollsIsBetter, false));
        var withReroll = NewEngine().Advise(Hand(offered, XenopurgeBoard()), allowed, default)!;
        var withoutReroll = NewEngine().Advise(Hand(offered, XenopurgeBoard()), denied, default)!;
        Check.That(withReroll.Pick.IsReroll, "control: this score should make a reroll look best");
        Check.That(!withoutReroll.Pick.IsReroll);
        Check.Equal(1, withoutReroll.Pick.Indexes.Length);
    }

    [Test]
    public void SimulateAtTheTargetStageRanksTheMonsterSlots()
    {
        var strategy = new Scripted(target: c => c.Simulate());
        var advice = NewEngine().Advise(Hand([Potion(Xenopurge)], PestGroups(2, 90, 4), selected: 0), strategy, default)!;
        Check.Equal(AdviceSource.Strategy, advice.Source);
        Check.Equal(1, advice.Pick.Indexes[0]);
    }

    [Test]
    public void TheContextDescribesTheStage()
    {
        StrategyContext? seen = null;
        var strategy = new Scripted(target: c =>
        {
            seen = c;
            return null;
        });
        var state = Hand([Potion(MoltingSkin), Potion(Xenopurge)], PestGroups(2, 9, 4), selected: 0);
        NewEngine().Advise(state, strategy, default);
        Check.Equal(Stage.Target, seen!.Stage);
        Check.Equal(2, seen.MaxTargets);
        Check.Equal(MoltingSkin, seen.Chosen!.Value.Id);
        Check.Equal(2, seen.Choices.Length);
        Check.That(ReferenceEquals(state, seen.State));
    }

    [Test]
    public void UnknownPotionIdGivesAnErrorAdviceAndNoPick()
    {
        var advice = NewEngine().Advise(Hand([Potion(Xenopurge), Potion(999)], XenopurgeBoard()), new Scripted(potion: _ => Pick.One(0, "x")), default)!;
        Check.That(advice.Error!.Contains("999"), advice.Error);
        Check.Equal(0, advice.Pick.Indexes.Length);
        Check.That(!advice.Pick.IsReroll);
        Check.Equal(Stage.Potion, advice.Stage);
    }

    [Test]
    public void AnUnmappedMonsterGivesAnErrorAdvice()
    {
        var advice = NewEngine().Advise(Hand([Potion(Xenopurge)], [Group(0, 777, 1, 36, 1)]), null, default)!;
        Check.That(advice.Error!.Contains("777"), advice.Error);
        Check.Equal(0, advice.Pick.Indexes.Length);
    }

    [Test]
    public void AMonsterKitInTheHandGivesAnErrorAdviceAndNoPick()
    {
        var advice = NewEngine().Advise(Hand([Potion(Xenopurge), Kit(3)], XenopurgeBoard()), new Scripted(potion: _ => Pick.One(0, "x")), default)!;
        Check.That(advice.Error is not null);
        Check.Equal(0, advice.Pick.Indexes.Length);
    }

    [Test]
    public void APotionKitIsAValidPotionChoice()
    {
        var board = XenopurgeBoard();
        var advice = NewEngine(OnlyXenopurge).Advise(Hand([Potion(SentryLimb), Kit(6)], board, rerolls: 0), null, default)!;
        Check.That(advice.Error is null, advice.Error);
        Check.Equal(Stage.Potion, advice.Stage);
        Check.That(advice.Pick.Indexes[0] == 1, $"{advice.Values![0]:0} vs {advice.Values[1]:0}");
        Check.Equal(2, advice.Values!.Length);
    }

    [Test]
    public void AStrategyCanPickAKit()
    {
        var advice = NewEngine().Advise(Hand([Potion(Xenopurge), Kit(2)], XenopurgeBoard()), new Scripted(potion: _ => Pick.One(1, "kit")), default)!;
        Check.Equal(AdviceSource.Strategy, advice.Source);
        Check.Equal(1, advice.Pick.Indexes[0]);
    }

    [Test]
    public void StageIsTargetOnlyWhenASelectPotionIsSelected()
    {
        var engine = NewEngine();
        var offered = new[] { Potion(Spawning), Potion(Xenopurge), Kit(2) };
        var board = PestGroups(2, 9);
        Check.Equal(Stage.Potion, engine.DetectStage(Hand(offered, board)));
        Check.Equal(Stage.Potion, engine.DetectStage(Hand(offered, board, selected: 0)));
        Check.Equal(Stage.Target, engine.DetectStage(Hand(offered, board, selected: 1)));
        Check.Equal(Stage.Potion, engine.DetectStage(Hand(offered, board, selected: 2)));
        Check.Equal(Stage.Potion, engine.DetectStage(Hand(offered, board, selected: 7)));
    }

    [Test]
    public void StageIsNoneWithoutOfferedCards()
    {
        var engine = NewEngine();
        Check.Equal(Stage.None, engine.DetectStage(Hand([])));
        Check.Equal(Stage.None, engine.DetectStage(Hand([], phase: VoraxPhase.Instrument)));
        Check.Equal(Stage.None, engine.DetectStage(Hand([Potion(Xenopurge)], phase: VoraxPhase.Other)));
        Check.That(engine.Advise(Hand([]), null, default) is null);
    }

    [Test]
    public void InstrumentStageRanksTheOfferedInstruments()
    {
        var offered = new[] { InstrumentCard(103), InstrumentCard(105), InstrumentCard(107) };
        var state = Hand(offered, PestGroups(1, 1, 1), phase: VoraxPhase.Instrument);
        var simulated = NewEngine().Advise(state, null, default)!;
        Check.Equal(Stage.Instrument, simulated.Stage);
        Check.That(simulated.Error is null, simulated.Error);
        Check.That(simulated.Pick.Indexes[0] is >= 0 and < 3);
        Check.Equal(3, simulated.Values!.Length);
        var chosen = NewEngine().Advise(state, new Scripted(instrument: _ => Pick.One(2, "third")), default)!;
        Check.Equal(AdviceSource.Strategy, chosen.Source);
        Check.Equal(2, chosen.Pick.Indexes[0]);
        var reroll = NewEngine().Advise(state, new Scripted(instrument: _ => Pick.Reroll("no")), default)!;
        Check.Equal(AdviceSource.SimulatorFallback, reroll.Source);
    }

    [Test]
    public void RegistryFindsTheExampleStrategiesByName()
    {
        var found = Registry.Discover(true);
        Check.That(found.Any(s => s is AlphaTestStrategy));
        Check.That(found.Any(s => s is BetaTestStrategy { Description: "second" }));
        var names = found.Select(s => s.Name).ToList();
        Check.That(names.IndexOf("Alpha test") >= 0 && names.IndexOf("Alpha test") < names.IndexOf("Beta test"));
        Check.That(names.SequenceEqual(names.OrderBy(n => n, StringComparer.Ordinal)));
        Check.That(found.All(s => s.GetType() != typeof(Scripted)));
    }

    [Test]
    public void RegistryHidesNestedStrategiesAndSurvivesAThrowingOne()
    {
        Check.That(!Registry.All().Any(s => s is AlphaTestStrategy or BetaTestStrategy));
        Check.That(Registry.ByName("Alpha test") is null);
        Check.That(!Registry.Discover(true).Any(s => s is ThrowingTestStrategy));
    }

    [Test]
    public void SelectedCardIsMatchedBySlotNotArrayPosition()
    {
        var offered = new[] { new VoraxCard(5, Spawning, VoraxCardKind.Potion, 1), new VoraxCard(7, Xenopurge, VoraxCardKind.Potion, 1) };
        var state = new VoraxState(VoraxPhase.Round, 1, 13, 100, 0, 0, 0, 3, 6, PestGroups(2, 90), offered, [], []) { SelectedCard = 7 };
        var engine = NewEngine();
        Check.Equal(Stage.Target, engine.DetectStage(state));
        Check.Equal(Stage.Potion, engine.DetectStage(state with { SelectedCard = 1 }));
        var advice = engine.Advise(state, new Scripted(target: c => Pick.One(1, c.Chosen!.Value.Id.ToString())), default)!;
        Check.Equal("207", advice.Pick.Reason);
    }

    [Test]
    public void UnknownStrategyNameGivesNull()
    {
        Check.That(Registry.ByName("no such strategy") is null);
        Check.That(Registry.ByName(null) is null);
        Check.That(Registry.ByName("") is null);
    }
}
