using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EmberHUD.Testing;
using Vorax.Sim;
using Rarity = Vorax.Sim.Rarity;

namespace Vorax;

public sealed class SolverTests
{
    static readonly SolveSettings Quick = new(60, 1500, 0.05);

    static Monster Normal(Species species, int activity = 1) => new(species, Rarity.Normal, activity, 36);

    [Test]
    public void ThePotentXenopurgeOnTheBiggestGroupWins()
    {
        var board = Board(Normal(Species.Agito), Normal(Species.Aemberon), new Monster(Species.Pests, Rarity.Rare, 15, 12));
        var ranking = Solver.RankPotions(board, [17, 10, 8], Quick, null, false, new Rng(1));
        Check.Equal(0, ranking.Best);
    }

    [Test]
    public void TargetPicksTheGroupWithTheMostActivityForQuantityPotions()
    {
        var board = Board(Normal(Species.Pests, 2), Normal(Species.Pests, 9), Normal(Species.Pests, 4));
        var options = Solver.TargetOptions(board, 17);
        var ranking = Solver.RankTargets(board, 17, Quick, null, new Rng(2));
        Check.Equal(options.Count, ranking.Values.Length);
        Check.Equal(1, options[ranking.Best][0]);
        Check.Equal(1, Solver.BestTargets(board, 17, Quick, null, new Rng(2))[0]);
    }

    [Test]
    public void RankingIsDeterministicForASeed()
    {
        var board = Board(Normal(Species.Pests), Normal(Species.Agito), Normal(Species.Aemberon));
        var a = Solver.RankPotions(board, [7, 17, 11], Quick, null, true, new Rng(9));
        var b = Solver.RankPotions(board, [7, 17, 11], Quick, null, true, new Rng(9));
        Check.That(a.Values.SequenceEqual(b.Values));
        Check.Equal((a.Best, a.RerollValue, a.RerollBest), (b.Best, b.RerollValue, b.RerollBest));
    }

    [Test]
    public void NoRerollIsEverBestWithZeroRerolls()
    {
        var board = StrongBoard();
        board.Rerolls = 0;
        var ranking = Solver.RankPotions(board, [33, 33, 33], Quick, null, true, new Rng(3));
        Check.That(!ranking.RerollBest);
    }

    [Test]
    public void AllowRerollFalseNeverRecommendsAReroll()
    {
        var board = StrongBoard();
        var allowed = Solver.RankPotions(board, [33, 33, 33], Quick, null, true, new Rng(4));
        var denied = Solver.RankPotions(board, [33, 33, 33], Quick, null, false, new Rng(4));
        Check.That(!denied.RerollBest);
        Check.That(allowed.Values.SequenceEqual(denied.Values));
    }

    [Test]
    public void AllBadHandWithRerollsRecommendsReroll()
    {
        var ranking = Solver.RankPotions(StrongBoard(), [33, 33, 33], Quick, null, true, new Rng(5));
        Check.That(ranking.RerollBest, $"reroll {ranking.RerollValue} vs {ranking.Values[ranking.Best]}");
    }

    [Test]
    public void StrongHandDoesNotRecommendReroll()
    {
        var board = Board(new Monster(Species.Pests, Rarity.Rare, 150, 12));
        board.Round = 2;
        var ranking = Solver.RankPotions(board, [17, 33, 33], Quick, null, true, new Rng(6));
        Check.That(!ranking.RerollBest, $"reroll {ranking.RerollValue} vs {ranking.Values[ranking.Best]}");
    }

    [Test]
    public void CustomScoreChangesTheWinner()
    {
        var board = Board(Normal(Species.Pests), Normal(Species.Agito));
        var byActivity = Solver.RankPotions(board, [17, 7], Quick, null, false, new Rng(7));
        double PestGroups(SimState s) => s.Dishes.Count(d => d is { Species: Species.Pests });
        var byPests = Solver.RankPotions(board, [17, 7], Quick, PestGroups, false, new Rng(7));
        Check.Equal(0, byActivity.Best);
        Check.Equal(1, byPests.Best);
    }

    [Test]
    public void InstrumentRankingPrefersTheOneThatScalesTheBoard()
    {
        var board = Board(Normal(Species.Pests), Normal(Species.Pests), Normal(Species.Pests), Normal(Species.Pests));
        board.Round = 3;
        var ranking = Solver.RankInstruments(board, [3, 1], Quick, null, new Rng(8));
        Check.Equal(1, ranking.Best);
        Check.That(!ranking.RerollBest);
        var noPests = Board(Normal(Species.Agito), Normal(Species.Aemberon));
        noPests.Round = 3;
        var withoutPests = Solver.RankInstruments(noPests, [1], Quick, null, new Rng(8));
        Check.That(ranking.Values[1] > withoutPests.Values[0] * 1.2);
    }

