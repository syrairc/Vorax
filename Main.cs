using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;
using Vorax.Sim;

namespace Vorax;

public sealed class Main : Plugin<Settings>
{
    const float Pad = 8;
    const float Width = 560;
    const string GamePanelName = "S13GamePlayMain_C";
    readonly TextStyle _style = TextStyle.Hud();
    readonly Overlay _overlay = new();
    int _unusedWidth, _unusedHeight;
    Worker? _worker;
    (string, int, int, int) _workerKey;

    internal Advice? AdviceOverride;

    internal Strategy? StrategyFor(string? mode) => mode == Settings.SimulatorMode ? null : Registry.ByName(mode);

    public override void Tick()
    {
        Game.WantVorax();
        if (!Settings.Enabled || Game.State.Vorax is not { } state) return;
        var key = (Settings.Mode, Settings.ClampedRollouts, Settings.BudgetMilliseconds, Settings.RerollMargin);
        if (_worker is null || key != _workerKey)
        {
            _worker?.Dispose();
            _workerKey = key;
            _worker = new(new Engine(new(key.Item2, key.Item3, key.Item4 / 100.0), new Rng((ulong)Environment.TickCount64)));
        }

        _worker.Submit(state, StrategyFor(Settings.Mode));
    }

    public override void Dispose() => _worker?.Dispose();

    public override void Render()
    {
        if (!Settings.Enabled || Game.State.Vorax is not { } state) return;
        var advice = AdviceOverride ?? (_worker is { } worker && worker.Matches(state) ? worker.Latest : null);
        if (Settings.ShowHighlights) DrawMarks(state, advice);
        if (Settings.ShowPanel) DrawPanel(state, advice);
    }

    void DrawMarks(VoraxState state, Advice? advice)
    {
        var parts = Game.State.Ui.Panels.FirstOrDefault(panel => panel.Name == GamePanelName).Parts ?? [];
        foreach (var mark in Highlight.Marks(state, advice, parts)) Draw.GlowRect(mark.Min, mark.Max, mark.Strategy ? Settings.StrategyColour : Settings.SimulatorColour, 3);
    }

    void DrawPanel(VoraxState state, Advice? advice)
    {
        var rows = PanelRows(state, advice);
        if (rows.Count == 0) return;
        var rowHeight = Draw.RowHeight(_style);
        var size = new Vector2(Width, rowHeight * rows.Count) + new Vector2(Pad * 2);
        _overlay.Locked = Settings.Lock;
        _overlay.MoveHeld = Settings.MoveKey.Key != Key.None && Input.IsDown(Settings.MoveKey.Key);
        var origin = new Vector2(Settings.PanelX, Settings.PanelY);
        var (hovered, _, active) = _overlay.Handle("vorax", ref origin, size, ref Settings.PanelX, ref Settings.PanelY, ref _unusedWidth, 60, 4000, ref _unusedHeight, 0, 0, Overlay.ResizeEdge.None);
        if (hovered || active) Overlay.ClickBlocker("vorax", origin, size);
        Draw.RectFilled(origin, origin + size, GameColors.LabelBackground, 4);
        if (_overlay.Movable) Overlay.DragHint(origin, origin + size, active, hovered, Overlay.ResizeEdge.None);
        var at = origin + new Vector2(Pad);
        foreach (var (left, right, colour) in rows)
        {
            Draw.Row(at, Width, left, "", _style);
            var rightStyle = _style.Clone();
            rightStyle.Color = colour;
            Draw.Text(right, new Vector2(at.X + Width, at.Y), rightStyle, Align.Right);
            at.Y += rowHeight;
        }
    }

    internal List<(string Left, string Right, Color Colour)> PanelRows(VoraxState state, Advice? advice)
    {
        var rows = AdviceRows(state, advice);
        if (Settings.Debug) rows.AddRange(Rows(state, Width, _style));
        return rows;
    }

    internal static string SourceLabel(Advice advice) => advice.Source switch
    {
        AdviceSource.Strategy => $"Strategy: {advice.StrategyName}",
        AdviceSource.SimulatorFallback => $"Simulator (fallback: {advice.Error})",
        _ => "Simulator",
    };

