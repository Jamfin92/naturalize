using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Domain;

namespace Naturalization.Api.Data;

/// <summary>
/// Seeds a fabricated register so every screen and report has something real to
/// render on a fresh clone.
///
/// Every name, A-Number, petition number and address below is invented. None of
/// it corresponds to a real person or a real filing.
/// </summary>
public static class DbInitializer
{
    /*
     * First, (optional) middle and last name are kept alongside country in one
     * tuple rather than in parallel arrays. Parallel arrays of different lengths
     * wrap at different rates, which is how the first cut of this seed produced
     * "Hassan Farah, born in Poland" — plausible in real life, but it reads as a
     * bug in a demo, and every reviewer stops on it.
     *
     * Most people have no middle name (Middle is null); a handful carry one so the
     * split-name field and the mailing labels visibly exercise all three parts.
     */
    private static readonly (string First, string? Middle, string Last, string Country)[] People =
    [
        ("Amara", "Chidinma", "Okafor", "Nigeria"),
        ("Wei", null, "Chen", "China"),
        ("Sofía", "Isabel", "Restrepo", "Colombia"),
        ("Dmitri", null, "Volkov", "Russia"),
        ("Priya", null, "Raghunathan", "India"),
        ("Miguel", "Antônio", "Santos", "Brazil"),
        ("Fatima", null, "Al-Rashid", "Syria"),
        ("Nguyen", "Thi", "Mai", "Vietnam"),
        ("Kwame", null, "Mensah", "Ghana"),
        ("Elena", null, "Petrova", "Bulgaria"),
        ("Rafael", null, "Duarte", "Portugal"),
        ("Aisha", null, "Kone", "Mali"),
        ("Jae-won", null, "Park", "South Korea"),
        ("Lucia", null, "Ferrari", "Italy"),
        ("Omar", null, "Haddad", "Lebanon"),
        ("Yuki", null, "Tanaka", "Japan"),
        ("Ana", null, "Kovač", "Croatia"),
        ("Tenzin", null, "Norbu", "Nepal"),
        ("Ingrid", null, "Larsen", "Norway"),
        ("Carlos", "Eduardo", "Mendoza", "Mexico"),
        ("Zainab", null, "Hussein", "Somalia"),
        ("Pavel", null, "Novák", "Czechia"),
        ("Rosa", "María", "Jiménez", "Peru"),
        ("Chidi", null, "Eze", "Nigeria"),
        ("Mei-ling", null, "Wu", "Taiwan"),
        ("Hassan", null, "Farah", "Somalia"),
        ("Katarzyna", null, "Nowak", "Poland"),
        ("Diego", null, "Vargas", "Chile"),
        ("Leila", null, "Nasser", "Lebanon"),
        ("Sipho", null, "Dlamini", "South Africa"),
        ("Isabela", null, "Cruz", "Philippines"),
        ("Arjun", "Kumar", "Patel", "India"),
        ("Marta", null, "Silva", "Portugal"),
        ("Bilal", null, "Ahmed", "Pakistan"),
        ("Nadia", null, "Popescu", "Romania"),
        ("Kofi", null, "Boateng", "Ghana"),
        ("Sara", null, "Lindqvist", "Sweden"),
        ("Tomás", null, "Herrera", "Guatemala"),
        ("Rania", null, "Khoury", "Jordan"),
        ("Viktor", null, "Horváth", "Hungary")
    ];

    // Town names for the lookup table; applicants are spread across them.
    private static readonly string[] Towns =
    [
        "Boston", "Cambridge", "Worcester", "Providence",
        "Hartford", "Manchester", "Portland", "Burlington"
    ];

    /// <summary>
    /// The demo accounts. Infrastructure, not fixtures: a database with no user in
    /// it is an API that nobody can log into, so this runs even when the demo
    /// register is switched off.
    ///
    /// These passwords are public, in a public repository, on purpose — this is a
    /// demo whose every applicant is fabricated. A real deployment provisions its
    /// users from its own identity provider (see the Okta carve-out) and never
    /// runs this. The README says so in more words.
    /// </summary>
    public const string DemoPassword = "Naturalize!Demo1";

    /*
     * One demo account per role, so the role gating is something you can actually
     * sign in and exercise rather than take on faith: the Admin can do everything,
     * the Officer can add and edit applicants but not withdraw them, and the
     * Viewer can only read. Every account shares the public demo password.
     */
    private static readonly (string Email, string Name, string Office, OfficerRole Role)[] Users =
    [
        ("a.hernandez@example.gov", "A. Hernandez", "Boston, MA", OfficerRole.Admin),
        ("m.whitfield@example.gov", "M. Whitfield", "Hartford, CT", OfficerRole.Officer),
        ("r.okafor@example.gov", "R. Okafor", "Providence, RI", OfficerRole.Viewer),
    ];

