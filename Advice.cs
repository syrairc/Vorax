using Vorax.Sim;

namespace Vorax;

public enum AdviceSource { Simulator, Strategy, SimulatorFallback }

public sealed record Advice(Stage Stage, AdviceSource Source, string StrategyName, Pick Pick, double[]? Values, double? RerollValue, string? Error);
