# BDVM - Web

`BDVM.Web` is the extensible web platform for BDVM. It gives Dispatch, Management and future modules one validated place to register routes, navigation, assets, realtime topics and permissions.

## Status

| Property | Value |
| --- | --- |
| Module kind | Web platform |
| Target framework | .NET Framework 4.8 (`net48`) |
| Required modules | `BDVM.Common`, `BDVM.Core` |
| Standalone business authority | Never; all business state remains host-authoritative |
| Transport | Abstract `IBdvmWebTransport`; no mandatory Remote Dispatch dependency |

## Responsibilities

- Load `IBdvmWebModule` implementations against the versioned BDVM Web API 1.0 contract.
- Stage registrations and publish them atomically only after complete validation.
- Reject incompatible API ranges, duplicate module IDs and collisions in routes, assets or navigation.
- Validate route namespaces, realtime subscriptions and declared permissions.
- Issue bounded sessions and enforce CSRF, same-origin, permission, payload, rate-limit, expected-version and idempotency checks before host execution.
- Authenticate accounts using per-account random salts and PBKDF2-SHA256 password hashes; plaintext passwords are never stored.
- Keep the immutable technical principal separate from the display name, preferring the Multiplayer name when supplied.
- Serve a responsive shell asset with shared connection state and module-owned navigation.
- Publish loaded module capabilities through the shared registry.
- Fail closed when a module throws during registration or requests capabilities outside its manifest.

## Key surfaces

`WebModuleHost` is the module registry. `WebAuthenticationService`, `WebSessionRegistry` and `WebIntentGateway` protect access before forwarding mutations to `IAuthoritativeWebIntentExecutor`. `IBdvmWebTransport` isolates HTTP/WebSocket mechanics from Web and domain code. `WebShellService` creates the versioned shell snapshot. The [surface inventory](SURFACE_INVENTORY.md) records ownership across Web, Dispatch and Management.

## Extension model

A web feature declares a stable module ID, compatible API range, permissions and capability names. During registration it may add only resources under its own namespace. The host validates the staged result before making it visible. This lets future modules extend the interface without sharing business logic or bypassing authority checks.

## Boundaries

Web is not an economy engine and never decides balances, ownership, prices or contract outcomes. Routes expose read models and accept intents; authoritative feature services validate and execute those intents. This repository ships browser-shell assets and a transport contract but deliberately does not bind a network listener or Unity runtime yet.

Dispatch and Management are distinct feature modules and user interfaces. They share only the Web shell, authentication and session transport.

## Authentication and identity

- Account names are normalized; passwords contain 10–256 characters.
- Only a random salt, PBKDF2-SHA256 hash and iteration count are persisted by an `IWebCredentialStore` implementation.
- Login attempts are rate-limited by remote address. Sessions are bounded, same-origin, CSRF-protected, permission-scoped and revocable.
- `PrincipalId` is the stable economic/security identity and never changes with the player's name.
- `DisplayName` is presentation-only. The host should use the Multiplayer name when available; otherwise the player chooses it.
- Production composition must provide a durable protected credential store. `InMemoryWebCredentialStore` is for tests and ephemeral local sessions only.

Concurrent retries sharing one principal, module and idempotency key share exactly one authoritative execution. Envelope tokens reject control characters before logging, and reusing a key with a different payload is refused.

External dependencies: none in the platform assembly. A runtime may provide a native BDVM listener or an optional Remote Dispatch adapter.

## Build

Keep Common and Core as sibling repositories under `src/`, then run:

```powershell
dotnet build .\BDVM.Web.csproj -c Release
dotnet run --project .\tests\BDVM.Web.Tests.csproj -c Release
node --test .\tests\shell.test.cjs
.\Package.ps1
```

## Testing and installation

Backend tests cover salted password authentication, technical/display identity separation, permissions, CSRF, same-origin, replay, idempotency conflicts, payload/rate bounds and module failure isolation. Frontend tests verify inert rendering of untrusted labels and identity.

## Compatibility

The host currently exposes Web API 1.0. Modules state an accepted range and are refused before registration when incompatible. Routes and capabilities are public identifiers; rename them only with a migration or compatibility period.

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) and the applied copyright [NOTICE](NOTICE).
