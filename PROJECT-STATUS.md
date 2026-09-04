# Project Status — E-Commerce

> Last updated: 2026-08-29. This file describes **this project only**. Every project in the
> workspace has its own status file; nothing here carries over to another.
>
> This repo already documents itself unusually well. This file is the entry point and the summary;
> it does not duplicate what those files hold:
> - `docs/current-state.md` — the authoritative, phase-by-phase account of what is built
> - `docs/decisions.md` — the ADR log (47+ decisions with rationale)
> - `docs/modules.md`, `docs/architecture.md`, `docs/security.md`, `docs/ci-cd.md`

---

## 1. Done and closed

Ten modules with real Domain/Application/Infrastructure code: Catalog, Inventory, Ordering,
Payments, Identity, Notifications, Customers, Promotions, Shipping, Reviews. Storefront, admin
panel, checkout, payments (fake gateway with real webhook signature verification and idempotency),
Redis caching, transactional Outbox with an in-process event bus, rate limiting, sitemap/robots.txt,
Arabic/English localization including the admin area, and a full design-system redesign with dark
mode.

Recent, and not covered by the phase log before this pass:
- **Security headers**, with CSP deliberately in **Report-Only** mode — enforcing it against a
  purchased template's inline styles and scripts would break the storefront. Report-Only collects
  the violations first.
- **CI gates on vulnerable NuGet packages.** The step inspects the command's *output*, not its exit
  code: `dotnet list package --vulnerable` exits 0 even when it finds something, so merely running
  it would have reported problems and passed anyway.
- **Dependabot** — weekly NuGet + github-actions, with Microsoft/System and test tooling grouped so
  a .NET release train arrives as one PR.
- **NuGet restore caching** — previously recorded as blocked on a `packages-lock.json` this repo
  does not generate. It was not blocked: `cache-dependency-path` hashes any file, and Central
  Package Management already puts every version in `Directory.Packages.props`.
- **Shared design system, Amber Commerce theme** — colour now comes from `MeCodex/design-system`.
  The Ecomus theme and the Young Serif / Albert Sans type stack are untouched on purpose.
- **Branch protection on `main`** is live and **enforced against admins too** (2026-08-29).
  `build-and-test` must pass before merge, force-pushes and deletions are blocked, and
  `enforce_admins` is on — so `main` is only reachable through a pull request with a green check.
  It previously shipped with `enforce_admins: false`, which meant a direct push from the owner
  bypassed the required check and said so (`Bypassed rule violations`). Verified by attempting a
  direct push, which is now refused with `GH006`.

---

## 2. Decisions adopted

| Decision | Detail |
|---|---|
| **Modular monolith, not microservices** | Full rationale in `docs/architecture.md` |
| **Azure** as the primary deployment target | Not yet wired |
| **Azure Key Vault** for production secrets | Not yet wired |
| **App Insights + Sentry** for monitoring | Not yet wired |
| **No Redis beyond caching** | Redis is already here for caching. The workspace decision to add Redis to PosFlow / Gym Manager / RealEstateCRM does not change anything in this repo |
| **Amber Commerce theme** | Colour only. Type stack and the purchased Ecomus theme stay |
| **No API versioning** | This app exposes one webhook receiver, not a public API. Versioning would be ceremony with no consumer |
| **Scope cuts, each with an ADR** | No Tax module, no 2FA/social login, no Wishlist module |

---

## 3. Still open

- **Azure deployment** — no CD to a real environment.
- **Azure Key Vault** — secrets come from configuration and User Secrets today.
- **Application Insights + Sentry** — neither is installed.
- **CSP is Report-Only.** Nothing consumes the violation reports yet, so the data that would let it
  be enforced is not being collected anywhere durable. Enforcing it is blocked on that.
- **`docker compose up --build` has never run here** — genuinely attempted; Docker Desktop's
  backend never reaches a ready state in this environment. The compose file is therefore unverified.

---

## 4. Known issues / technical debt

