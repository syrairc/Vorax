using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using EmberHUD;
using EmberHUD.Testing;
using Vorax.Sim;

namespace Vorax;

public sealed class WorkerTests
{
    static readonly TimeSpan Bound = TimeSpan.FromSeconds(20);

    static readonly SolveSettings Quick = new(60, 1500, 0.05);

    sealed class Probe(string name, ManualResetEventSlim? gate = null, ManualResetEventSlim? entered = null) : Strategy
    {
        public int Calls;

        public override string Name => name;

        public override Pick? PickPotion(StrategyContext c)
        {
            Interlocked.Increment(ref Calls);
            entered?.Set();
            gate?.Wait(Bound);
            return Pick.One(0, name);
        }
    }

    sealed class NameThrows : Strategy
    {
        public override string Name => throw new InvalidOperationException("name");
    }

    static Worker NewWorker() => new(new Engine(Quick, new Rng(1)));

    static VoraxState Hand(int round = 1, int activity = 100, int[]? instruments = null, VoraxPhase phase = VoraxPhase.Round) =>
        new(phase, round, 13, activity, 0, 0, 0, 3, 6, [new(0, 1, 203, 1, 36, 1)], [new(0, 207, VoraxCardKind.Potion, 1), new(1, 204, VoraxCardKind.Potion, 1)], [.. (instruments ?? []).Select(i => new VoraxCard(0, i, VoraxCardKind.Instrument, 0))], []);

