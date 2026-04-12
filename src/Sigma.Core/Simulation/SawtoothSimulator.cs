namespace Sigma.Core.Simulation;

public class SawtoothSimulator : IValueSimulator
{
    private const double PeriodSeconds = 60.0;

    public double GetAnalogValue(double elapsedSeconds, double phaseOffset, double min, double max)
    {
        var t = ((elapsedSeconds + phaseOffset) % PeriodSeconds) / PeriodSeconds;
        return min + t * (max - min);
    }

    public bool GetBinaryValue(double elapsedSeconds, double phaseOffset)
    {
        var t = ((elapsedSeconds + phaseOffset) % PeriodSeconds) / PeriodSeconds;
        return t >= 0.5;
    }
}
