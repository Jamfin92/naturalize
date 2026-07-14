# Naturalize

An open-source records system for a naturalization caseworker: **add an applicant, keep their record
current, and print reports.** The operator is a clerk or adjudicator, not the applicant.

> **This is a reference implementation, not a government system.** It is not affiliated with USCIS or
> any government agency, it is not legal advice, and every applicant, A-Number, receipt number and
> decision in the seeded database is **fabricated**. It ships with demo officer accounts whose
> passwords are printed below and a signing key committed to the repo — read
> [Before you deploy this](#before-you-deploy-this) before pointing it at real records.

This branch (`master`) is deliberately small. The case queue, status transitions, approvals and
evidence handling live on [`enhancement/case-workflow`](https://github.com/Jamfin92/naturalize/tree/enhancement/case-workflow) —
see [Scope](#scope).

## What's in it

| | |
|---|---|
| **Frontend** | Vite 8 · React 19 · TypeScript · Tailwind v4 · shadcn/ui · React Router 7 |
| **Backend** | ASP.NET Core 8 minimal APIs · EF Core 8 (migrations) · SQLite |
| **Auth** | Local JWT forms auth (HS256), with a dormant Okta carve-out |
| **Reports** | PDFsharp + MigraDoc (MIT) — three server-rendered PDFs with embedded fonts |
| **Tests** | xUnit integration tests over the real host · Playwright end-to-end |
| **Licence** | MIT |

- **Applicants** — a searchable, paginated register. Add, edit, view, and *withdraw* (a soft delete
  that keeps the record and its audit trail), with restore.
- **Record history** — every change to a record, and the officer who made it, taken from their bearer
  token. Append-only: withdrawing a record adds to this trail rather than erasing it.
- **Reports** — Case Record, Approvals (per-office approval rates), and Pipeline (caseload by status,
  plus aging), each rendered server-side as a PDF with embedded fonts.

## Running it

You need **Node 20+** and the **.NET 8 SDK**.

```bash
npm install
npm run dev:all      # API on :5099 (Swagger at /swagger) + frontend on :5173, together
```

`dev:all` runs both servers in one terminal and Ctrl+C stops both. The API applies migrations and
seeds a SQLite database on first run. Prefer separate terminals? Run them independently — `npm run
dev` starts **only** the frontend:

```bash
dotnet run --project api/src/Naturalization.Api   # API   → http://localhost:5099
npm run dev                                        # front → http://localhost:5173
```

Sign in with **`a.hernandez@example.gov`** / **`Naturalize!Demo1`**. The seeded database contains 40
fabricated applicants across every case status, so the register and every report have something to
show.

> **Upgrading from an older checkout?** The pre-migrations builds used `EnsureCreated`, which leaves a
> `naturalization.db` with no migrations-history table. `MigrateAsync()` will then try to create
> tables that already exist and fail at startup. Delete `api/src/Naturalization.Api/naturalization.db`
> (and its `-wal` / `-shm` siblings) once, and it rebuilds cleanly. It is gitignored, so there is
> nothing to commit.

## Authentication

Sign-in exchanges credentials for a JWT; every endpoint except `/health` and `/api/auth/login`
requires it. The officer identity in the token is what gets written into the audit trail against
every change — so it comes from the token, never from the request body. (An earlier build let the
client name its own actor, which meant the trail could be signed with anyone's name.)

**Demo officers** (seeded, passwords public because every applicant is fabricated):

| Email | Password | Field office |
|---|---|---|
| `a.hernandez@example.gov` | `Naturalize!Demo1` | Boston, MA |
| `m.whitfield@example.gov` | `Naturalize!Demo1` | Hartford, CT |

**A real signing key** is required in any non-development run — startup validates it and refuses to
boot without one. Generate one and pass it through the environment; never commit it:

```bash
export Auth__Jwt__Key="$(openssl rand -base64 48)"
```

The dev key in `appsettings.Development.json` exists only so `dotnet run` works on a fresh clone. It
is public and every fork shares it, which is exactly why it must never reach a deployment.

### The Okta carve-out

The backend is wired for Okta but it is **off**. A [policy scheme](api/src/Naturalization.Api/Auth/AuthExtensions.cs)
routes each request on its token's `iss` claim, so with Okta enabled the API validates Okta-issued
tokens *alongside* locally-issued ones — local sign-in keeps working while you migrate. Turn it on
with three config values:

```jsonc
"Auth": {
  "Okta": {
    "Enabled":   true,
    "Authority": "https://<your-org>.okta.com/oauth2/default",
    "Audience":  "api://naturalize"
  }
}
```

**Honest limit:** the *backend* path is genuinely config-only. The *frontend* still needs an OIDC
redirect flow, which this does not build — but the seam is ready for it:
[`src/lib/token.ts`](src/lib/token.ts) is the single place the app reads its bearer token, so an OIDC
flow only has to deposit one there. Nothing downstream cares which provider minted it.

## The design decisions worth arguing about

### Soft deletes, because an audit trail you can destroy is not one

Deleting an applicant used to cascade through their cases, evidence and — fatally — their entire
audit trail. Now:

- Withdrawing an applicant **stamps and hides** the record; the row, its cases and its events all
  stay in the database. A query filter removes them from every read path; `IgnoreQueryFilters()`
  reads them back. All four foreign keys are `DeleteBehavior.Restrict`, so an accidental *hard* delete
  now throws instead of silently erasing the record.
- **An A-Number is never released.** Its unique index is deliberately unfiltered — a withdrawn
  applicant keeps their number. So re-adding it returns an actionable **409** ("belongs to withdrawn
  applicant #N, restore instead"), not the raw 500 the index would otherwise throw. The same applies
  to a case's receipt number.
- **Withdrawing an applicant does not stamp their cases as deleted.** The chained query filter hides
  them anyway, and leaving them unstamped keeps "hidden because the applicant was withdrawn" distinct
  from "deleted on its own merits" — so restore can't resurrect a case that was meant to stay gone.

The invariants and the reasons they bite are written into
[`NaturalizationDbContext`](api/src/Naturalization.Api/Data/NaturalizationDbContext.cs).

### Reports are fetched with the token, not linked

The download buttons used to be plain `<a href>` links. A browser *navigation* cannot carry an
`Authorization` header, so the moment reports went behind auth, every link would have opened a blank
tab full of 401 JSON. They now fetch the PDF with the bearer token and save the blob; CORS exposes
`Content-Disposition` so the server-chosen filename survives. Putting the token in the query string to
keep the `<a href>` would have leaked a full-lifetime bearer into browser history, the `Referer`
header and every proxy log — rejected.

### Why not QuestPDF

QuestPDF has a nicer API, but its Community licence is free only *below a revenue threshold* — a
field-of-use restriction that is **not OSI-approved**, that Debian/Fedora packaging would reject, and
that silently binds every downstream fork. This is meant to be picked up by nonprofits and legal-aid
clinics; handing them a dependency with a revenue trigger is the wrong trade. **PDFsharp + MigraDoc is
MIT.** The cost is a one-time ~200-line helper in
[`Reports/ReportTheme.cs`](api/src/Naturalization.Api/Reports/ReportTheme.cs); everything sits behind
[`IReportGenerator`](api/src/Naturalization.Api/Reports/IReportGenerator.cs) so a private fork can swap
QuestPDF back in by replacing one file. (iText is AGPL — viral over the network. Headless-Chromium
HTML→PDF drags 150–300 MB into every container plus an XSS/SSRF surface. Both rejected.)

### Fonts are embedded, and that's not incidental

PdfSharp has **no default font resolver on macOS or Linux** — without one, the first render throws. So
the reports carry PT Serif and PT Sans compiled into the assembly, served by
[`EmbeddedFontResolver`](api/src/Naturalization.Api/Reports/EmbeddedFontResolver.cs). Report output is
therefore identical on a laptop, in CI, and in a slim container with no system fonts. Both families
ship *static* faces (the reason they were chosen over Libre Baskerville, now variable-only — PdfSharp
cannot instance a weight axis), and the web app uses the same two, so screen and print are one stack.

### The theme

"Parchment & Old Glory". The flag's own colours, converted to OKLCH, *are* the semantic tokens: Old
Glory Blue is `--primary`, Old Glory Red is `--destructive`, over warm parchment neutrals rather than
grey. The flag itself is built to **Executive Order 10834** geometry — 1:1.9, union 7/13 hoist × 0.76
fly, 50 stars generated from the 9-row 6/5 grid — and never changes colour in dark mode.

## Scope

`master` is **applicants + reports**. Everything that mutates a *case* — the queue, guarded status
transitions, approvals/decisions, and evidence — lives on
[`enhancement/case-workflow`](https://github.com/Jamfin92/naturalize/tree/enhancement/case-workflow).

The split is by *screen and endpoint*, not by domain. The case/decision/document/event model, the
`CaseMetrics` service and the `StatusTransitions` state machine all remain on `master`, because the
reports read them — the Case Record PDF prints a case's audit trail, and Pipeline reads the whole
caseload. The enhancement branch carries the same auth, migrations, soft deletes and tests, so it
stays mergeable.

## API

Swagger UI at `http://localhost:5099/swagger`. Everything except `/health` and `/api/auth/login`
requires a bearer token.

```
POST   /api/auth/login                          GET    /api/auth/me

GET    /api/applicants?q=&page=&pageSize=       POST   /api/applicants
GET    /api/applicants/{id}                     PUT    /api/applicants/{id}
DELETE /api/applicants/{id}   <- soft delete    POST   /api/applicants/{id}/restore
GET    /api/applicants/{id}/cases               GET    /api/applicants/{id}/history

GET    /api/reports/case/{id}.pdf
GET    /api/reports/approvals.pdf?from=&to=&fieldOffice=
GET    /api/reports/pipeline.pdf
```

## Tests

```bash
dotnet test api/Naturalization.sln   # xUnit integration tests over a real WebApplicationFactory host
npx playwright test                  # end-to-end: a real browser -> SPA -> API -> SQLite
```

The xUnit suite covers auth (including that unknown-email and wrong-password return the *same* 401),
the token-derived audit actor, soft-delete preservation, restore, and the 409-not-500 A-Number
collision. Playwright drives the real app: unauthenticated redirect, a rejected password, add, edit,
that withdrawing a record keeps its trail, and that all three PDFs actually download behind auth.

## Before you deploy this

It is a reference implementation. To hold real records it still needs, at minimum:

- **A real signing key and a real identity provider.** Set `Auth__Jwt__Key` from a secret store, and
  either replace the seeded officer accounts or turn on the Okta path (and build the frontend OIDC
  flow it needs). There are **no refresh tokens** and **no roles** — anyone who can sign in has the
  same access — and a deactivated account keeps working until its token expires.
- **Real document storage.** `EvidenceDocument` registers *metadata only* and never accepts file
  bytes. Accepting immigration evidence means virus scanning, content-type sniffing, encryption at
  rest, a retention policy and access logging — choices a deployer must make.
- **Your own CORS origins.** Currently pinned to the Vite dev server; see
  [`Program.cs`](api/src/Naturalization.Api/Program.cs).
- **A privacy and retention review.** This models country of origin, immigration status and denial
  reasons — data whose exposure can genuinely hurt people.

### Known limitations

- **No roles.** Authorisation is out of scope on both branches; a half-built role system reads as a
  real one, so there isn't one.
- **SQLite migrations** rebuild a table to drop or alter a column — review any generated migration,
  because anything not in the EF model (hand-added triggers, indexes) is lost in the rebuild.
- On the enhancement branch, a *Continued* decision leaves a case blocked from ever being decided
  again, because `Decision` is unique per case; modelling decisions as an ordered history would fix it.

## Licence

MIT — see [LICENSE](LICENSE). Bundled PT Serif and PT Sans are SIL OFL; every dependency is
permissively licensed. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
