namespace Sigma.Core.Simulation;

public class RampSimulator : IValueSimulator
{
    private const double PeriodSeconds = 60.0;

    public double GetAnalogValue(double elapsedSeconds, double phaseOffset, double min, double max)
    {
        var t = ((elapsedSeconds + phaseOffset) % PeriodSeconds) / PeriodSeconds;
        // Triangle wave: up first half, down second half
        var tri = t < 0.5 ? t * 2.0 : (1.0 - t) * 2.0;
        return min + tri * (max - min);
    }

    public bool GetBinaryValue(double elapsedSeconds, double phaseOffset)
    {
        var t = ((elapsedSeconds + phaseOffset) % PeriodSeconds) / PeriodSeconds;
        return t < 0.5;
    }
}