    internal static List<(string Left, string Right, Color Colour)> AdviceRows(VoraxState state, Advice? advice)
    {
        var rows = new List<(string, string, Color)>();
        if (advice is null)
        {
            if (state.Offered.Length > 0) rows.Add(("Advice", "thinking...", GameColors.TextDim));
            return rows;
        }

        var indexes = advice.Pick.Indexes ?? [];
        rows.Add(("Stage", advice.Stage.ToString(), GameColors.Text));
        rows.Add(("Source", SourceLabel(advice), GameColors.Text));
        if (advice.Pick.IsReroll) rows.Add(("Pick: reroll", advice.Pick.Reason, GameColors.Gold));
        else if (indexes.Length > 0) rows.Add(($"Pick: {string.Join(", ", indexes.Select(i => PickName(state, advice.Stage, i)))}", advice.Pick.Reason, GameColors.Gold));
        if (advice.Values is { } values && advice.Stage != Stage.Target)
            for (var i = 0; i < Math.Min(values.Length, state.Offered.Length); i++)
                rows.Add(($"  {CardName(state.Offered[i])}", $"{values[i]:0}", GameColors.Text));
        if (advice.RerollValue is { } reroll) rows.Add(("Reroll", $"{reroll:0}", GameColors.Text));
        if (advice.Error is { } error && advice.Source != AdviceSource.SimulatorFallback) rows.Add(("Error", error, GameColors.Negative));
        return rows;
    }

    static string PickName(VoraxState state, Stage stage, int index)
    {
        if (stage == Stage.Target) return state.Monsters.Where(m => m.Slot == index).Select(m => m.Name).FirstOrDefault(name => name != "") ?? $"slot {index}";
        return index >= 0 && index < state.Offered.Length ? CardName(state.Offered[index]) : $"#{index}";
    }

    static string CardName(VoraxCard card) => card.Name != "" ? card.Name : $"card {card.Slot}";

    internal static List<(string Left, string Right, Color Colour)> Rows(VoraxState state, float width, TextStyle style)
    {
        var rows = new List<(string, string, Color)>
        {
            ("Phase", state.Phase.ToString(), GameColors.Text),
            ("Round", $"{state.Round} / {state.Rounds}", GameColors.Text),
            ("Activity", $"{state.Activity} / next {state.NextRewardActivity}", GameColors.Gold),
            ("Rewards", $"{state.RewardsTaken} / {state.RewardsTotal}", GameColors.Text),
            ("Rerolls", state.Rerolls.ToString(), GameColors.Text),
            ("Monster slots", state.MonsterSlots.ToString(), GameColors.Text),
            ("Reward bonus", $"+{state.RewardBonusPercent}%", GameColors.Text),
            ("Chests", string.Join(" ", state.Chests), GameColors.TextDim),
            ("Monsters", state.Monsters.Length.ToString(), GameColors.Gold),
        };
        rows.AddRange(state.Monsters.Select(m => ((string, string, Color))($"  {m.Slot}  {m.Name} (id {m.Id}, species {m.Species}, rarity {m.Rarity} {m.RarityName})", $"{m.Count} x {m.Activity} = {m.Total}", GameColors.Text)));
        AddCards(rows, "Offered", state.Offered, width, style);
        AddCards(rows, "Instruments", state.Instruments, width, style);
        AddCards(rows, "Protocols", state.Protocols, width, style);
        return rows;
    }

    static void AddCards(List<(string, string, Color)> rows, string title, VoraxCard[] cards, float width, TextStyle style)
    {
        rows.Add((title, cards.Length.ToString(), GameColors.Gold));
        foreach (var card in cards)
        {
            rows.Add(($"  {card.Slot}  {card.Name} (id {card.Id}, rarity {card.Rarity})", $"{card.Kind} / {card.TypeName}", GameColors.Text));
            foreach (var line in Text.Wrap(card.Description, width - 24, style)) rows.Add(("      " + line, "", GameColors.TextDim));
        }
    }
}
