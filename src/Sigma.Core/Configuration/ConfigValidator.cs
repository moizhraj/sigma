using Sigma.Core.Enums;

namespace Sigma.Core.Configuration;

public static class ConfigValidator
{
    public static List<string> Validate(SigmaConfig config)
    {
        var errors = new List<string>();

        if (config.Devices.Count == 0)
        {
            errors.Add("At least one device is required.");
            return errors;
        }

        var unitIds = new HashSet<byte>();
        foreach (var device in config.Devices)
        {
            errors.AddRange(ValidateDevice(device, unitIds));
        }

        return errors;
    }

    private static List<string> ValidateDevice(DeviceConfig device, HashSet<byte> seenUnitIds)
    {
        var errors = new List<string>();
        var prefix = $"Device '{device.Name}' (unitId={device.UnitId})";

        if (string.IsNullOrWhiteSpace(device.Name))
            errors.Add($"Device with unitId={device.UnitId} is missing a name.");

        if (device.UnitId == 0)
            errors.Add($"{prefix}: unitId must be between 1 and 247.");

        if (!seenUnitIds.Add(device.UnitId))
            errors.Add($"Duplicate unitId={device.UnitId}. Unit IDs must be unique.");

        var allGroups =
            device.Registers.HoldingRegisters.Select(g => (RegisterType.HoldingRegister, g))
            .Concat(device.Registers.InputRegisters.Select(g => (RegisterType.InputRegister, g)))
            .Concat(device.Registers.Coils.Select(g => (RegisterType.Coil, g)))
            .Concat(device.Registers.DiscreteInputs.Select(g => (RegisterType.DiscreteInput, g)))
            .ToList();

        if (allGroups.Count == 0)
            errors.Add($"{prefix}: must have at least one register group.");

        // Validate each group individually
        foreach (var (type, group) in allGroups)
            errors.AddRange(ValidateRegisterGroup(group, type, prefix));

        // Check for address overlaps within each register type
        errors.AddRange(ValidateNoOverlaps(device.Registers.HoldingRegisters, "holdingRegisters", prefix));
        errors.AddRange(ValidateNoOverlaps(device.Registers.InputRegisters, "inputRegisters", prefix));
        errors.AddRange(ValidateNoOverlaps(device.Registers.Coils, "coils", prefix));
        errors.AddRange(ValidateNoOverlaps(device.Registers.DiscreteInputs, "discreteInputs", prefix));

        return errors;
    }

    private static List<string> ValidateRegisterGroup(RegisterGroupConfig group, RegisterType type, string devicePrefix)
    {
        var errors = new List<string>();
        var prefix = $"{devicePrefix} [{type} @ {group.StartAddress}]";

        if (group.Count <= 0)
            errors.Add($"{prefix}: count must be greater than 0.");

        if (group.StartAddress < 0)
            errors.Add($"{prefix}: startAddress cannot be negative.");

        if (group.StartAddress + group.Count > 65536)
            errors.Add($"{prefix}: startAddress ({group.StartAddress}) + count ({group.Count}) exceeds 65536.");

        if (group.UpdateIntervalMs.HasValue && group.UpdateIntervalMs.Value < 100)
            errors.Add($"{prefix}: updateIntervalMs must be at least 100ms.");

        // Float32/Int32/UInt32 consume 2 registers per value — count must be even
        if (group.DataType is DataType.Float32 or DataType.Int32 or DataType.UInt32)
        {
            if (group.Count % 2 != 0)
                errors.Add($"{prefix}: count must be even for dataType {group.DataType} (each value uses 2 registers).");
        }

        // Coils and discrete inputs are binary — dataType is not applicable
        if (type is RegisterType.Coil or RegisterType.DiscreteInput)
        {
            if (group.DataType != DataType.UInt16)
                errors.Add($"{prefix}: dataType is not applicable for {type}. Remove the dataType field.");
        }

        return errors;
    }

    private static List<string> ValidateNoOverlaps(List<RegisterGroupConfig> groups, string tableName, string devicePrefix)
    {
        var errors = new List<string>();

        for (int i = 0; i < groups.Count; i++)
        {
            for (int j = i + 1; j < groups.Count; j++)
            {
                var a = groups[i];
                var b = groups[j];

                int aEnd = a.StartAddress + a.Count - 1;
                int bEnd = b.StartAddress + b.Count - 1;

                bool overlaps = a.StartAddress <= bEnd && b.StartAddress <= aEnd;
                if (overlaps)
                {
                    errors.Add(
                        $"{devicePrefix} [{tableName}]: register groups overlap. " +
                        $"Group at address {a.StartAddress}–{aEnd} overlaps with {b.StartAddress}–{bEnd}.");
                }
            }
        }

        return errors;
    }
}
