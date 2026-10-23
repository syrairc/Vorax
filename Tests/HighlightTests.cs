using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;
using Vorax.Sim;

namespace Vorax;

public sealed class HighlightTests
{
    static readonly NamedRect[] Buttons = [new("RefreshBtn", new(1976, 1338), new(2122, 1391)), new("ComfirmBtn", new(1727, 1338), new(1960, 1391))];

    static VoraxCard Card(int slot, int id, VoraxCardKind kind, bool rect = true) =>
        new(slot, id, kind, 1) { Name = $"card{slot}", Min = rect ? new(100 + slot * 300, 400) : default, Max = rect ? new(350 + slot * 300, 900) : default };

    static VoraxState State(VoraxPhase phase, int selectedCard = -1, int[]? selectedMonsters = null, bool rects = true) =>
        new(phase, 2, 13, 500, 2000, 0, 2, 3, 6,
            [new VoraxMonster(1, 3, 201, 2, 24, 5) { Name = "Fly", Min = rects ? new(10, 10) : default, Max = rects ? new(60, 30) : default },
             new VoraxMonster(4, 3, 201, 2, 24, 5) { Name = "Bee", Min = rects ? new(10, 50) : default, Max = rects ? new(60, 70) : default }],
            [Card(0, 203, VoraxCardKind.Potion, rects), Card(1, 204, VoraxCardKind.Potion, rects), Card(2, 207, VoraxCardKind.Bundle, rects)], [], [])
        { SelectedCard = selectedCard, SelectedMonsters = selectedMonsters ?? [] };

    static Advice Advise(Stage stage, Pick pick, AdviceSource source = AdviceSource.Simulator) => new(stage, source, "", pick, null, null, null);

    [Test]
    public void InstrumentPickMarksTheCardThenConfirm()
    {
        var advice = Advise(Stage.Instrument, Pick.One(1, "x"));
        var before = Highlight.Marks(State(VoraxPhase.Instrument), advice, Buttons);
        Check.Equal(1, before.Count);
        Check.Equal(new Vector2(400, 400), before[0].Min);
        var after = Highlight.Marks(State(VoraxPhase.Instrument, selectedCard: 1), advice, Buttons);
        Check.Equal(2, after.Count);
        Check.Equal(new Vector2(1727, 1338), after[1].Min);
        Check.Equal(1, Highlight.Marks(State(VoraxPhase.Instrument, selectedCard: 0), advice, Buttons).Count);
    }

    [Test]
    public void AKitCardIsMarkedLikeAPotion()
    {
        var marks = Highlight.Marks(State(VoraxPhase.Round, selectedCard: 2), Advise(Stage.Potion, Pick.One(2, "kit")), Buttons);
        Check.Equal(2, marks.Count);
        Check.Equal(new Vector2(700, 400), marks[0].Min);
    }

    [Test]
    public void RerollAdviceMarksTheRefreshButton()
    {
        var marks = Highlight.Marks(State(VoraxPhase.Round), Advise(Stage.Potion, Pick.Reroll("r")), Buttons);
        Check.Equal(1, marks.Count);
        Check.Equal(new Vector2(1976, 1338), marks[0].Min);
    }

    [Test]
    public void TargetPickMarksTheLabelOfEveryPickedGroup()
    {
        var advice = Advise(Stage.Target, new([1, 4], "t"));
        var marks = Highlight.Marks(State(VoraxPhase.Round, selectedCard: 0), advice, Buttons);
        Check.Equal(2, marks.Count);
        Check.Equal(new Vector2(10, 10), marks[0].Min);
        Check.Equal(new Vector2(10, 50), marks[1].Min);
        Check.Equal(2, Highlight.Marks(State(VoraxPhase.Round, 0, [1]), advice, Buttons).Count);
        var confirmed = Highlight.Marks(State(VoraxPhase.Round, 0, [4, 1]), advice, Buttons);
        Check.Equal(3, confirmed.Count);
        Check.Equal(new Vector2(1727, 1338), confirmed[2].Min);
    }

    [Test]
    public void MissingRectsGiveNoMarkButTheTextStays()
    {
        Check.Equal(0, Highlight.Marks(State(VoraxPhase.Round, rects: false), Advise(Stage.Potion, Pick.One(0, "x")), []).Count);
        Check.Equal(0, Highlight.Marks(State(VoraxPhase.Round, 0, [1], rects: false), Advise(Stage.Target, Pick.One(1, "x")), []).Count);
        Check.Equal(0, Highlight.Marks(State(VoraxPhase.Round), Advise(Stage.Potion, Pick.Reroll("r")), []).Count);
        var rows = Main.AdviceRows(State(VoraxPhase.Round, rects: false), Advise(Stage.Potion, Pick.One(0, "simulated 5")));
        Check.That(rows.Any(r => r.Left.Contains("card0") && r.Right.Contains("simulated 5")), "pick row");
    }

    [Test]
    public void NoAdviceDrawsNothing()
    {
        Check.Equal(0, Highlight.Marks(State(VoraxPhase.Round), null, Buttons).Count);
        Check.Equal(0, Highlight.Marks(State(VoraxPhase.Round), Advise(Stage.None, new([], "")), Buttons).Count);
        Check.Equal(0, Highlight.Marks(State(VoraxPhase.Round), Advise(Stage.Potion, Pick.One(9, "x")), Buttons).Count);
    }

    [Test]
    public void StrategyAndSimulatorMarksUseDifferentColours()
    {
        var pick = Pick.One(0, "x");
        Check.That(Highlight.Marks(State(VoraxPhase.Round), Advise(Stage.Potion, pick, AdviceSource.Strategy), Buttons)[0].Strategy);
        Check.That(!Highlight.Marks(State(VoraxPhase.Round), Advise(Stage.Potion, pick), Buttons)[0].Strategy);
        var settings = new Settings();
        Check.That(settings.StrategyColour != settings.SimulatorColour, "default colours differ");
    }
}
