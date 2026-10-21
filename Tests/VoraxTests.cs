using EmberHUD;
using EmberHUD.Testing;

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
}
