# Going live on PostgreSQL

How to move the app — which ships targeting SQL Server — to a real PostgreSQL database holding your own
records, replacing the seeded demo register of 40 **fabricated** applicants and demo officer accounts
with public passwords.

This is the operational companion to the README's [**Before you deploy
this**](README.md#before-you-deploy-this) section: that section says *what* a real deployment needs; this
says *how* for Postgres. Read it first — the signing key, identity-provider, document-storage and
privacy caveats there all still apply.

> This branch (`master`) is scoped to applicants + reports. If you are deploying the fuller
> `enhancement/case-workflow` branch (cases, decisions, documents, and the dashboard, on top of the
> same applicants and reports), see the copy of this file there; the database steps are identical.

---

## 1. Prerequisites

- **.NET 8 SDK** and **Node 20+** (same as local dev).
- A reachable **PostgreSQL 14+** server. For a local trial:

  ```bash
  docker run --name naturalize-pg -e POSTGRES_PASSWORD=change-me \
    -e POSTGRES_DB=naturalize -p 5432:5432 -d postgres:16
  ```

  In production use a managed instance (Amazon RDS / Aurora, Azure Database for PostgreSQL, Cloud SQL,
  Crunchy, etc.) or your own hardened server.

---

## 2. Licensing & cost

**PostgreSQL is free — including for commercial and production use.** It is released under the
[PostgreSQL License](https://www.postgresql.org/about/licence/), a permissive, OSI-approved,
BSD/MIT-style licence. There are **no per-core, per-socket, or per-instance fees**, ever.

The .NET driver and EF Core provider are the same story: [`Npgsql` and
`Npgsql.EntityFrameworkCore.PostgreSQL`](https://github.com/npgsql/npgsql/blob/main/LICENSE) ship under
the PostgreSQL License. **Total database-licensing cost: $0.**

Your only spend is the infrastructure the server runs on (a VM, or a managed instance's hourly/storage
price) — not the database software.

---

## 3. Switch the EF Core provider

The app ships targeting **SQL Server** (with SQLite kept for the tests and zero-setup local runs — see
the [SQL Server guide](SETUP-SQLSERVER.md)). To go to Postgres instead, change the provider in three
places.

**a. Swap the NuGet package** in `api/src/Naturalization.Api/Naturalization.Api.csproj` — replace the
SQL Server provider

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.11" />
```

with the Npgsql provider (keep it on the EF Core 8 line):

```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.11" />
```

Leave `Microsoft.EntityFrameworkCore.Design` in place — it's provider-agnostic and the `dotnet ef`
tooling needs it. You can keep or drop `Microsoft.EntityFrameworkCore.Sqlite`; the tests use it.

**b. Point the runtime at Postgres** in
[`api/src/Naturalization.Api/Program.cs`](api/src/Naturalization.Api/Program.cs) — the provider is
chosen in the `AddDbContext` call. Replace the SQL Server branch (`o.UseSqlServer(connection)`) with
`o.UseNpgsql(connection)`, and in the startup DB-init block swap the `db.Database.IsSqlServer()` check
for `db.Database.IsNpgsql()` so `MigrateAsync()` runs on the Postgres path:

```csharp
// AddDbContext: the non-SQLite branch
o.UseNpgsql(connection);            // was o.UseSqlServer(connection)

// DB init on startup
if (db.Database.IsNpgsql())         // was db.Database.IsSqlServer()
    await db.Database.MigrateAsync();
else
    await db.Database.EnsureCreatedAsync();
```

Also update the default connection-string fallback next to it if you rely on it (it currently points at
a local SQL Server).

**c. Point the design-time factory at Postgres** in
[`api/src/Naturalization.Api/Data/DesignTimeDbContextFactory.cs`](api/src/Naturalization.Api/Data/DesignTimeDbContextFactory.cs).
This is what `dotnet ef` uses to build the model; if it still says SQL Server, your migrations scaffold
for the wrong provider:

```csharp
var options = new DbContextOptionsBuilder<NaturalizationDbContext>()
    .UseNpgsql("Host=localhost;Port=5432;Database=naturalize;Username=postgres;Password=change-me")
    .Options;
```

The design-time connection only needs to reach *a* Postgres instance so EF can read provider metadata;
it never touches production data.

---

## 4. Connection string & environment variables

The runtime reads its connection string from configuration key `ConnectionStrings:Default`
(env `ConnectionStrings__Default`). Unset, it falls back to the default the active provider hard-codes
(so update that fallback in `Program.cs` when you switch to Npgsql) — see
[`Program.cs`](api/src/Naturalization.Api/Program.cs). Set these on the API host:

| Variable | Required | Purpose |
|---|---|---|
| `ConnectionStrings__Default` | **yes** | `Host=…;Port=5432;Database=naturalize;Username=…;Password=…` (add `;SSL Mode=Require;Trust Server Certificate=true` for managed instances that force TLS). |
| `Auth__Jwt__Key` | **yes** | JWT signing secret, ≥ 32 bytes. Startup **refuses to boot** without it. Generate with `openssl rand -base64 48`. |
| `Seed__Demo` | **yes** | Set to `false` to switch off the 40 fabricated applicants. |
| `ASPNETCORE_ENVIRONMENT` | **yes** | Set to `Production` so the throwaway dev signing key in `appsettings.Development.json` is not used and Swagger is off. |
| `Auth__Okta__Enabled` / `Auth__Okta__Authority` / `Auth__Okta__Audience` | optional | Turn on the Okta carve-out (see README). The frontend OIDC redirect flow is **not** built. |

```bash
export ConnectionStrings__Default="Host=db.internal;Port=5432;Database=naturalize;Username=naturalize;Password=$DB_PASSWORD"
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

## 5. Regenerate the migrations for Postgres

The migrations under `api/src/Naturalization.Api/Data/Migrations/` were generated for **SQL Server**
(SQL Server column types) and **will not apply to Postgres**. Because you are going live from an empty
database with no rows to preserve, the clean move is — after the provider swap in §3 — to regenerate a
single fresh `InitialCreate` for Postgres:

```bash
# from the repo root
dotnet tool restore                                   # restores the pinned dotnet-ef 8.0.11

rm api/src/Naturalization.Api/Data/Migrations/*.cs    # drop the SQL Server-shaped migrations

dotnet ef migrations add InitialCreate \
  -p api/src/Naturalization.Api -s api/src/Naturalization.Api -o Data/Migrations
```

Then apply the schema. Either:

- **let the app do it** — on boot, `Program.cs` calls `await db.Database.MigrateAsync()`, which applies
  any pending migrations; or
- **apply it yourself** before first boot:

  ```bash
  dotnet ef database update -p api/src/Naturalization.Api -s api/src/Naturalization.Api
  ```

Review the generated migration before applying it to anything you care about. (Like SQL Server,
Postgres does in-place `ALTER TABLE`, so the SQLite table-rebuild caveat does not apply.)

Confirm it applied:

```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory";   -- should list InitialCreate
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
- **bulk SQL / ETL** — insert straight into `Applicants`.

Either way, respect the schema the app relies on:

- `AlienNumber` has a **unique index** — duplicates are rejected.
- `FirstName` and `LastName` are **required**; `MiddleName` is nullable.
- Rows are hidden unless `IsDeleted = false` — the global soft-delete query filter excludes withdrawn
  records from every read path.

**Cities.** The `Cities` lookup (`ZipCode` → `Name`, `ZipCode` **unique**, `Name` indexed) ships with no
demo seed — populate it from any free ZIP↔city dataset. Three good public sources, least-work first:

- **GeoNames postal codes** — [download](http://download.geonames.org/export/zip/) `US.zip`, unzip to
  `US.txt`. Tab-delimited UTF-8; **column 2 is the ZIP, column 3 the city name** (see the
  [readme](http://download.geonames.org/export/zip/readme.txt)). Direct ZIP→city, but licensed
  **[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)** — credit GeoNames wherever you surface it.
- **SimpleMaps US ZIP Codes, Basic tier** — [simplemaps.com/data/us-zips](https://simplemaps.com/data/us-zips).
  Clean CSV with `zip`, `city`, `state_id`, one row per ZIP. **Free, but requires a back-link** to that page
  from a public page that uses the data.
- **US Census ZCTA Gazetteer** — [census.gov Gazetteer files](https://www.census.gov/geographies/reference-files/time-series/geo/gazetteer-files.html).
  **Public domain**, no attribution — but the ZCTA file carries **codes and coordinates, not city names**, so
  you must join it to the Census *Places* gazetteer (or a ZIP crosswalk) to get a name. Most work of the three.

Reduce whichever you pick to two columns and load it. `ZipCode` is unique, so collapse GeoNames' several
rows-per-ZIP to one first — e.g. from `US.txt`:

```bash
# tab-delimited: $2 = postal code, $3 = place name → one city per ZIP
awk -F'\t' '!seen[$2]++ { printf "%s,%s\n", $2, $3 }' US.txt > cities.csv
```

Load `cities.csv` with `\copy "Cities" ("ZipCode", "Name") FROM 'cities.csv' WITH (FORMAT csv)` in `psql`
— the identity `Id` fills itself, so supply only `ZipCode` and `Name`. For a repeatable in-app seed instead,
add a `SeedCitiesAsync` to `DbInitializer` that reads the file and `db.Cities.Add(...)`s the rows behind an
`if (await db.Cities.AnyAsync()) return;` guard, mirroring `SeedDemoApplicantsAsync`.

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
dotnet build api/Naturalization.sln            # provider swap compiles
dotnet ef database update -p api/src/Naturalization.Api -s api/src/Naturalization.Api
dotnet test api/Naturalization.sln             # integration suite still green
```

Then, against the running system: sign in with a **real** officer account, confirm the applicant grid
lists your live rows, search by name and A-Number, and download a report PDF. Check
`__EFMigrationsHistory` contains `InitialCreate` and that `Seed__Demo=false` left no fabricated
applicants behind.
