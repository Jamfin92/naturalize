using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Domain;

namespace Naturalization.Api.Data;

/// <summary>
/// Seeds a fabricated caseload so every screen and report has something real to
/// render on a fresh clone.
///
/// Every name, A-Number, receipt number and address below is invented. None of
/// it corresponds to a real person or a real filing.
/// </summary>
public static class DbInitializer
{
    /*
     * First, (optional) middle and last name are kept alongside country and
     * nationality in one tuple rather than in parallel arrays. Parallel arrays
     * of different lengths wrap at different rates, which is how the first cut
     * of this seed produced "Hassan Farah, born in Poland, Polish" — plausible
     * in real life, but it reads as a bug in a demo, and every reviewer stops
     * on it.
     *
     * Most people have no middle name (Middle is null); a handful carry one so
     * the split-name field and the mailing labels visibly exercise all three
     * parts. The names are given as explicit parts rather than split from a
     * single string, so multi-token and hyphenated names ("Nguyen Thi Mai",
     * "Fatima Al-Rashid") land in the right columns.
     */
    private static readonly (string First, string? Middle, string Last, string Country, string Nationality)[] People =
    [
        ("Amara", "Chidinma", "Okafor", "Nigeria", "Nigerian"),
        ("Wei", null, "Chen", "China", "Chinese"),
        ("Sofía", "Isabel", "Restrepo", "Colombia", "Colombian"),
        ("Dmitri", null, "Volkov", "Russia", "Russian"),
        ("Priya", null, "Raghunathan", "India", "Indian"),
        ("Miguel", "Antônio", "Santos", "Brazil", "Brazilian"),
        ("Fatima", null, "Al-Rashid", "Syria", "Syrian"),
        ("Nguyen", "Thi", "Mai", "Vietnam", "Vietnamese"),
        ("Kwame", null, "Mensah", "Ghana", "Ghanaian"),
        ("Elena", null, "Petrova", "Bulgaria", "Bulgarian"),
        ("Rafael", null, "Duarte", "Portugal", "Portuguese"),
        ("Aisha", null, "Kone", "Mali", "Malian"),
        ("Jae-won", null, "Park", "South Korea", "South Korean"),
        ("Lucia", null, "Ferrari", "Italy", "Italian"),
        ("Omar", null, "Haddad", "Lebanon", "Lebanese"),
        ("Yuki", null, "Tanaka", "Japan", "Japanese"),
        ("Ana", null, "Kovač", "Croatia", "Croatian"),
        ("Tenzin", null, "Norbu", "Nepal", "Nepali"),
        ("Ingrid", null, "Larsen", "Norway", "Norwegian"),
        ("Carlos", "Eduardo", "Mendoza", "Mexico", "Mexican"),
        ("Zainab", null, "Hussein", "Somalia", "Somali"),
        ("Pavel", null, "Novák", "Czechia", "Czech"),
        ("Rosa", "María", "Jiménez", "Peru", "Peruvian"),
        ("Chidi", null, "Eze", "Nigeria", "Nigerian"),
        ("Mei-ling", null, "Wu", "Taiwan", "Taiwanese"),
        ("Hassan", null, "Farah", "Somalia", "Somali"),
        ("Katarzyna", null, "Nowak", "Poland", "Polish"),
        ("Diego", null, "Vargas", "Chile", "Chilean"),
        ("Leila", null, "Nasser", "Lebanon", "Lebanese"),
        ("Sipho", null, "Dlamini", "South Africa", "South African"),
        ("Isabela", null, "Cruz", "Philippines", "Filipino"),
        ("Arjun", "Kumar", "Patel", "India", "Indian"),
        ("Marta", null, "Silva", "Portugal", "Portuguese"),
        ("Bilal", null, "Ahmed", "Pakistan", "Pakistani"),
        ("Nadia", null, "Popescu", "Romania", "Romanian"),
        ("Kofi", null, "Boateng", "Ghana", "Ghanaian"),
        ("Sara", null, "Lindqvist", "Sweden", "Swedish"),
        ("Tomás", null, "Herrera", "Guatemala", "Guatemalan"),
        ("Rania", null, "Khoury", "Jordan", "Jordanian"),
        ("Viktor", null, "Horváth", "Hungary", "Hungarian")
    ];

