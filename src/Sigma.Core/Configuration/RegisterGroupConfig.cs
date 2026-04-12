using Sigma.Core.Enums;

namespace Sigma.Core.Configuration;

public class RegisterGroupConfig
{
    public int StartAddress { get; set; }
    public int Count { get; set; }
    public string? Label { get; set; }
    public DataType DataType { get; set; } = DataType.UInt16;
    public SimulationPattern? SimulationPattern { get; set; }
    public ValueRange? ValueRange { get; set; }
    public int? UpdateIntervalMs { get; set; }
}
