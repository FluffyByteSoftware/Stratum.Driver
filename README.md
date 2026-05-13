# Stratum

A C# multiplayer RPG game server built around a star-topology process
architecture and a struct-first, GC-conscious design.

Stratum is a from-scratch rewrite of a monolithic game server I built
previously. The original worked but was memory-inefficient — too many
allocations on hot paths, too much garbage churn from class-heavy data
models. This rewrite reorganizes the same problem space around `readonly
struct` value types, pooled buffers, and `Span<T>` where it matters, and
splits the single process into specialized executables that communicate
over the network rather than sharing a heap.

> **Status:** Early. The solution builds clean. Several core components
> (logging, disk I/O, tick clock, certificate provisioning, TCP host with
> packet framing and dispatch) are implemented and tested in isolation.
> The executable processes that consume them are scaffolded but not yet
> wired up end-to-end. Nothing useful runs yet — this is a work in
> progress, not a release.

---

## Why it exists

The target is a persistent shared voxel world for ~25 concurrent players,
with authoritative server simulation, permanent consequences, and
simulation-based NPC perception (no cheating AI). It's also a deliberate
learning exercise — every component is hand-written rather than pulled
from a framework, and I'm rewriting the code as I go to internalize the
patterns.

The repository name (`Stratum.Driver`) refers to the .NET solution that
"drives" all of the server processes — login, connection management,
zone supervision, individual zones — from a single source tree.

---

## Architecture

Stratum is a **star topology** of cooperating processes. Each process is
its own executable in the solution; they're never linked into a single
binary.

```
                    ┌──────────────┐
                    │ LoginServer  │   TLS/TCP auth only
                    └──────┬───────┘
                           │ session token
                           ▼
   Client  ◄────►  ┌────────────────────┐  ◄────►  ZoneManager  ◄────►  Zones
                   │ ConnectionManager  │             (master            (authoritative
                   │ (Sentinel)         │              clock,             simulation,
                   │  TCP + UDP         │              supervisor)        one process
                   └────────────────────┘                                 per zone)
```

- **LoginServer** — handles authentication only. TLS over TCP. Ed25519
  keypairs with public keys in a JSON allowlist; no passwords stored.
  Issues short-lived session tokens.
- **ConnectionManager** (aka Sentinel) — the only process clients ever
  talk to. TCP for session lifetime and reliable commands; UDP (via
  LiteNetLib) for realtime state, input, and position updates.
  Translates between client packets and the zone processes.
- **ZoneManager** — master clock, cross-zone coordinator, zone lifecycle
  supervisor. Zones never talk to each other directly; routing flows
  through ZoneManager.
- **Zones** — one OS process per zone. Authoritative simulation of
  entities, voxels, AI, and world state in that zone.

The separation isn't accidental. Putting auth in its own process means
the public-facing attack surface for login is minimal and replaceable.
Putting each zone in its own process means a misbehaving zone can be
killed and restarted without taking down anyone in a neighboring zone.
Putting the realtime UDP plane behind ConnectionManager means clients
never need to know about the internal topology — they connect to one
address and the rest is server business.

---

## Design principles

A few choices that drive everything else:

- **Struct-leaning, allocation-conscious.** `readonly struct` for value
  types, `Span<T>` and `stackalloc` on hot paths, `ArrayPool<T>.Shared`
  for per-operation buffers. `sealed class` for things with identity and
  lifetime (connections, hosts, singletons). The goal is to keep the
  per-tick allocation rate low enough that the GC stays out of the way.
- **Flat-file persistence, no database.** JSON for plaintext, binary for
  voxel deltas, append-only `.log` for events. All writes go through a
  single `DiskManager` with atomic tmp+fsync+rename semantics and a
  write-back cache.
- **Freeze/Thaw via algebra, not replay.** When a zone has no players,
  it freezes. When someone enters, the zone catches up by computing
  elapsed-time deltas per system, not by re-simulating ticks.
  Ephemerals (mobs, projectiles, fauna) are discarded on freeze and
  regenerated from spawners on thaw. Persistent state (voxels, named
  NPCs, colonies, world flags) survives.
- **No cheating NPCs.** Perception is fully simulation-based: field of
  view + line of sight, sound with wall dampening, smell, touch. Agents
  act on beliefs with decay, not on ground-truth world state.
- **Permanent consequences.** Extinct colonies stay extinct. Dead named
  NPCs stay dead. Alienated trainers stay alienated. Player choices
  have lasting world impact by design.
- **Blueprint pattern is universal.** NPCs, items, projectiles, world
  objects, and effects all instantiate from static JSON blueprints with
  `extends`/`overrides` inheritance. Blueprint IDs are FNV-1a hashed
  strings — readable in source, fast int at runtime.
