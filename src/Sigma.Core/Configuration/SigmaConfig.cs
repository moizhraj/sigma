namespace Sigma.Core.Configuration;

public class SigmaConfig
{
    public NetworkConfig Network { get; set; } = new();
    public DefaultsConfig Defaults { get; set; } = new();
    public List<DeviceConfig> Devices { get; set; } = [];
}
