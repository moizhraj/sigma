# sigma — Simulator for Generic Modbus Applications

**Created:** 2026-04-11  
**Reference:** [simbol BACnet simulator](https://github.com/moizhraj/simbol)

---

## Overview

**sigma** is a CLI-based Modbus device simulator written in C# / .NET 8. It simulates multiple Modbus slave devices from a JSON configuration file, with realistic value simulation (sine, ramp, random, sawtooth, static). Modeled after the simbol BACnet simulator in architecture and user experience.

---

## Architecture: Three Projects

```
sigma/
├── src/
│   ├── Sigma.Core/          # Config models, simulation patterns, enums
│   ├── Sigma.Engine/        # Modbus protocol server, device factory, simulation engine
│   └── Sigma.Cli/           # CLI entry point, live dashboard
├── config/                  # Example configs
├── tests/                   # Unit + integration tests
└── sigma.sln
```

**Layer responsibilities:**
- **Core** — pure C#, no protocol dependencies. Config parsing/validation, simulation math (`IValueSimulator`), enums.
- **Engine** — Modbus TCP server, device/register factory, simulation engine timer loop, request handler, stats.
- **Cli** — System.CommandLine for commands, Spectre.Console live dashboard, Serilog file logging, DI wiring.

---

## Modbus Protocol Concepts

Unlike BACnet's object model, Modbus has a flat register address space with four distinct tables per device:

| Table | Address Space | Access | Type |
|---|---|---|---|
| Coils (0x) | 0–65535 | Read/Write | Binary |
| Discrete Inputs (1x) | 0–65535 | Read-Only | Binary |
| Holding Registers (4x) | 0–65535 | Read/Write | Analog (16-bit words) |
| Input Registers (3x) | 0–65535 | Read-Only | Analog (16-bit words) |

Each slave device is identified by a **Unit ID** (1–247). Multiple slaves are served from one TCP server on port 502.

---

## Configuration Format

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
            "dataType": "UInt16",
            "simulationPattern": "Random",
            "valueRange": { "min": 200, "max": 800 }
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
    },
    {
      "unitId": 2,
      "name": "Power Meter",
      "description": "3-phase power measurement",
      "registers": {
        "holdingRegisters": [
          {
            "startAddress": 0,
            "count": 6,
            "dataType": "Float32",
            "simulationPattern": "Sine",
            "valueRange": { "min": 0.0, "max": 240.0 },
            "label": "Voltage L1/L2/L3 (2 registers each)"
          }
        ]
      }
    }
  ]
}
```

**Key decisions vs simbol:**
- Devices identified by `unitId` (1–247) instead of `instanceId`
- Four register table types instead of 9 BACnet object types
- Register groups defined as arrays (multiple non-overlapping ranges per table per device)
- `dataType` controls how 16-bit registers encode values: `UInt16`, `Int16`, `Float32`, `Int32`, `UInt32`
- `startAddress` + `count` defines the address range; validation prevents overlaps within a device

---

## Sigma.Core

### Enums
- `SimulationPattern`: Static, Sine, Ramp, Random, Sawtooth
- `RegisterType`: Coil, DiscreteInput, HoldingRegister, InputRegister
- `DataType`: UInt16, Int16, Float32, Int32, UInt32

### Configuration Models
```
SigmaConfig
  NetworkConfig { Interface, Port }
  DefaultsConfig { SimulationIntervalMs, UpdateIntervalMs, Jitter, ValueRange, SimulationPattern }
  DeviceConfig[] { UnitId, Name, Description, Registers }

DeviceConfig.Registers
  RegisterGroupConfig[] HoldingRegisters
  RegisterGroupConfig[] InputRegisters
  RegisterGroupConfig[] Coils
  RegisterGroupConfig[] DiscreteInputs

RegisterGroupConfig
  int StartAddress
  int Count
  string? Label
  DataType DataType         // UInt16 default
  SimulationPattern?        // overrides defaults
  ValueRange? ValueRange    // overrides defaults
  int? UpdateIntervalMs     // overrides defaults
