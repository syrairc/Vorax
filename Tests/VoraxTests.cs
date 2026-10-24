using System.Linq;
using EmberHUD;
using EmberHUD.Testing;
using EmberHUD.Ui;
using Vorax.Sim;

namespace Vorax;

public sealed class VoraxTests
{
    [Test]
    public void TickAsksForVorax()
    {
        TestGame.Set(GameState.Empty);
        Check.That(TestGame.VoraxAskedDuring(new Main().Tick));
    }

    [Test]
    public void TheSettingsPageDraws()
    {
        Check.That(TestFrame.Vertices(new Settings().Draw, inWindow: true) > 0, "settings page draws");
    }

    static VoraxState Sample() => new(VoraxPhase.Round, 2, 13, 761, 2000, 0, 2, 3, 6,
        [new VoraxMonster(0, 14, 202, 4, 1, 300) { Name = "Bone Warden" }, new VoraxMonster(1, 3, 201, 2, 24, 5) { Name = "Putrid Fly", RarityName = "Magic" }],
        [new VoraxCard(0, 203, VoraxCardKind.Potion, 2) { Name = "Eggshell Powder", Description = "+25 Quantity to 1 random group(s) of monster. If they are Pests, add 1 group(s) of Pests with the same name and +25 to their Quantity", TypeName = "Potion" }],
        [new VoraxCard(0, 101, VoraxCardKind.Instrument, 0) { Name = "Aberrant Sprout", TypeName = "Surgical Instrument" }], [0, 0, 0, 0, 0, 0])
    {
        Protocols = [new VoraxCard(0, 3, VoraxCardKind.Modifier, 1) { Name = "Spinal Cord Stimulation", TypeName = "Surgical Protocol" }],
        RewardBonusPercent = 20,
    };

    static int Frame(VoraxState? state, Settings? settings = null, Advice? advice = null)
    {
        TestGame.Set(new GameState { Vorax = state });
        try
        {
            var plugin = new Main { AdviceOverride = advice };
            if (settings is not null) typeof(Plugin<Settings>).GetProperty("Settings")!.GetSetMethod(true)!.Invoke(plugin, [settings]);
            return TestFrame.Vertices(plugin.Render);
        }
        finally { TestGame.Set(GameState.Empty); }
    }

    [Test]
    public void RenderDrawsNothingWithoutState() => Check.Equal(0, Frame(null));

    [Test]
    public void RenderDrawsThePanel() => Check.That(Frame(Sample()) > 0, "panel drew");

    [Test]
    public void RenderHiddenWhenPanelOff() => Check.Equal(0, Frame(Sample(), new Settings { ShowPanel = false, ShowBar = false }));

    [Test]
    public void RowsListEveryValue()
    {
        var rows = Main.Rows(Sample(), 560, TextStyle.Hud());
        Check.That(rows.Any(r => r.Left == "Round" && r.Right == "2 / 13"), "round");
        Check.That(rows.Any(r => r.Left == "Activity" && r.Right == "761 / next 2000"), "activity");
        Check.That(rows.Any(r => r.Left.Contains("Putrid Fly") && r.Right == "24 x 5 = 120"), "monster total");
        Check.That(rows.Any(r => r.Left.Contains("Eggshell Powder") && r.Right == "Potion / Potion"), "offered card");
        Check.That(rows.Count(r => r.Left.StartsWith("      ")) >= 2, "description wraps");
        Check.That(rows.Any(r => r.Left.Contains("Aberrant Sprout")), "instrument");
        Check.That(rows.Any(r => r.Left.Contains("Spinal Cord Stimulation") && r.Right == "Modifier / Surgical Protocol"), "protocol");
        Check.That(rows.Any(r => r.Left == "Reward bonus" && r.Right == "+20%"), "reward bonus");
    }

