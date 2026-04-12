namespace Sigma.Core.Configuration;

public class DeviceRegistersConfig
{
    public List<RegisterGroupConfig> HoldingRegisters { get; set; } = [];
    public List<RegisterGroupConfig> InputRegisters { get; set; } = [];
    public List<RegisterGroupConfig> Coils { get; set; } = [];
    public List<RegisterGroupConfig> DiscreteInputs { get; set; } = [];
}