    private static readonly (string City, string State, string Office)[] Places =
    [
        ("Boston", "MA", "Boston, MA"), ("Cambridge", "MA", "Boston, MA"),
        ("Worcester", "MA", "Boston, MA"), ("Providence", "RI", "Providence, RI"),
        ("Hartford", "CT", "Hartford, CT"), ("Manchester", "NH", "Manchester, NH"),
        ("Portland", "ME", "Portland, ME"), ("Burlington", "VT", "St. Albans, VT")
    ];

    private static readonly string[] DocTypes =
    [
        "Permanent Resident Card (copy)",
        "Photographs (2)",
        "Marriage certificate",
        "Tax transcripts (5 years)",
        "Selective Service registration",
        "Court disposition record",
        "Travel history affidavit"
    ];

    /// <summary>
    /// The demo officers. Infrastructure, not fixtures: a database with no
    /// officer in it is an API that nobody can log into, so this runs even when
    /// the demo caseload is switched off.
    ///
    /// These passwords are public, in a public repository, on purpose — this is a
    /// demo whose every applicant is fabricated. A real deployment provisions its
    /// officers from its own identity provider (see the Okta carve-out) and never
    /// runs this. The README says so in more words.
    /// </summary>
    public const string DemoPassword = "Naturalize!Demo1";

    /*
     * One demo officer per role, so the role gating is something you can actually
     * sign in and exercise rather than take on faith: the Admin can do everything,
     * the Officer can add and edit applicants but not withdraw them, and the
     * Viewer can only read. Every account shares the public demo password.
     */
    private static readonly (string Email, string Name, string Office, OfficerRole Role)[] Officers =
    [
        ("a.hernandez@example.gov", "A. Hernandez", "Boston, MA", OfficerRole.Admin),
        ("m.whitfield@example.gov", "M. Whitfield", "Hartford, CT", OfficerRole.Officer),
        ("r.okafor@example.gov", "R. Okafor", "Providence, RI", OfficerRole.Viewer),
    ];

    public static async Task SeedOfficersAsync(
        NaturalizationDbContext db, IPasswordHasher<OfficerAccount> hasher)
    {
        if (await db.Officers.AnyAsync()) return;

        foreach (var (email, name, office, role) in Officers)
        {
            var officer = new OfficerAccount
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
            officer.PasswordHash = hasher.HashPassword(officer, DemoPassword);
            db.Officers.Add(officer);
        }

        await db.SaveChangesAsync();
    }

