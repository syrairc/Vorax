using System.Threading;
using EmberHUD.Testing;

namespace Vorax;

public sealed class ProgressTests
{
    [Test]
    public void ItIsIdleAndFullUntilBegun()
    {
        var progress = new Progress();
        Check.That(!progress.Active && progress.Fraction == 1);
    }

    [Test]
    public void ItStartsAtZeroAndNeverReachesThePhaseEndOnItsOwn()
    {
        var progress = new Progress();
        progress.Begin();
        progress.Phase(0.5, 20);
        Check.That(progress.Active && progress.Fraction < 0.5);
        Thread.Sleep(60);
        var late = progress.Fraction;
        Check.That(late > 0.4 && late < 0.5, $"{late}");
    }

    [Test]
    public void ThePhasesChainFromWhereTheLastOneStood()
    {
        var progress = new Progress();
        progress.Begin();
        progress.Phase(0.5, 10);
        Thread.Sleep(40);
        progress.Phase(1, 10000);
        var start = progress.Fraction;
        Check.That(start > 0.4 && start < 0.6, $"{start}");
    }

    [Test]
    public void EndMakesItFullAndInactive()
    {
        var progress = new Progress();
        progress.Begin();
        progress.End();
        Check.That(!progress.Active && progress.Fraction == 1);
    }
}