- **17 analyzer warnings**, all style/performance suggestions in working code:
  24 `CA1861` (constant array arguments should be static readonly), 6 `CA1873` (potentially
  expensive logging), 2 `CA1826` (`FirstOrDefault` on an indexable collection), 2 `CA1805`
  (`Unit.Value = default`, where the explicit default is arguably clearer than the analyzer's
  preference). Left alone deliberately: clearing them is a mechanical pass over two dozen call
  sites in working code — tidiness, not correctness.
- **`ecomus-package/` is 175 MB and 2205 tracked files.** It is the original purchased template
  plus its documentation. The app does **not** use it — the storefront serves a curated subset
  copied into `wwwroot/ecomus` and `wwwroot/admin-ecomus`. It is kept because a purchased asset has
  real future use (pulling further components), but it makes every clone heavy. Git LFS or storing
  it outside the repo would be the fix.
- **Fake payment gateway.** The webhook mechanics (signature verification, idempotency) are real;
  the gateway is not. No real PSP integration exists.

---

## 5. Deliberately deferred

| Item | Why |
|---|---|
| **Enforcing CSP** | The purchased template relies on inline styles and scripts. Enforcing before collecting Report-Only data would break the storefront with no evidence about what to allow |
| **Deleting `ecomus-package/`** | It is a purchased asset with plausible future use, which is exactly the case the cleanup brief excludes. Flagged as repo weight instead |
| **Clearing the 17 analyzer warnings** | A mechanical refactor across working code for tidiness alone |
| **API versioning** | One webhook receiver, no public API consumers |
| **Tax module, 2FA/social login, Wishlist** | Explicit scope cuts, each recorded in its own ADR |

---

## Update 2026-08-30 — Key Vault, App Insights, and enforced protection

| Feature | Enabled by | Notes |
|---|---|---|
| Azure Key Vault | `KeyVault__Uri` | Registered as the first configuration step, so connection strings and the payment webhook signing secret can come from a vault |
| Application Insights | `APPLICATIONINSIGHTS_CONNECTION_STRING` | Reads through configuration, so a value held in Key Vault works |

Both are **inert until configured** — each registers only when its value is present, so nothing
changes for a deployment that supplies neither.

**Branch protection now applies to admins too.** It previously shipped with `enforce_admins: false`,
which meant a direct push from the owner bypassed the required `build-and-test` check and said so
(`Bypassed rule violations`). Verified by attempting a direct push, which is now refused with
`GH006`. `main` is reachable only through a pull request with a green check — for everyone.

**No browser-side Sentry here, deliberately.** The workspace decision was "Sentry frontend", and
that was applied to the five application frontends. This storefront is server-rendered Razor: the
logic that can fail lives on the server, where Application Insights now covers it, and the
client-side JavaScript is largely the purchased Ecomus template. Adding a browser error reporter
would also interact with the Content-Security-Policy work that is still in Report-Only mode. If
client-side reporting is wanted later, Application Insights' own JavaScript snippet is the natural
fit, since the backend is already reporting there.

---

## Update 2026-09-04 — one branch, protected; routine dependency PRs off

**This repo keeps a single branch: `main`.** Any leftover Dependabot branches were deleted and no
long-lived branches are kept.

**`main` is protected**, and the protection is deliberately the kind that fits a one-branch
workflow:

| Setting | Value | Why |
|---|---|---|
| Force pushes | **blocked** | History cannot be rewritten or silently rolled back. Verified by attempting one and having it rejected |
| Branch deletion | **blocked** | `main` cannot be removed |
| Applies to admins | **yes** | The owner is not exempt; that exemption was the hole fixed on E-Commerce earlier |
| Required status checks | **none** | Deliberate trade-off. Required checks make direct pushes to `main` impossible and force every change through a branch and PR, which is exactly what the one-branch decision rules out. CI still runs on every push — it reports rather than gates |

**Routine dependency PRs are off.** Every `open-pull-requests-limit` in `.github/dependabot.yml` is
`0`, because weekly version bumps meant a continuous stream of branches to merge or close.
**Security updates are unaffected** — Dependabot ignores that limit for security advisories, so a
dependency with a known vulnerability still opens a PR. Set the limits back to a non-zero number to
bring routine updates back.
