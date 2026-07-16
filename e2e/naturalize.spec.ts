import { expect, test, type Page } from '@playwright/test'

/*
 * The demo officer, seeded by DbInitializer. These credentials are public and
 * fabricated; see api/src/Naturalization.Api/Data/DbInitializer.cs.
 */
const OFFICER = { email: 'a.hernandez@example.gov', password: 'Naturalize!Demo1' }
// The seeded read-only account. Same public demo password as everyone else.
const VIEWER = { email: 'r.okafor@example.gov', password: 'Naturalize!Demo1' }

async function signInAs(page: Page, creds: { email: string; password: string }) {
  await page.goto('/')
  await page.getByLabel('Work email').fill(creds.email)
  await page.getByLabel('Password').fill(creds.password)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page.getByRole('link', { name: 'Applicants' })).toBeVisible()
}

async function signIn(page: Page) {
  await signInAs(page, OFFICER)
}

/** A unique A-Number per test, so runs don't collide on the unique index. */
function uniqueANumber() {
  return `A9${Math.floor(Math.random() * 90_000_000 + 10_000_000)}`
}

test.describe('authentication', () => {
  test('an unauthenticated visitor cannot reach a protected page', async ({ page }) => {
    await page.goto('/applicants')

    // Bounced back to sign-in, not shown the shell.
    await expect(page.getByRole('heading', { name: 'Officer sign-in' })).toBeVisible()
    await expect(page).toHaveURL('/')
  })

  test('the wrong password is rejected and does not sign you in', async ({ page }) => {
    await page.goto('/')
    await page.getByLabel('Work email').fill(OFFICER.email)
    await page.getByLabel('Password').fill('not-the-password')
    await page.getByRole('button', { name: 'Sign in' }).click()

    await expect(page.getByRole('alert')).toContainText('not recognised')
    await expect(page.getByRole('heading', { name: 'Officer sign-in' })).toBeVisible()
  })

  test('correct credentials sign you in', async ({ page }) => {
    await signIn(page)
    // The officer identity came from the SERVER, not from the email we typed.
    await expect(page.getByText('A. Hernandez')).toBeVisible()
  })

  test('signing out closes the session', async ({ page }) => {
    await signIn(page)
    await page.getByRole('button', { name: 'Sign out' }).click()

    await expect(page).toHaveURL('/')

    // And the session is really gone — not just the UI.
    await page.goto('/applicants')
    await expect(page.getByRole('heading', { name: 'Officer sign-in' })).toBeVisible()
  })
})