    [Test]
    public void BudgetStopsEarlyButStillRanks()
    {
        var board = Board(Normal(Species.Pests), Normal(Species.Agito));
        var clock = Stopwatch.StartNew();
        var ranking = Solver.RankPotions(board, [17, 7, 11], new SolveSettings(100000, 1, 0.05), null, true, new Rng(10));
        Check.That(clock.ElapsedMilliseconds < 500, $"{clock.ElapsedMilliseconds} ms");
        Check.Equal(3, ranking.Values.Length);
        Check.That(ranking.Values.All(v => v > 0));
    }

    [Test]
    public void OneOrTwoTargetPotionRanksSinglesAndPairs()
    {
        var board = Board(Normal(Species.Pests), Normal(Species.Pests, 3), Normal(Species.Pests, 5));
        var options = Solver.TargetOptions(board, 1);
        Check.Equal(6, options.Count);
        Check.Equal(3, options.Count(o => o.Length == 1));
        Check.Equal(3, options.Count(o => o.Length == 2));
        var ranking = Solver.RankTargets(board, 1, Quick, null, new Rng(11));
        Check.Equal(6, ranking.Values.Length);
        Check.That(ranking.Best is >= 0 and < 6);
        Check.That(Solver.BestTargets(board, 1, Quick, null, new Rng(11)).Count is 1 or 2);
    }

    [Test]
    public void NothingToTargetRanksNothing()
    {
        Check.Equal(1, Solver.TargetOptions(Board(Normal(Species.Pests)), 7).Count);
        Check.Equal(0, Solver.BestTargets(Board(Normal(Species.Pests)), 7, Quick, null, new Rng(1)).Count);
    }

    [Test]
    public void DrawModelDefaultsAreUniformWithOnePercentSupreme()
    {
        var pool = DrawModel.PotionPool(null);
        Check.That(pool.All(p => p.SimId is not (33 or 34 or 40)));
        Check.That(System.Math.Abs(pool.Sum(p => p.Weight) - 1.0) < 1e-9);
        Check.That(System.Math.Abs(pool.Where(p => p.SimId is >= 35 and <= 39).Sum(p => p.Weight) - 0.01) < 1e-9);
        var real = DrawModel.PotionPool(new Dictionary<int, int> { [17] = 5, [7] = 1, [33] = 9 });
        Check.Equal(2, real.Count);
        Check.Equal(5.0, real.First(p => p.SimId == 17).Weight);
    }

    [Test]
    public void KitValueIsTheExpectedBestOfItsHand()
    {
        var board = Board(Normal(Species.Pests, 3), Normal(Species.Agito));
        var only17 = new SolveSettings(60, 1500, 0.05) { RealWeights = new Dictionary<int, int> { [17] = 1 } };
        var direct = Solver.RankPotions(board, [17], only17, null, false, new Rng(12)).Values[0];
        var kit = new KitDef(1, [100, 100, 100, 100]);
        var viaKit = Solver.RankPotions(board, [-1], only17, null, false, new Rng(12), [kit]).Values[0];
        Check.That(System.Math.Abs(viaKit - direct) < direct * 0.03, $"kit {viaKit} vs direct {direct}");
    }

    [Test]
    public void KitBeatsAWeakHandOfPotions()
    {
        var board = Board(Normal(Species.Pests, 3), Normal(Species.Agito));
        var only17 = new SolveSettings(60, 1500, 0.05) { RealWeights = new Dictionary<int, int> { [17] = 1 } };
        var kit = new KitDef(3, [100, 100, 100, 100]);
        var ranking = Solver.RankPotions(board, [33, -1], only17, null, false, new Rng(13), [null, kit]);
        Check.Equal(1, ranking.Best);
        Check.That(ranking.Values[1] > ranking.Values[0]);
    }

