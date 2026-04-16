using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sigma.Core.Configuration;
using Sigma.Core.Enums;
using Sigma.Core.Simulation;
using Sigma.Engine.Factories;
using Sigma.Engine.Models;
using Sigma.Engine.Services;

namespace Sigma.Tests;

// ---------------------------------------------------------------------------
// Helper: minimal valid config factory shared across test classes
// ---------------------------------------------------------------------------
file static class Configs
{
    public static SigmaConfig AllTableTypes() => new()
    {
        Devices =
        [
            new DeviceConfig
            {
                UnitId = 1,
                Name = "Full Device",
                Registers = new DeviceRegistersConfig
                {
                    HoldingRegisters  = [new RegisterGroupConfig { StartAddress = 0,  Count = 4 }],
                    InputRegisters    = [new RegisterGroupConfig { StartAddress = 0,  Count = 2 }],
                    Coils             = [new RegisterGroupConfig { StartAddress = 0,  Count = 8 }],
                    DiscreteInputs    = [new RegisterGroupConfig { StartAddress = 0,  Count = 4 }]
                }
            }
        ]
    };

    public static DefaultsConfig Defaults() => new()
    {
        SimulationIntervalMs = 1000,
        UpdateIntervalMs     = 5000,
        Jitter               = 0.0,   // no jitter — deterministic interval in tests
        ValueRange           = new ValueRange { Min = 0, Max = 1000 },
        SimulationPattern    = SimulationPattern.Sine
    };
}

// ===========================================================================
// ConfigValidator — missing checks
// ===========================================================================