    public static async Task SeedDemoCaseloadAsync(NaturalizationDbContext db)
    {
        if (await db.Applicants.AnyAsync()) return;

        /*
         * The RNG is LOCAL, and that is not a style preference.
         *
         * It used to be a `static readonly Random`, which is neither thread-safe
         * nor reset between calls. Harmless with one process seeding one database
         * once — but the integration tests boot several hosts in a single process,
         * so the "deterministic" fixture would quietly differ between test classes,
         * and two hosts seeding at once can corrupt Random's internal state badly
         * enough to emit duplicate A-Numbers and trip the unique index. A suite
         * that fails once a fortnight is worse than one that fails every time.
         *
         * Fixed seed, so the demo data is still identical on every clone.
         */
        var rng = new Random(20260714);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Weighted so the pipeline looks like a real office: lots of cases in
        // the middle of the process, a handful finished, a couple denied.
        CaseStatus[] distribution =
        [
            CaseStatus.Received, CaseStatus.Received, CaseStatus.Received, CaseStatus.Received,
            CaseStatus.BiometricsScheduled, CaseStatus.BiometricsScheduled, CaseStatus.BiometricsScheduled,
            CaseStatus.BiometricsCompleted, CaseStatus.BiometricsCompleted, CaseStatus.BiometricsCompleted,
            CaseStatus.InterviewScheduled, CaseStatus.InterviewScheduled, CaseStatus.InterviewScheduled,
            CaseStatus.InterviewCompleted, CaseStatus.InterviewCompleted, CaseStatus.InterviewCompleted,
            CaseStatus.InterviewCompleted, CaseStatus.InterviewCompleted,
            CaseStatus.Approved, CaseStatus.Approved, CaseStatus.Approved,
            CaseStatus.OathScheduled, CaseStatus.OathScheduled,
            CaseStatus.Naturalized, CaseStatus.Naturalized, CaseStatus.Naturalized, CaseStatus.Naturalized,
            CaseStatus.Denied, CaseStatus.Denied,
            CaseStatus.Withdrawn
        ];

        for (var i = 0; i < People.Length; i++)
        {
            var person = People[i];
            var place = Places[i % Places.Length];

            var lprYears = rng.Next(5, 12);
            var applicant = new Applicant
            {
                // Derived from the index rather than drawn at random. A random
                // A-Number can collide with an earlier one and trip the unique
                // index; a stride does not, and it also makes the seeded data
                // assertable from a test.
                AlienNumber = $"A{100_000_000 + i * 7_919_237}",
                FirstName = person.First,
                MiddleName = person.Middle,
                LastName = person.Last,
                DateOfBirth = today.AddDays(-rng.Next(23 * 365, 62 * 365)),
                CountryOfBirth = person.Country,
                Nationality = person.Nationality,
                AddressLine = $"{rng.Next(4, 1990)} {new[] { "Elm", "Maple", "Beacon", "Washington", "Liberty", "Concord" }[rng.Next(6)]} St",
                City = place.City,
                State = place.State,
                PostalCode = $"0{rng.Next(1000, 9999)}",
                Email = $"{person.First.ToLowerInvariant()}.{person.Last.ToLowerInvariant()}@example.com",
                Phone = $"({rng.Next(200, 989)}) {rng.Next(200, 999)}-{rng.Next(1000, 9999)}",
                LawfulPermanentResidentSince = today.AddDays(-lprYears * 365 - rng.Next(0, 300)),
                CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(400, 800))
            };
            db.Applicants.Add(applicant);

            var status = distribution[i % distribution.Length];

            /*
             * A case's age has to match how far it has actually got. Filing
             * every case at a uniformly random date lets a *Naturalized* case be
             * only 90 days old, which then forces its oath date into the future —
             * and the dashboard cheerfully reports an oath administered in 2027.
             * So the age band is derived from the status.
             */
            var (minAge, maxAge) = AgeBandDays(status);
            var filedOn = today.AddDays(-rng.Next(minAge, maxAge));

            var c = new NaturalizationCase
            {
                Applicant = applicant,
                ReceiptNumber = $"NBC{2024 + (i % 2)}{100_000 + i * 7_919:D6}",   // stride, not random: cannot collide
                FiledOn = filedOn,
                FieldOffice = place.Office,
                Status = status
            };

            /*
             * Milestone dates. A *scheduled* milestone is legitimately in the
             * future — that is what "scheduled" means. A completed one must be
             * in the past. Anything else is incoherent on its face.
             */
            var reached = ReachedMilestones(status);

            if (status == CaseStatus.BiometricsScheduled)
                c.BiometricsOn = today.AddDays(rng.Next(5, 30));
            else if (reached.biometrics)
                c.BiometricsOn = PastBetween(rng, filedOn.AddDays(25), filedOn.AddDays(70), today);

            if (status == CaseStatus.InterviewScheduled)
                c.InterviewOn = today.AddDays(rng.Next(5, 40));
            else if (status is CaseStatus.Approved or CaseStatus.Denied)
                // Freshly adjudicated: interview in the last few weeks, so the
                // decision that follows it lands inside the current month and the
                // dashboard's "this month" tiles are not permanently zero.
                c.InterviewOn = PastBetween(rng, today.AddDays(-45), today.AddDays(-8), today);
            else if (reached.interview)
                c.InterviewOn = PastBetween(rng, filedOn.AddDays(120), filedOn.AddDays(300), today);

            if (status == CaseStatus.OathScheduled)
                c.OathOn = today.AddDays(rng.Next(5, 45));
            else if (reached.oath)
                c.OathOn = PastBetween(rng,
                    (c.InterviewOn ?? filedOn).AddDays(40), (c.InterviewOn ?? filedOn).AddDays(120), today);

            // Evidence.
            var docCount = rng.Next(2, 5);
            for (var d = 0; d < docCount; d++)
            {
                var type = DocTypes[(i + d) % DocTypes.Length];
                c.Documents.Add(new EvidenceDocument
                {
                    DocumentType = type,
                    FileName = $"{type.ToLowerInvariant().Replace(' ', '-').Replace("(", "").Replace(")", "")}.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = rng.Next(80_000, 4_500_000),
                    Sha256 = Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(
                            System.Text.Encoding.UTF8.GetBytes($"{c.ReceiptNumber}:{type}"))).ToLowerInvariant(),
                    Status = status >= CaseStatus.InterviewCompleted
                        ? DocumentStatus.Verified
                        : (DocumentStatus)rng.Next(0, 2),
                    UploadedAt = filedOn.ToDateTime(TimeOnly.MinValue).AddDays(rng.Next(1, 30))
                });
            }