```

### Simulation Patterns
`IValueSimulator` with 5 implementations — identical math to simbol:
- `StaticSimulator`
- `SineSimulator`
- `RampSimulator`
- `RandomSimulator`
- `SawtoothSimulator`

Each `SimulatedRegister` holds a `PhaseOffset` (randomized at creation) to prevent lockstep oscillation.

---

## Sigma.Engine

### Modbus Library
**FluentModbus** (`SebastianSternschulte.FluentModbus`) — supports Modbus TCP server with multiple unit IDs, and exposes register memory as `Span<short>` / `Span<byte>`.

### ModbusDeviceFactory
Creates `SimulatedDevice` from `DeviceConfig + DefaultsConfig`:
1. For each register group in each table, create `SimulatedRegister` instances
2. Assign phase offsets, jittered update intervals, and simulator instances
3. Return `SimulatedDevice`

### SimulatedDevice / SimulatedRegister
```
SimulatedDevice
  byte UnitId
  string Name
  string Description
  SimulatedRegister[] AllRegisters
  DeviceStats Stats

SimulatedRegister
  RegisterType Type
  int Address
  DataType DataType
  bool IsWritable            // true for Coils + HoldingRegisters
  bool IsOverridden          // set when externally written via FC05/06/15/16
  IValueSimulator Simulator
  double PhaseOffset
  int UpdateIntervalMs
  DateTime LastUpdateTime