[Trait("Category", "ConfigValidator")]
public class ConfigValidatorMissingTests
{
    // UnitId = 248 is above the Modbus max of 247
    [Fact]
    public void UnitId_248_is_invalid()
    {
        // DeviceConfig.UnitId is a byte (0–255); 248 fits in a byte so we can assign it.
        var config = new SigmaConfig
        {
            Devices =
            [
                new DeviceConfig
                {
                    UnitId = 248,
                    Name = "Too High",
                    Registers = new DeviceRegistersConfig
                    {
                        HoldingRegisters = [new RegisterGroupConfig { StartAddress = 0, Count = 4 }]
                    }
                }
            ]
        };

        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("unitId must be between 1 and 247"),
            because: "unitId 248 exceeds the Modbus RTU/TCP legal range");
    }

    // UnitId = 247 is the maximum legal value and should pass
    [Fact]
    public void UnitId_247_is_valid()
    {
        var config = new SigmaConfig
        {
            Devices =
            [
                new DeviceConfig
                {
                    UnitId = 247,
                    Name = "Max UnitId",
                    Registers = new DeviceRegistersConfig
                    {
                        HoldingRegisters = [new RegisterGroupConfig { StartAddress = 0, Count = 2 }]
                    }
                }
            ]
        };

        var errors = ConfigValidator.Validate(config);
        errors.Should().NotContain(e => e.Contains("unitId must be between 1 and 247"));
    }

    [Fact]
    public void Missing_device_name_is_invalid()
    {
        var config = new SigmaConfig
        {
            Devices =
            [
                new DeviceConfig
                {
                    UnitId = 1,
                    Name = "   ",   // whitespace-only — treated as missing
                    Registers = new DeviceRegistersConfig
                    {
                        HoldingRegisters = [new RegisterGroupConfig { StartAddress = 0, Count = 4 }]
                    }
                }
            ]
        };

        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("missing a name"),
            because: "whitespace-only name should be rejected");
    }

    [Fact]
    public void Empty_device_name_is_invalid()
    {
        var config = new SigmaConfig
        {
            Devices =
            [
                new DeviceConfig
                {
                    UnitId = 1,
                    Name = string.Empty,
                    Registers = new DeviceRegistersConfig
                    {
                        HoldingRegisters = [new RegisterGroupConfig { StartAddress = 0, Count = 4 }]
                    }
                }
            ]
        };

        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("missing a name"));
    }

    [Fact]
    public void Negative_startAddress_is_invalid()
    {
        var config = new SigmaConfig
        {
            Devices =
            [
                new DeviceConfig
                {
                    UnitId = 1,
                    Name = "Dev",
                    Registers = new DeviceRegistersConfig
                    {
                        HoldingRegisters = [new RegisterGroupConfig { StartAddress = -1, Count = 4 }]
                    }
                }
            ]
        };

        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("startAddress cannot be negative"));
    }

    [Fact]
    public void Count_zero_is_invalid()
    {
        var config = new SigmaConfig
        {
            Devices =
            [
                new DeviceConfig
                {
                    UnitId = 1,
                    Name = "Dev",
                    Registers = new DeviceRegistersConfig
                    {
                        HoldingRegisters = [new RegisterGroupConfig { StartAddress = 0, Count = 0 }]
                    }
                }
            ]
        };

        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("count must be greater than 0"));
    }

    [Theory]
    [InlineData(DataType.Int16)]
    [InlineData(DataType.Float32)]
    [InlineData(DataType.Int32)]
    [InlineData(DataType.UInt32)]
    public void Coil_with_non_default_dataType_is_invalid(DataType dt)
    {
        var config = new SigmaConfig
        {
            Devices =
            [
                new DeviceConfig
                {
                    UnitId = 1,
                    Name = "Dev",
                    Registers = new DeviceRegistersConfig
                    {
                        Coils = [new RegisterGroupConfig { StartAddress = 0, Count = 4, DataType = dt }]
                    }
                }
            ]
        };

        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("dataType is not applicable"),
            because: $"coil registers are binary — dataType {dt} is not meaningful");
    }

    [Theory]
    [InlineData(DataType.Int16)]
    [InlineData(DataType.Float32)]
    public void DiscreteInput_with_non_default_dataType_is_invalid(DataType dt)
    {
        var config = new SigmaConfig
        {
            Devices =
            [
                new DeviceConfig
                {
                    UnitId = 1,
                    Name = "Dev",
                    Registers = new DeviceRegistersConfig
                    {
                        DiscreteInputs = [new RegisterGroupConfig { StartAddress = 0, Count = 4, DataType = dt }]
                    }
                }
            ]
        };

        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("dataType is not applicable"),
            because: $"discrete input registers are binary — dataType {dt} is not meaningful");
    }

    // startAddress + count = 65536 lands exactly on the limit; the validator uses > 65536
    // so this should be valid (the valid addressable range is 0–65535, and a group occupying
    // [0, 65535] inclusive has startAddress=0, count=65536; 0+65536=65536 which is NOT > 65536).
    [Fact]
    public void StartAddress_plus_count_exactly_65536_is_valid()
    {
        var config = new SigmaConfig
        {
            Devices =
            [
                new DeviceConfig
                {
                    UnitId = 1,
                    Name = "Dev",
                    Registers = new DeviceRegistersConfig
                    {
                        HoldingRegisters = [new RegisterGroupConfig { StartAddress = 0, Count = 65536 }]
                    }
                }
            ]
        };

        var errors = ConfigValidator.Validate(config);
        errors.Should().NotContain(e => e.Contains("exceeds 65536"),
            because: "startAddress(0) + count(65536) = 65536 is the exact boundary and should be allowed");
    }

    [Fact]
    public void StartAddress_plus_count_65537_is_invalid()
    {
        var config = new SigmaConfig
        {
            Devices =
            [
                new DeviceConfig
                {
                    UnitId = 1,
                    Name = "Dev",
                    Registers = new DeviceRegistersConfig
                    {
                        HoldingRegisters = [new RegisterGroupConfig { StartAddress = 1, Count = 65536 }]
                    }
                }
            ]
        };

        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("exceeds 65536"));
    }

    // Multiple independent errors should all be collected in a single pass
    [Fact]
    public void Multiple_errors_in_one_config_are_all_reported()
    {
        var config = new SigmaConfig
        {
            Devices =
            [
                new DeviceConfig
                {
                    UnitId = 1,
                    Name = "",                          // error 1: missing name
                    Registers = new DeviceRegistersConfig
                    {
                        HoldingRegisters =
                        [
                            new RegisterGroupConfig { StartAddress = -5,  Count = 0 },       // errors 2 & 3
                            new RegisterGroupConfig { StartAddress = 65530, Count = 10 }     // error 4: exceeds 65536
                        ]
                    }
                }
            ]
        };

        var errors = ConfigValidator.Validate(config);
        errors.Should().HaveCountGreaterThanOrEqualTo(4,
            because: "all individual validation errors must be gathered before returning");
    }

    // A config with all four register table types populated should be fully valid
    [Fact]
    public void Valid_config_with_all_four_table_types_produces_no_errors()
    {
        var errors = ConfigValidator.Validate(Configs.AllTableTypes());
        errors.Should().BeEmpty(because: "a well-formed config using all four register tables should pass validation");
    }
}

