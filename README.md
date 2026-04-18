# sigma

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Simulator for Generic Modbus Applications**

sigma is a CLI tool that simulates one or more Modbus TCP slave devices from a JSON configuration file. It is useful for testing Modbus clients, SCADA systems, and BMS integrations without needing real hardware.

Modelled after the architecture of [simbol](https://github.com/moizhraj/simbol), a BACnet device simulator.

---

## Features

- Simulate multiple Modbus TCP slave devices (unit IDs 1–247) on a single TCP server
- Four register table types per device: Holding Registers, Input Registers, Coils, Discrete Inputs
- Five simulation patterns: **Sine**, **Ramp**, **Sawtooth**, **Random**, **Static**
- Multi-register data types: UInt16, Int16, Float32, Int32, UInt32
- Realistic variation — per-register phase offsets and jittered update intervals prevent lockstep oscillation
- Live terminal dashboard showing per-device request counts, writes, and req/s rate
- Activity log of recent Modbus function code requests
- Config validation with clear error messages before startup
- Sample config generator (`sigma init`)

---

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## Quick Start

```bash
# 1. Generate a sample configuration
dotnet run --project src/Sigma.Cli -- init --output my-config.json

# 2. Validate the configuration
dotnet run --project src/Sigma.Cli -- validate --config my-config.json

# 3. Start the simulator
dotnet run --project src/Sigma.Cli -- run --config my-config.json
```

Connect any Modbus TCP client to `localhost:502`.

---

## Commands

| Command | Description |
|---|---|
| `sigma run -c <file>` | Start the simulator with a configuration file |
| `sigma validate -c <file>` | Validate a configuration file without starting |
| `sigma init [-o <file>]` | Generate a sample configuration file |

### Options

| Option | Description |
|---|---|
| `--config`, `-c` | Path to the JSON configuration file (required for `run` and `validate`) |
| `--output`, `-o` | Output path for `init` (default: `sigma-config.json`) |
| `--verbose`, `-v` | Enable DEBUG-level logging (also shows logs in terminal) |
| `--quiet`, `-q` | Suppress INFO messages (WARNING and above only) |

---

## Configuration

Configuration is a JSON file. All fields in `defaults` and per-register groups are optional and fall back to defaults.

```json
{
  "network": {
    "interface": "0.0.0.0",
    "port": 502
  },
  "defaults": {
    "simulationIntervalMs": 1000,
    "updateIntervalMs": 5000,
    "jitter": 0.5,
    "valueRange": { "min": 0, "max": 1000 },
    "simulationPattern": "Sine"
  },
  "devices": [
    {
      "unitId": 1,
      "name": "HVAC Controller",
      "description": "Zone temperature and humidity controller",
      "registers": {
        "holdingRegisters": [
          {
            "startAddress": 0,
            "count": 10,
            "dataType": "UInt16",
            "simulationPattern": "Sine",
            "valueRange": { "min": 150, "max": 950 },
            "updateIntervalMs": 3000,
            "label": "Temperature sensors"
          }
        ],
        "inputRegisters": [
          {
            "startAddress": 0,
            "count": 5,
            "simulationPattern": "Random"
          }
        ],
        "coils": [
          {
            "startAddress": 0,
            "count": 8,
            "simulationPattern": "Ramp"
          }
        ],
        "discreteInputs": [
          {
            "startAddress": 0,
            "count": 4,
            "simulationPattern": "Sine"
          }
        ]
      }
    }
  ]
}
```

### Network

| Field | Default | Description |
|---|---|---|
| `interface` | `"0.0.0.0"` | IP address to bind (use `"0.0.0.0"` for all interfaces) |
| `port` | `502` | TCP port |

### Defaults

| Field | Default | Description |
|---|---|---|
| `simulationIntervalMs` | `1000` | How often the simulation engine ticks (ms) |
| `updateIntervalMs` | `5000` | How often each register value updates (ms) |
| `jitter` | `0.5` | Fractional variation applied to update intervals (0 = none, 1 = ±100%) |
| `valueRange` | `{ min: 0, max: 1000 }` | Default value range for analog registers |
| `simulationPattern` | `"Sine"` | Default simulation pattern |

### Device

| Field | Required | Description |
|---|---|---|
| `unitId` | Yes | Modbus unit ID (1–247, must be unique) |
| `name` | Yes | Human-readable device name |
| `description` | No | Optional description |
| `registers` | Yes | Register tables (at least one group required) |

### Register Group

Defined under `holdingRegisters`, `inputRegisters`, `coils`, or `discreteInputs`. Multiple groups per table are allowed and must not overlap in address space.

| Field | Default | Description |
|---|---|---|
| `startAddress` | — | Starting register address (0–65535) |
| `count` | — | Number of 16-bit register slots |
| `label` | — | Optional description of this group |
| `dataType` | `"UInt16"` | Encoding for holding/input registers (see below) |
| `simulationPattern` | from defaults | Overrides default pattern for this group |
| `valueRange` | from defaults | Overrides default value range for this group |
| `updateIntervalMs` | from defaults | Overrides default update interval (min 100ms) |

#### Data Types

| Type | Registers | Description |
|---|---|---|
| `UInt16` | 1 | Unsigned 16-bit integer (0–65535) |
| `Int16` | 1 | Signed 16-bit integer (-32768–32767) |
| `Float32` | 2 | IEEE 754 single-precision float (big-endian, count must be even) |
| `Int32` | 2 | Signed 32-bit integer (big-endian, count must be even) |
| `UInt32` | 2 | Unsigned 32-bit integer (big-endian, count must be even) |

> `dataType` is not applicable to `coils` or `discreteInputs` (they are always binary).

#### Simulation Patterns

| Pattern | Behaviour |
|---|---|
| `Sine` | Smooth oscillation over a 60-second period |
| `Ramp` | Triangle wave — ramps up then down over 60 seconds |
| `Sawtooth` | Linear rise from min to max then instant reset, 60-second period |
| `Random` | Uniformly distributed random value on each update |
| `Static` | Constant midpoint value, never changes |

Each register receives a random phase offset at startup so registers within the same group do not oscillate in lockstep. Update intervals are also individually jittered.

---

## Supported Function Codes

| FC | Name | Direction |
|---|---|---|
| 01 | Read Coils | Read |
| 02 | Read Discrete Inputs | Read |
| 03 | Read Holding Registers | Read |
| 04 | Read Input Registers | Read |
| 05 | Write Single Coil | Write (marks register as overridden) |
| 06 | Write Single Register | Write (marks register as overridden) |
| 15 | Write Multiple Coils | Write (marks registers as overridden) |
| 16 | Write Multiple Registers | Write (marks registers as overridden) |

When a writable register (holding register or coil) is written by an external client, it is marked as **overridden** and its simulated value stops updating automatically.

---

## Live Dashboard

When `sigma run` starts, a live terminal dashboard is displayed:

```
╭──────────────────────────────────────────────────────────────╮
│  sigma  Simulator for Generic Modbus Applications            │
│  TCP 0.0.0.0:502  ·  2 device(s)  ·  uptime 00:01:23       │
╰──────────────────────────────────────────────────────────────╯
╭──────────────────────┬─────────┬───────┬────────┬──────╮
│ Device               │ Unit ID │ Reads │ Writes │ Req/s│
├──────────────────────┼─────────┼───────┼────────┼──────┤
│ HVAC Controller      │    1    │  1204 │     12 │  8.0 │
│ Power Meter          │    2    │   890 │      0 │  5.0 │
╰──────────────────────┴─────────┴───────┴────────┴──────╯
╭─ Recent Activity ─────────────────────────────────────────╮
│  [12:34:01] FC03 Read Holding Registers  unitId=1 addr=0  │
│  [12:34:02] FC04 Read Input Registers    unitId=2 addr=0  │
╰───────────────────────────────────────────────────────────╯
Press Ctrl+C to stop
```

All structured logs are written to a timestamped file in the system temp directory. The log file path is shown at startup.

---

## Project Structure

```
sigma/
├── src/
│   ├── Sigma.Core/          # Config models, simulation patterns, enums — no protocol dependencies
│   ├── Sigma.Engine/        # Modbus TCP server (FluentModbus), device factory, simulation engine
│   └── Sigma.Cli/           # CLI entry point, live dashboard (Spectre.Console), Serilog logging
├── config/                  # Example configuration files
├── tests/                   # Unit tests (xUnit + FluentAssertions)
├── plan.md                  # Implementation plan and change log
└── sigma.sln
```

---

## Development

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run directly
dotnet run --project src/Sigma.Cli -- run --config config/sample.json
```

---

## Dependencies

| Package | Purpose |
|---|---|
| [FluentModbus](https://github.com/Apollo3zehn/FluentModbus) | Modbus TCP server |
| [System.CommandLine](https://github.com/dotnet/command-line-api) | CLI parsing |
| [Spectre.Console](https://spectreconsole.net/) | Rich terminal UI and live dashboard |
| [Serilog](https://serilog.net/) | Structured logging to file |
| [Microsoft.Extensions.Hosting](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host) | Hosted service lifecycle + DI |
| [xUnit](https://xunit.net/) + [FluentAssertions](https://fluentassertions.com/) | Unit testing |

---

## License

MIT — see [LICENSE](LICENSE) for details.
