# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run a single test class or method
dotnet test --filter "FullyQualifiedName~ConfigValidatorMissingTests"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Start the simulator
dotnet run --project src/Sigma.Cli -- run --config config/sample.json

# Validate a config without starting
dotnet run --project src/Sigma.Cli -- validate --config my-config.json

# Generate a sample config
dotnet run --project src/Sigma.Cli -- init --output my-config.json
```

## Architecture

Three-project layered solution — dependencies flow strictly downward: Core ← Engine ← Cli.

**Sigma.Core** — pure C#, no protocol or I/O dependencies.
- `Configuration/`: JSON config models (`SigmaConfig` → `DeviceConfig` → `RegisterGroupConfig`), `ConfigLoader` (load + `GenerateSample()`), `ConfigValidator` (collects all errors before returning).
- `Simulation/`: `IValueSimulator` + 5 stateless implementations (Sine, Ramp, Sawtooth, Random, Static). Each computes a value from `(elapsedSeconds, phaseOffset, min, max)`. `ValueSimulatorFactory` maps `SimulationPattern` enum to implementation.
- `Enums/`: `SimulationPattern`, `RegisterType`, `DataType`.

**Sigma.Engine** — Modbus TCP server and simulation loop.
- `Factories/ModbusDeviceFactory`: builds `SimulatedDevice` (and its `SimulatedRegister[]`) from config. Assigns random per-register `PhaseOffset` and jittered `UpdateIntervalMs` at creation time.
- `Models/SimulatedRegister`: holds runtime state per register — `IsOverridden` (see below), `LastUpdateTime`, `Min`/`Max`, `RegisterWidth` (1 for UInt16/Int16, 2 for Float32/Int32/UInt32).
- `Services/ModbusServerHandler`: wraps FluentModbus `ModbusTcpServer`. Handles register memory writes for all data types; exposes `OnRequest` event for the dashboard. The `RequestValidator` callback marks written registers as overridden.
- `Services/SimulationEngine`: `System.Threading.Timer` loop. Each tick iterates all devices/registers, skips overridden or not-yet-due registers, calls the simulator, and writes values to FluentModbus memory.
- `Services/SimulatorHostedService`: `IHostedService` — wires everything together on `StartAsync`/`StopAsync`.

**Sigma.Cli** — entry point only; no business logic.
- `Program.cs`: `System.CommandLine` commands (`run`, `validate`, `init`), DI host setup, Serilog configuration (file-only while dashboard is active; also console with `--verbose`).
- `SpectreConsoleDisplay`: Spectre.Console live dashboard refreshed every 500ms. Subscribes to `ModbusServerHandler.OnRequest` to count reads/writes and build the activity log.

## Key Non-Obvious Patterns

**`IsOverridden` cross-thread pattern.** When a Modbus client writes a register (FC05/06/15/16), `ModbusServerHandler.RequestValidator` calls `MarkOverridden()` on the network thread. `SimulationEngine.DoTick()` reads `IsOverridden` on the timer thread. The field is backed by `volatile bool _isOverridden` in `SimulatedRegister` to make the write visible across threads without a lock.

**Re-entrancy guard in `SimulationEngine`.** `Tick()` (the timer callback) uses `Interlocked.CompareExchange(ref _tickRunning, 1, 0)` to skip a tick if the previous one is still running. `DoTick()` is the private inner method containing the actual logic — tests invoke it via reflection to avoid needing a live TCP server.

**Multi-register data types.** Float32, Int32, and UInt32 span 2 consecutive 16-bit register slots. `ModbusDeviceFactory` increments the address cursor by `RegisterWidth` per logical register. `WriteAnalogToSpan` in `ModbusServerHandler` encodes values big-endian using `BinaryPrimitives` across two `Span<short>` slots. FluentModbus stores coils as `byte` (0x00 = false, 0xFF = true), not as bits.

**`count` in config vs logical registers.** `RegisterGroupConfig.Count` is the number of raw 16-bit slots, not the number of logical values. A group with `count=4, dataType=Float32` produces 2 logical `SimulatedRegister` objects.

## Test Coverage

Tests use xUnit + FluentAssertions. `Sigma.Core` is at 100% coverage. `Sigma.Engine` is ~33% — `ModbusServerHandler` (5.5%) and `SimulatorHostedService` (0%) require a live `ModbusTcpServer` to exercise their core paths; current tests avoid opening TCP ports. `DeviceStats` (0%) has no tests yet. Tests that need `DoTick()` call it via reflection to avoid this constraint.

When adding tests for `Sigma.Engine` services, prefer using an OS-assigned port (`new IPEndPoint(IPAddress.Loopback, 0)`) to avoid port conflicts in CI.

## Git Workflow

Always branch → commit → push → open PR to `main`. Never push directly to `main`.

## Agents

Three specialized sub-agents are available in `.claude/agents/`. See `agents.md` at the repo root for full usage guidance and invocation examples.

| Agent | When to use |
|---|---|
| `@modbus-expert` | Protocol questions, compliance audits, FC/encoding reference, FluentModbus API |
| `@sigma-test-writer` | Writing tests, filling `Sigma.Engine` coverage gaps, integration tests with a live server |
| `@sigma-reviewer` | Pre-PR review for layer violations, encoding correctness, thread-safety, override tracking |

The `/modbus-expert` **skill** is a lighter inline alternative for quick mid-coding Q&A without leaving the main conversation.