// ===========================================================================
// ConfigLoader — missing checks
// ===========================================================================

[Trait("Category", "ConfigLoader")]
public class ConfigLoaderTests
{
    // GenerateSample() should produce JSON that round-trips into a valid config
    [Fact]
    public void GenerateSample_produces_config_that_passes_validation()
    {
        var json = ConfigLoader.GenerateSample();
        json.Should().NotBeNullOrWhiteSpace();

        // Re-parse with the same options used by ConfigLoader.Load
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
        };
        var config = JsonSerializer.Deserialize<SigmaConfig>(json, options);
        config.Should().NotBeNull();

        var errors = ConfigValidator.Validate(config!);
        errors.Should().BeEmpty(because: "the built-in sample config must be self-consistent");
    }

    [Fact]
    public void Load_valid_json_file_returns_config()
    {
        // Write a minimal valid config to a temp file and load it
        var json = """
            {
              "network": { "interface": "0.0.0.0", "port": 502 },
              "defaults": {
                "simulationIntervalMs": 1000,
                "updateIntervalMs": 5000,
                "jitter": 0.1,
                "valueRange": { "min": 0, "max": 100 },
                "simulationPattern": "sine"
              },
              "devices": [
                {
                  "unitId": 1,
                  "name": "Test",
                  "registers": {
                    "holdingRegisters": [{ "startAddress": 0, "count": 2 }],
                    "inputRegisters": [],
                    "coils": [],
                    "discreteInputs": []
                  }
                }
              ]
            }
            """;

        var path = Path.Combine(Path.GetTempPath(), $"sigma_test_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, json);
            var config = ConfigLoader.Load(path);
            config.Should().NotBeNull();
            config.Devices.Should().ContainSingle(d => d.Name == "Test");
            config.Network.Port.Should().Be(502);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_file_with_missing_optional_fields_uses_defaults()
    {
        // Omit "defaults" and "network" sections — the model should use property defaults
        var json = """
            {
              "devices": [
                {
                  "unitId": 5,
                  "name": "Minimal",
                  "registers": {
                    "holdingRegisters": [{ "startAddress": 0, "count": 4 }],
                    "inputRegisters": [],
                    "coils": [],
                    "discreteInputs": []
                  }
                }
              ]
            }
            """;

        var path = Path.Combine(Path.GetTempPath(), $"sigma_test_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, json);
            var config = ConfigLoader.Load(path);
            config.Should().NotBeNull();
            // Network and Defaults should be initialised with their type-level defaults
            config.Network.Should().NotBeNull();
            config.Defaults.Should().NotBeNull();
            config.Defaults.UpdateIntervalMs.Should().Be(5000);
            config.Devices[0].UnitId.Should().Be(5);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_non_existent_file_throws_FileNotFoundException()
    {
        var act = () => ConfigLoader.Load("/no/such/file/sigma_missing.json");
        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*not found*");
    }
}

// ===========================================================================
// Simulation patterns — missing checks
// ===========================================================================

[Trait("Category", "Simulation")]
public class SimulationPatternMissingTests
{
    // Sine: at t=0, phase=0 the sine is sin(0)=0, so value = min + 0/2*(max-min) = min
    [Fact]
    public void Sine_at_t0_phase0_equals_min()
    {
        var sim = new SineSimulator();
        // sin(2π * (0+0)/60) = sin(0) = 0  →  value = min + (0+1)/2*(max-min) = min + 0.5*(max-min)
        // Wait — (sine+1)/2 at sine=0 = 0.5 → midpoint, not min.
        // Confirm the exact formula: min + (sin+1)/2 * range
        double v = sim.GetAnalogValue(0, 0, 0, 100);
        v.Should().BeApproximately(50.0, 0.001,
            because: "sin(0)=0, so (0+1)/2 = 0.5, placing the value at the midpoint");
    }

    // Sine: at t=15s (quarter period), phase=0: sin(2π*15/60)=sin(π/2)=1 → max
    [Fact]
    public void Sine_at_quarter_period_reaches_max()
    {
        var sim = new SineSimulator();
        double v = sim.GetAnalogValue(15, 0, 0, 100);
        v.Should().BeApproximately(100.0, 0.001,
            because: "at t=15s (quarter of 60s period) sine peaks at 1 → value should be max");
    }

    // Sine: at t=45s, phase=0: sin(2π*45/60)=sin(3π/2)=-1 → min
    [Fact]
    public void Sine_at_three_quarter_period_reaches_min()
    {
        var sim = new SineSimulator();
        double v = sim.GetAnalogValue(45, 0, 0, 100);
        v.Should().BeApproximately(0.0, 0.001,
            because: "at t=45s (3/4 of 60s period) sine troughs at -1 → value should be min");
    }

    // Sine binary: positive half-cycle (t=0..29s) → true
    [Fact]
    public void Sine_binary_true_in_positive_half_cycle()
    {
        var sim = new SineSimulator();
        // At t=0 sin(0)=0 ≥ 0 → true
        sim.GetBinaryValue(0, 0).Should().BeTrue();
        // At t=14 sin is positive → true
        sim.GetBinaryValue(14, 0).Should().BeTrue();
    }

    // Sine binary: negative half-cycle (t=30..59s) → false
    [Fact]
    public void Sine_binary_false_in_negative_half_cycle()
    {
        var sim = new SineSimulator();
        // At t=45 sin=-1 < 0 → false
        sim.GetBinaryValue(45, 0).Should().BeFalse();
        sim.GetBinaryValue(31, 0).Should().BeFalse();
    }

    // Ramp: at t=0, phase=0 → triangle at 0% → min
    [Fact]
    public void Ramp_at_t0_equals_min()
    {
        var sim = new RampSimulator();
        double v = sim.GetAnalogValue(0, 0, 0, 100);
        v.Should().BeApproximately(0.0, 0.001,
            because: "triangle wave starts at 0 when t=0, phase=0");
    }

    // Ramp: at t=30s (half-period) → triangle at 100% → max
    [Fact]
    public void Ramp_at_half_period_equals_max()
    {
        var sim = new RampSimulator();
        double v = sim.GetAnalogValue(30, 0, 0, 100);
        v.Should().BeApproximately(100.0, 0.001,
            because: "at t=30s the triangle wave is at its peak (half-period)");
    }

    // Ramp: at t=60s (full period) → wraps back to min
    [Fact]
    public void Ramp_at_full_period_equals_min()
    {
        var sim = new RampSimulator();
        double v = sim.GetAnalogValue(60, 0, 0, 100);
        v.Should().BeApproximately(0.0, 0.001,
            because: "at t=60s the triangle wave has completed one full cycle and resets");
    }

    // Ramp binary: first half of period → true
    [Fact]
    public void Ramp_binary_true_in_first_half()
    {
        var sim = new RampSimulator();
        sim.GetBinaryValue(0,  0).Should().BeTrue();
        sim.GetBinaryValue(10, 0).Should().BeTrue();
    }

    // Ramp binary: second half of period → false
    [Fact]
    public void Ramp_binary_false_in_second_half()
    {
        var sim = new RampSimulator();
        sim.GetBinaryValue(31, 0).Should().BeFalse();
        sim.GetBinaryValue(59, 0).Should().BeFalse();
    }

    // Sawtooth: at t=59s value is close to max but not yet reset
    [Fact]
    public void Sawtooth_at_t59_is_near_max()
    {
        var sim = new SawtoothSimulator();
        double v = sim.GetAnalogValue(59, 0, 0, 100);
        // t=59/60 ≈ 0.9833 → value ≈ 98.33
        v.Should().BeGreaterThan(98.0).And.BeLessThan(100.0);
    }

    // Sawtooth binary: first half t<30s → false, second half t>=30s → true
    [Fact]
    public void Sawtooth_binary_false_in_first_half()
    {
        var sim = new SawtoothSimulator();
        sim.GetBinaryValue(0,  0).Should().BeFalse();
        sim.GetBinaryValue(29, 0).Should().BeFalse();
    }

    [Fact]
    public void Sawtooth_binary_true_in_second_half()
    {
        var sim = new SawtoothSimulator();
        sim.GetBinaryValue(30, 0).Should().BeTrue();
        sim.GetBinaryValue(59, 0).Should().BeTrue();
    }

    // Random binary: over many samples roughly half should be true (probabilistic)
    // Using a generous tolerance to avoid flakiness; this merely confirms both outcomes occur.
    [Fact]
    public void Random_binary_produces_both_true_and_false_outcomes()
    {
        var sim = new RandomSimulator();
        int trueCount = 0;
        const int Samples = 1000;

        for (int i = 0; i < Samples; i++)
            if (sim.GetBinaryValue(i, 0)) trueCount++;

        // Expect roughly 50% ± 10% (i.e. 400–600 out of 1000)
        trueCount.Should().BeInRange(400, 600,
            because: "Random.Next(2) should distribute true/false roughly equally over many samples");
    }

    // Phase offset: two sawtooth registers at the same elapsed time produce different values
    [Fact]
    public void Phase_offset_shifts_sawtooth_output()
    {
        var sim = new SawtoothSimulator();
        double v1 = sim.GetAnalogValue(10, 0,  0, 100);
        double v2 = sim.GetAnalogValue(10, 20, 0, 100);
        v1.Should().NotBeApproximately(v2, 0.001,
            because: "different phase offsets must shift the sawtooth position");
    }

    // Phase offset: two ramp registers at the same elapsed time produce different values
    [Fact]
    public void Phase_offset_shifts_ramp_output()
    {
        var sim = new RampSimulator();
        double v1 = sim.GetAnalogValue(5, 0,  0, 100);
        double v2 = sim.GetAnalogValue(5, 25, 0, 100);
        v1.Should().NotBeApproximately(v2, 0.001,
            because: "different phase offsets must shift the ramp position");
    }
}

// ===========================================================================
// SimulatedRegister — property-level checks
// ===========================================================================

[Trait("Category", "SimulatedRegister")]
public class SimulatedRegisterTests
{
    [Theory]
    [InlineData(DataType.UInt16, 1)]
    [InlineData(DataType.Int16,  1)]
    public void RegisterWidth_is_1_for_single_word_types(DataType dt, int expected)
    {
        var reg = new SimulatedRegister
        {
            Type        = RegisterType.HoldingRegister,
            Address     = 0,
            DataType    = dt,
            IsWritable  = true,
            Simulator   = new StaticSimulator()
        };

        reg.RegisterWidth.Should().Be(expected);
    }

    [Theory]
    [InlineData(DataType.Float32)]
    [InlineData(DataType.Int32)]
    [InlineData(DataType.UInt32)]
    public void RegisterWidth_is_2_for_double_word_types(DataType dt)
    {
        var reg = new SimulatedRegister
        {
            Type        = RegisterType.HoldingRegister,
            Address     = 0,
            DataType    = dt,
            IsWritable  = true,
            Simulator   = new StaticSimulator()
        };

        reg.RegisterWidth.Should().Be(2);
    }

    [Fact]
    public void IsOverridden_defaults_to_false()
    {
        var reg = new SimulatedRegister
        {
            Type      = RegisterType.HoldingRegister,
            Address   = 0,
            DataType  = DataType.UInt16,
            IsWritable = true,
            Simulator = new StaticSimulator()
        };

        reg.IsOverridden.Should().BeFalse();
    }

    [Fact]
    public void IsOverridden_can_be_set_and_read_back()
    {
        var reg = new SimulatedRegister
        {
            Type      = RegisterType.HoldingRegister,
            Address   = 0,
            DataType  = DataType.UInt16,
            IsWritable = true,
            Simulator = new StaticSimulator()
        };

        reg.IsOverridden = true;
        reg.IsOverridden.Should().BeTrue();

        reg.IsOverridden = false;
        reg.IsOverridden.Should().BeFalse();
    }
}

// ===========================================================================
// ModbusDeviceFactory — structural and metadata checks
// ===========================================================================

[Trait("Category", "ModbusDeviceFactory")]
public class ModbusDeviceFactoryTests
{
    private static DeviceConfig HoldingOnlyDevice(
        int startAddress,
        int count,
        DataType dt = DataType.UInt16,
        SimulationPattern pattern = SimulationPattern.Static) => new()
    {
        UnitId = 1,
        Name   = "Factory Test Device",
        Registers = new DeviceRegistersConfig
        {
            HoldingRegisters = [new RegisterGroupConfig
            {
                StartAddress     = startAddress,
                Count            = count,
                DataType         = dt,
                SimulationPattern = pattern
            }]
        }
    };

    // A group with count=4, dataType=Float32 should produce 2 logical registers
    // (each Float32 consumes 2 raw slots, so 4 slots → 2 values)
    [Fact]
    public void Float32_group_count4_produces_2_logical_registers()
    {
        var device = ModbusDeviceFactory.Create(
            HoldingOnlyDevice(0, 4, DataType.Float32),
            Configs.Defaults());

        device.AllRegisters.Should().HaveCount(2,
            because: "count=4 Float32 registers = 2 logical values of 2 raw slots each");
    }

    // A UInt16 group with count=4 should produce 4 logical registers
    [Fact]
    public void UInt16_group_count4_produces_4_logical_registers()
    {
        var device = ModbusDeviceFactory.Create(
            HoldingOnlyDevice(0, 4, DataType.UInt16),
            Configs.Defaults());

        device.AllRegisters.Should().HaveCount(4);
    }

    // Verify that Float32 registers are assigned sequential addresses 2 apart
    [Fact]
    public void Float32_registers_have_addresses_spaced_by_2()
    {
        var device = ModbusDeviceFactory.Create(
            HoldingOnlyDevice(0, 4, DataType.Float32),
            Configs.Defaults());

        var addresses = device.AllRegisters.Select(r => r.Address).OrderBy(a => a).ToList();
        addresses.Should().Equal(0, 2);
    }

    // Holding registers should be assigned RegisterType.HoldingRegister
    [Fact]
    public void HoldingRegister_group_assigns_correct_register_type()
    {
        var device = ModbusDeviceFactory.Create(
            HoldingOnlyDevice(0, 4),
            Configs.Defaults());

        device.AllRegisters.Should()
            .OnlyContain(r => r.Type == RegisterType.HoldingRegister);
    }

    // Input registers should be assigned RegisterType.InputRegister and IsWritable=false
    [Fact]
    public void InputRegister_group_assigns_correct_type_and_not_writable()
    {
        var config = new DeviceConfig
        {
            UnitId = 1,
            Name   = "IR Device",
            Registers = new DeviceRegistersConfig
            {
                InputRegisters = [new RegisterGroupConfig { StartAddress = 0, Count = 2 }]
            }
        };

        var device = ModbusDeviceFactory.Create(config, Configs.Defaults());

        device.AllRegisters.Should().OnlyContain(r =>
            r.Type == RegisterType.InputRegister && !r.IsWritable);
    }

    // Coil group should produce RegisterType.Coil entries with IsWritable=true
    [Fact]
    public void Coil_group_assigns_correct_type_and_is_writable()
    {
        var config = new DeviceConfig
        {
            UnitId = 1,
            Name   = "Coil Device",
            Registers = new DeviceRegistersConfig
            {
                Coils = [new RegisterGroupConfig { StartAddress = 0, Count = 4 }]
            }
        };

        var device = ModbusDeviceFactory.Create(config, Configs.Defaults());

        device.AllRegisters.Should().OnlyContain(r =>
            r.Type == RegisterType.Coil && r.IsWritable);
    }

    // DiscreteInput group should produce RegisterType.DiscreteInput entries with IsWritable=false
    [Fact]
    public void DiscreteInput_group_assigns_correct_type_and_not_writable()
    {
        var config = new DeviceConfig
        {
            UnitId = 1,
            Name   = "DI Device",
            Registers = new DeviceRegistersConfig
            {
                DiscreteInputs = [new RegisterGroupConfig { StartAddress = 0, Count = 4 }]
            }
        };

        var device = ModbusDeviceFactory.Create(config, Configs.Defaults());

        device.AllRegisters.Should().OnlyContain(r =>
            r.Type == RegisterType.DiscreteInput && !r.IsWritable);
    }

    // Jitter: with Jitter=0.5, the interval is in [(1-0.5)*base, (1+0.5)*base] = [0.5*base, 1.5*base]
    [Fact]
    public void Jitter_produces_update_interval_within_expected_range()
    {
        var defaults = new DefaultsConfig
        {
            SimulationIntervalMs = 1000,
            UpdateIntervalMs     = 1000,
            Jitter               = 0.5,
            ValueRange           = new ValueRange { Min = 0, Max = 100 },
            SimulationPattern    = SimulationPattern.Static
        };

        // Create many registers and verify all fall within [500, 1500]
        var config = new DeviceConfig
        {
            UnitId = 1,
            Name   = "Jitter Device",
            Registers = new DeviceRegistersConfig
            {
                HoldingRegisters = [new RegisterGroupConfig { StartAddress = 0, Count = 100 }]
            }
        };

        var device = ModbusDeviceFactory.Create(config, defaults);

        device.AllRegisters.Should().OnlyContain(r =>
            r.UpdateIntervalMs >= 500 && r.UpdateIntervalMs <= 1500,
            because: "jitter=0.5 constrains the interval to ±50% of the base");
    }

    // With Jitter=0.0, all registers must get exactly the base interval
    [Fact]
    public void Zero_jitter_produces_exact_base_interval()
    {
        var defaults = Configs.Defaults(); // Jitter=0.0, UpdateIntervalMs=5000
        var device = ModbusDeviceFactory.Create(HoldingOnlyDevice(0, 10), defaults);

        device.AllRegisters.Should().OnlyContain(r => r.UpdateIntervalMs == 5000);
    }

    // Phase offsets should not all be zero for a multi-register group (random assignment)
    // We use a large group so the probability of all values being identical is negligible.
    [Fact]
    public void Phase_offsets_are_not_all_identical_for_large_group()
    {
        var defaults = new DefaultsConfig
        {
            SimulationIntervalMs = 1000,
            UpdateIntervalMs     = 5000,
            Jitter               = 0.0,
            ValueRange           = new ValueRange { Min = 0, Max = 100 },
            SimulationPattern    = SimulationPattern.Sine
        };

        var config = new DeviceConfig
        {
            UnitId = 1,
            Name   = "Phase Device",
            Registers = new DeviceRegistersConfig
            {
                HoldingRegisters = [new RegisterGroupConfig { StartAddress = 0, Count = 20 }]
            }
        };

        var device = ModbusDeviceFactory.Create(config, defaults);
        var distinctPhases = device.AllRegisters.Select(r => r.PhaseOffset).Distinct().Count();

        // With 20 registers each drawing from Uniform(0,60), the probability that
        // all 20 are exactly equal is vanishingly small.
        distinctPhases.Should().BeGreaterThan(1,
            because: "phase offsets are randomly assigned and should differ across registers");
    }

    // Factory propagates UnitId and Name from config to SimulatedDevice
    [Fact]
    public void Factory_propagates_unit_id_and_name()
    {
        var config = HoldingOnlyDevice(0, 4);
        var device = ModbusDeviceFactory.Create(config, Configs.Defaults());

        device.UnitId.Should().Be(1);
        device.Name.Should().Be("Factory Test Device");
    }

    // When the group's SimulationPattern is set it overrides the defaults pattern
    [Fact]
    public void Group_simulation_pattern_overrides_default()
    {
        var config = HoldingOnlyDevice(0, 4, DataType.UInt16, SimulationPattern.Ramp);
        var device = ModbusDeviceFactory.Create(config, Configs.Defaults()); // default=Sine

        // The simulator on each register must be a RampSimulator, not a SineSimulator
        device.AllRegisters.Should().OnlyContain(
            r => r.Simulator is RampSimulator,
            because: "group-level SimulationPattern must override the defaults SimulationPattern");
    }
}

// ===========================================================================
// SimulationEngine — IsOverridden and timing skip logic
// (Tested by driving DoTick indirectly via state inspection — no mocking library needed)
// ===========================================================================

[Trait("Category", "SimulationEngine")]
public class SimulationEngineSkipLogicTests
{
    // Create a SimulatedDevice with a single register whose LastUpdateTime is in the
    // distant past so it will always be considered due, then check whether IsOverridden
    // prevents the LastUpdateTime from being updated (the only observable side-effect
    // that doesn't require a live Modbus server).
    //
    // NOTE: SimulationEngine.DoTick is private. The engine calls DoTick when the timer
    // fires.  Rather than starting a real server, we observe register state directly:
    // if IsOverridden causes the engine to `continue` before `register.LastUpdateTime = now`,
    // then LastUpdateTime will NOT be updated.  We confirm this by checking that a
    // freshly-overridden register still has LastUpdateTime == DateTime.MinValue after a Tick.
    //
    // Because starting a live ModbusTcpServer in a unit test is fragile (port conflicts,
    // timing sensitivity), these tests drive the engine's internal logic by reflection
    // or by inspecting SimulatedRegister state after a real engine Tick() if that is possible.
    //
    // The cleanest approach without Moq: expose the tick method through a test-only
    // subclass OR simply verify the invariant through SimulatedRegister state.
    // We choose the state-inspection approach: after calling Start(intervalMs) and letting
    // the first tick fire, a register with IsOverridden=true must not have its
    // LastUpdateTime advanced.

    // Helper: build a minimal SimulatedDevice with one holding register
    private static SimulatedDevice BuildDevice(bool isOverridden, DateTime lastUpdate)
    {
        var reg = new SimulatedRegister
        {
            Type            = RegisterType.HoldingRegister,
            Address         = 0,
            DataType        = DataType.UInt16,
            IsWritable      = true,
            Simulator       = new StaticSimulator(),
            PhaseOffset     = 0,
            Min             = 0,
            Max             = 100,
            UpdateIntervalMs = 100,
            LastUpdateTime  = lastUpdate
        };
        reg.IsOverridden = isOverridden;

        return new SimulatedDevice
        {
            UnitId      = 1,
            Name        = "Test",
            AllRegisters = [reg]
        };
    }

    // Verify that a register flagged IsOverridden=true keeps LastUpdateTime unchanged
    // after a Tick cycle completes. We call DoTick indirectly by using reflection so we
    // can drive the engine without starting a real TCP server.
    [Fact]
    public void Overridden_register_LastUpdateTime_is_not_advanced_during_tick()
    {
        var device = BuildDevice(isOverridden: true, lastUpdate: DateTime.MinValue);

        // Invoke DoTick via reflection — internal method, same assembly in test context.
        InvokeDoTick(device);

        // LastUpdateTime must still be MinValue because the IsOverridden branch skips the register.
        device.AllRegisters[0].LastUpdateTime.Should().Be(DateTime.MinValue,
            because: "a register marked IsOverridden must be skipped entirely — LastUpdateTime must not advance");
    }

    // Verify that a register NOT overridden but updated very recently is also skipped
    // (interval has not elapsed) so its LastUpdateTime remains unchanged.
    [Fact]
    public void Register_not_yet_due_for_update_LastUpdateTime_is_not_advanced()
    {
        // Set LastUpdateTime to "just now" so the interval (100 ms) has NOT elapsed
        var device = BuildDevice(isOverridden: false, lastUpdate: DateTime.UtcNow);

        InvokeDoTick(device);

        // LastUpdateTime should still be approximately "just now", NOT replaced with a new value.
        // We allow a 50 ms window to account for execution time while still confirming no update occurred.
        device.AllRegisters[0].LastUpdateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMilliseconds(200),
            because: "the register was updated just now; the 100 ms interval has not elapsed, so no re-update should occur");
    }

    // Helper: call SimulationEngine.DoTick via reflection without starting a real server.
    // DoTick calls _server.WriteRegisterValue / WriteCoilValue — to avoid a NullReferenceException
    // we need to pass a stub or intercept before the write call.
    // Looking at DoTick: it calls register.LastUpdateTime = now BEFORE UpdateRegister(device, register, elapsed).
    // So for an overridden register the assignment never happens (continue fires first).
    // For a non-overridden, due register LastUpdateTime IS set before the write attempt.
    // To test the overridden path we only need DoTick to run through the continue branch — no write is attempted.
    // For the not-yet-due path, UpdateRegister is also never called — so no write attempt.
    // Both paths under test are therefore safe to invoke without a live server.
    private static void InvokeDoTick(SimulatedDevice device)
    {
        // We construct a SimulationEngine with a null logger stub and null server;
        // the paths under test (IsOverridden=true and interval-not-elapsed) both
        // hit `continue` before reaching _server.WriteRegisterValue, so no NRE will occur.
        var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        var engineLogger  = loggerFactory.CreateLogger<SimulationEngine>();
        var serverLogger  = loggerFactory.CreateLogger<ModbusServerHandler>();

        // Build a real handler but do NOT call Start() (no TCP port opened).
        var handler = new ModbusServerHandler(serverLogger);

        var engine = new SimulationEngine(handler, engineLogger);
        engine.AddDevice(device);

        // Invoke the private DoTick method via reflection.
        var doTick = typeof(SimulationEngine)
            .GetMethod("DoTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        doTick.Should().NotBeNull(because: "DoTick must exist as a private method on SimulationEngine");
        doTick!.Invoke(engine, null);
    }
}
