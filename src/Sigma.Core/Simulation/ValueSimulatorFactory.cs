using Sigma.Core.Enums;

namespace Sigma.Core.Simulation;

public static class ValueSimulatorFactory
{
    public static IValueSimulator Create(SimulationPattern pattern) => pattern switch
    {
        SimulationPattern.Static   => new StaticSimulator(),
        SimulationPattern.Sine     => new SineSimulator(),
        SimulationPattern.Ramp     => new RampSimulator(),
        SimulationPattern.Random   => new RandomSimulator(),
        SimulationPattern.Sawtooth => new SawtoothSimulator(),
        _ => throw new ArgumentOutOfRangeException(nameof(pattern), pattern, "Unknown simulation pattern.")
    };
}
