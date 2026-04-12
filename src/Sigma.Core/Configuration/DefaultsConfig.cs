using Sigma.Core.Enums;

namespace Sigma.Core.Configuration;

public class DefaultsConfig
{
    public int SimulationIntervalMs { get; set; } = 1000;
    public int UpdateIntervalMs { get; set; } = 5000;
    public double Jitter { get; set; } = 0.5;
    public ValueRange ValueRange { get; set; } = new ValueRange { Min = 0, Max = 1000 };
    public SimulationPattern SimulationPattern { get; set; } = SimulationPattern.Sine;
}
