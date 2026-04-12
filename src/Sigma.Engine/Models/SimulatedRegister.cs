using Sigma.Core.Enums;
using Sigma.Core.Simulation;

namespace Sigma.Engine.Models;

public class SimulatedRegister
{
    public RegisterType Type { get; init; }
    public int Address { get; init; }
    public DataType DataType { get; init; }
    public bool IsWritable { get; init; }
    public bool IsOverridden { get; set; }
    public IValueSimulator Simulator { get; init; } = null!;
    public double PhaseOffset { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public int UpdateIntervalMs { get; init; }
    public DateTime LastUpdateTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Number of raw 16-bit register slots consumed by this logical value.
    /// UInt16/Int16 = 1, Float32/Int32/UInt32 = 2.
    /// </summary>
    public int RegisterWidth => DataType is DataType.Float32 or DataType.Int32 or DataType.UInt32 ? 2 : 1;
}
