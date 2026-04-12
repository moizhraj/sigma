using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sigma.Core.Configuration;
using Sigma.Engine.Factories;
using Sigma.Engine.Models;

namespace Sigma.Engine.Services;

public class SimulatorHostedService : IHostedService
{
    private readonly SigmaConfig _config;
    private readonly ModbusServerHandler _server;
    private readonly SimulationEngine _engine;
    private readonly ILogger<SimulatorHostedService> _logger;

    public List<SimulatedDevice> Devices { get; } = [];

    public SimulatorHostedService(
        SigmaConfig config,
        ModbusServerHandler server,
        SimulationEngine engine,
        ILogger<SimulatorHostedService> logger)
    {
        _config = config;
        _server = server;
        _engine = engine;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting sigma — {Count} device(s) configured", _config.Devices.Count);

        foreach (var deviceConfig in _config.Devices)
        {
            var device = ModbusDeviceFactory.Create(deviceConfig, _config.Defaults);
            Devices.Add(device);
            _server.RegisterDevice(device);
            _engine.AddDevice(device);
            _logger.LogInformation(
                "  Device '{Name}' (unitId={UnitId}) — {RegisterCount} logical register(s)",
                device.Name, device.UnitId, device.AllRegisters.Length);
        }

        _server.Start(_config.Network.Interface, _config.Network.Port);
        _engine.Start(_config.Defaults.SimulationIntervalMs);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping sigma…");
        _engine.Stop();
        _server.Stop();
        return Task.CompletedTask;
    }
}