    static Advice Potion(AdviceSource source = AdviceSource.Simulator, string error = "") => new(Stage.Potion, source, "Pests", Pick.One(0, "simulated 12"), [12, 8], 6, error == "" ? null : error);

    static VoraxState WithRects() => Sample() with { Offered = [Sample().Offered[0] with { Min = new(100, 400), Max = new(300, 900) }] };

    [Test]
    public void RenderDrawsHighlightsForAnAdvice()
    {
        var advice = Potion();
        Check.That(Frame(WithRects(), new Settings { ShowPanel = false }, advice) > 0, "marks drew without the panel");
        Check.Equal(0, Frame(WithRects(), new Settings { ShowPanel = false, ShowHighlights = false, ShowBar = false }, advice));
        Check.That(Frame(WithRects(), new Settings(), advice) > Frame(WithRects(), new Settings { ShowHighlights = false }, advice), "marks add vertices");
    }

    [Test]
    public void ModeSettingPicksTheStrategyByName()
    {
        var first = Registry.All()[0];
        var main = new Main();
        Check.That(ReferenceEquals(first, main.StrategyFor(first.Name)), "same instance by name");
        Check.That(ReferenceEquals(main.StrategyFor(first.Name), main.StrategyFor(first.Name)), "stable");
    }

    [Test]
    public void UnknownModeUsesTheSimulator()
    {
        var main = new Main();
        Check.That(main.StrategyFor("Simulator") is null, "Simulator");
        Check.That(main.StrategyFor("No such strategy") is null, "unknown");
        Check.That(main.StrategyFor("") is null, "empty");
    }

    [Test]
    public void SourceLabelsNameTheStrategyOrTheFallback()
    {
        Check.Equal("Simulator", Main.SourceLabel(Potion()));
        Check.Equal("Strategy: Pests", Main.SourceLabel(Potion(AdviceSource.Strategy)));
        Check.Equal("Simulator (fallback: no rule)", Main.SourceLabel(Potion(AdviceSource.SimulatorFallback, "no rule")));
    }

    [Test]
    public void AdviceRowsShowPickValuesRerollAndError()
    {
        var rows = Main.AdviceRows(Sample(), Potion(error: "bad read"));
        Check.That(rows.Any(r => r.Left == "Stage" && r.Right == "Potion"), "stage");
        Check.That(rows.Any(r => r.Left == "Source" && r.Right == "Simulator"), "source");
        Check.That(rows.Any(r => r.Left.Contains("Eggshell Powder") && r.Right == "simulated 12"), "pick");
        Check.That(rows.Any(r => r.Left.Contains("Eggshell Powder") && r.Right == "12"), "value");
        Check.That(rows.Any(r => r.Left == "Reroll" && r.Right == "6"), "reroll value");
        Check.That(rows.Any(r => r.Left == "Error" && r.Right == "bad read"), "error");
    }

    [Test]
    public void NoAdviceYetSaysThinking()
    {
        var rows = Main.AdviceRows(Sample(), null);
        Check.That(rows.Any(r => r.Right == "thinking..."), "thinking");
        Check.Equal(0, Main.AdviceRows(Sample() with { Offered = [] }, null).Count);
    }

    [Test]
    public void DebugRowsStayBehindTheDebugSetting()
    {
        var plain = new Main().PanelRows(Sample(), Potion());
        var withDebug = new Main();
        typeof(Plugin<Settings>).GetProperty("Settings")!.GetSetMethod(true)!.Invoke(withDebug, [new Settings { Debug = true }]);
        var debug = withDebug.PanelRows(Sample(), Potion());
        Check.That(debug.Count > plain.Count && debug.Any(r => r.Left == "Phase"), "debug adds rows");
    }

    [Test]
    public void RolloutsClampToTheNoiseFloor()
    {
        Check.Equal(200, new Settings { Rollouts = 50 }.ClampedRollouts);
        Check.Equal(400, new Settings { Rollouts = 400 }.ClampedRollouts);
    }

