using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Sigma.Cli;
using Sigma.Core.Configuration;
using Sigma.Engine.Services;

var configOption = new Option<FileInfo>(
    aliases: ["--config", "-c"],
    description: "Path to the sigma JSON configuration file.")
{ IsRequired = true };

var verboseOption = new Option<bool>(
    aliases: ["--verbose", "-v"],
    description: "Enable DEBUG-level logging.");

var quietOption = new Option<bool>(
    aliases: ["--quiet", "-q"],
    description: "Suppress INFO messages (WARNING and above only).");

var outputOption = new Option<FileInfo>(
    aliases: ["--output", "-o"],
    description: "Output path for the generated configuration file.",
    getDefaultValue: () => new FileInfo("sigma-config.json"));

// ── run ──────────────────────────────────────────────────────────────────────
var runCommand = new Command("run", "Start the Modbus device simulator.");
runCommand.AddOption(configOption);
runCommand.AddOption(verboseOption);
runCommand.AddOption(quietOption);

runCommand.SetHandler(async (FileInfo config, bool verbose, bool quiet) =>
{
    SigmaConfig sigmaConfig;
    try { sigmaConfig = ConfigLoader.Load(config.FullName); }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[error] {ex.Message}");
        Environment.Exit(1);
        return;
    }

    var errors = ConfigValidator.Validate(sigmaConfig);
    if (errors.Count > 0)
    {
        Console.Error.WriteLine("[error] Configuration is invalid:");
        foreach (var e in errors) Console.Error.WriteLine($"  • {e}");
        Environment.Exit(1);
        return;
    }

    var logPath = Path.Combine(Path.GetTempPath(), $"sigma-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    var logLevel = verbose ? Serilog.Events.LogEventLevel.Debug
                 : quiet   ? Serilog.Events.LogEventLevel.Warning
                           : Serilog.Events.LogEventLevel.Information;

    // Dashboard is active → log to file only so the terminal stays clean.
    // In --verbose mode, also emit to console beneath the dashboard (useful for debugging).
    var logConfig = new LoggerConfiguration()
        .MinimumLevel.Is(logLevel)
        .WriteTo.File(logPath, rollingInterval: RollingInterval.Day);

    if (verbose)
        logConfig.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

    Log.Logger = logConfig.CreateLogger();

    var host = Host.CreateDefaultBuilder()
        .UseSerilog()
        .ConfigureServices(services =>
        {
            services.AddSingleton(sigmaConfig);
            services.AddSingleton<ModbusServerHandler>();
            services.AddSingleton<SimulationEngine>();
            services.AddSingleton<SimulatorHostedService>();
            services.AddHostedService(sp => sp.GetRequiredService<SimulatorHostedService>());
        })
        .Build();

    try
    {
        // Start host (registers devices, starts TCP server + simulation engine)
        await host.StartAsync();

        var hostedService = host.Services.GetRequiredService<SimulatorHostedService>();
        var serverHandler = host.Services.GetRequiredService<ModbusServerHandler>();
        var lifetime      = host.Services.GetRequiredService<IHostApplicationLifetime>();

        var display = new SpectreConsoleDisplay(hostedService.Devices, sigmaConfig);
        serverHandler.OnRequest += display.HandleRequest;

        Console.WriteLine($"Log file: {logPath}");

        // Run the live dashboard and wait for host shutdown (Ctrl+C) concurrently.
        using var displayCts = CancellationTokenSource.CreateLinkedTokenSource(
            lifetime.ApplicationStopping);

        await Task.WhenAll(
            display.RunAsync(displayCts.Token),
            host.WaitForShutdownAsync(lifetime.ApplicationStopping));

        displayCts.Cancel();
    }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
        Log.Fatal(ex, "sigma terminated unexpectedly.");
        Environment.Exit(1);
    }
    finally
    {
        await host.StopAsync(TimeSpan.FromSeconds(5));
        await Log.CloseAndFlushAsync();
    }
}, configOption, verboseOption, quietOption);

// ── validate ─────────────────────────────────────────────────────────────────
var validateCommand = new Command("validate", "Validate a configuration file without starting the simulator.");
validateCommand.AddOption(configOption);
validateCommand.AddOption(verboseOption);

validateCommand.SetHandler((FileInfo config, bool verbose) =>
{
    SigmaConfig sigmaConfig;
    try { sigmaConfig = ConfigLoader.Load(config.FullName); }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[error] {ex.Message}");
        Environment.Exit(1);
        return;
    }

    var errors = ConfigValidator.Validate(sigmaConfig);
    if (errors.Count > 0)
    {
        Console.Error.WriteLine("Configuration is INVALID:");
        foreach (var e in errors) Console.Error.WriteLine($"  • {e}");
        Environment.Exit(1);
        return;
    }

    Console.WriteLine("Configuration is valid.");
    Console.WriteLine($"  Network : {sigmaConfig.Network.Interface}:{sigmaConfig.Network.Port}");
    Console.WriteLine($"  Devices : {sigmaConfig.Devices.Count}");
    foreach (var device in sigmaConfig.Devices)
    {
        int hr = device.Registers.HoldingRegisters.Sum(g => g.Count);
        int ir = device.Registers.InputRegisters.Sum(g => g.Count);
        int co = device.Registers.Coils.Sum(g => g.Count);
        int di = device.Registers.DiscreteInputs.Sum(g => g.Count);
        Console.WriteLine($"  [{device.UnitId:D3}] {device.Name}");
        if (hr > 0) Console.WriteLine($"         Holding Registers : {hr}");
        if (ir > 0) Console.WriteLine($"         Input Registers   : {ir}");
        if (co > 0) Console.WriteLine($"         Coils             : {co}");
        if (di > 0) Console.WriteLine($"         Discrete Inputs   : {di}");
    }
}, configOption, verboseOption);

// ── init ─────────────────────────────────────────────────────────────────────
var initCommand = new Command("init", "Generate a sample configuration file.");
initCommand.AddOption(outputOption);

initCommand.SetHandler((FileInfo output) =>
{
    if (output.Exists)
    {
        Console.Error.WriteLine($"[error] File already exists: {output.FullName}. Use a different --output path.");
        Environment.Exit(1);
        return;
    }

    var json = ConfigLoader.GenerateSample();
    File.WriteAllText(output.FullName, json);
    Console.WriteLine($"Sample configuration written to: {output.FullName}");
}, outputOption);

// ── root ─────────────────────────────────────────────────────────────────────
var root = new RootCommand("sigma — Simulator for Generic Modbus Applications");
root.AddCommand(runCommand);
root.AddCommand(validateCommand);
root.AddCommand(initCommand);

return await root.InvokeAsync(args);
