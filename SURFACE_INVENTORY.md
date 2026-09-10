# Web surface inventory

This inventory assigns every migrated surface to one functional owner. A view may only display an authoritative snapshot. A browser action emits a versioned intent and never performs the business mutation itself.

## Views

| Source surface | Data | Owning module | Destination |
| --- | --- | --- | --- |
| In-game UI — connection, release, host/client role and logs | session, transport state and correlation IDs | Web | shared shell and administration |
| In-game UI — diagnostics and export | runtime diagnostics | Web | administration and log export |
| In-game UI — companies and wallets | companies, members, permissions and balances | Management | Companies and Wallets |
| In-game UI — fleet, bundles, resale and maintenance | economic assets keyed by `AssetId` | Management | Fleet |
| In-game UI — catalog and initial delivery | virtual stock, listings, grants and delivery tracks | Management | Catalog |
| In-game UI — leases and assignments | contracts, installments, operator and lifecycle | Management | Leases and Assignments |
| RemoteDispatchLive — map | map background and geometry | Dispatch | Map |
| RemoteDispatchLive — trains and physical vehicles | positions, consists and observable state | Dispatch | Trains; no economic ownership |
| RemoteDispatchLive — tracks, junctions and signals | infrastructure and authorized controls | Dispatch | Tracks, Junctions and Signals |
| RemoteDispatchLive — routes and AI Traffic | occupation, reservations and external traffic | Dispatch | Occupations and Routes |
| RemoteDispatchLive — former BDVM prototype tab | every economic view | Management | replaced by namespaced Management surfaces |

## Actions

| Existing or planned action | Module | Intent or route | Confirmation |
| --- | --- | --- | --- |
| Create, apply, invite, accept/refuse, leave and change membership policy | Management | `bdvm.management.company-governance.v1` | required for leaving and destructive changes |
| Delegate a permission or transfer leadership | Management | `bdvm.management.company-governance.v1` | required |
| Dissolve a company | Management | `bdvm.management.company-dissolve.v1` | required and explicit |
| Contribute or withdraw | Management | `bdvm.management.wallet-transfer.v1` | explicit amount and payer |
| Rename or change owner/operator/state | Management | `bdvm.management.fleet.rename.v1` or `fleet-manage.v1` | depends on mutation |
| Create a bundle, resell or begin manual maintenance observation | Management | `fleet-bundle.v1`, `fleet-resale.v1`, `fleet-maintenance.v1` | required for resale or maintenance |
| Purchase from the catalog and perform the one-time initial delivery | Management | `market.purchase.v1`, `initial-delivery.v1` | explicit owner, payer and track |
| Create, return or purchase a lease | Management | `lease-manage.v1` | required for financial commitment |
| Assign, complete or cancel a mission | Management | `assignment-manage.v1` | follows authoritative lifecycle state |
| Financing, planning-only yard assistance and industry | Management | `finance-manage.v1`, `yard-manage.v1`, `industry-manage.v1` | adapters and capabilities required |
| Preview or apply a route | Dispatch | `bdvm.dispatch.route-control.v1` | host token and expected version |
| Control a junction | Dispatch | `bdvm.dispatch.junction-control.v1` | dedicated Dispatch permission |

## Boundaries

- Web owns sessions, security, the module registry, navigation, connection and realtime transport.
- Dispatch owns the physical network representation; it knows nothing about wallets, companies or the market.
- Management owns economic views and intents; it controls neither infrastructure nor HTTP transport.
- The transport authenticates the principal. `WebIntentGateway` validates the session, CSRF token, same origin, permission, expected version, idempotency key, payload size and rate limit before calling `IAuthoritativeWebIntentExecutor`.
- The authoritative executor belongs to the host runtime and remains outside these frontends.
