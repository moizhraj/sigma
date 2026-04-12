using System.Buffers.Binary;
using System.Net;
using FluentModbus;
using Microsoft.Extensions.Logging;
using Sigma.Core.Enums;
using Sigma.Engine.Models;

namespace Sigma.Engine.Services;

/// <summary>
/// Wraps the FluentModbus TCP server and keeps register memory in sync
/// with SimulatedDevice data. Also exposes the server for DI consumers.
/// </summary>
public class ModbusServerHandler : IDisposable
{
    private readonly ModbusTcpServer _server;
    private readonly ILogger<ModbusServerHandler> _logger;
    private readonly List<SimulatedDevice> _devices = [];
    private bool _disposed;

    public ModbusServerHandler(ILogger<ModbusServerHandler> logger)
    {
        _logger = logger;
        _server = new ModbusTcpServer();
    }

    public void RegisterDevice(SimulatedDevice device)
    {
        _devices.Add(device);
        _logger.LogDebug("Registered device '{Name}' with unitId={UnitId}", device.Name, device.UnitId);
    }

    public IReadOnlyList<SimulatedDevice> Devices => _devices;

    /// <summary>
    /// Fires before every incoming Modbus request is processed.
    /// Args: unitId, functionCode, startAddress, quantity.
    /// </summary>
    public event Action<byte, ModbusFunctionCode, ushort, ushort>? OnRequest;

    public void Start(string ipAddress, int port)
    {
        // Hook every incoming request so the dashboard can count reads/writes.
        // Return ModbusExceptionCode.OK (0x00) to allow all requests through.
        _server.RequestValidator = (unitId, fc, address, count) =>
        {
            try { OnRequest?.Invoke(unitId, fc, address, count); }
            catch { /* never let a display error break request processing */ }
            return ModbusExceptionCode.OK;
        };

        var endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
        _server.Start(endpoint);
        _logger.LogInformation("Modbus TCP server listening on {Endpoint}", endpoint);
    }

    public void Stop()
    {
        _server.Stop();
        _logger.LogInformation("Modbus TCP server stopped.");
    }

    /// <summary>
    /// Writes a computed analog value for a register into the server's memory buffer.
    /// </summary>
    public void WriteRegisterValue(SimulatedDevice device, SimulatedRegister register, double value)
    {
        try
        {
            switch (register.Type)
            {
                case RegisterType.HoldingRegister:
                    WriteAnalogToSpan(_server.GetHoldingRegisters(device.UnitId), register, value);
                    break;
                case RegisterType.InputRegister:
                    WriteAnalogToSpan(_server.GetInputRegisters(device.UnitId), register, value);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write register unitId={UnitId} addr={Address}", device.UnitId, register.Address);
        }
    }

    /// <summary>
    /// Writes a computed binary value for a coil/discrete input into server memory.
    /// FluentModbus stores coils as bytes (0x00 = false, 0xFF = true).
    /// </summary>
    public void WriteCoilValue(SimulatedDevice device, SimulatedRegister register, bool value)
    {
        try
        {
            byte byteValue = value ? (byte)0xFF : (byte)0x00;
            switch (register.Type)
            {
                case RegisterType.Coil:
                    _server.GetCoils(device.UnitId)[register.Address] = byteValue;
                    break;
                case RegisterType.DiscreteInput:
                    _server.GetDiscreteInputs(device.UnitId)[register.Address] = byteValue;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write coil unitId={UnitId} addr={Address}", device.UnitId, register.Address);
        }
    }

    private static void WriteAnalogToSpan(Span<short> span, SimulatedRegister register, double value)
    {
        switch (register.DataType)
        {
            case DataType.UInt16:
                span[register.Address] = (short)(ushort)Math.Clamp(value, ushort.MinValue, ushort.MaxValue);
                break;

            case DataType.Int16:
                span[register.Address] = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
                break;

            case DataType.Float32:
            {
                Span<byte> bytes = stackalloc byte[4];
                BinaryPrimitives.WriteSingleBigEndian(bytes, (float)value);
                span[register.Address]     = BinaryPrimitives.ReadInt16BigEndian(bytes[..2]);
                span[register.Address + 1] = BinaryPrimitives.ReadInt16BigEndian(bytes[2..]);
                break;
            }

            case DataType.Int32:
            {
                Span<byte> bytes = stackalloc byte[4];
                BinaryPrimitives.WriteInt32BigEndian(bytes, (int)Math.Clamp(value, int.MinValue, int.MaxValue));
                span[register.Address]     = BinaryPrimitives.ReadInt16BigEndian(bytes[..2]);
                span[register.Address + 1] = BinaryPrimitives.ReadInt16BigEndian(bytes[2..]);
                break;
            }

            case DataType.UInt32:
            {
                Span<byte> bytes = stackalloc byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)Math.Clamp(value, uint.MinValue, uint.MaxValue));
                span[register.Address]     = BinaryPrimitives.ReadInt16BigEndian(bytes[..2]);
                span[register.Address + 1] = BinaryPrimitives.ReadInt16BigEndian(bytes[2..]);
                break;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _server.Dispose();
    }
}