- **Files are canonical and hand-editable.** Tooling (Unity blueprint
  composer, optional WPF editors) is an optional layer over the same
  files. JSON schemas provide autocomplete in editors; a CLI validator
  runs in CI.

---

## Solution layout

```
Stratum.Driver.sln
├── Stratum.SystemTools/    .NET 10              Process-level infrastructure
├── Stratum.Shared/         .NET Standard 2.1    Client/server contract surface
├── Stratum.Networking/     .NET 10              Server-side networking
├── Stratum.LoginServer/    .NET 10              Auth executable (TCP+TLS)
└── Stratum.Connection/     .NET 10              ConnectionManager executable
```

**Project placement rule:** if the Unity client wouldn't call it, it
doesn't belong in `Shared`. `Shared` is the contract between client and
server, nothing more. Server-only infrastructure goes in `SystemTools`
or its own server-side library.

**Namespace style:** `Stratum.Shared` uses block-scoped namespaces as a
visual reminder that it must compile on both .NET Standard 2.1 (server)
and Unity's Mono/IL2CPP (client). Every other project uses file-scoped
namespaces.

---

## What's implemented

**`Stratum.SystemTools`** — process-level infrastructure:

- **Scribe** (logging) — static facade over a bounded channel with
  `DropOldest` backpressure. Five severity tiers. Source context
  captured automatically via caller attributes. Routes writes through
  DiskManager when running.
- **DiskManager** (storage) — singleton write-back cache with 50 MB or
  2 s flush triggers. All writes are atomic (tmp + fsync + rename).
  Daily-rotating log buffer for Scribe.
- **Heartbeat** (clock) — singleton fixed-timestep tick driver, 30 Hz
  default. Sleep-then-spin timing with bounded 5-tick catch-up under
  load.
- **CertificateProvider** (security) — load-or-generate self-signed
  ECDSA P-256 certificate, 1-year expiry. Persisted as PFX (private)
  and CER (public, distributed to clients via the patcher).

**`Stratum.Shared`** — client/server contract surface:

- `Channel` enum, `PacketIds` static class, `DisconnectReason` enum,
  `InvalidPacketException`.
- Packet structs: `PingPacket`, `AuthRequestPacket`,
  `AuthResponsePacket`, `DisconnectPacket`.

Packets are `readonly struct` with a convention rather than an
interface: each has a `public const uint TypeId`, an instance
`Serialize(NetDataWriter)`, and a static `Deserialize(NetDataReader)`.
The dispatcher captures typed deserialize and handler delegates at
registration time — one allocation per registration, none per dispatch.

**`Stratum.Networking`** — server-side networking:

- `PacketDispatcher<TConnection>` with an explicit `Freeze()` lifecycle
  (registration mutates; after freeze, dispatch is read-only and safe
  for concurrent access from connection threads).
- `DispatchResult` / `DispatchOutcome` for routine error categories
  (unknown type, invalid packet, handler exception) — hybrid result
  pattern reserved for outcomes the caller branches on routinely.
- `TcpHost`, `TcpConnection`, `PacketFramer` — length-prefixed framing
  over TLS, with deferred-disconnect semantics (a disconnect request
  sets a flag; the read loop actually disconnects on the next pass,
  never mid-handler, never mid-frame).

All multi-byte values on the wire are big-endian
(`BinaryPrimitives.WriteInt32BigEndian` etc., never the system-endian
overloads).

---

## What's not yet wired up

The components above are individually functional. The executables
that compose them are scaffolded but not connected end-to-end:

- LoginServer doesn't yet wire cert → dispatcher → handlers → host →
  shutdown signal.
- Ed25519 signature verification and allowlist loading aren't
  implemented.
- Session token issuance isn't implemented.
- ConnectionManager (TCP + UDP via LiteNetLib) isn't implemented.

Beyond that, the larger architecture — ZoneManager, per-zone processes,
cross-zone messaging, ECS core, AgentManager, blueprint loader, voxel
system, patcher — is designed but unbuilt.

---

## Building

Requires **.NET 10 SDK**.

```sh
git clone https://github.com/<your-account>/Stratum.Driver.git
cd Stratum.Driver
dotnet build
```

The solution builds clean. The executable projects produce binaries but
don't yet do anything useful when run — they're entry-point stubs
waiting on the wiring described above.

---

## Stack

- **.NET 10** — server processes
- **.NET Standard 2.1** — shared library (cross-compatible with Unity
  Mono/IL2CPP)
- **Unity 6** — client (separate repository)
- **LiteNetLib** — UDP transport with reliability channels (planned,
  ConnectionManager)
- **No database** — flat files for everything

---

## License

All I ask is that you try and keep your own code open source so other people can learn and grow.
