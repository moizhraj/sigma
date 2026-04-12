using FluentAssertions;
using Sigma.Core.Configuration;
using Sigma.Core.Enums;

namespace Sigma.Tests;

public class ConfigValidatorTests
{
    private static SigmaConfig ValidConfig() => new()
    {
        Devices =
        [
            new DeviceConfig
            {
                UnitId = 1,
                Name = "Test Device",
                Registers = new DeviceRegistersConfig
                {
                    HoldingRegisters = [new RegisterGroupConfig { StartAddress = 0, Count = 4 }]
                }
            }
        ]
    };

    [Fact]
    public void Valid_config_produces_no_errors()
    {
        var errors = ConfigValidator.Validate(ValidConfig());
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Empty_devices_list_is_invalid()
    {
        var config = new SigmaConfig { Devices = [] };
        var errors = ConfigValidator.Validate(config);
        errors.Should().ContainSingle(e => e.Contains("At least one device"));
    }

    [Fact]
    public void Duplicate_unit_ids_are_invalid()
    {
        var config = ValidConfig();
        config.Devices.Add(new DeviceConfig
        {
            UnitId = 1,
            Name = "Duplicate",
            Registers = new DeviceRegistersConfig
            {
                HoldingRegisters = [new RegisterGroupConfig { StartAddress = 0, Count = 2 }]
            }
        });
        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("Duplicate unitId"));
    }

    [Fact]
    public void UnitId_zero_is_invalid()
    {
        var config = ValidConfig();
        config.Devices[0].UnitId = 0;
        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("unitId must be between 1 and 247"));
    }

    [Fact]
    public void Overlapping_holding_registers_are_invalid()
    {
        var config = ValidConfig();
        config.Devices[0].Registers.HoldingRegisters =
        [
            new RegisterGroupConfig { StartAddress = 0, Count = 10 },
            new RegisterGroupConfig { StartAddress = 5, Count = 10 }
        ];
        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("overlap"));
    }

    [Fact]
    public void Non_overlapping_groups_are_valid()
    {
        var config = ValidConfig();
        config.Devices[0].Registers.HoldingRegisters =
        [
            new RegisterGroupConfig { StartAddress = 0, Count = 10 },
            new RegisterGroupConfig { StartAddress = 10, Count = 10 }
        ];
        var errors = ConfigValidator.Validate(config);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Float32_with_odd_count_is_invalid()
    {
        var config = ValidConfig();
        config.Devices[0].Registers.HoldingRegisters =
        [
            new RegisterGroupConfig { StartAddress = 0, Count = 5, DataType = DataType.Float32 }
        ];
        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("count must be even"));
    }

    [Fact]
    public void Float32_with_even_count_is_valid()
    {
        var config = ValidConfig();
        config.Devices[0].Registers.HoldingRegisters =
        [
            new RegisterGroupConfig { StartAddress = 0, Count = 6, DataType = DataType.Float32 }
        ];
        var errors = ConfigValidator.Validate(config);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Address_range_exceeding_65536_is_invalid()
    {
        var config = ValidConfig();
        config.Devices[0].Registers.HoldingRegisters =
        [
            new RegisterGroupConfig { StartAddress = 65530, Count = 10 }
        ];
        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("exceeds 65536"));
    }

    [Fact]
    public void UpdateIntervalMs_below_100_is_invalid()
    {
        var config = ValidConfig();
        config.Devices[0].Registers.HoldingRegisters =
        [
            new RegisterGroupConfig { StartAddress = 0, Count = 4, UpdateIntervalMs = 50 }
        ];
        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("updateIntervalMs must be at least 100ms"));
    }

    [Fact]
    public void Device_with_no_registers_is_invalid()
    {
        var config = new SigmaConfig
        {
            Devices =
            [
                new DeviceConfig { UnitId = 1, Name = "Empty", Registers = new DeviceRegistersConfig() }
            ]
        };
        var errors = ConfigValidator.Validate(config);
        errors.Should().Contain(e => e.Contains("at least one register group"));
    }
}