            // Audit trail: one event per milestone actually reached.
            AddEvent(c, "Application received", filedOn, "Intake clerk",
                $"N-400 received at {place.Office} lockbox.");

            /*
             * The notice for an appointment is mailed some weeks BEFORE it — so
             * for an upcoming appointment the notice date is a recent past date,
             * not "appointment minus 14 days" (which would itself be in the
             * future whenever the appointment is under a fortnight away).
             */
            if (reached.biometrics)
            {
                AddEvent(c, "Biometrics scheduled",
                    PastBetween(rng, c.BiometricsOn!.Value.AddDays(-35), c.BiometricsOn!.Value.AddDays(-10), today),
                    "Scheduling", "Appointment notice mailed.");

                if (status != CaseStatus.BiometricsScheduled)
                    AddEvent(c, "Biometrics captured", c.BiometricsOn!.Value, "ASC Technician",
                        "Fingerprints and photograph captured.");
            }

            if (reached.interview)
            {
                AddEvent(c, "Interview scheduled",
                    PastBetween(rng, c.InterviewOn!.Value.AddDays(-45), c.InterviewOn!.Value.AddDays(-15), today),
                    "Scheduling", "Interview notice mailed.");

                if (status != CaseStatus.InterviewScheduled)
                    AddEvent(c, "Interview conducted", c.InterviewOn!.Value, "Officer A. Hernandez",
                        "Civics and English tests administered.");
            }

            // Decided cases get a Decision row and a matching audit entry.
            // A decision, like any completed act, cannot be dated in the future.
            if (status is CaseStatus.Approved or CaseStatus.OathScheduled or CaseStatus.Naturalized)
            {
                var decidedOn = PastBetween(rng,
                    c.InterviewOn!.Value.AddDays(3), c.InterviewOn!.Value.AddDays(25), today);
                c.Decision = new Decision
                {
                    Outcome = DecisionOutcome.Approved,
                    DecidedOn = decidedOn,
                    DecidedBy = "Officer A. Hernandez",
                    Rationale = "All statutory requirements met. Civics and English tests passed."
                };
                AddEvent(c, "Application approved", decidedOn, "Officer A. Hernandez",
                    "Approved for naturalization.");
            }
            else if (status == CaseStatus.Denied)
            {
                var decidedOn = PastBetween(rng,
                    c.InterviewOn!.Value.AddDays(5), c.InterviewOn!.Value.AddDays(30), today);
                c.Decision = new Decision
                {
                    Outcome = DecisionOutcome.Denied,
                    DecidedOn = decidedOn,
                    DecidedBy = "Officer M. Whitfield",
                    Rationale = "Continuous residence not established for the statutory period.",
                    DenialReasonCode = "316(a)"
                };
                AddEvent(c, "Application denied", decidedOn, "Officer M. Whitfield",
                    "Denied under INA 316(a).");
            }

