# Inventaire des surfaces Web

Cet inventaire fixe la propriété fonctionnelle des surfaces migrées. Une vue peut afficher uniquement un snapshot autoritaire. Une action browser produit une intent versionnée ; elle ne réalise jamais la mutation.

## Vues

| Surface d'origine | Données | Module propriétaire | Destination |
| --- | --- | --- | --- |
| UI en jeu — connexion, release, rôle host/client et logs | session, état transport, correlation IDs | Web | shell et administration communs |
| UI en jeu — diagnostics et export | diagnostics runtime | Web | administration et export de logs |
| UI en jeu — compagnies et wallets | compagnies, membres, permissions, soldes | Management | Compagnies et Portefeuilles |
| UI en jeu — flotte, bundles, revente et maintenance | actifs économiques par `AssetId` | Management | Flotte |
| UI en jeu — catalogue et livraison initiale | stock virtuel, offres, droits et delivery tracks | Management | Catalogue |
| UI en jeu — locations et assignments | contrats, échéances, operator et lifecycle | Management | Locations et Missions |
| RemoteDispatchLive — carte | fond de carte et géométrie | Dispatch | Carte |
| RemoteDispatchLive — trains et véhicules physiques | positions, rames et état observable | Dispatch | Trains ; aucune ownership économique |
| RemoteDispatchLive — voies, aiguillages et signaux | infrastructure et commandes autorisées | Dispatch | Voies, Aiguillages et Signaux |
| RemoteDispatchLive — routes et AI Traffic | occupations, réservations et trafic extérieur | Dispatch | Occupations et Routes |
| RemoteDispatchLive — onglet BDVM prototype | toutes les vues économiques | Management | remplacé par les espaces Management namespaced |

## Actions

| Action existante ou prévue | Module | Intent ou route | Confirmation |
| --- | --- | --- | --- |
| Créer, candidater, inviter, accepter/refuser, quitter et changer la policy | Management | `bdvm.management.company-governance.v1` | pour départ et changements destructifs |
| Déléguer une permission ou transférer la direction | Management | `bdvm.management.company-governance.v1` | obligatoire |
| Dissoudre une compagnie | Management | `bdvm.management.company-dissolve.v1` | obligatoire et explicite |
| Contribuer ou retirer | Management | `bdvm.management.wallet-transfer.v1` | montant et payer explicites |
| Renommer, changer owner/operator/state | Management | `bdvm.management.fleet.rename.v1` ou `fleet-manage.v1` | selon mutation |
| Créer un bundle, revendre ou lancer une maintenance manuelle | Management | `fleet-bundle.v1`, `fleet-resale.v1`, `fleet-maintenance.v1` | obligatoire pour revente/maintenance |
| Acheter dans le catalogue et livrer une première fois | Management | `market.purchase.v1`, `initial-delivery.v1` | owner, payer et track explicites |
| Créer, rendre ou acheter une location | Management | `lease-manage.v1` | obligatoire pour engagement financier |
| Affecter ou annuler une mission | Management | `assignment-manage.v1` | suivant état autoritaire |
| Financement, plan de triage et industrie | Management | `finance-manage.v1`, `yard-manage.v1`, `industry-manage.v1` | adapters/capabilities requis |
| Prévisualiser ou appliquer une route | Dispatch | `bdvm.dispatch.route-control.v1` | token/expected version host |
| Commander un aiguillage | Dispatch | `bdvm.dispatch.junction-control.v1` | permission Dispatch dédiée |

## Frontières

- Web possède session, sécurité, module registry, navigation, connexion et realtime.
- Dispatch possède la représentation du réseau physique ; il ne connaît ni wallet, ni compagnie, ni marché.
- Management possède les vues et intents économiques ; il ne contrôle ni infrastructure, ni transport HTTP.
- Le transport authentifie le principal. `WebIntentGateway` vérifie session, CSRF, same-origin, permission, version, idempotency key, taille et rate limit avant d'appeler `IAuthoritativeWebIntentExecutor`.
- L'executor autoritaire appartient au runtime host et reste hors de ces frontends.
