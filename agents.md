# agents.md

Specialized Claude Code sub-agents for the sigma repository. Agent files live in `.claude/agents/` and are invoked with `@agent-name` in Claude Code.

---

## Available agents

### `@modbus-expert`

**When to use:** Protocol questions, compliance audits, encoding/decoding problems, FluentModbus API questions, function code reference.

**Invocation examples:**
- `@modbus-expert audit the simulator for Modbus protocol compliance`
- `@modbus-expert how do I decode a Float32 from two holding registers?`
- `@modbus-expert what exception code should FC16 return if the address range is out of bounds?`

**Scope:** Full Modbus TCP/RTU protocol reference — MBAP framing, all standard FCs, data types, exception codes, FluentModbus patterns, and sigma-specific architecture context.

---

### `@sigma-test-writer`

**When to use:** Writing new tests for sigma, filling coverage gaps in `Sigma.Engine` services, or any test that needs to interact with a live `ModbusTcpServer`.

**Invocation examples:**
- `@sigma-test-writer add tests for DeviceStats`
- `@sigma-test-writer write an integration test for ModbusServerHandler.WriteRegisterValue for all data types`
- `@sigma-test-writer add coverage for the SimulationEngine UpdateRegister path`

**Scope:** Knows the xUnit + FluentAssertions conventions used in this project, current coverage gaps, the reflection-based `DoTick` testing pattern, and how to use an OS-assigned loopback port for live-server integration tests.

---

### `@sigma-reviewer`

**When to use:** Before raising a PR, or after making changes to `Sigma.Engine` services.

**Invocation examples:**
- `@sigma-reviewer review the changes on this branch`
- `@sigma-reviewer check this new register handler for thread safety and protocol compliance`

**Scope:** Checks for layer violations, Modbus protocol encoding correctness (especially multi-register data types and the Int32 IEEE 754 clamp edge case), thread-safety of the `IsOverridden`/`volatile` pattern, write-FC override tracking completeness, and config validation coverage.

---

## Agent vs skill

The `/modbus-expert` **skill** (`.claude/commands/modbus-expert.md`) is a lighter inline alternative that runs in the main conversation context. Use it for quick mid-coding questions. Use `@modbus-expert` (the agent) when you want an isolated review or audit with its own context window.
