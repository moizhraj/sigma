using Sigma.Core.Configuration;
using Sigma.Core.Enums;
using Sigma.Core.Simulation;
using Sigma.Engine.Models;

namespace Sigma.Engine.Factories;

public static class ModbusDeviceFactory
{
    private static readonly Random Rng = new();

    public static SimulatedDevice Create(DeviceConfig config, DefaultsConfig defaults)
    {
        var registers = new List<SimulatedRegister>();

        registers.AddRange(BuildGroup(config.Registers.HoldingRegisters, RegisterType.HoldingRegister, isWritable: true, defaults));
        registers.AddRange(BuildGroup(config.Registers.InputRegisters, RegisterType.InputRegister, isWritable: false, defaults));
        registers.AddRange(BuildGroup(config.Registers.Coils, RegisterType.Coil, isWritable: true, defaults));
        registers.AddRange(BuildGroup(config.Registers.DiscreteInputs, RegisterType.DiscreteInput, isWritable: false, defaults));

        return new SimulatedDevice
        {
            UnitId = config.UnitId,
            Name = config.Name,
            Description = config.Description,
            AllRegisters = [.. registers]
        };
    }

    private static IEnumerable<SimulatedRegister> BuildGroup(
        List<RegisterGroupConfig> groups,
        RegisterType type,
        bool isWritable,
        DefaultsConfig defaults)
    {
        foreach (var group in groups)
        {
            var pattern = group.SimulationPattern ?? defaults.SimulationPattern;
            var range = group.ValueRange ?? defaults.ValueRange;
            var updateMs = group.UpdateIntervalMs ?? defaults.UpdateIntervalMs;
            var dataType = (type is RegisterType.Coil or RegisterType.DiscreteInput)
                ? DataType.UInt16  // binary; dataType not meaningful
                : group.DataType;

            // How many logical values does this group represent?
            int registerWidth = dataType is DataType.Float32 or DataType.Int32 or DataType.UInt32 ? 2 : 1;
            int valueCount = group.Count / registerWidth;

            for (int i = 0; i < valueCount; i++)
            {
                int address = group.StartAddress + i * registerWidth;

                // Jitter: actual update interval varies between (1-jitter)*base and (1+jitter)*base
                double jitterFactor = (1.0 - defaults.Jitter) + Rng.NextDouble() * (2.0 * defaults.Jitter);
                int jitteredMs = Math.Max(100, (int)(updateMs * jitterFactor));

                // Random phase offset so registers don't oscillate in lockstep
                double phaseOffset = Rng.NextDouble() * 60.0;

                yield return new SimulatedRegister
                {
                    Type = type,
                    Address = address,
                    DataType = dataType,
                    IsWritable = isWritable,
                    Simulator = ValueSimulatorFactory.Create(pattern),
                    PhaseOffset = phaseOffset,
                    Min = range.Min,
                    Max = range.Max,
                    UpdateIntervalMs = jitteredMs
                };
            }
        }
    }
}
