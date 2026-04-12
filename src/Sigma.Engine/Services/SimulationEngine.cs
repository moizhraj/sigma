using Microsoft.Extensions.Logging;
using Sigma.Core.Enums;
using Sigma.Engine.Models;

namespace Sigma.Engine.Services;

/// <summary>
/// Periodic timer loop that drives value updates for all simulated registers.
/// On each tick, checks each register to see if its update interval has elapsed,
/// computes the new value, and writes it to the Modbus server memory.
/// </summary>
public class SimulationEngine : IDisposable
{
    private readonly ModbusServerHandler _server;
    private readonly ILogger<SimulationEngine> _logger;
    private readonly List<SimulatedDevice> _devices = [];
    private readonly DateTime _startTime = DateTime.UtcNow;
    private Timer? _timer;
    private bool _disposed;

    public SimulationEngine(ModbusServerHandler server, ILogger<SimulationEngine> logger)
    {
        _server = server;
        _logger = logger;
    }

    public void AddDevice(SimulatedDevice device) => _devices.Add(device);

    public void Start(int intervalMs)
    {
        _logger.LogInformation("Simulation engine starting (tick interval: {IntervalMs}ms)", intervalMs);
        _timer = new Timer(Tick, null, 0, intervalMs);
    }

    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        _logger.LogInformation("Simulation engine stopped.");
    }

    private void Tick(object? _)
    {
        double elapsed = (DateTime.UtcNow - _startTime).TotalSeconds;

        foreach (var device in _devices)
        {
            foreach (var register in device.AllRegisters)
            {
                try
                {
                    if (register.IsOverridden)
                        continue;

                    var now = DateTime.UtcNow;
                    if ((now - register.LastUpdateTime).TotalMilliseconds < register.UpdateIntervalMs)
                        continue;

                    register.LastUpdateTime = now;
                    UpdateRegister(device, register, elapsed);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error updating register unitId={UnitId} addr={Address}", device.UnitId, register.Address);
                }
            }
        }
    }

    private void UpdateRegister(SimulatedDevice device, SimulatedRegister register, double elapsed)
    {
        // Determine value range from the simulator — use defaults embedded at creation time.
        // The register stores min/max via its simulator but we need the range to pass in.
        // Since we don't store range on register directly (simulator is stateless), we pass
        // 0/1 for binary and use the simulator's analog range stored at construction.
        // To keep it simple, ranges are baked into each register.
        if (register.Type is RegisterType.Coil or RegisterType.DiscreteInput)
        {
            bool value = register.Simulator.GetBinaryValue(elapsed, register.PhaseOffset);
            _server.WriteCoilValue(device, register, value);
        }
        else
        {
            double value = register.Simulator.GetAnalogValue(elapsed, register.PhaseOffset, register.Min, register.Max);
            _server.WriteRegisterValue(device, register, value);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
    }
}
