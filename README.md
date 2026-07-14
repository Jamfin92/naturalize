# Naturalize

An open-source records system for tracking N-400 naturalization applicants — from filing, through
biometrics and interview, to the oath of allegiance — and for recording the decisions made along the
way.

The operator is a caseworker or clerk, not the applicant. It answers: *who is on file, where is each
case, what evidence is in, who decided what, and when.*

> **This is a reference implementation, not a government system.** It is not affiliated with USCIS or
> any government agency, it is not legal advice, and every applicant, A-Number, receipt number and
> decision in the seeded database is **fabricated**. Authentication is stubbed: any email signs you
> in. Read [Before you deploy this](#before-you-deploy-this) before pointing it at real records.

## What's in it

| | |
|---|---|
| **Frontend** | Vite 8 · React 19 · TypeScript · Tailwind v4 · shadcn/ui · React Router 7 |
| **Backend** | ASP.NET Core 8 minimal APIs · EF Core 8 · SQLite |
| **Reports** | PDFsharp + MigraDoc (MIT) — three server-rendered PDFs with embedded fonts |
| **Licence** | MIT |

- **Applicants** — searchable, paginated register; personal particulars and case history.
- **Cases** — the N-400 lifecycle as an explicit state machine, filterable by status, with an
  append-only audit trail.
- **Approvals** — record an approve / deny / continue decision against a case that has completed
  interview. Writes the decision, advances the case, and appends to the audit trail atomically.
- **Reports** — Case Record, Approvals (with per-office approval rates), and Pipeline (caseload by
  status, plus aging).

## Running it

You need **Node 20+** and the **.NET 8 SDK**.

```bash
# 1. API — http://localhost:5099 (Swagger at /swagger)
#    Creates and seeds a SQLite database on first run.
dotnet run --project api/src/Naturalization.Api

# 2. Frontend — http://localhost:5173
npm install
npm run dev
```

Sign in with any email address. The seeded database contains 40 fabricated applicants spread across
every case status, so every screen and every report has something to show.

To start over, delete `api/src/Naturalization.Api/naturalization.db` and re-run.

## The design decisions worth arguing about

### Should the app window be resizable?

This app's ancestor was a desktop tool that lived in the corner of the screen. The obvious move is to
reproduce that: a small, persistent, always-there panel. **We didn't, and the reasoning matters more
than the conclusion.**

**A browser tab cannot float above your other applications.** It can only dock to the corner of its
*own page*. The desktop original's entire value was that it floated over your work while you did
something else. In a tab, if you are looking at our page then you are by definition not doing
anything else — so a "corner panel" is a small panel on an otherwise empty page. That reproduces the
*shape* of the idea while discarding the *point* of it, and a great many corner-docked web apps are
exactly this cargo cult.

The second problem is the content. A corner dock earns its keep when the task is *widget-shaped*:
short, repetitive, high-frequency, low-density — the reason flashcard drilling works in a tiny window,
and why Duolingo owns the phone. **Nothing in this app is widget-shaped.** Applicant records, case
queues, decision entry, evidence checklists and audit timelines are tables and multi-field forms.
Squeezing a case queue into 380px produces something strictly worse than a phone layout.

So: **fully responsive and resizable, conventional app shell, no dock.** The real target is 1280px
and up — this is a desk tool — degrading gracefully to tablet.

**The one honest way back**, recorded here in case a widget-shaped task ever appears (a "cases
awaiting my decision" ticker is the plausible candidate): the
[Document Picture-in-Picture API](https://developer.chrome.com/docs/web-platform/document-picture-in-picture)
opens a genuine always-on-top OS-level window that renders your own DOM. It is the *only* web API
that actually reproduces the desktop original. Chromium 116+, so roughly 70% of desktop users —
progressive enhancement, feature-detected. The gotcha to know before starting: the PiP window is a
separate document with no stylesheets, so you must copy `document.styleSheets` across, and you must
test it under `vite preview` rather than `vite dev` (dev injects `<style>`, prod ships `<link>`).

### Container queries, in exactly one place

`<main>` in the app shell is a container (`@container`), and its children reflow with `@3xl:` rather
than `3xl:`. This is not fashion: **the sidebar collapses, which changes how much room `main` has
without the viewport changing at all.** A viewport media query is physically incapable of seeing
that. Everything else in the app uses ordinary media queries, because everything else genuinely does
depend on the viewport.

### Why not QuestPDF

QuestPDF has a nicer API than MigraDoc, and for a private product it would be the easy pick. Its
Community licence is free only *below a revenue threshold* — a field-of-use restriction that is **not
OSI-approved** (it fails OSD §6), that Debian and Fedora packaging would reject, and that silently
binds every downstream fork. Your code must also *assert* `LicenseType.Community`, which is a legal
claim your users may not be entitled to make.

This project is meant to be picked up and run by nonprofits and legal-aid clinics. Handing them a
dependency with a revenue trigger is the wrong trade. **PDFsharp + MigraDoc is MIT**: OSI-approved,
GPL-compatible, and it imposes nothing on anyone who forks or deploys this.

The cost is real but bounded — MigraDoc is a document object model (styled tables, headers, automatic
pagination), so the ergonomics gap is a one-time ~200-line helper in
[`Reports/ReportTheme.cs`](api/src/Naturalization.Api/Reports/ReportTheme.cs). Paid once, here,
instead of taxing every downstream user forever. Everything sits behind
[`IReportGenerator`](api/src/Naturalization.Api/Reports/IReportGenerator.cs), so a private fork under
the threshold swaps in QuestPDF by replacing one file.

(iText is AGPL — viral over the network, so anyone *deploying* the API would have to open-source
their whole stack. Headless-Chromium HTML→PDF drags 150–300 MB into every container and adds an
XSS/SSRF surface. Both rejected.)

### Fonts are embedded, and that's not incidental

PdfSharp has **no default font resolver on macOS or Linux** — without one, the first render throws.
So the reports carry their own: PT Serif and PT Sans TTFs are compiled into the assembly and served
by [`EmbeddedFontResolver`](api/src/Naturalization.Api/Reports/EmbeddedFontResolver.cs). Report output
is therefore identical on a laptop, in CI, and in a slim container with no system fonts installed.

Both families ship *static* faces, which is why they were chosen over the nicer-looking Libre
Baskerville: Google Fonts now distributes that one as a variable font only, and PdfSharp cannot
instance a weight axis — so every bold heading would silently render wrong in print. The web app uses
the same two families, so screen and print are one font stack rather than two.

### The state machine has one home

Legal case transitions are defined once, in
[`Services/StatusTransitions.cs`](api/src/Naturalization.Api/Services/StatusTransitions.cs). Every
case read returns its `allowedTransitions`, and the UI renders *exactly those* as buttons. The
frontend does not know the rules and cannot drift from them, and there is deliberately no endpoint
that sets `Status` to an arbitrary value — every transition is validated and writes an audit event.

### The theme

"Parchment & Old Glory". The flag's own colours, converted to OKLCH, *are* the semantic tokens: Old
Glory Blue is `--primary`, Old Glory Red is `--destructive`. Two hues plus a brass accent, over warm
parchment neutrals (hue ~85) rather than grey. Those warm neutrals are most of what keeps it from
reading as default shadcn slate.

The flag itself is built to **Executive Order 10834** geometry — 1:1.9 proportions, union 7/13 hoist
× 0.76 fly, 50 stars generated from the 9-row 6/5 grid — not eyeballed. It never changes colour in
dark mode: `--flag-red`, `--flag-white` and `--flag-blue` are literals, deliberately outside the
theme.

## API

Swagger UI at `http://localhost:5099/swagger`.

```
GET    /api/applicants?q=&page=&pageSize=      POST   /api/applicants
GET    /api/applicants/{id}                    PUT    /api/applicants/{id}
DELETE /api/applicants/{id}                    GET    /api/applicants/{id}/cases

GET    /api/cases?q=&status=&page=&pageSize=   POST   /api/cases
GET    /api/cases/{id}                         DELETE /api/cases/{id}
GET    /api/cases/{id}/events
POST   /api/cases/{id}/status                  <- guarded transition; rejects illegal jumps

GET    /api/decisions?from=&to=&fieldOffice=   POST   /api/decisions
GET    /api/decisions/{id}                     DELETE /api/decisions/{id}

GET    /api/documents?caseId=                  POST   /api/documents
PUT    /api/documents/{id}/status              DELETE /api/documents/{id}

GET    /api/metrics
GET    /api/reports/case/{id}.pdf
GET    /api/reports/approvals.pdf?from=&to=&fieldOffice=
GET    /api/reports/pipeline.pdf
```

## Before you deploy this

It is a demo. To hold real records it needs, at minimum:

- **Real authentication and authorisation.** [`src/lib/auth.tsx`](src/lib/auth.tsx) is a stub that
  accepts any email and verifies nothing. There are no roles: anyone signed in can decide any case.
- **Migrations.** Startup calls `EnsureCreated()`, which cannot evolve a schema. Switch to
  `dotnet ef migrations` before there is data you care about.
- **Real document storage.** `/api/documents` registers *metadata only* and never accepts file bytes.
  That is deliberate — accepting immigration evidence means virus scanning, content-type sniffing,
  encryption at rest, a retention policy and access logging, and a deployer must make those choices
  themselves.
- **Soft deletes.** `DELETE /api/applicants/{id}` cascades and takes the audit trail with it. An
  audit trail you can destroy is not an audit trail.
- **CORS.** Currently pinned to the Vite dev server. See
  [`Program.cs`](api/src/Naturalization.Api/Program.cs).
- **A privacy and retention review.** This models data — country of origin, immigration status,
  denial reasons — whose exposure can genuinely hurt people.

### Known limitations

- A `Decision` is unique per case, so a *Continued* decision (adjudication deferred pending further
  evidence) leaves the case at `InterviewCompleted` but blocks it from ever being decided again.
  Modelling decisions as an ordered history rather than a single row would fix it.
- No automated tests yet.
- `medianDaysToDecision` counts filing→decision, which flatters the number when a case sat waiting on
  the applicant rather than on the office.

## Licence

MIT — see [LICENSE](LICENSE).

Bundled PT Serif and PT Sans are SIL OFL; every dependency is permissively licensed. See
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
