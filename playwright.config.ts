import { execSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import { defineConfig, devices } from '@playwright/test'

/*
 * End-to-end: a real browser, driving the real SPA, against the real API and a
 * real SQLite file. Nothing is mocked. If the bearer token doesn't reach the API,
 * or CORS blocks the preflight, or a report download opens a 401 tab instead of
 * saving a PDF, these fail — and those are exactly the bugs a typechecker and an
 * API test cannot see.
 */

const API_PORT = 5099
const WEB_PORT = 5173
const API_URL = `http://localhost:${API_PORT}`
const WEB_URL = `http://localhost:${WEB_PORT}`

/** A throwaway database per run, rebuilt from migrations every time. */
const E2E_DB = path.resolve('.e2e/naturalize-e2e.db')

/*
 * Reset the database and build the API — in the MAIN process only.
 *
 * Two ordering traps, and between them they leave exactly one correct place for
 * this code:
 *
 *   - It cannot go in globalSetup, because Playwright starts `webServer` BEFORE
 *     globalSetup runs. The API would boot first, find no .e2e directory, and die
 *     with "SQLite Error 14: unable to open database file".
 *
 *   - It cannot go at the top level of this config unguarded, because Playwright
 *     re-evaluates this file in EVERY WORKER PROCESS. The worker's copy of the
 *     deletion then runs after the API has already migrated, yanking the database
 *     file out from under the live server. SQLite quietly recreates an empty one
 *     and every request fails with "no such table: Applicants" — which looks like
 *     a broken API, and isn't.
 *
 * TEST_WORKER_INDEX is set by Playwright in worker processes and unset in the
 * main one, so this runs exactly once, before either server starts.
 */
if (process.env.TEST_WORKER_INDEX === undefined) {
  fs.mkdirSync(path.dirname(E2E_DB), { recursive: true })

  // Tests add and withdraw applicants. A database left over from the last run
  // makes them pass or fail depending on history.
  for (const file of [E2E_DB, `${E2E_DB}-wal`, `${E2E_DB}-shm`]) {
    fs.rmSync(file, { force: true })
  }

  // The API is launched from its compiled DLL (see below), so it has to exist.
  execSync('dotnet build api/Naturalization.sln -v quiet --nologo', { stdio: 'inherit' })
}

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false, // one API, one database: these tests share state
  workers: 1,
  reporter: process.env.CI ? 'line' : 'list',
  retries: process.env.CI ? 1 : 0,

  use: {
    baseURL: WEB_URL,
    trace: 'retain-on-failure',
  },

  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],

  webServer: [
    {
      /*
       * The BUILT DLL, not `dotnet run`.
       *
       * `dotnet run` launches the app as a CHILD process. Playwright kills the
       * parent when the run finishes, the child survives still holding port 5099,
       * and the next run dies with "address already in use".
       *
       * Running the DLL directly also sidesteps launchSettings.json, which sets
       * "launchBrowser": true and "launchUrl": "swagger" — i.e. a profile-driven
       * run pops a Swagger tab in your face on every single test run.
       *
       * `cwd` is load-bearing: ASP.NET resolves its content root — and therefore
       * appsettings.json — from the working directory. Launch the DLL from the
       * repo root and the config file is simply never found, so the app dies at
       * startup with "Auth:Jwt:Issuer and Auth:Jwt:Audience are both required".
       * The SDK copies appsettings*.json into the output folder, so run it there.
       *
       * And because the working directory is now the output folder, the connection
       * string MUST be absolute, or the API creates a second database next to the
       * DLL and the tests assert against the wrong one.
       */
      command: 'dotnet Naturalization.Api.dll',
      cwd: 'api/src/Naturalization.Api/bin/Debug/net8.0',
      url: `${API_URL}/health`,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: API_URL,
        // The app defaults to SQL Server (production); the e2e host runs off a
        // throwaway SQLite file instead, so no database server is needed to run
        // the browser suite. See Program.cs on the provider switch.
        Database__Provider: 'Sqlite',
        ConnectionStrings__Default: `Data Source=${E2E_DB}`,
        Seed__Demo: 'true', // the reports have nothing to render without a caseload
        Auth__Jwt__Key: 'e2e-signing-key-not-a-secret-0123456789abcdef',
        Auth__Okta__Enabled: 'false',
      },
      reuseExistingServer: false,
      timeout: 60_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
    {
      command: 'npm run dev',
      url: WEB_URL,
      env: { VITE_API_URL: API_URL },
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
    },
  ],
})
