namespace Sigma.Engine.Models;

public class SimulatedDevice
{
    public byte UnitId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public SimulatedRegister[] AllRegisters { get; init; } = [];
    public DeviceStats Stats { get; } = new();
}
