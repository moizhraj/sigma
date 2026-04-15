namespace Sigma.Core.Simulation;

public class RandomSimulator : IValueSimulator
{
    // Random.Shared is thread-safe; avoids races when multiple simulation ticks overlap.
    public double GetAnalogValue(double elapsedSeconds, double phaseOffset, double min, double max)
        => min + Random.Shared.NextDouble() * (max - min);

    public bool GetBinaryValue(double elapsedSeconds, double phaseOffset)
        => Random.Shared.Next(2) == 1;
}