test.describe('applicants', () => {
  test('an applicant can be added and appears in the register', async ({ page }) => {
    const aNumber = uniqueANumber()
    await signIn(page)

    await page.getByRole('link', { name: 'Applicants' }).click()
    await page.getByRole('link', { name: 'New applicant' }).click()

    await page.getByLabel('First name').fill('Testcase')
    await page.getByLabel('Last name').fill('Applicant')
    await page.getByLabel('A-Number').fill(aNumber)
    await page.getByLabel('Date of birth').fill('1988-03-14')
    await page.getByLabel('LPR since').fill('2016-06-01')
    await page.getByLabel('Country of birth').fill('Kenya')
    await page.getByLabel('Nationality').fill('Kenyan')
    await page.getByRole('button', { name: 'Add applicant' }).click()

    // Lands on the new record. (.first(): the A-Number also appears in the audit
    // trail entry below — "Applicant Testcase Applicant (A…) added to the register".)
    await expect(page.getByRole('heading', { name: 'Testcase Applicant' })).toBeVisible()
    await expect(page.getByText(aNumber).first()).toBeVisible()

    // And it is genuinely in the register, not just on screen.
    await page.getByRole('link', { name: 'All applicants' }).click()
    await page.getByPlaceholder('Name or A-Number…').fill(aNumber)
    await page.getByRole('button', { name: 'Search' }).click()
    await expect(page.getByRole('link', { name: 'Testcase Applicant' })).toBeVisible()
  })

  test('an edit persists and is recorded in the audit trail', async ({ page }) => {
    const aNumber = uniqueANumber()
    await signIn(page)

    // Create one to edit.
    await page.goto('/applicants/new')
    await page.getByLabel('First name').fill('Editable')
    await page.getByLabel('Last name').fill('Person')
    await page.getByLabel('A-Number').fill(aNumber)
    await page.getByLabel('Date of birth').fill('1990-01-01')
    await page.getByLabel('LPR since').fill('2015-01-01')
    await page.getByLabel('City').fill('Boston')
    await page.getByRole('button', { name: 'Add applicant' }).click()
    await expect(page.getByRole('heading', { name: 'Editable Person' })).toBeVisible()

    await page.getByRole('link', { name: 'Edit' }).click()
    await page.getByLabel('City').fill('Cambridge')
    await page.getByRole('button', { name: 'Save changes' }).click()

    // The change stuck...
    await expect(page.getByText('Cambridge', { exact: false })).toBeVisible()

    // ...and the record's own history names the officer FROM THE TOKEN. If
    // MapInboundClaims were left on, this would read "Unknown officer".
    const history = page.locator('table', { has: page.getByText('Officer') }).last()
    await expect(history.getByText('Updated')).toBeVisible()
    await expect(history.getByText('A. Hernandez').first()).toBeVisible()
  })

  test('withdrawing a record keeps its history instead of destroying it', async ({ page }) => {
    const aNumber = uniqueANumber()
    await signIn(page)

    await page.goto('/applicants/new')
    await page.getByLabel('First name').fill('Withdrawn')
    await page.getByLabel('Last name').fill('Person')
    await page.getByLabel('A-Number').fill(aNumber)
    await page.getByLabel('Date of birth').fill('1985-05-05')
    await page.getByLabel('LPR since').fill('2014-01-01')
    await page.getByRole('button', { name: 'Add applicant' }).click()
    await expect(page.getByRole('heading', { name: 'Withdrawn Person' })).toBeVisible()

    await page.getByRole('button', { name: 'Withdraw' }).click()
    await page.getByRole('button', { name: 'Withdraw record' }).click()

    // Gone from the register.
    await expect(page).toHaveURL('/applicants')
    await page.getByPlaceholder('Name or A-Number…').fill(aNumber)
    await page.getByRole('button', { name: 'Search' }).click()
    await expect(page.getByText(/No applicants match/)).toBeVisible()

    /*
     * But NOT destroyed. Re-adding the same A-Number must be refused with the
     * server pointing at the withdrawn record — proof the row, and its trail, are
     * still on file. Before soft deletes this A-Number would have been free,
     * because the applicant and their whole audit trail had been erased.
     */
    await page.goto('/applicants/new')
    await page.getByLabel('First name').fill('Impostor')
    await page.getByLabel('Last name').fill('Person')
    await page.getByLabel('A-Number').fill(aNumber)
    await page.getByLabel('Date of birth').fill('1985-05-05')
    await page.getByLabel('LPR since').fill('2014-01-01')
    await page.getByRole('button', { name: 'Add applicant' }).click()

    await expect(page.getByText(/belongs to withdrawn applicant/)).toBeVisible()
  })

  test('an admin can withdraw a record straight from the register list', async ({ page }) => {
    const aNumber = uniqueANumber()
    await signIn(page)

    // Create one to withdraw.
    await page.goto('/applicants/new')
    await page.getByLabel('First name').fill('Listwithdraw')
    await page.getByLabel('Last name').fill('Person')
    await page.getByLabel('A-Number').fill(aNumber)
    await page.getByLabel('Date of birth').fill('1980-02-02')
    await page.getByLabel('LPR since').fill('2013-01-01')
    await page.getByRole('button', { name: 'Add applicant' }).click()
    await expect(page.getByRole('heading', { name: 'Listwithdraw Person' })).toBeVisible()

    // Withdraw it from the row's actions menu, WITHOUT opening the detail page —
    // this is exactly the "select a user and modify or delete" the list now offers.
    await page.getByRole('link', { name: 'All applicants' }).click()
    await page.getByPlaceholder('Name or A-Number…').fill(aNumber)
    await page.getByRole('button', { name: 'Search' }).click()
    await expect(page.getByRole('link', { name: 'Listwithdraw Person' })).toBeVisible()

    await page.getByRole('button', { name: 'Actions for Listwithdraw Person' }).click()
    await page.getByRole('menuitem', { name: 'Withdraw record' }).click()
    await page.getByRole('button', { name: 'Withdraw record' }).click()

    // The list refreshes in place and the row is gone from the register.
    await expect(page.getByRole('link', { name: 'Listwithdraw Person' })).toHaveCount(0)
  })
})