    static Main WithSettings(Settings settings)
    {
        var main = new Main();
        typeof(Plugin<Settings>).GetProperty("Settings")!.GetSetMethod(true)!.Invoke(main, [settings]);
        return main;
    }

    [Test]
    public void TickCreatesTheWorkerAndKeepsItAcrossModeChanges()
    {
        TestGame.Set(new GameState { Vorax = Sample() });
        try
        {
            var settings = new Settings();
            var main = WithSettings(settings);
            main.Tick();
            var worker = main.CurrentWorker;
            Check.That(worker is not null, "worker created");
            settings.Mode = Registry.All()[0].Name;
            main.Tick();
            Check.That(ReferenceEquals(worker, main.CurrentWorker), "mode change keeps the worker");
            main.Dispose();
        }
        finally { TestGame.Set(GameState.Empty); }
    }

    [Test]
    public void TickRebuildsTheWorkerOnARolloutsChange()
    {
        TestGame.Set(new GameState { Vorax = Sample() });
        try
        {
            var settings = new Settings();
            var main = WithSettings(settings);
            main.Tick();
            var first = main.CurrentWorker;
            settings.Rollouts = 400;
            main.Tick();
            Check.That(main.CurrentWorker is not null && !ReferenceEquals(first, main.CurrentWorker), "new worker");
            main.Dispose();
        }
        finally { TestGame.Set(GameState.Empty); }
    }

    [Test]
    public void DisposeStopsTheWorker()
    {
        TestGame.Set(new GameState { Vorax = Sample() });
        try
        {
            var main = WithSettings(new Settings());
            main.Tick();
            var worker = main.CurrentWorker!;
            Check.That(worker.ThreadAlive, "thread running");
            main.Dispose();
            Check.That(!worker.ThreadAlive, "thread stopped");
        }
        finally { TestGame.Set(GameState.Empty); }
    }

    [Test]
    public void BudgetAndMarginClamp()
    {
        Check.Equal(100, new Settings { BudgetMilliseconds = -5 }.ClampedBudget);
        Check.Equal(0, new Settings { RerollMargin = -3 }.ClampedMargin);
        Check.Equal(100, new Settings { RerollMargin = 900 }.ClampedMargin);
    }

    [Test]
    public void TheBarShowsThePickAndTheEstimate()
    {
        var state = Sample();
        var advice = new Advice(Stage.Potion, AdviceSource.Strategy, "X", Pick.One(0, ""), null, null, null, 773771);
        Check.Equal("Eggshell Powder", Main.PickText(state, advice));
        Check.Equal("Est. 773,771", Main.BarScore(advice));
        Check.Equal("thinking...", Main.PickText(state, null));
        Check.Equal("Est. -", Main.BarScore(null));
        Check.Equal("Reroll", Main.PickText(state, advice with { Pick = Pick.Reroll("") }));
    }

    [Test]
    public void TheBarDrawsOnlyWhenACardIsOffered()
    {
        var advice = new Advice(Stage.Potion, AdviceSource.Strategy, "X", Pick.One(0, ""), null, null, null, 773771);
        var settings = new Settings { ShowPanel = false, ShowHighlights = false };
        Check.That(Frame(Sample(), settings, advice) > 0, "bar drew");
        Check.Equal(0, Frame(Sample(), new Settings { ShowPanel = false, ShowHighlights = false, ShowBar = false }, advice));
        Check.Equal(0, Frame(Sample() with { Offered = [] }, settings, advice));
    }

    [Test]
    public void ScreensWithoutAdviceShowNoBarAndNoThinkingRow()
    {
        var other = Sample() with { Phase = VoraxPhase.Other };
        Check.Equal(0, Frame(other, new Settings { ShowPanel = false, ShowHighlights = false }));
        var row = Main.AdviceRows(other, null).Single();
        Check.That(row.Right == "none for this screen", row.Right);
    }
}

