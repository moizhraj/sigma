namespace Sigma.Core.Simulation;

public class SineSimulator : IValueSimulator
{
    private const double PeriodSeconds = 60.0;

    public double GetAnalogValue(double elapsedSeconds, double phaseOffset, double min, double max)
    {
        var sine = Math.Sin(2 * Math.PI * (elapsedSeconds + phaseOffset) / PeriodSeconds);
        return min + (sine + 1.0) / 2.0 * (max - min);
    }

    public bool GetBinaryValue(double elapsedSeconds, double phaseOffset)
    {
        var sine = Math.Sin(2 * Math.PI * (elapsedSeconds + phaseOffset) / PeriodSeconds);
        return sine >= 0;
    }
}
