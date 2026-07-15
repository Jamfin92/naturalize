# Naturalize

An open-source records system for tracking N-400 naturalization applicants — from filing, through
biometrics and interview, to the oath of allegiance — and for recording the decisions made along the
way. The operator is a caseworker or clerk, not the applicant.

> **This is a reference implementation, not a government system.** It is not affiliated with USCIS or
> any government agency, it is not legal advice, and every applicant, A-Number, receipt number and
> decision in the seeded database is **fabricated**. It ships with demo officer accounts whose
> passwords are printed below and a signing key committed to the repo — read
> [Before you deploy this](#before-you-deploy-this) before pointing it at real records.

> **This is the full-featured branch.** The default branch,
> [`master`](https://github.com/Jamfin92/naturalize/tree/master), is deliberately narrowed to
> *applicants + reports*. This branch (`enhancement/case-workflow`) adds the case queue, guarded
> status transitions, approvals/decisions and evidence handling on top of the same auth, migrations,
> soft deletes and tests. See [Scope](#scope).

## What's in it

| | |
|---|---|
| **Frontend** | Vite 8 · React 19 · TypeScript · Tailwind v4 · shadcn/ui · React Router 7 |
| **Backend** | ASP.NET Core 8 minimal APIs · EF Core 8 (migrations) · SQLite |
| **Auth** | Local JWT forms auth (HS256), with a dormant Okta carve-out |
| **Reports** | PDFsharp + MigraDoc (MIT) — four server-rendered PDFs with embedded fonts |
| **Tests** | xUnit integration tests over the real host · Playwright end-to-end |
| **Licence** | MIT |

- **Applicants** — a searchable, paginated register. Add, edit, view, and *withdraw* (a soft delete
  that keeps the record and its audit trail), with restore.
- **Cases** — the N-400 lifecycle as an explicit state machine, filterable by status, with an
  append-only audit trail. Status changes go through a guarded endpoint that rejects illegal jumps.
- **Approvals** — record an approve / deny / continue decision against a case that has completed
  interview. Writes the decision, advances the case, and appends to the audit trail atomically.
- **Reports** — Case Record, Approvals (per-office approval rates), Pipeline (caseload by status,
  plus aging), and Mailing labels (Avery 5160, one per active applicant), each rendered server-side as
  a PDF with embedded fonts.
- **Record history** — every change to a record, and the officer who made it, taken from their bearer
  token, never from the request body.

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
fabricated applicants across every case status, so every screen and every report has something to
show.

> **Upgrading from an older checkout?** The pre-migrations builds used `EnsureCreated`, which leaves a
> `naturalization.db` with no migrations-history table. `MigrateAsync()` will then try to create
> tables that already exist and fail at startup. Delete `api/src/Naturalization.Api/naturalization.db`
> (and its `-wal` / `-shm` siblings) once, and it rebuilds cleanly. It is gitignored.

## Authentication

Sign-in exchanges credentials for a JWT; every endpoint except `/health` and `/api/auth/login`
requires it. The officer identity in the token is what gets written into the audit trail against
every case transition and decision — so it comes from the token, never from the request body. (An
earlier build let the client name its own actor, which meant a decision could be signed with anyone's
name.)

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
redirect flow, which this does not build — but [`src/lib/token.ts`](src/lib/token.ts) is the single
place the app reads its bearer token, so an OIDC flow only has to deposit one there.

## The design decisions worth arguing about

### Should the app window be resizable?

This app's ancestor was a desktop tool that lived in the corner of the screen. The obvious move is to
reproduce that: a small, persistent panel. **We didn't, and the reasoning matters more than the
conclusion.** A browser tab cannot float above your other applications — it can only dock to the
corner of its *own* page, so a "corner panel" is a small panel on an otherwise empty page, reproducing
the *shape* of the idea while discarding the *point*. And nothing here is widget-shaped: applicant
records, case queues and decision entry are tables and multi-field forms that a 380px panel makes
strictly worse than a phone layout. So: fully responsive, conventional app shell, no dock — targeting
1280px+ and degrading to tablet.

The one honest way back, if a persistent widget-shaped task ever appears (a "cases awaiting my
decision" ticker is the candidate): the
[Document Picture-in-Picture API](https://developer.chrome.com/docs/web-platform/document-picture-in-picture)
opens a genuine always-on-top OS window rendering your own DOM. It is the only web API that actually
reproduces the desktop original.

### Soft deletes, because an audit trail you can destroy is not one

Deleting an applicant used to cascade through their cases, evidence and — fatally — their entire audit
trail. Now withdrawing an applicant **stamps and hides** the record; the row, its cases and its events
stay in the database, hidden by a query filter and readable with `IgnoreQueryFilters()`. All four
foreign keys are `DeleteBehavior.Restrict`, so an accidental *hard* delete throws instead of erasing
the record. An A-Number is never released — re-adding a withdrawn one returns an actionable **409**,
not the raw 500 the unfiltered unique index would otherwise throw. The invariants are written into
[`NaturalizationDbContext`](api/src/Naturalization.Api/Data/NaturalizationDbContext.cs).

### The state machine has one home

Legal case transitions are defined once, in
[`Services/StatusTransitions.cs`](api/src/Naturalization.Api/Services/StatusTransitions.cs). Every case
read returns its `allowedTransitions`, and the UI renders *exactly those* as buttons — so the frontend
cannot drift from the rules, and there is deliberately no endpoint that sets `Status` to an arbitrary
value.

### Reports are fetched with the token, not linked

The download buttons used to be plain `<a href>` links, but a browser *navigation* cannot carry an
`Authorization` header — so once reports went behind auth, every link would have opened a blank tab of
401 JSON. They now fetch the PDF with the bearer token and save the blob; CORS exposes
`Content-Disposition` so the server-chosen filename survives.

### Why not QuestPDF

QuestPDF has a nicer API, but its Community licence is free only *below a revenue threshold* — a
field-of-use restriction that is **not OSI-approved** and silently binds every downstream fork. This
is meant for nonprofits and legal-aid clinics, so **PDFsharp + MigraDoc (MIT)** it is, behind
[`IReportGenerator`](api/src/Naturalization.Api/Reports/IReportGenerator.cs) so a private fork can swap
QuestPDF back in by replacing one file. (iText is AGPL; headless-Chromium HTML→PDF is 150–300 MB plus
an XSS/SSRF surface. Both rejected.)

### Fonts are embedded, and that's not incidental

PdfSharp has **no default font resolver on macOS or Linux** — without one, the first render throws. So
PT Serif and PT Sans are compiled into the assembly and served by
[`EmbeddedFontResolver`](api/src/Naturalization.Api/Reports/EmbeddedFontResolver.cs), and report output
is identical on a laptop, in CI, and in a container with no system fonts. Both ship *static* faces
(PdfSharp cannot instance a variable-font weight axis), and the web app uses the same two.

### The theme

"Parchment & Old Glory". The flag's own colours, converted to OKLCH, *are* the semantic tokens: Old
Glory Blue is `--primary`, Old Glory Red is `--destructive`, over warm parchment neutrals. The flag is
built to **Executive Order 10834** geometry — 1:1.9, union 7/13 hoist × 0.76 fly, 50 stars from the
9-row 6/5 grid — and never changes colour in dark mode.

## Scope

This branch is the full case-management app. The default branch,
[`master`](https://github.com/Jamfin92/naturalize/tree/master), is narrowed to *applicants + reports*
— the case queue, transitions, approvals and evidence screens and their endpoints are removed there,
though the underlying domain stays so the reports still work. Both branches share the same auth,
migrations, soft deletes and test suites, so this branch remains mergeable into `master`.

## API

Swagger UI at `http://localhost:5099/swagger`. Everything except `/health` and `/api/auth/login`
requires a bearer token.

```
POST   /api/auth/login                          GET    /api/auth/me

GET    /api/applicants?q=&page=&pageSize=       POST   /api/applicants
GET    /api/applicants/{id}                     PUT    /api/applicants/{id}
DELETE /api/applicants/{id}   <- soft delete    POST   /api/applicants/{id}/restore
GET    /api/applicants/{id}/cases               GET    /api/applicants/{id}/history

GET    /api/cases?q=&status=&page=&pageSize=    POST   /api/cases
GET    /api/cases/{id}                          DELETE /api/cases/{id}   <- soft delete
GET    /api/cases/{id}/events
POST   /api/cases/{id}/status                   <- guarded transition; rejects illegal jumps

GET    /api/decisions?from=&to=&fieldOffice=    POST   /api/decisions
GET    /api/decisions/{id}

GET    /api/documents?caseId=                   POST   /api/documents
PUT    /api/documents/{id}/status

GET    /api/metrics
GET    /api/reports/case/{id}.pdf
GET    /api/reports/approvals.pdf?from=&to=&fieldOffice=
GET    /api/reports/pipeline.pdf
GET    /api/reports/labels.pdf
```

## Tests

```bash
dotnet test api/Naturalization.sln   # xUnit integration tests over a real WebApplicationFactory host
npx playwright test                  # end-to-end: a real browser -> SPA -> API -> SQLite
```

## Before you deploy this

It is a reference implementation. To hold real records it still needs, at minimum:

- **A real signing key and a real identity provider.** Set `Auth__Jwt__Key` from a secret store, and
  either replace the seeded officer accounts or turn on the Okta path (and build the frontend OIDC
  flow it needs). There are **no refresh tokens** and **no roles** — anyone signed in can decide any
  case — and a deactivated account keeps working until its token expires.
- **Real document storage.** `/api/documents` registers *metadata only* and never accepts file bytes.
  Accepting immigration evidence means virus scanning, content-type sniffing, encryption at rest, a
  retention policy and access logging — choices a deployer must make.
- **Your own CORS origins.** Currently pinned to the Vite dev server; see
  [`Program.cs`](api/src/Naturalization.Api/Program.cs).
- **A privacy and retention review.** This models country of origin, immigration status and denial
  reasons — data whose exposure can genuinely hurt people.

### Known limitations

- **No roles.** Authorisation is out of scope; a half-built role system reads as a real one.
- A `Decision` is unique per case, so a *Continued* decision leaves the case at `InterviewCompleted`
  but blocks it from ever being decided again. Modelling decisions as an ordered history would fix it.
- **SQLite migrations** rebuild a table to drop or alter a column — review any generated migration,
  because anything not in the EF model (hand-added triggers, indexes) is lost in the rebuild.
- `medianDaysToDecision` counts filing→decision, which flatters the number when a case sat waiting on
  the applicant rather than on the office.

## Licence

MIT — see [LICENSE](LICENSE). Bundled PT Serif and PT Sans are SIL OFL; every dependency is
permissively licensed. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
