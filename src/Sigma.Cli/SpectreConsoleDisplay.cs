using FluentModbus;
using Spectre.Console;
using Spectre.Console.Rendering;
using Sigma.Core.Configuration;
using Sigma.Engine.Models;

namespace Sigma.Cli;

public class SpectreConsoleDisplay
{
    private readonly IReadOnlyList<SimulatedDevice> _devices;
    private readonly SigmaConfig _config;
    private readonly DateTime _startTime = DateTime.UtcNow;

    // Activity log — last N entries
    private readonly Queue<string> _log = new();
    private readonly object _logLock = new();
    private const int MaxLogLines = 14;

    // Per-device rate tracking (reads + writes per second)
    private readonly double[] _rates;
    private readonly DateTime[] _rateWindowStart;
    private readonly long[] _rateWindowTotal;

    // Write-function codes
    private static readonly HashSet<ModbusFunctionCode> WriteCodes =
    [
        ModbusFunctionCode.WriteSingleCoil,
        ModbusFunctionCode.WriteSingleRegister,
        ModbusFunctionCode.WriteMultipleCoils,
        ModbusFunctionCode.WriteMultipleRegisters
    ];

    public SpectreConsoleDisplay(IReadOnlyList<SimulatedDevice> devices, SigmaConfig config)
    {
        _devices = devices;
        _config = config;
        _rates = new double[devices.Count];
        _rateWindowStart = Enumerable.Repeat(DateTime.UtcNow, devices.Count).ToArray();
        _rateWindowTotal = new long[devices.Count];
    }

    /// <summary>
    /// Called from the ModbusServerHandler.OnRequest event (network thread).
    /// Updates per-device stats and adds an entry to the activity log.
    /// </summary>
    public void HandleRequest(byte unitId, ModbusFunctionCode fc, ushort address, ushort count)
    {
        var device = _devices.FirstOrDefault(d => d.UnitId == unitId);
        if (device != null)
        {
            if (WriteCodes.Contains(fc))
                device.Stats.RecordWrite();
            else
                device.Stats.RecordRead();
        }

        var fcLabel = fc switch
        {
            ModbusFunctionCode.ReadCoils                => "FC01 Read Coils",
            ModbusFunctionCode.ReadDiscreteInputs       => "FC02 Read Discrete Inputs",
            ModbusFunctionCode.ReadHoldingRegisters     => "FC03 Read Holding Registers",
            ModbusFunctionCode.ReadInputRegisters       => "FC04 Read Input Registers",
            ModbusFunctionCode.WriteSingleCoil          => "FC05 Write Single Coil",
            ModbusFunctionCode.WriteSingleRegister      => "FC06 Write Single Register",
            ModbusFunctionCode.WriteMultipleCoils       => "FC15 Write Multiple Coils",
            ModbusFunctionCode.WriteMultipleRegisters   => "FC16 Write Multiple Registers",
            _ => $"FC{(int)fc:D2}"
        };

        var entry = $"[{DateTime.Now:HH:mm:ss}] {fcLabel,-34} unitId={unitId} addr={address} qty={count}";
        lock (_logLock)
        {
            _log.Enqueue(entry);
            if (_log.Count > MaxLogLines) _log.Dequeue();
        }
    }

    public async Task RunAsync(CancellationToken ct)
    {
        await AnsiConsole.Live(BuildDisplay())
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                while (!ct.IsCancellationRequested)
                {
                    ctx.UpdateTarget(BuildDisplay());
                    try { await Task.Delay(500, ct); }
                    catch (OperationCanceledException) { break; }
                }
                // Final update so the screen isn't stale after Ctrl+C
                ctx.UpdateTarget(BuildDisplay());
            });
    }

    private IRenderable BuildDisplay()
    {
        var uptime = DateTime.UtcNow - _startTime;
        var uptimeStr = $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

        // ── Header ────────────────────────────────────────────────────────────
        var header = new Panel(
            new Markup(
                $"[bold white]sigma[/]  [dim]Simulator for Generic Modbus Applications[/]\n" +
                $"[dim]TCP {Markup.Escape(_config.Network.Interface)}:{_config.Network.Port}  " +
                $"·  {_devices.Count} device(s)  ·  uptime {uptimeStr}[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(1, 0)
        };

        // ── Device stats table ────────────────────────────────────────────────
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Device[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Unit ID[/]").Centered())
            .AddColumn(new TableColumn("[bold]Reads[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Writes[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Req/s[/]").RightAligned());

        for (int i = 0; i < _devices.Count; i++)
        {
            var d = _devices[i];

            // Update per-device rate once per second
            var now = DateTime.UtcNow;
            long total = d.Stats.TotalReads + d.Stats.TotalWrites;
            double elapsed = (now - _rateWindowStart[i]).TotalSeconds;
            if (elapsed >= 1.0)
            {
                _rates[i] = (total - _rateWindowTotal[i]) / elapsed;
                _rateWindowTotal[i] = total;
                _rateWindowStart[i] = now;
            }

            var rateColor = _rates[i] > 0 ? "green" : "dim";
            table.AddRow(
                $"[white]{Markup.Escape(d.Name)}[/]",
                $"[yellow]{d.UnitId}[/]",
                $"[cyan]{d.Stats.TotalReads:N0}[/]",
                $"[orange1]{d.Stats.TotalWrites:N0}[/]",
                $"[{rateColor}]{_rates[i]:F1}[/]");
        }

        // ── Activity log ──────────────────────────────────────────────────────
        string[] logLines;
        lock (_logLock) logLines = [.. _log];

        var logContent = logLines.Length > 0
            ? string.Join("\n", logLines.Select(l => $"[dim]{Markup.Escape(l)}[/]"))
            : "[dim]Waiting for connections…[/]";

        var logPanel = new Panel(new Markup(logContent))
        {
            Header = new PanelHeader("[bold] Recent Activity [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0)
        };

        // ── Footer ────────────────────────────────────────────────────────────
        var footer = new Markup("[dim]Press [bold]Ctrl+C[/] to stop[/]");

        return new Rows(header, table, logPanel, footer);
    }
}
