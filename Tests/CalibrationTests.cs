using System.Collections.Generic;
using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace Vorax;

public sealed class CalibrationTests
{
    const int PotentXenopurge = 207;
    const int Probiotic = 305;
    const int SmallKit = 2;

    static VoraxMonster Group(int slot, int species, int count = 36, int activity = 1) => new(slot, 0, species, 1, count, activity);

    static VoraxState Round(int round, VoraxMonster[] monsters, VoraxCard[]? offered = null, int selected = -1, int[]? targets = null, VoraxPhase phase = VoraxPhase.Round) =>
        new(phase, round, 13, 100, 0, 0, 0, 3, 6, monsters, offered ?? [], [], []) { SelectedCard = selected, SelectedMonsters = targets ?? [] };

    static VoraxCard Potion(int id) => new(0, id, VoraxCardKind.Potion, 1);

    static (Calibration Calibration, List<string> Lines) Make()
    {
        var lines = new List<string>();
        return (new(lines.Add), lines);
    }

    [Test]
    public void CalibrationMatchesTheXenopurgeCase()
    {
        var (calibration, lines) = Make();
        calibration.Observe(Round(3, [Group(0, 203), Group(1, 201), Group(2, 204)], [Potion(PotentXenopurge)], 0, [0]));
        calibration.Observe(Round(4, [Group(0, 203, 190)]));
        Check.Equal(1, lines.Count);
        Check.That(lines[0].Contains("round 3") && lines[0].Contains("Potent Xenopurge Powder match"), lines[0]);
    }

    [Test]
    public void CalibrationFlagsADifferingBoard()
    {
        string Play(int realActivity)
        {
            var (calibration, lines) = Make();
            calibration.Observe(Round(2, [Group(0, 201), Group(1, 201)], [Potion(Probiotic)], 0));
            calibration.Observe(Round(3, [Group(0, 201, 36, 23), Group(1, 201, 36, realActivity)]));
            return lines.Single();
        }

        var differs = Play(99);
        Check.That(differs.Contains("differs") && differs.Contains("slot 1") && differs.Contains("23x36") && differs.Contains("99x36"), differs);
        Check.That(Play(23).Contains("Probiotic Enzyme Solution match"));
    }

    [Test]
    public void CalibrationComparesARandomPotionByReachableOutcomes()
    {
        var (calibration, lines) = Make();
        calibration.Observe(Round(1, [Group(0, 201, 36, 1)], [Potion(212)], 0, [0]));
        calibration.Observe(Round(2, [Group(0, 201, 36, 42)]));
        Check.That(lines[0].Contains("match (random)"), lines[0]);

        calibration.Observe(Round(2, [Group(0, 201, 36, 42)], [Potion(212)], 0, [0]));
        calibration.Observe(Round(3, [Group(0, 201, 36, 7)]));
        Check.That(lines[1].Contains("random,") && !lines[1].Contains("match"), lines[1]);
    }

    [Test]
    public void CalibrationSkipsKits()
    {
        var (calibration, lines) = Make();
        var kit = new VoraxCard(0, SmallKit, VoraxCardKind.Bundle, 0);
        calibration.Observe(Round(3, [Group(0, 201)], [kit], 0));
        calibration.Observe(Round(3, [Group(0, 201)], [Potion(Probiotic)], 0));
        calibration.Observe(Round(4, [Group(0, 201, 36, 12)]));
        Check.Equal(1, lines.Count);
        Check.That(lines[0].Contains("skipped") && lines[0].Contains("kit"), lines[0]);
    }

    [Test]
    public void CalibrationSkipsInstrumentPicks()
    {
        var (calibration, lines) = Make();
        calibration.Observe(Round(5, [Group(0, 201)], [Potion(Probiotic)], 0));
        calibration.Observe(Round(6, [Group(0, 201, 36, 12)], phase: VoraxPhase.Instrument));
        calibration.Observe(Round(6, [Group(0, 201, 36, 12)]));
        Check.That(lines.Single().Contains("skipped") && lines[0].Contains("instrument"), lines[0]);
    }

    [Test]
    public void CalibrationIgnoresARoundThatDidNotAdvance()
    {
        var (calibration, lines) = Make();
        var board = new[] { Group(0, 201) };
        calibration.Observe(Round(3, board, [Potion(Probiotic)], 0));
        calibration.Observe(Round(3, board, [Potion(Probiotic)], 0));
        calibration.Observe(Round(3, board, phase: VoraxPhase.Other));
        Check.Equal(0, lines.Count);
    }

    [Test]
    public void CalibrationNotesARoundWithNoPickSeen()
    {
        var (calibration, lines) = Make();
        calibration.Observe(Round(3, [Group(0, 201)], [Potion(Probiotic)]));
        calibration.Observe(Round(4, [Group(0, 201)]));
        Check.That(lines.Single().Contains("no potion pick"), lines[0]);
    }
}
