namespace Sigma.Core.Simulation;

public class RandomSimulator : IValueSimulator
{
    private readonly Random _random = new();

    public double GetAnalogValue(double elapsedSeconds, double phaseOffset, double min, double max)
        => min + _random.NextDouble() * (max - min);

    public bool GetBinaryValue(double elapsedSeconds, double phaseOffset)
        => _random.Next(2) == 1;
}
