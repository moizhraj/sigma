namespace Sigma.Core.Simulation;

public interface IValueSimulator
{
    double GetAnalogValue(double elapsedSeconds, double phaseOffset, double min, double max);
    bool GetBinaryValue(double elapsedSeconds, double phaseOffset);
}
