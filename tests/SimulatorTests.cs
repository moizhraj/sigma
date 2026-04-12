using FluentAssertions;
using Sigma.Core.Enums;
using Sigma.Core.Simulation;

namespace Sigma.Tests;

public class SimulatorTests
{
    [Theory]
    [InlineData(SimulationPattern.Static)]
    [InlineData(SimulationPattern.Sine)]
    [InlineData(SimulationPattern.Ramp)]
    [InlineData(SimulationPattern.Random)]
    [InlineData(SimulationPattern.Sawtooth)]
    public void Analog_value_is_within_range(SimulationPattern pattern)
    {
        var simulator = ValueSimulatorFactory.Create(pattern);
        const double min = 100, max = 500;

        for (int i = 0; i < 100; i++)
        {
            double value = simulator.GetAnalogValue(i * 0.7, i * 1.3, min, max);
            value.Should().BeGreaterThanOrEqualTo(min - 0.001)
                          .And.BeLessThanOrEqualTo(max + 0.001,
                              because: $"pattern={pattern}, elapsed={i * 0.7}");
        }
    }

    [Fact]
    public void Static_simulator_returns_midpoint()
    {
        var sim = new StaticSimulator();
        sim.GetAnalogValue(0, 0, 0, 100).Should().BeApproximately(50, 0.001);
        sim.GetAnalogValue(999, 42, 200, 400).Should().BeApproximately(300, 0.001);
    }

    [Fact]
    public void Static_simulator_binary_always_false()
    {
        var sim = new StaticSimulator();
        sim.GetBinaryValue(0, 0).Should().BeFalse();
        sim.GetBinaryValue(100, 50).Should().BeFalse();
    }

    [Fact]
    public void Phase_offset_shifts_sine_output()
    {
        var sim = new SineSimulator();
        double v1 = sim.GetAnalogValue(0, 0, 0, 100);
        double v2 = sim.GetAnalogValue(0, 15, 0, 100);
        v1.Should().NotBeApproximately(v2, 0.001, "different phase offsets should produce different values at t=0");
    }

    [Fact]
    public void Sawtooth_resets_at_period_boundary()
    {
        var sim = new SawtoothSimulator();
        // At elapsed=0 (phase=0) the sawtooth starts at min
        double start = sim.GetAnalogValue(0, 0, 0, 100);
        start.Should().BeApproximately(0, 0.001);

        // At elapsed=60 (one full period) it resets back to ~0
        double reset = sim.GetAnalogValue(60, 0, 0, 100);
        reset.Should().BeApproximately(0, 0.1);
    }

    [Fact]
    public void ValueSimulatorFactory_throws_on_unknown_pattern()
    {
        var act = () => ValueSimulatorFactory.Create((SimulationPattern)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
