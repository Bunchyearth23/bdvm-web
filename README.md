# BDVM - Web

`BDVM.Web` is the extensible web platform for BDVM. It gives Dispatch, Management and future modules one validated place to register routes, navigation, assets, realtime topics and permissions.

## Status

| Property | Value |
| --- | --- |
| Module kind | Web platform |
| Target framework | .NET Framework 4.8 (`net48`) |
| Required modules | `BDVM.Common`, `BDVM.Core` |
| Standalone web server | Not yet |
| Current browser transport | Remote Dispatch integration through `BDVM.Dispatch` |

## Responsibilities

- Load `IBdvmWebModule` implementations against the versioned BDVM Web API 1.0 contract.
- Stage registrations and publish them atomically only after complete validation.
- Reject incompatible API ranges, duplicate module IDs and collisions in routes, assets or navigation.
- Validate route namespaces, realtime subscriptions and declared permissions.
- Publish loaded module capabilities through the shared registry.
- Fail closed when a module throws during registration or requests capabilities outside its manifest.

## Key surfaces

`WebModuleHost` is the platform entry point. It produces `WebModuleLoadResult` and `LoadedWebModule` records and consumes the contracts defined in `BDVM.Common`, including `BdvmWebModuleManifest`, `IBdvmWebRegistrar` and `IBdvmWebModule`.

## Extension model

A web feature declares a stable module ID, compatible API range, permissions and capability names. During registration it may add only resources under its own namespace. The host validates the staged result before making it visible. This lets future modules extend the interface without sharing business logic or bypassing authority checks.

## Boundaries

Web is not an economy engine and never decides balances, ownership, prices or contract outcomes. Routes expose read models and accept intents; authoritative feature services validate and execute those intents. This repository also does not yet ship an HTTP listener, browser shell or Unity Mod Manager package by itself.

## Build

Keep Common and Core as sibling repositories under `src/`, then run:

```powershell
dotnet build .\BDVM.Web.csproj -c Release
```

## Testing and installation

Validation tests cover API incompatibility, namespace isolation, duplicate registrations, undeclared permissions and atomic failure. The currently usable browser transport comes from the BDVM Remote Dispatch fork and `BDVM.Dispatch`; install the matching `BDVM.Full` composition rather than this DLL alone.

## Compatibility

The host currently exposes Web API 1.0. Modules state an accepted range and are refused before registration when incompatible. Routes and capabilities are public identifiers; rename them only with a migration or compatibility period.

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE).