            if (reached.oath)
            {
                AddEvent(c, "Oath ceremony scheduled",
                    PastBetween(rng, c.OathOn!.Value.AddDays(-40), c.OathOn!.Value.AddDays(-12), today),
                    "Scheduling", "Form N-445 mailed.");

                if (status == CaseStatus.Naturalized)
                    AddEvent(c, "Oath administered", c.OathOn!.Value, "Clerk of Court",
                        "Certificate of Naturalization issued.");
            }

            if (status == CaseStatus.Withdrawn)
                AddEvent(c, "Application withdrawn", filedOn.AddDays(rng.Next(30, 120)), "Intake clerk",
                    "Withdrawal requested in writing by applicant.");

            db.Cases.Add(c);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// How old a case must plausibly be, given how far it has progressed. A
    /// naturalized case cannot be three months old.
    /// </summary>
    private static (int Min, int Max) AgeBandDays(CaseStatus s) => s switch
    {
        CaseStatus.Received => (20, 110),
        CaseStatus.BiometricsScheduled => (45, 150),
        CaseStatus.BiometricsCompleted => (90, 220),
        CaseStatus.InterviewScheduled => (150, 330),
        CaseStatus.InterviewCompleted => (200, 400),
        CaseStatus.Approved => (240, 430),
        CaseStatus.Denied => (240, 430),
        CaseStatus.OathScheduled => (300, 500),
        CaseStatus.Naturalized => (380, 660),
        CaseStatus.Withdrawn => (60, 300),
        _ => (60, 300)
    };

    /// <summary>Pick a date in [earliest, latest], never later than <paramref name="today"/>.</summary>
    private static DateOnly PastBetween(Random rng, DateOnly earliest, DateOnly latest, DateOnly today)
    {
        if (latest > today) latest = today;
        if (earliest > latest) earliest = latest;

        var span = latest.DayNumber - earliest.DayNumber;
        return span <= 0 ? earliest : earliest.AddDays(rng.Next(0, span + 1));
    }

    private static (bool biometrics, bool interview, bool oath) ReachedMilestones(CaseStatus s) => s switch
    {
        CaseStatus.Received => (false, false, false),
        CaseStatus.Withdrawn => (false, false, false),
        CaseStatus.BiometricsScheduled => (true, false, false),
        CaseStatus.BiometricsCompleted => (true, false, false),
        CaseStatus.InterviewScheduled => (true, true, false),
        CaseStatus.InterviewCompleted => (true, true, false),
        CaseStatus.Approved => (true, true, false),
        CaseStatus.Denied => (true, true, false),
        CaseStatus.OathScheduled => (true, true, true),
        CaseStatus.Naturalized => (true, true, true),
        _ => (false, false, false)
    };

    /*
     * An audit event records something that HAS happened, so it can never be
     * dated in the future — clamped here rather than at each call site.
     *
     * The distinction that makes this necessary: an appointment date (the
     * BiometricsOn / InterviewOn / OathOn columns) is legitimately in the
     * future, but the act of *scheduling* it happened in the past. Deriving the
     * notice-mailed event as "appointment minus 14 days" therefore lands in the
     * future whenever the appointment is less than two weeks out, and the
     * dashboard then reports work done in 2027.
     */
    private static void AddEvent(NaturalizationCase c, string type, DateOnly on, string actor, string notes)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (on > today) on = today;

        c.Events.Add(new CaseEvent
        {
            EventType = type,
            OccurredAt = on.ToDateTime(new TimeOnly(9, 0)),
            Actor = actor,
            Notes = notes
        });
    }
}
