using System.Linq;
using EmberHUD.Testing;
using Vorax.Sim;

namespace Vorax;

public sealed class SimStateTests
{
    [Test]
    public void FreshStatsMatchTheGame()
    {
        Check.Equal((1, 36), Stats(Rarity.Normal));
        Check.Equal((5, 24), Stats(Rarity.Magic));
        Check.Equal((15, 12), Stats(Rarity.Rare));
        Check.Equal((300, 1), Stats(Rarity.Boss));
    }

    [Test]
    public void AddUsesTheFirstEmptyDish()
    {
        var s = new SimState();
        s.Add(Monster.Fresh(Species.Pests, Rarity.Normal));
        s.Add(Monster.Fresh(Species.Pests, Rarity.Normal));
        s.Remove(0);
        Check.Equal(0, s.Add(Monster.Fresh(Species.Agito, Rarity.Rare)));
        Check.Equal(Species.Agito, s.Dishes[0]!.Value.Species);
    }

    [Test]
    public void AddOnAFullBoardRecordsOverflowAndChangesNothing()
    {
        var s = Full();
        var before = s.Dishes.ToArray();
        Check.Equal(-1, s.Add(Monster.Fresh(Species.Agito, Rarity.Boss)));
        Check.That(s.Dishes.SequenceEqual(before));
        Check.Equal(EventKind.AddOverflow, s.Events[^1].Kind);
    }

    [Test]
    public void RemoveRecordsTheSpecies()
    {
        var s = new SimState();
        s.Add(Monster.Fresh(Species.Aemberon, Rarity.Magic));
        Check.Equal(Species.Aemberon, s.Remove(0)!.Value.Species);
        Check.That(s.Dishes[0] is null);
        Check.That(s.Events[^1] is { Kind: EventKind.Remove, Species: Species.Aemberon });
    }

    [Test]
    public void MutateKeepsActivityAndQuantity()
    {
        var s = new SimState();
        s.Add(new Monster(Species.Pests, Rarity.Rare, 77, 33));
        s.Mutate(0, Species.Agito, null, new Rng(1));
        var m = s.Dishes[0]!.Value;
        Check.Equal(Species.Agito, m.Species);
        Check.Equal((77, 33), (m.Activity, m.Quantity));
        s.Mutate(0, null, Rarity.Boss, new Rng(2));
        Check.Equal(Rarity.Boss, s.Dishes[0]!.Value.Rarity);
    }

    [Test]
    public void AwakenBonusIsSixtyForMagicAndOneTwentyForRareAndBoss()
    {
        var s = new SimState();
        s.Add(Monster.Fresh(Species.Pests, Rarity.Normal));
        s.Awaken(0);
        Check.Equal((Rarity.Magic, 61), (s.Dishes[0]!.Value.Rarity, s.Dishes[0]!.Value.Activity));
        s.Awaken(0);
        Check.Equal((Rarity.Rare, 181), (s.Dishes[0]!.Value.Rarity, s.Dishes[0]!.Value.Activity));
        s.Awaken(0);
        Check.Equal((Rarity.Boss, 301), (s.Dishes[0]!.Value.Rarity, s.Dishes[0]!.Value.Activity));
    }

    [Test]
    public void AwakeningABossStaysBossAndStillGetsTheBonus()
    {
        var s = new SimState();
        s.Add(Monster.Fresh(Species.Pests, Rarity.Boss));
        s.Awaken(0);
        Check.Equal((Rarity.Boss, 420), (s.Dishes[0]!.Value.Rarity, s.Dishes[0]!.Value.Activity));
    }

    [Test]
    public void FuseSumsBothStatsAndRemovesTheSources()
    {
        var s = new SimState();
        s.Add(new Monster(Species.Pests, Rarity.Normal, 100, 100));
        s.Add(new Monster(Species.Agito, Rarity.Normal, 100, 100));
        var dish = s.Fuse([0, 1], Species.BoneSentry, Rarity.Magic);
        var m = s.Dishes[dish]!.Value;
        Check.Equal((200, 200, 40000), (m.Activity, m.Quantity, m.Total));
        Check.Equal(1, s.Dishes.Count(d => d is not null));
        Check.Equal(2, s.Events.Count(e => e.Kind == EventKind.Remove));
    }

    [Test]
    public void DevourMovesStatsAndRemovesThePrey()
    {
        var s = new SimState();
        s.Add(new Monster(Species.Pests, Rarity.Normal, 10, 20));
        s.Add(new Monster(Species.Agito, Rarity.Normal, 5, 7));
        s.Devour(0, 1);
        Check.Equal((15, 27), (s.Dishes[0]!.Value.Activity, s.Dishes[0]!.Value.Quantity));
        Check.That(s.Dishes[1] is null);
    }

    [Test]
    public void CloneIsIndependent()
    {
        var s = new SimState { Round = 3 };
        s.Add(Monster.Fresh(Species.Pests, Rarity.Normal));
        s.Instruments.Add(5);
        s.AcquiredRound[5] = 1;
        var c = s.Clone();
        c.Remove(0);
        c.Instruments.Add(6);
        c.AcquiredRound[6] = 2;
        c.Round = 9;
        Check.That(s.Dishes[0] is not null);
        Check.Equal(1, s.Instruments.Count);
        Check.Equal(1, s.AcquiredRound.Count);
        Check.Equal(3, s.Round);
        Check.Equal(1, s.Events.Count);
    }

    [Test]
    public void RngIsDeterministicForASeed()
    {
        var a = new Rng(42);
        var b = new Rng(42);
        for (var i = 0; i < 20; i++) Check.Equal(a.NextDouble(), b.NextDouble());
        var items = new[] { 1, 2, 3, 4, 5 };
        Check.That(new Rng(7).Sample(items, 3).SequenceEqual(new Rng(7).Sample(items, 3)));
        Check.Equal(3, new Rng(7).Sample(items, 3).Distinct().Count());
        Check.That(new Rng(9).Next(6) is >= 0 and < 6);
    }

    static (int, int) Stats(Rarity r)
    {
        var m = Monster.Fresh(Species.Pests, r);
        return (m.Activity, m.Quantity);
    }

    static SimState Full()
    {
        var s = new SimState();
        for (var i = 0; i < SimState.DishCount; i++) s.Add(Monster.Fresh(Species.Pests, Rarity.Normal));
        return s;
    }
}
