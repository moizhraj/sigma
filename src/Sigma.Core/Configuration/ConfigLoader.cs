using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sigma.Core.Configuration;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static SigmaConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Configuration file not found: {path}");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SigmaConfig>(json, Options)
               ?? throw new InvalidOperationException("Configuration file is empty or invalid.");
    }

    public static string GenerateSample()
    {
        var sample = new SigmaConfig
        {
            Network = new NetworkConfig { Interface = "0.0.0.0", Port = 502 },
            Defaults = new DefaultsConfig
            {
                SimulationIntervalMs = 1000,
                UpdateIntervalMs = 5000,
                Jitter = 0.5,
                ValueRange = new ValueRange { Min = 0, Max = 1000 },
                SimulationPattern = Enums.SimulationPattern.Sine
            },
            Devices =
            [
                new DeviceConfig
                {
                    UnitId = 1,
                    Name = "HVAC Controller",
                    Description = "Zone temperature and humidity controller",
                    Registers = new DeviceRegistersConfig
                    {
                        HoldingRegisters =
                        [
                            new RegisterGroupConfig
                            {
                                StartAddress = 0,
                                Count = 10,
                                DataType = Enums.DataType.UInt16,
                                SimulationPattern = Enums.SimulationPattern.Sine,
                                ValueRange = new ValueRange { Min = 150, Max = 950 },
                                UpdateIntervalMs = 3000,
                                Label = "Temperature sensors (scaled x10)"
                            }
                        ],
                        InputRegisters =
                        [
                            new RegisterGroupConfig
                            {
                                StartAddress = 0,
                                Count = 5,
                                DataType = Enums.DataType.UInt16,
                                SimulationPattern = Enums.SimulationPattern.Random,
                                ValueRange = new ValueRange { Min = 200, Max = 800 },
                                Label = "Humidity sensors (scaled x10)"
                            }
                        ],
                        Coils =
                        [
                            new RegisterGroupConfig
                            {
                                StartAddress = 0,
                                Count = 8,
                                SimulationPattern = Enums.SimulationPattern.Ramp,
                                Label = "Fan and valve controls"
                            }
                        ],
                        DiscreteInputs =
                        [
                            new RegisterGroupConfig
                            {
                                StartAddress = 0,
                                Count = 4,
                                SimulationPattern = Enums.SimulationPattern.Sine,
                                Label = "Door and window sensors"
                            }
                        ]
                    }
                },
                new DeviceConfig
                {
                    UnitId = 2,
                    Name = "Power Meter",
                    Description = "3-phase power measurement",
                    Registers = new DeviceRegistersConfig
                    {
                        HoldingRegisters =
                        [
                            new RegisterGroupConfig
                            {
                                StartAddress = 0,
                                Count = 6,
                                DataType = Enums.DataType.Float32,
                                SimulationPattern = Enums.SimulationPattern.Sine,
                                ValueRange = new ValueRange { Min = 210.0, Max = 240.0 },
                                UpdateIntervalMs = 2000,
                                Label = "Voltage L1/L2/L3 (IEEE 754 float, 2 registers each)"
                            },
                            new RegisterGroupConfig
                            {
                                StartAddress = 10,
                                Count = 6,
                                DataType = Enums.DataType.Float32,
                                SimulationPattern = Enums.SimulationPattern.Sawtooth,
                                ValueRange = new ValueRange { Min = 0.0, Max = 32.0 },
                                Label = "Current L1/L2/L3 (IEEE 754 float, 2 registers each)"
                            }
                        ]
                    }
                }
            ]
        };

        return JsonSerializer.Serialize(sample, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        });
    }
}
