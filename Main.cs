using EmberHUD;

namespace Vorax;

public sealed class Main : Plugin<Settings>
{
    public override void Tick() => Game.WantVorax();

    public override void Render()
    {
        if (!Settings.Enabled) return;
    }
}
