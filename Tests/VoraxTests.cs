using System.Linq;
using EmberHUD;
using EmberHUD.Testing;
using EmberHUD.Ui;

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
        [new VoraxCard(0, 101, VoraxCardKind.Instrument, 0) { Name = "Aberrant Sprout", TypeName = "Surgical Instrument" }], [0, 0, 0, 0, 0, 0]);

    static int Frame(VoraxState? state, Settings? settings = null)
    {
        TestGame.Set(new GameState { Vorax = state });
        try
        {
            var plugin = new Main();
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
    public void RenderHiddenWhenPanelOff() => Check.Equal(0, Frame(Sample(), new Settings { ShowPanel = false }));

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
    }
}