    public static async Task SeedUsersAsync(
        NaturalizationDbContext db, IPasswordHasher<ApplicationUser> hasher)
    {
        /*
         * Upsert-by-email, not "bail if any account exists".
         *
         * The old guard made seeding all-or-nothing, which broke the one thing this
         * demo exists to show: a database seeded before roles existed has every
         * account backfilled to Admin, and the early return then meant the Officer
         * and Viewer demo accounts stayed Admin forever. This reconciles each demo
         * account's role to its intended value on startup instead.
         */
        var anyExisting = await db.Users.AnyAsync();

        foreach (var (email, name, office, role) in Users)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user is null)
            {
                /*
                 * Only introduce a demo account on a database that has none of its
                 * own. These accounts share one public password, so injecting them
                 * into a deployment that has already provisioned real users would be
                 * handing out a backdoor — hence the guard rather than an
                 * unconditional insert.
                 */
                if (anyExisting) continue;

                user = new ApplicationUser
                {
                    Email = email,
                    FullName = name,
                    FieldOffice = office,
                    Role = role,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                };

                // Hashed, never stored in the clear — even for a demo account whose
                // password is printed in the README. The habit is the point.
                user.PasswordHash = hasher.HashPassword(user, DemoPassword);
                db.Users.Add(user);
            }
            else if (user.Role != role)
            {
                // A drifted role (a hand-edited row). Correct it so the gating a
                // Viewer or Officer is supposed to demonstrate actually shows up.
                user.Role = role;
            }
        }

        await db.SaveChangesAsync();
    }

    public static async Task SeedDemoApplicantsAsync(NaturalizationDbContext db)
    {
        if (await db.Applicants.AnyAsync()) return;

        /*
         * The RNG is LOCAL, and that is not a style preference. A shared static
         * Random is neither thread-safe nor reset between calls, and the integration
         * tests boot several hosts in one process — two hosts seeding at once can
         * corrupt its state badly enough to emit duplicate A-Numbers and trip the
         * unique index. Fixed seed, so the demo data is identical on every clone.
         */
        var rng = new Random(20260717);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // --- Lookups ----------------------------------------------------------
        // Country codes, one per distinct country in the People table.
        var countryNames = People.Select(p => p.Country).Distinct().OrderBy(x => x).ToList();
        var countryCodeByName = new Dictionary<string, string>();
        for (var i = 0; i < countryNames.Count; i++)
        {
            var code = (i + 1).ToString("D3");
            countryCodeByName[countryNames[i]] = code;
            db.CountryCodes.Add(new CountryCode { BaseCode = "C", Code = code, Description = countryNames[i] });
        }

        // Town codes, one per town.
        var townCodeByName = new Dictionary<string, string>();
        for (var i = 0; i < Towns.Length; i++)
        {
            var code = (i + 1).ToString("D3");
            townCodeByName[Towns[i]] = code;
            db.TownCodes.Add(new TownCode { BaseCode = "T", Code = code, Description = Towns[i] });
        }

        await db.SaveChangesAsync();

        // --- Applicants -------------------------------------------------------
        // Weighted so the register looks like a real office: plenty in the middle
        // of the process, a handful finished, a couple denied.
        ApplicationStatus[] distribution =
        [
            ApplicationStatus.Received, ApplicationStatus.Received, ApplicationStatus.Received,
            ApplicationStatus.InReview, ApplicationStatus.InReview, ApplicationStatus.InReview,
            ApplicationStatus.InReview, ApplicationStatus.InReview,
            ApplicationStatus.Approved, ApplicationStatus.Approved,
            ApplicationStatus.Naturalized, ApplicationStatus.Naturalized, ApplicationStatus.Naturalized,
            ApplicationStatus.Denied,
            ApplicationStatus.Withdrawn
        ];

        var streets = new[] { "Elm", "Maple", "Beacon", "Washington", "Liberty", "Concord" };

        for (var i = 0; i < People.Length; i++)
        {
            var person = People[i];
            var town = Towns[i % Towns.Length];
            var status = distribution[i % distribution.Length];

            var admissionYears = rng.Next(5, 12);
            var applicant = new Applicant
            {
                // Derived from the index rather than drawn at random: a random
                // A-Number can collide with an earlier one and trip the unique
                // index; a stride does not, and it makes the seed assertable.
                AlienNumber = $"A{100_000_000 + i * 7_919_237}",
                PetitionNumber = $"NBC{2024 + (i % 2)}{100_000 + i * 7_919:D6}",
                NaturalizationNumber = status == ApplicationStatus.Naturalized ? $"{30_000_000 + i * 137}" : "",
                FirstName = person.First,
                MiddleName = person.Middle,
                LastName = person.Last,
                BirthDate = today.AddDays(-rng.Next(23 * 365, 62 * 365)),
                AdmissionDate = today.AddDays(-admissionYears * 365 - rng.Next(0, 300)),
                Address1 = $"{rng.Next(4, 1990)} {streets[rng.Next(streets.Length)]} St",
                TownCode = townCodeByName[town],
                CountryCode = countryCodeByName[person.Country],
                ZipCode = $"0{rng.Next(1000, 9999)}",
                Email = $"{person.First.ToLowerInvariant()}.{person.Last.ToLowerInvariant()}@example.com",
                Status = status,
                CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(400, 800)),
            };

            // Decided records carry a decision date and a note. A decision, like any
            // completed act, cannot be dated in the future.
            if (status is ApplicationStatus.Approved or ApplicationStatus.Naturalized)
            {
                applicant.DecisionDate = today.AddDays(-rng.Next(5, 250));
                applicant.DecisionNotes = "All statutory requirements met. Civics and English tests passed.";
            }
            else if (status == ApplicationStatus.Denied)
            {
                applicant.DecisionDate = today.AddDays(-rng.Next(5, 250));
                applicant.DecisionNotes = "Continuous residence not established for the statutory period (INA 316(a)).";
            }

            db.Applicants.Add(applicant);
        }

        await db.SaveChangesAsync();
    }
}
