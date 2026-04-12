namespace Sigma.Core.Configuration;

public class DeviceConfig
{
    public byte UnitId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DeviceRegistersConfig Registers { get; set; } = new();
}