    static void WaitFor(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            Check.That(watch.Elapsed < Bound, "timed out waiting");
            Thread.Sleep(1);
        }
    }

    [Test]
    public void LatestIsNullUntilTheFirstResult()
    {
        using var worker = NewWorker();
        Check.That(worker.Latest is null);
        var gate = new ManualResetEventSlim();
        var entered = new ManualResetEventSlim();
        worker.Submit(Hand(), new Probe("A", gate, entered), default);
        Check.That(entered.Wait(Bound));
        Check.That(worker.Latest is null);
        gate.Set();
        WaitFor(() => worker.Latest is not null);
    }

    [Test]
    public void SameDecisionKeyIsComputedOnce()
    {
        using var worker = NewWorker();
        var gate = new ManualResetEventSlim();
        var entered = new ManualResetEventSlim();
        var probe = new Probe("A", gate, entered);
        var state = Hand();
        worker.Submit(state, probe, default);
        Check.That(entered.Wait(Bound));
        worker.Submit(Hand(), probe, default);
        worker.Submit(Hand(activity: 5000), probe, default);
        gate.Set();
        WaitFor(() => worker.Latest is not null);
        worker.Submit(Hand(), probe, default);
        Check.That(worker.Latest is not null, "same key must not clear the result");
        Check.Equal(1, probe.Calls);
        Check.That(worker.Matches(state));
    }

    [Test]
    public void ANewStateDropsTheStaleResult()
    {
        using var worker = NewWorker();
        var gate = new ManualResetEventSlim();
        var entered = new ManualResetEventSlim();
        var stateA = Hand(round: 1);
        var stateB = Hand(round: 2);
        worker.Submit(stateA, new Probe("A", gate, entered), default);
        Check.That(entered.Wait(Bound));
        worker.Submit(stateB, new Probe("B"), default);
        gate.Set();
        WaitFor(() => worker.Latest is not null);
        Check.Equal("B", worker.Latest!.StrategyName);
        Check.That(worker.Matches(stateB));
        Check.That(!worker.Matches(stateA));
    }

    [Test]
    public void AStaleResultNeverMatchesTheCurrentState()
    {
        using var worker = NewWorker();
        worker.Submit(Hand(round: 1), new Probe("A"), default);
        WaitFor(() => worker.Latest is not null);
        var gate = new ManualResetEventSlim();
        var entered = new ManualResetEventSlim();
        worker.Submit(Hand(round: 2), new Probe("B", gate, entered), default);
        Check.That(worker.Latest is null);
        Check.That(!worker.Matches(Hand(round: 1)));
        Check.That(!worker.Matches(Hand(round: 2)));
        gate.Set();
    }

    [Test]
    public void AThrowingEvaluationDoesNotKillTheThread()
    {
        using var worker = NewWorker();
        worker.Submit(Hand(round: 1), new NameThrows(), default);
        WaitFor(() => worker.Latest is not null);
        Check.That(worker.Latest!.Error is not null);
        Check.Equal(0, worker.Latest.Pick.Indexes.Length);
        worker.Submit(Hand(round: 2), new Probe("B"), default);
        WaitFor(() => worker.Latest is { Error: null });
        Check.Equal("B", worker.Latest!.StrategyName);
    }

    [Test]
    public void DisposeStopsTheThread()
    {
        var worker = NewWorker();
        worker.Submit(Hand(), new Probe("A"), default);
        WaitFor(() => worker.Latest is not null);
        Check.That(worker.ThreadAlive);
        worker.Dispose();
        Check.That(!worker.ThreadAlive);
        var probe = new Probe("late");
        worker.Submit(Hand(round: 2), probe, default);
        Check.Equal(0, probe.Calls);
        worker.Dispose();
    }

    [Test]
    public void TrackerSetsThe8000FlagOnceActivityWasSeenAbove8000()
    {
        var tracker = new RunTracker();
        Check.That(!tracker.Update(Hand(round: 2, activity: 7999)).Passed8000);
        var options = tracker.Update(Hand(round: 3, activity: 8000));
        Check.That(options.Passed8000 && !options.Passed28000);
        options = tracker.Update(Hand(round: 4, activity: 28000));
        Check.That(options.Passed8000 && options.Passed28000);
    }

    [Test]
    public void TrackerKeepsTheFlagWhenActivityLaterDrops()
    {
        var tracker = new RunTracker();
        tracker.Update(Hand(round: 2, activity: 100));
        tracker.Update(Hand(round: 3, activity: 9000));
        var options = tracker.Update(Hand(round: 4, activity: 7000));
        Check.That(options.Passed8000);
    }

    [Test]
    public void TrackerResetsOnANewRun()
    {
        var tracker = new RunTracker();
        tracker.Update(Hand(round: 2, activity: 100, instruments: [216]));
        tracker.Update(Hand(round: 5, activity: 30000, instruments: [216]));
        var options = tracker.Update(Hand(round: 1, activity: 100));
        Check.That(!options.Passed8000 && !options.Passed28000);
        Check.That(options.AcquiredRounds is null or { Count: 0 });
    }

    [Test]
    public void AnActivityDipDoesNotStartANewRun()
    {
        var tracker = new RunTracker();
        tracker.Update(Hand(round: 6, activity: 9000, instruments: [101]));
        var options = tracker.Update(Hand(round: 7, activity: 50, instruments: [101]));
        Check.That(options.Passed8000);
        Check.Equal(6, options.AcquiredRounds![101]);
    }

    [Test]
    public void OwnedRelicsImplyThePassedThresholds()
    {
        var tracker = new RunTracker();
        var one = tracker.Update(Hand(round: 6, activity: 50, instruments: [101, 216]));
        Check.That(one.Passed8000 && !one.Passed28000);
        var two = new RunTracker().Update(Hand(round: 9, activity: 50, instruments: [216, 217]));
        Check.That(two.Passed8000 && two.Passed28000);
        Check.That(!new RunTracker().Update(Hand(round: 9, activity: 50, instruments: [101, 102])).Passed8000);
    }

    static VoraxState Pickable(int selected = -1, int[]? monsters = null) =>
        new(VoraxPhase.Round, 3, 13, 100, 0, 0, 0, 3, 6, [new(0, 1, 203, 1, 36, 1)], [new(0, 305, VoraxCardKind.Potion, 1), new(1, 302, VoraxCardKind.Potion, 1), new(2, 204, VoraxCardKind.Potion, 1)], [], [])
        { SelectedCard = selected, SelectedMonsters = monsters ?? [] };

    [Test]
    public void ClickingTheAdvisedNonTargetingCardDoesNotRecompute()
    {
        using var worker = NewWorker();
        var probe = new Probe("A");
        worker.Submit(Pickable(), probe, default);
        WaitFor(() => worker.Latest is not null);
        worker.Submit(Pickable(selected: 0), probe, default);
        Check.That(worker.Latest is not null);
        Check.That(worker.Matches(Pickable(selected: 0)));
        Check.Equal(1, probe.Calls);
    }

    [Test]
    public void SelectingATargetingCardMovesToTheTargetStageOnce()
    {
        using var worker = NewWorker();
        var probe = new Probe("A");
        worker.Submit(Pickable(), probe, default);
        WaitFor(() => worker.Latest is not null);
        Check.Equal(Stage.Potion, worker.Latest!.Stage);
        worker.Submit(Pickable(selected: 1), probe, default);
        WaitFor(() => worker.Latest is { Stage: Stage.Target });
        worker.Submit(Pickable(selected: 1), probe, default);
        Check.That(worker.Latest is { Stage: Stage.Target });
    }

    [Test]
    public void SelectingThePickedCylinderDoesNotRecompute()
    {
        using var worker = NewWorker();
        var probe = new Probe("A");
        worker.Submit(Pickable(selected: 1), probe, default);
        WaitFor(() => worker.Latest is not null);
        var advice = worker.Latest;
        worker.Submit(Pickable(selected: 1, monsters: [0]), probe, default);
        Check.That(ReferenceEquals(advice, worker.Latest));
        Check.That(worker.Matches(Pickable(selected: 1, monsters: [0])));
    }

    [Test]
    public void TrackerRecordsTheRoundEachInstrumentFirstAppeared()
    {
        var tracker = new RunTracker();
        tracker.Update(Hand(round: 2, instruments: [216]));
        tracker.Update(Hand(round: 3, instruments: [216]));
        var options = tracker.Update(Hand(round: 4, instruments: [216, 217]));
        Check.Equal(2, options.AcquiredRounds![216]);
        Check.Equal(4, options.AcquiredRounds[217]);
    }

    [Test]
    public void TrackerIgnoresStatesOutsideARun()
    {
        var tracker = new RunTracker();
        tracker.Update(Hand(round: 3, activity: 9000));
        var options = tracker.Update(Hand(round: 0, activity: 0, phase: VoraxPhase.Other));
        Check.That(options.Passed8000);
    }
}
