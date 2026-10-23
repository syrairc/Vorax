using System.Linq;
using EmberHUD;
using EmberHUD.Testing;
using Vorax.Sim;
using Rarity = Vorax.Sim.Rarity;

namespace Vorax;

public sealed class StateMapperTests
{
    static readonly int[] PotionIds = [101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215, 216, 301, 302, 303, 304, 305, 306, 307, 401, 402, 403, 404, 405, 406];

    static readonly int[] InstrumentIds = [101, 102, 103, 104, 105, 106, 107, 108, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215, 216, 217];

    static VoraxState State(VoraxMonster[]? monsters = null, VoraxCard[]? instruments = null) =>
        new(VoraxPhase.Round, 5, 13, 100, 0, 0, 0, 3, 6, monsters ?? [], [], instruments ?? [], []);

    static VoraxCard Instrument(int id) => new(0, id, VoraxCardKind.Instrument, 0);

    static VoraxCard Potion(int id) => new(0, id, VoraxCardKind.Potion, 1);

    [Test]
    public void EveryGamePotionIdMapsToADistinctSimPotion()
    {
        var sim = PotionIds.Select(id => GameIds.TryPotion(id, out var s) ? s : -1).ToList();
        Check.Equal(PotionIds.Length, sim.Count(s => s >= 0));
        Check.Equal(sim.Count, sim.Distinct().Count());
        Check.That(sim.All(s => Potions.All.Any(p => p.SimId == s)));
    }

    [Test]
    public void EveryGameInstrumentIdMaps()
    {
        var sim = InstrumentIds.Select(id => GameIds.TryInstrument(id, out var s) ? s : -1).ToList();
        Check.Equal(25, sim.Count(s => s >= 0));
        Check.Equal(25, sim.Distinct().Count());
        Check.That(sim.All(Instruments.Known));
    }

    [Test]
    public void StartSetupInstrumentsAreSkippedNotErrors()
    {
        var result = StateMapper.Map(State(instruments: [Instrument(1), Instrument(4), Instrument(101)]), default);
        Check.That(result.Error is null);
        Check.Equal(1, result.State!.Instruments.Count);
        Check.That(GameIds.IsStartSetup(2) && !GameIds.IsStartSetup(5) && !GameIds.IsStartSetup(101));
    }

    [Test]
    public void AnUnknownPotionIdIsAnErrorNamingTheId()
    {
        Check.That(!StateMapper.TryPotions([Potion(101), Potion(999)], out _, out var error));
        Check.That(error!.Contains("999"));
    }

    [Test]
    public void AnUnknownInstrumentIdIsAnErrorNamingTheId()
    {
        var result = StateMapper.Map(State(instruments: [Instrument(999)]), default);
        Check.That(result.State is null);
        Check.That(result.Error!.Contains("999"));
    }

    [Test]
    public void AnUnknownSpeciesIsAnError()
    {
        var result = StateMapper.Map(State([new(0, 1, 777, 1, 36, 1)]), default);
        Check.That(result.State is null);
        Check.That(result.Error!.Contains("777"));
    }

    [Test]
    public void AnUnknownRarityIsAnError()
    {
        Check.That(StateMapper.Map(State([new(0, 1, 201, 9, 36, 1)]), default).Error is not null);
    }

    [Test]
    public void MonstersLandInTheirSlots()
    {
        var result = StateMapper.Map(State([new(2, 1, 203, 2, 24, 5), new(4, 2, 204, 4, 1, 300)]), new(true, false));
        var s = result.State!;
        Check.That(s.Dishes[0] is null && s.Dishes[1] is null && s.Dishes[3] is null && s.Dishes[5] is null);
        Check.Equal(new Monster(Species.Agito, Rarity.Magic, 5, 24), s.Dishes[2]!.Value);
        Check.Equal(new Monster(Species.Aemberon, Rarity.Boss, 300, 1), s.Dishes[4]!.Value);
        Check.Equal(5, s.Round);
        Check.Equal(3, s.Rerolls);
        Check.That(s.Passed8000 && !s.Passed28000);
    }

    [Test]
    public void ASlotOutsideTheBoardIsAnError()
    {
        Check.That(StateMapper.Map(State([new(6, 1, 201, 1, 36, 1)]), default).Error is not null);
    }

    [Test]
    public void ADuplicateInstrumentIdIsKeptOnce()
    {
        var result = StateMapper.Map(State(instruments: [Instrument(101), Instrument(101), Instrument(201)]), default);
        Check.Equal(2, result.State!.Instruments.Count);
        Check.Equal(2, result.State.Instruments.Distinct().Count());
    }

    [Test]
    public void KitsMapWithTheirAmountAndWeights()
    {
        Check.That(GameIds.TryKit(2, out var small) && small.Amount == 3 && small.RarityWeights.SequenceEqual([40, 44, 15, 1]));
        Check.That(GameIds.TryKit(4, out var rare) && rare.Amount == 3 && rare.RarityWeights.SequenceEqual([40, 44, 18, 2]));
        Check.That(GameIds.TryKit(6, out var large) && large.Amount == 5 && large.RarityWeights.SequenceEqual([40, 44, 15, 1]));
        Check.That(!GameIds.TryKit(1, out _));
    }

    [Test]
    public void AMonsterKitIsAnErrorNamingTheId()
    {
        Check.That(!StateMapper.TryPotions([new VoraxCard(0, 3, VoraxCardKind.Bundle, 0)], out _, out var error));
        Check.That(error!.Contains('3'));
    }

    [Test]
    public void OfferedPotionsMapForEveryOfferedCard()
    {
        Check.That(StateMapper.TryPotions([Potion(101), Potion(301), new VoraxCard(1, 4, VoraxCardKind.Bundle, 0)], out var ids, out var error));
        Check.That(error is null);
        Check.Equal(2, ids.Count);
        Check.Equal(30, ids[0]);
        Check.Equal(4, ids[1]);
    }
}
