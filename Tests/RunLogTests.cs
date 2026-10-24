using System.Collections.Generic;
using System.Linq;
using EmberHUD;
using EmberHUD.Testing;
using Vorax.Sim;

namespace Vorax;

public sealed class RunLogTests
{
    const int Probiotic = 305;

    static VoraxMonster Group(int slot, int activity = 1) => new(slot, 0, 201, 1, 36, activity);

    static VoraxCard Potion(int slot, int id = Probiotic) => new(slot, id, VoraxCardKind.Potion, 1) { Name = "P" + id };

    static VoraxState Board(int round, int activity = 100, VoraxCard[]? offered = null, int selected = -1, int[]? targets = null, int rerolls = 3, VoraxCard[]? instruments = null, VoraxPhase phase = VoraxPhase.Round) =>
        new(phase, round, 13, activity, 0, 0, 2, rerolls, 6, [Group(0)], offered ?? [], instruments ?? [], []) { SelectedCard = selected, SelectedMonsters = targets ?? [] };

    static Advice Advise(Stage stage, Pick pick, double[]? values = null) => new(stage, AdviceSource.Strategy, "Notorious v1", pick, values, null, null);

    static (RunLog Log, List<string> Lines) Make()
    {
        var lines = new List<string>();
        return (new(lines.Add), lines);
    }

    static string Event(string line) => line.Split(' ')[1];

    [Test]
    public void ARunStartsOnTheFirstStateAndLogsItsConfig()
    {
        var (log, lines) = Make();
        log.Config = "rollouts=200";
        log.Observe(Board(1), null);
        Check.Equal(1, lines.Count);
        Check.That(Event(lines[0]) == "start" && lines[0].Contains("rollouts=200") && lines[0].StartsWith("run="), lines[0]);
    }

    [Test]
    public void ADecisionIsLoggedOnceWithTheOfferAndTheAdvice()
    {
        var (log, lines) = Make();
        var offered = new[] { Potion(0), Potion(1, 212) };
        var advice = Advise(Stage.Potion, Pick.One(1, "simulated 900"), [850.4, 900.2]);
        log.Observe(Board(2, offered: offered), null);
        log.Observe(Board(2, offered: offered), advice);
        log.Observe(Board(2, offered: offered), advice);
        var decisions = lines.Where(l => Event(l) == "decision").ToList();
        Check.Equal(1, decisions.Count);
        Check.That(decisions[0].Contains("\"stage\":\"Potion\"") && decisions[0].Contains("\"pick\":[1]") && decisions[0].Contains("\"values\":[850,900]") && decisions[0].Contains("\"id\":212") && decisions[0].Contains("Notorious v1"), decisions[0]);
    }

    [Test]
    public void AChoiceSaysWhetherTheAdviceWasFollowed()
    {
        string Choose(int slot)
        {
            var (log, lines) = Make();
            var offered = new[] { Potion(0), Potion(1, 212) };
            log.Observe(Board(2, offered: offered), Advise(Stage.Potion, Pick.One(1, "")));
            log.Observe(Board(2, offered: offered, selected: slot), null);
            return lines.Single(l => Event(l) == "choice");
        }

        Check.That(Choose(1).Contains("\"followed\":true"), Choose(1));
        Check.That(Choose(0).Contains("\"followed\":false"), Choose(0));
    }

    [Test]
    public void ARerollIsLogged()
    {
        var (log, lines) = Make();
        var offered = new[] { Potion(0) };
        log.Observe(Board(2, offered: offered), Advise(Stage.Potion, Pick.Reroll("reroll")));
        log.Observe(Board(2, offered: offered, rerolls: 2), null);
        var line = lines.Single(l => Event(l) == "reroll");
        Check.That(line.Contains("\"left\":2") && line.Contains("\"followed\":true"), line);
    }

    [Test]
    public void TargetsAreComparedWithTheAdvice()
    {
        var (log, lines) = Make();
        var offered = new[] { Potion(0) };
        log.Observe(Board(2, offered: offered, selected: 0), Advise(Stage.Target, new([2, 0], "")));
        log.Observe(Board(2, offered: offered, selected: 0, targets: [0, 2]), null);
        Check.That(lines.Single(l => Event(l) == "targets").Contains("\"followed\":true"));
    }

    [Test]
    public void AChoiceWithNoMatchingAdviceHasNoVerdict()
    {
        var (log, lines) = Make();
        var offered = new[] { Potion(0) };
        log.Observe(Board(2, offered: offered), null);
        log.Observe(Board(2, offered: offered, selected: 0), null);
        Check.That(lines.Single(l => Event(l) == "choice").Contains("\"followed\":null"));
    }

    [Test]
    public void AnInstrumentPickIsLogged()
    {
        var (log, lines) = Make();
        var offered = new[] { new VoraxCard(0, 101, VoraxCardKind.Instrument, 0), new VoraxCard(1, 102, VoraxCardKind.Instrument, 0) };
        log.Observe(Board(5, offered: offered, phase: VoraxPhase.Instrument), Advise(Stage.Instrument, Pick.One(1, "")));
        log.Observe(Board(5, instruments: [offered[1]]), null);
        var line = lines.Single(l => Event(l) == "instrument");
        Check.That(line.Contains("\"id\":102") && line.Contains("\"followed\":true"), line);
    }

    [Test]
    public void AnAdvancedRoundLogsTheScoreGained()
    {
        var (log, lines) = Make();
        log.Observe(Board(3, 100), null);
        log.Observe(Board(4, 160), null);
        var line = lines.Single(l => Event(l) == "round");
        Check.That(line.Contains("\"from\":3") && line.Contains("\"to\":4") && line.Contains("\"gained\":60"), line);
    }

    [Test]
    public void LeavingTheGameEndsTheRunAndTheNextOneGetsANewId()
    {
        var (log, lines) = Make();
        log.Observe(Board(13, 5000), null);
        log.Observe(null, null);
        log.Observe(null, null);
        log.Observe(Board(1), null);
        Check.Equal("start end start", string.Join(" ", lines.Select(Event)));
        Check.That(lines[1].Contains("\"complete\":true") && lines[1].Contains("\"activity\":5000"), lines[1]);
        Check.That(lines[0].Split(' ')[0] != lines[2].Split(' ')[0]);
    }

    [Test]
    public void ARoundBackToOneStartsANewRunWithoutLeaving()
    {
        var (log, lines) = Make();
        log.Observe(Board(7), null);
        log.Observe(Board(1), null);
        Check.Equal("start end start", string.Join(" ", lines.Select(Event)));
    }

    [Test]
    public void ScreensWithoutAdviceAreSnapshottedAndDoNotEndTheRun()
    {
        var (log, lines) = Make();
        VoraxState Final(int activity) => Board(10, activity, phase: VoraxPhase.Other) with { State = "S13FinalPlay" };
        log.Observe(Board(10, 100), null);
        log.Observe(Final(100), null);
        log.Observe(Final(100), null);
        log.Observe(Final(900), null);
        Check.Equal("start stage stage", string.Join(" ", lines.Select(Event)));
        Check.That(lines[1].Contains("S13FinalPlay") && lines[2].Contains("\"activity\":900"), lines[2]);
        log.Observe(null, null);
        Check.Equal("end", Event(lines[^1]));
    }
}