test.describe('roles', () => {
  test('a read-only viewer is not offered add, edit or withdraw', async ({ page }) => {
    await signInAs(page, VIEWER)
    await page.goto('/applicants')

    // The register is readable...
    await expect(page.getByRole('heading', { name: 'Applicants' })).toBeVisible()
    // ...but there is no way to add a record...
    await expect(page.getByRole('link', { name: 'New applicant' })).toHaveCount(0)
    // ...and no per-row actions menu to edit or withdraw from the list either.
    await expect(page.getByRole('button', { name: /^Actions for / })).toHaveCount(0)

    // Open the first applicant.
    await page.locator('table tbody tr').first().getByRole('link').first().click()
    await expect(page.getByRole('heading', { name: 'Personal particulars' })).toBeVisible()

    // Neither Edit nor Withdraw is offered to a viewer.
    await expect(page.getByRole('link', { name: 'Edit' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Withdraw' })).toHaveCount(0)
  })

  test('the edit route is blocked for a viewer even by direct URL', async ({ page }) => {
    await signInAs(page, VIEWER)
    await page.goto('/applicants/1/edit')

    await expect(page.getByRole('heading', { name: 'Not permitted' })).toBeVisible()
    // The form itself is not rendered.
    await expect(page.getByRole('button', { name: 'Save changes' })).toHaveCount(0)
  })
})

test.describe('reports', () => {
  /*
   * The regression these exist for: the report links were plain <a href> tags,
   * and a browser navigation cannot carry an Authorization header. The moment the
   * reports went behind auth, every one of these buttons would have opened a
   * blank tab containing 401 JSON — and no API test would have noticed, because
   * the request never went through the API client at all.
   */
  test('the pipeline report downloads as a PDF', async ({ page }) => {
    await signIn(page)
    await page.getByRole('link', { name: 'Reports' }).click()

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'Download Pipeline report' }).click(),
    ])

    // The server-set filename survived the cross-origin trip, which only works
    // because the API sends Access-Control-Expose-Headers: Content-Disposition.
    // Without it this would silently fall back to our client-side default.
    expect(download.suggestedFilename()).toMatch(/^pipeline-\d{8}\.pdf$/)
  })

  test('the approvals report downloads as a PDF', async ({ page }) => {
    await signIn(page)
    await page.goto('/reports')

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'Download Approvals report' }).click(),
    ])

    expect(download.suggestedFilename()).toMatch(/^approvals-\d{8}-\d{8}\.pdf$/)
  })

  test('the case record report downloads as a PDF', async ({ page }) => {
    await signIn(page)
    await page.goto('/reports')

    const button = page.getByRole('button', { name: 'Download Case record' })

    // Disabled until a case is named. It used to render href="#", so clicking it
    // with the field blank opened a blank tab.
    await expect(button).toBeDisabled()

    await page.getByLabel('Case ID').fill('1')
    const [download] = await Promise.all([page.waitForEvent('download'), button.click()])

    expect(download.suggestedFilename()).toBe('case-record-1.pdf')
  })

  test('the mailing labels download as a PDF', async ({ page }) => {
    await signIn(page)
    await page.goto('/reports')

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'Download Mailing labels' }).click(),
    ])

    expect(download.suggestedFilename()).toMatch(/^mailing-labels-\d{8}\.pdf$/)
  })

  test('the mailing labels download for a single date', async ({ page }) => {
    await signIn(page)
    await page.goto('/reports')

    // "From"/"To" also appear on the approvals card, so scope to the labels card —
    // its description is the only place "Avery 5160" appears.
    const card = page.locator('[class*="flex-col"]').filter({ hasText: 'Avery 5160' }).first()

    await card.getByLabel('Applicants added').click()
    await page.getByRole('option', { name: 'On a single date' }).click()
    await card.getByLabel('Date').fill('2026-01-10')

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      card.getByRole('button', { name: 'Download Mailing labels' }).click(),
    ])

    expect(download.suggestedFilename()).toBe('mailing-labels-20260110.pdf')
  })

  test('the mailing labels download for a date range', async ({ page }) => {
    await signIn(page)
    await page.goto('/reports')

    const card = page.locator('[class*="flex-col"]').filter({ hasText: 'Avery 5160' }).first()

    await card.getByLabel('Applicants added').click()
    await page.getByRole('option', { name: 'Within a date range' }).click()
    await card.getByLabel('From').fill('2026-01-01')
    await card.getByLabel('To').fill('2026-02-01')

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      card.getByRole('button', { name: 'Download Mailing labels' }).click(),
    ])

    expect(download.suggestedFilename()).toBe('mailing-labels-20260101-20260201.pdf')
  })
})