```

### ModbusServiceHandler
Wraps the FluentModbus server. Handles:
- **FC01** Read Coils
- **FC02** Read Discrete Inputs
- **FC03** Read Holding Registers
- **FC04** Read Input Registers
- **FC05** Write Single Coil → marks register overridden
- **FC06** Write Single Register → marks register overridden
- **FC15** Write Multiple Coils
- **FC16** Write Multiple Registers

### SimulationEngine
Timer loop (default 1000ms tick):
- For each device → each register → check `elapsed > UpdateIntervalMs`
- Compute new value from simulator (skipped if `IsOverridden`)
- Write to FluentModbus register memory
- Update stats

### SimulatorHostedService
`IHostedService` lifecycle:
- `StartAsync`: create devices, register unit IDs with server, start TCP server, start simulation engine
- `StopAsync`: graceful shutdown

---

## Sigma.Cli

### Commands
```bash
sigma run      --config sigma-config.json [--verbose] [--quiet]
sigma validate --config sigma-config.json [--verbose] [--quiet]
sigma init     [--output sigma-config.json]
```

- **run** — full startup with live dashboard
- **validate** — parse + validate config, print summary, exit 0/1
- **init** — write a sample config with two devices (HVAC + Power Meter)

### Live Dashboard (Spectre.Console)
```
╔══════════════════════════════════════════════════════════════════╗
║  sigma — Modbus Device Simulator         [2 devices | TCP:502]  ║
╠══════════════════════════════════════════════════════════════════╣
║  Device              │ Unit │ Reads │ Writes │ Clients │ Rate   ║
║  HVAC Controller     │   1  │  1204 │    12  │    2    │  8/s  ║
║  Power Meter         │   2  │   890 │     0  │    1    │  5/s  ║
╠══════════════════════════════════════════════════════════════════╣
║  Activity Log                                                    ║
║  [12:34:01] FC03 Read Holding Registers  unitId=1 addr=0 qty=10 ║
║  [12:34:01] FC04 Read Input Registers    unitId=2 addr=0 qty=6  ║
╚══════════════════════════════════════════════════════════════════╝
```

---

## Validation Rules

- At least one device required
- Unit IDs must be unique (1–247)
- Register groups within the same table on the same device must not have overlapping address ranges
- `startAddress` + `count` must not exceed 65536
- `updateIntervalMs` ≥ 100ms when specified
- `count` > 0
- For `Float32` / `Int32` / `UInt32`: count must be even (2 registers per value)
- Device must have at least one register group

---

## Key Dependencies

| Package | Purpose |
|---|---|
| `FluentModbus` | Modbus TCP server |
| `System.CommandLine` | CLI parsing |
| `Spectre.Console` | Rich terminal UI |
| `Serilog` + `Serilog.Sinks.File` | Structured logging |
| `Microsoft.Extensions.Hosting` | IHostedService + DI |
| `xunit` + `FluentAssertions` | Tests |

---

## What Carries Over from simbol

| Component | Reuse | Notes |
|---|---|---|
| `IValueSimulator` + 5 impls | Direct | Identical math |
| `ValueRange` model | Direct | Same concept |
| `SimulationPattern` enum | Direct | Same 5 patterns |
| Jitter formula | Direct | Same randomized interval |
| Phase offset per register | Direct | Prevents lockstep oscillation |
| Hosted service lifecycle | Direct | Same .NET pattern |
| CLI command structure | Adapted | Same 3 commands |
| Config validation pipeline | Adapted | Different rules (overlaps, even counts) |
| Spectre dashboard | Adapted | Different columns |
| `DeviceStats` | Adapted | Track reads/writes by function code |
| Config hierarchy pattern | Adapted | Register types instead of object types |

## What's Different from simbol

| Aspect | simbol (BACnet) | sigma (Modbus) |
|---|---|---|
| Discovery | Who-Is / I-Am broadcast | None (polling-based) |
| Device identifier | Instance ID (0–4M) | Unit ID (1–247) |
| Data model | Object-based (9 types) | Flat register tables (4 types) |
| Address space | Object identifiers | 0–65535 per table per device |
| Transport | UDP | TCP/IP port 502 |
| COV / subscriptions | Native BACnet feature | Not applicable |
| Multi-register values | N/A | Float32 spans 2 registers |
| Write semantics | Per-object property | Per-address range (FC05/06/15/16) |

---

## Implementation Phases

### Phase 1 — Skeleton & Config [DONE — 2026-04-11]
- [x] Create solution + three projects (Sigma.Core, Sigma.Engine, Sigma.Cli, Sigma.Tests)
- [x] Config models + JSON deserialization (SigmaConfig, DeviceConfig, RegisterGroupConfig, etc.)
- [x] Config validation (ConfigValidator — overlaps, unitId uniqueness, even-count for 32-bit types, etc.)
- [x] `validate` command working end-to-end
- [x] `init` command generates sample config (HVAC + Power Meter)

### Phase 2 — Core Simulation [DONE — 2026-04-11]
- [x] `IValueSimulator` interface + 5 implementations (Static, Sine, Ramp, Random, Sawtooth)
- [x] `ValueSimulatorFactory`
- [x] `SimulatedDevice` + `SimulatedRegister` models (with Min/Max, PhaseOffset, jitter)
- [x] `ModbusDeviceFactory` (builds registers from config with jitter + phase offsets)
- [x] `SimulationEngine` timer loop
- [x] `ModbusServerHandler` (FluentModbus TCP server, UInt16/Int16/Float32/Int32/UInt32 encoding)
- [x] `SimulatorHostedService` (IHostedService lifecycle)
- [x] `run` command wired up with DI + Serilog
- [x] 21 unit tests passing (config validation + all 5 simulators)

### Phase 3 — Modbus Server [ ]
- [ ] Smoke-test `run` command with a real Modbus client (e.g. ModRSsim2 or Python pymodbus)
- [ ] Verify FC01/02/03/04 reads return correct simulated values
- [ ] Verify FC05/06/15/16 writes mark registers as overridden (stop simulation)

### Phase 4 — CLI & Dashboard [DONE — 2026-04-11]
- [x] Spectre.Console live dashboard (`SpectreConsoleDisplay`) — header, per-device stats table, activity log, footer
- [x] Per-device read/write counting via `ModbusServerHandler.RequestValidator` → `OnRequest` event
- [x] Rate calculation (req/s) using per-device rolling window, refreshed every 500ms
- [x] Activity log: last 14 requests with FC label, unitId, address, quantity
- [x] Dashboard runs concurrently with host via `Task.WhenAll` + `IHostApplicationLifetime`
- [x] File-only Serilog logging while dashboard is active (clean terminal); console logging in `--verbose` mode
- [x] Graceful shutdown on Ctrl+C via `IHostedService` + `CancellationTokenSource`

### Phase 5 — Polish & Tests [ ]
- [ ] Integration test: Python pymodbus client reads from running simulator
- [ ] Additional example configs in `config/`
- [ ] README

---

## Change Log

| Date | Change |
|---|---|
| 2026-04-11 | Initial plan created |
| 2026-04-11 | Phase 1 + 2 complete — solution built, all 21 tests passing, `init` and `validate` commands working |
| 2026-04-11 | Phase 4 complete — Spectre.Console live dashboard, per-device stats + activity log, clean Ctrl+C shutdown |
