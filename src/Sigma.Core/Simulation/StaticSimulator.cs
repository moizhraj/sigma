namespace Sigma.Core.Simulation;

public class StaticSimulator : IValueSimulator
{
    public double GetAnalogValue(double elapsedSeconds, double phaseOffset, double min, double max)
        => (min + max) / 2.0;

    public bool GetBinaryValue(double elapsedSeconds, double phaseOffset)
        => false;
}