    [Test]
    public void DefaultSettingsFitTheBudgetOnASixGroupLateBoard()
    {
        var board = Board(new(Species.Pests, Rarity.Rare, 90, 150), new(Species.Pests, Rarity.Magic, 40, 80), Normal(Species.Agito, 30), new(Species.Aemberon, Rarity.Rare, 60, 40), Normal(Species.BoneSentry, 20), new(Species.Pests, Rarity.Boss, 400, 1));
        board.Round = 5;
        board.Rerolls = 3;
        board.Instruments.AddRange([1, 2, 6, 12, 16]);
        var clock = Stopwatch.StartNew();
        var ranking = Solver.RankPotions(board, [17, 23, 7], new SolveSettings(200, 1500, 0.05), null, true, new Rng(14));
        Check.That(clock.ElapsedMilliseconds < 1700, $"elapsed {clock.ElapsedMilliseconds} ms");
        Check.That(ranking.Values.All(v => v > 0));
    }

    [Test]
    public void TheInputStateIsNeverMutated()
    {
        var board = Board(Normal(Species.Pests), Normal(Species.Agito));
        board.Instruments.Add(1);
        var before = (board.TotalActivity, board.Round, board.Rerolls, board.Instruments.Count, board.Events.Count);
        Solver.RankPotions(board, [17, 7, 11], Quick, null, true, new Rng(15));
        Solver.RankTargets(board, 17, Quick, null, new Rng(15));
        Solver.RankInstruments(board, [2, 3], Quick, null, new Rng(15));
        Check.Equal(before, (board.TotalActivity, board.Round, board.Rerolls, board.Instruments.Count, board.Events.Count));
    }

    [Test]
    public void NearEqualOptionsDoNotRecommendAReroll()
    {
        var settings = new SolveSettings(200, 1500, 0.05) { RealWeights = new Dictionary<int, int> { [17] = 1, [18] = 1 } };
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var board = Board(new Monster(Species.Pests, Rarity.Rare, 15, 12), Normal(Species.Pests, 4), Normal(Species.Agito));
            board.Round = 3;
            var ranking = Solver.RankPotions(board, [17, 18, 17], settings, null, true, new Rng(seed));
            Check.That(!ranking.RerollBest, $"seed {seed}: reroll {ranking.RerollValue} vs {ranking.Values[ranking.Best]}");
        }
    }

    [Test]
    public void NegativeScoresUseASignSafeMargin()
    {
        var board = Board(Normal(Species.Pests), Normal(Species.Agito));
        var ranking = Solver.RankPotions(board, [17, 17, 17], Quick, s => -s.TotalActivity, true, new Rng(21));
        Check.That(!ranking.RerollBest || ranking.RerollValue > ranking.Values[ranking.Best] + System.Math.Abs(ranking.Values[ranking.Best]) * 0.05);
    }

    [Test]
    public void KitWeightsOnlyYieldTheirRarity()
    {
        var pool = DrawModel.PotionPool(null);
        var rare = DrawModel.KitPool(new KitDef(3, [0, 0, 100, 0]), pool);
        Check.That(rare.Count > 0 && rare.All(p => p.SimId is >= 0 and <= 6));
        var mixed = DrawModel.KitPool(new KitDef(3, [40, 44, 15, 1]), pool);
        Check.That(System.Math.Abs(mixed.Where(p => p.SimId is >= 23 and <= 32).Sum(p => p.Weight) - 40) < 1e-9);
        Check.That(System.Math.Abs(mixed.Where(p => p.SimId is >= 35).Sum(p => p.Weight) - 1) < 1e-9);
        var rng = new Rng(3);
        for (var n = 0; n < 50; n++) Check.That(DrawModel.Draw(rare, 3, rng).All(id => id is >= 0 and <= 6));
    }

    [Test]
    public void GameRarityBandsMatchTheSimRarityClasses()
    {
        foreach (var (low, high, rarity) in new[] { (101, 110, 0), (201, 216, 1), (301, 307, 2), (401, 406, 3) })
            for (var id = low; id <= high; id++)
            {
                Check.That(GameIds.TryPotion(id, out var sim), $"game {id}");
                Check.Equal(rarity, DrawModel.RarityClass(sim));
            }
    }

    static SimState Board(params Monster[] monsters)
    {
        var s = new SimState { Round = 1, Rerolls = 3 };
        foreach (var m in monsters) s.Add(m);
        s.Events.Clear();
        return s;
    }

    static SimState StrongBoard()
    {
        var s = Board(new(Species.Pests, Rarity.Normal, 80, 36), new(Species.Pests, Rarity.Magic, 120, 24), new(Species.Pests, Rarity.Rare, 150, 12));
        s.Round = 2;
        return s;
    }
}
