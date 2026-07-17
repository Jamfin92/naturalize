# Going live on SQL Server

How to point the app — which already targets SQL Server — at a real Microsoft SQL Server database
holding your own records, replacing the seeded demo register of 40 **fabricated** applicants and demo
officer accounts with public passwords.

This is the operational companion to the README's [**Before you deploy
this**](README.md#before-you-deploy-this) section: that section says *what* a real deployment needs; this
says *how* for SQL Server. Read it first — the signing key, identity-provider, document-storage and
privacy caveats there all still apply.

> This branch (`master`) is scoped to applicants + reports. If you are deploying the fuller
> `enhancement/case-workflow` branch (cases, decisions, documents, and the dashboard, on top of the
> same applicants and reports), see the copy of this file there; the database steps are identical.

---

## 1. Prerequisites

- **.NET 8 SDK** and **Node 20+** (same as local dev).
- A reachable **SQL Server 2019+** (or Azure SQL Database). For a local trial — this image runs on
  macOS and Linux too:

  ```bash
  docker run --name naturalize-mssql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Change-me-123' \
    -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
  ```

  In production use a licensed instance (see §2), Azure SQL Database, or Amazon RDS for SQL Server.

---

## 2. Licensing & cost

Unlike PostgreSQL, **SQL Server is commercial software and the engine is licensed per core** (figures
below are U.S. list prices, July 2026 — confirm with a Microsoft reseller, and note Microsoft has raised
prices in recent releases):

| Edition | Cost | Notes |
|---|---|---|
| **Express** | **Free** | Caps: ~10 GB per database, ~1 GB RAM, 4 cores / 1 socket. **Allowed in production** — fits a small office comfortably. |
| **Developer** | **Free** | Full-featured, but licensed for **non-production (dev/test) only**. |
| **Standard** | ~$1,800–$2,000 / core (~$3,600–$4,000 per 2-core pack; 4-core minimum) | Or the Server + CAL model. |
| **Enterprise** | ~$15,123 per 2-core pack (~$7,500 / core; 8-core minimum → ~$60k+ to start) | Advanced availability/scale features. |

Sources: [SQL Server 2022 pricing](https://www.microsoft.com/en-us/sql-server/sql-server-2022-pricing),
[per-core licensing guide](https://redresscompliance.com/sql-server-2022-licensing-a-comprehensive-guide).

Notes:

- The **EF Core provider** [`Microsoft.EntityFrameworkCore.SqlServer`](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer)
  is free and open-source (MIT). Only the **database engine** carries a licence.
- **Azure SQL Database** avoids buying core licences entirely — it's billed by vCore/DTU or serverless
  consumption, licence included.
- For most single-office deployments of this app, **Express is the pragmatic choice** ($0) unless you
  expect to exceed its 10 GB / 1 GB-RAM ceilings.

> If a per-core licence is a non-starter, PostgreSQL is functionally equivalent here and free for
> production — see [`SETUP-POSTGRES.md`](SETUP-POSTGRES.md).

---

## 3. The provider is already SQL Server

**Nothing to switch — the app targets SQL Server out of the box.** The runtime
([`Program.cs`](api/src/Naturalization.Api/Program.cs)) and the design-time factory
([`DesignTimeDbContextFactory.cs`](api/src/Naturalization.Api/Data/DesignTimeDbContextFactory.cs)) both
use `UseSqlServer`, the migrations under `Data/Migrations` are SQL Server-shaped, and `MigrateAsync()`
applies them on startup. Your only job is to point it at *your* instance (§4).

How the provider is chosen: the API references both `Microsoft.EntityFrameworkCore.SqlServer` (the
production provider) and `Microsoft.EntityFrameworkCore.Sqlite`, and `Program.cs` reads the
configuration key `Database:Provider` (default `SqlServer`) to decide which to use:

```csharp
var provider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var useSqlite = provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);
// ... UseSqlite(connection) when useSqlite, else UseSqlServer(connection)
```

- **Production / this guide** — leave `Database:Provider` unset (or `SqlServer`). SQL Server, real
  migrations.
- **Zero-setup local run** — set `Database__Provider=Sqlite` to run off a single SQLite file with no
  server. That path builds its schema from the model (`EnsureCreated`) and therefore does **not**
  exercise the migrations; it exists for a fast `dotnet run` and for the test suites.

The integration tests and the Playwright e2e host use the SQLite path deliberately, so the suites stay
fast and need no database server — see §8.

`Microsoft.EntityFrameworkCore.Design` stays referenced either way; it is provider-agnostic and the
`dotnet ef` tooling needs it.

---

## 4. Connection string & environment variables

The runtime reads its connection string from configuration key `ConnectionStrings:Default`
(env `ConnectionStrings__Default`). Unset, it falls back to a local SQL Server instance (the docker
one in §1) — or, on the `Database:Provider=Sqlite` path, to a local SQLite file. See
[`Program.cs`](api/src/Naturalization.Api/Program.cs). Set these on the API host:

| Variable | Required | Purpose |
|---|---|---|
| `ConnectionStrings__Default` | **yes** | `Server=…,1433;Database=naturalize;User Id=…;Password=…;TrustServerCertificate=True` (drop `TrustServerCertificate` and configure a real certificate in production; or append `;Authentication=Active Directory Default` for Azure AD / managed identity). |
| `Auth__Jwt__Key` | **yes** | JWT signing secret, ≥ 32 bytes. Startup **refuses to boot** without it. Generate with `openssl rand -base64 48`. |
| `Seed__Demo` | **yes** | Set to `false` to switch off the 40 fabricated applicants. |
| `ASPNETCORE_ENVIRONMENT` | **yes** | Set to `Production` so the throwaway dev signing key in `appsettings.Development.json` is not used and Swagger is off. |
| `Auth__Okta__Enabled` / `Auth__Okta__Authority` / `Auth__Okta__Audience` | optional | Turn on the Okta carve-out (see README). The frontend OIDC redirect flow is **not** built. |

```bash
export ConnectionStrings__Default="Server=db.internal,1433;Database=naturalize;User Id=naturalize;Password=$DB_PASSWORD;TrustServerCertificate=True"
export Auth__Jwt__Key="$(openssl rand -base64 48)"
export Seed__Demo=false
export ASPNETCORE_ENVIRONMENT=Production
```

**CORS is not an environment variable.** It is pinned to the Vite dev origin in
[`Program.cs`](api/src/Naturalization.Api/Program.cs):

```csharp
.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
```

Edit this to your real front-end origin(s) before deploying (or refactor it to read from configuration
if you prefer). Cross-origin requests from any other origin — including your production SPA — are
refused until you do.

**Front end.** The SPA's API base URL is the **build-time** variable `VITE_API_URL` (default
`http://localhost:5099`, see [`.env.example`](.env.example) and `src/lib/api.ts`). It is baked into the
bundle, so set it before you build:

```bash
VITE_API_URL="https://api.your-office.example" npm run build
```

---

## 5. Apply the migrations

The migrations under `api/src/Naturalization.Api/Data/Migrations/` are already SQL Server-shaped
(`InitialCreate` — SQL Server column types, in-place `ALTER` semantics), so there is nothing to
regenerate. You just need to apply them to your instance. Either:

- **let the app do it** — on boot, `Program.cs` calls `await db.Database.MigrateAsync()` (on the SQL
  Server path), which applies any pending migrations; or
- **apply them yourself** before first boot:

  ```bash
  # from the repo root
  dotnet tool restore                                   # restores the pinned dotnet-ef 8.0.11
  dotnet ef database update -p api/src/Naturalization.Api -s api/src/Naturalization.Api
  ```

  `dotnet ef` reads the connection from `ConnectionStrings__Default` (falling back to the design-time
  factory's local default), so export yours first if it is not the local docker instance.

Review the migration before applying it to anything you care about — anything not in the EF model
(hand-added triggers, indexes) is not captured by it.

Confirm it applied:

```sql
SELECT [MigrationId] FROM [__EFMigrationsHistory];   -- should list InitialCreate
```

**Adding a schema change later.** Because the app is already on SQL Server, a new migration scaffolds
directly for it — no provider juggling:

```bash
dotnet ef migrations add <ChangeName> -p api/src/Naturalization.Api -s api/src/Naturalization.Api -o Data/Migrations
```

---

## 6. Load your real data

**Officers first.** `DbInitializer.SeedOfficersAsync` early-returns if the `Officers` table already has
any row, and otherwise inserts the **demo** officers whose passwords are printed in the repo. So before
first boot, insert your real officer account(s) — hash passwords with the same
`IPasswordHasher<OfficerAccount>` the app uses — **or** enable the Okta path. Never let the demo officers
reach production.

**Applicants.** Load your records either by:

- **the API** — `POST /api/applicants` per record (validated, audited, and it stamps the audit trail); or
- **bulk SQL / ETL** — insert straight into `Applicants` (`bcp` / `BULK INSERT` / SSIS for large loads).

Either way, respect the schema the app relies on:

- `AlienNumber` has a **unique index** — duplicates are rejected.
- `FirstName` and `LastName` are **required**; `MiddleName` is nullable.
- Rows are hidden unless `IsDeleted = false` — the global soft-delete query filter excludes withdrawn
  records from every read path.

**Localities.** The `Localities` lookup (`Id`, `ZipCode` **unique**, `Name`, `State`) is the single
residential-address reference — an applicant's `LocalityId` points at one row, which supplies the city, state
and ZIP. The demo seeds a handful of New England localities; for real data, populate it from any free ZIP↔city
dataset. Three good public sources, least-work first:

- **GeoNames postal codes** — [download](http://download.geonames.org/export/zip/) `US.zip`, unzip to
  `US.txt`. Tab-delimited UTF-8; **column 2 is the ZIP, column 3 the city name, column 5 the 2-letter state**
  (see the [readme](http://download.geonames.org/export/zip/readme.txt)). Direct ZIP→city→state, but licensed
  **[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)** — credit GeoNames wherever you surface it.
- **SimpleMaps US ZIP Codes, Basic tier** — [simplemaps.com/data/us-zips](https://simplemaps.com/data/us-zips).
  Clean CSV with `zip`, `city`, `state_id`, one row per ZIP. **Free, but requires a back-link** to that page
  from a public page that uses the data.
- **US Census ZCTA Gazetteer** — [census.gov Gazetteer files](https://www.census.gov/geographies/reference-files/time-series/geo/gazetteer-files.html).
  **Public domain**, no attribution — but the ZCTA file carries **codes and coordinates, not city names**, so
  you must join it to the Census *Places* gazetteer (or a ZIP crosswalk) to get name and state. Most work of the three.

Reduce whichever you pick to `ZipCode,Name,State` and load it. `ZipCode` is unique, so collapse GeoNames'
several rows-per-ZIP to one first — e.g. from `US.txt`:

```bash
# tab-delimited: $2 = ZIP, $3 = city, $5 = state → one row per ZIP
awk -F'\t' '!seen[$2]++ { printf "%s,%s,%s\n", $2, $3, $5 }' US.txt > localities.csv
```

Load `localities.csv` with `bcp` / `BULK INSERT` — the identity `Id` fills itself, so supply only `ZipCode`,
`Name` and `State`. For a repeatable in-app seed instead, add a `SeedLocalitiesAsync` to `DbInitializer` that
reads the file and `db.Localities.Add(...)`s the rows behind an `if (await db.Localities.AnyAsync()) return;`
guard, mirroring `SeedDemoApplicantsAsync`.

---

## 7. Adjusting the grid columns

The applicant register grid lives in
[`src/pages/applicants.tsx`](src/pages/applicants.tsx) and currently shows **Name** (links to the
record), **A-Number**, **Country of birth**, **LPR since**, and **Residence** (`city, state`), with the
last three progressively revealed at the `@2xl` / `@3xl` / `@4xl` container breakpoints.

Changing what an applicant record *stores* is an end-to-end change. The most recent commit that split
`FullName` into `FirstName` / `MiddleName` / `LastName` is a complete worked example (`git log` for
"Split applicant name into parts") — it touched exactly this chain:

1. **Entity** — `api/src/Naturalization.Api/Domain/Entities.cs` (`class Applicant`): add/rename/remove the
   property. A value derived from other columns can be a computed `[NotMapped]` property (like `FullName`);
   a stored value is a plain auto-property.
2. **DbContext config** — `api/src/Naturalization.Api/Data/NaturalizationDbContext.cs`: max length /
   required / nullable.
3. **Migration** — `dotnet ef migrations add <ChangeName> -p api/src/Naturalization.Api`.
4. **DTOs** — `api/src/Naturalization.Api/Dtos/Dtos.cs`: `ApplicantDto` (what the API returns) and
   `ApplicantInput` (what create/edit accepts).
5. **Endpoints** — `api/src/Naturalization.Api/Endpoints/ApplicantEndpoints.cs`: the search `LIKE`, the
   `OrderBy`, and create / update / `Validate` / `Diff`. ⚠️ Search and sort run **in SQL**, so they must
   reference real columns — a computed `[NotMapped]` property cannot be translated and will throw.
6. **Front-end type** — `src/lib/types.ts` (`interface Applicant`).
7. **Grid** — `src/pages/applicants.tsx`: add/remove a `<TableHead>` and the matching `<TableCell>`; use the
   `hidden @2xl:table-cell` pattern to control when a column appears.
8. **Form** — `src/pages/applicant-form.tsx`: the create/edit field(s).

To only *reorder or hide* columns that already exist, step 7 alone is enough — no migration needed.

---

## 8. Verify

```bash
dotnet build api/Naturalization.sln            # compiles
dotnet ef database update -p api/src/Naturalization.Api -s api/src/Naturalization.Api
dotnet test api/Naturalization.sln             # integration suite still green (runs on the SQLite path)
```

Then, against the running system: sign in with a **real** officer account, confirm the applicant grid
lists your live rows, search by name and A-Number, and download a report PDF. Check
`__EFMigrationsHistory` contains `InitialCreate` and that `Seed__Demo=false` left no fabricated
applicants behind.
