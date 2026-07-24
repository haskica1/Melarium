using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Seed;

/// <summary>
/// Runs after migrations. In Development it seeds demo users with well-known passwords;
/// in Production it locks those demo accounts and provisions the real SystemAdmin
/// from configuration instead. Safe to call on every startup.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Emails of the demo accounts created by <see cref="SeedUsersAsync"/>. Their passwords are
    /// public (committed to this repository), so production must never let them log in.
    /// </summary>
    private static readonly string[] DemoAccountEmails =
    [
        "sysadmin@beehive.com", "sysadmin2@beehive.com",
        "admin@goldenhive.com", "admin2@goldenhive.com",
        "admin@mountainbees.com", "admin2@mountainbees.com",
        "orgadmin@goldenhive.com", "orgadmin2@goldenhive.com",
        "orgadmin@mountainbees.com", "orgadmin2@mountainbees.com",
        "user1@goldenhive.com", "user2@goldenhive.com",
        "user1@mountainbees.com", "user2@mountainbees.com",
    ];

    /// <summary>
    /// Makes the demo accounts unusable: replaces their passwords with an unguessable random
    /// value and revokes their active refresh tokens. Runs on every production startup, so the
    /// accounts stay locked even if a demo seeder ever re-creates them.
    /// </summary>
    public static async Task LockDemoAccountsAsync(MelariumDbContext context)
    {
        var demoUsers = await context.Users
            .Where(u => DemoAccountEmails.Contains(u.Email))
            .ToListAsync();
        if (demoUsers.Count == 0) return;

        // One shared random hash is enough — no one is meant to know this password.
        var lockedHash = Hash(Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        foreach (var user in demoUsers)
            user.PasswordHash = lockedHash;

        var demoUserIds = demoUsers.Select(u => u.Id).ToList();
        var now = DateTime.UtcNow;
        var activeTokens = await context.RefreshTokens
            .Where(t => demoUserIds.Contains(t.UserId) && t.RevokedAt == null)
            .ToListAsync();
        foreach (var token in activeTokens)
            token.RevokedAt = now;

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Ensures a SystemAdmin with the configured email exists and uses the configured password
    /// (create-or-update, so rotating the env var rotates the password on the next deploy).
    /// No-ops when either value is missing. Use a dedicated email — an existing account with
    /// this email is promoted to SystemAdmin and detached from its organization.
    /// </summary>
    public static async Task EnsureBootstrapAdminAsync(MelariumDbContext context, string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var normalized = email.Trim().ToLower();
        var admin = await context.Users.FirstOrDefaultAsync(u => u.Email == normalized);

        if (admin == null)
        {
            context.Users.Add(new User
            {
                FirstName    = "System",
                LastName     = "Admin",
                Email        = normalized,
                PasswordHash = Hash(password),
                Role         = UserRole.SystemAdmin,
                CreatedAt    = DateTime.UtcNow
            });
        }
        else
        {
            admin.Role         = UserRole.SystemAdmin;
            admin.PasswordHash = Hash(password);
            // SystemAdmin must not belong to an organization/apiary (AdminService consistency rule).
            admin.OrganizationId = null;
            admin.ApiaryId       = null;
        }

        await context.SaveChangesAsync();
    }

    public static async Task SeedUsersAsync(MelariumDbContext context)
    {
        // Fix any Admin users that were seeded without an ApiaryId (e.g. from an older
        // version of this seeder). Assign the first apiary in their organisation.
        var adminsWithoutApiary = await context.Users
            .Where(u => u.Role == UserRole.ApiaryAdmin && u.ApiaryId == null && u.OrganizationId != null)
            .ToListAsync();

        bool anyFixed = false;
        foreach (var admin in adminsWithoutApiary)
        {
            var apiary = await context.Apiaries
                .Where(a => a.OrganizationId == admin.OrganizationId)
                .OrderBy(a => a.Id)
                .FirstOrDefaultAsync();

            if (apiary == null) continue;
            admin.ApiaryId = apiary.Id;
            anyFixed = true;
        }

        if (anyFixed)
            await context.SaveChangesAsync();

        var orgs = await context.Organizations.ToListAsync();
        var goldenHive   = orgs.FirstOrDefault(o => o.Id == 1);
        var mountainBees = orgs.FirstOrDefault(o => o.Id == 2);

        // Resolve actual apiary IDs from the database rather than hardcoding them,
        // so the seeder survives if the seeded apiaries were deleted or IDs shifted.
        var goldenHiveApiaryId = goldenHive == null ? null :
            await context.Apiaries
                .Where(a => a.OrganizationId == goldenHive.Id)
                .OrderBy(a => a.Id)
                .Select(a => (int?)a.Id)
                .FirstOrDefaultAsync();

        var mountainBeesApiaryId = mountainBees == null ? null :
            await context.Apiaries
                .Where(a => a.OrganizationId == mountainBees.Id)
                .OrderBy(a => a.Id)
                .Select(a => (int?)a.Id)
                .FirstOrDefaultAsync();

        var usersToAdd = new List<User>();

        // ── SystemAdmin ───────────────────────────────────────────────────────
        // Two system-wide admins with no org affiliation.

        AddIfMissing(context, usersToAdd, new User
        {
            FirstName    = "System",
            LastName     = "Admin",
            Email        = "sysadmin@beehive.com",
            PasswordHash = Hash("SysAdmin123!"),
            Role         = UserRole.SystemAdmin,
            CreatedAt    = DateTime.UtcNow
        });

        AddIfMissing(context, usersToAdd, new User
        {
            FirstName    = "Emma",
            LastName     = "Systems",
            Email        = "sysadmin2@beehive.com",
            PasswordHash = Hash("SysAdmin123!"),
            Role         = UserRole.SystemAdmin,
            CreatedAt    = DateTime.UtcNow
        });

        // ── Admin ─────────────────────────────────────────────────────────────
        // Apiary-scoped admins — one per apiary per org.
        // ApiaryId is looked up at runtime so hardcoded IDs never cause FK failures.

        if (goldenHive != null && goldenHiveApiaryId != null)
        {
            AddIfMissing(context, usersToAdd, new User
            {
                FirstName      = "Alice",
                LastName       = "Goldsworth",
                Email          = "admin@goldenhive.com",
                PasswordHash   = Hash("Admin123!"),
                Role           = UserRole.ApiaryAdmin,
                OrganizationId = goldenHive.Id,
                ApiaryId       = goldenHiveApiaryId,
                CreatedAt      = DateTime.UtcNow
            });

            AddIfMissing(context, usersToAdd, new User
            {
                FirstName      = "James",
                LastName       = "Holt",
                Email          = "admin2@goldenhive.com",
                PasswordHash   = Hash("Admin123!"),
                Role           = UserRole.ApiaryAdmin,
                OrganizationId = goldenHive.Id,
                ApiaryId       = goldenHiveApiaryId,
                CreatedAt      = DateTime.UtcNow
            });
        }

        if (mountainBees != null && mountainBeesApiaryId != null)
        {
            AddIfMissing(context, usersToAdd, new User
            {
                FirstName      = "Marco",
                LastName       = "Bianchi",
                Email          = "admin@mountainbees.com",
                PasswordHash   = Hash("Admin123!"),
                Role           = UserRole.ApiaryAdmin,
                OrganizationId = mountainBees.Id,
                ApiaryId       = mountainBeesApiaryId,
                CreatedAt      = DateTime.UtcNow
            });

            AddIfMissing(context, usersToAdd, new User
            {
                FirstName      = "Nina",
                LastName       = "Horvat",
                Email          = "admin2@mountainbees.com",
                PasswordHash   = Hash("Admin123!"),
                Role           = UserRole.ApiaryAdmin,
                OrganizationId = mountainBees.Id,
                ApiaryId       = mountainBeesApiaryId,
                CreatedAt      = DateTime.UtcNow
            });
        }

        // ── OrgAdmin ──────────────────────────────────────────────────────────
        // Org-level admins — manage the entire organisation.

        if (goldenHive != null)
        {
            AddIfMissing(context, usersToAdd, new User
            {
                FirstName      = "Bob",
                LastName       = "Keeper",
                Email          = "orgadmin@goldenhive.com",
                PasswordHash   = Hash("OrgAdmin123!"),
                Role           = UserRole.OrganizationAdmin,
                OrganizationId = goldenHive.Id,
                CreatedAt      = DateTime.UtcNow
            });

            AddIfMissing(context, usersToAdd, new User
            {
                FirstName      = "Diana",
                LastName       = "Fields",
                Email          = "orgadmin2@goldenhive.com",
                PasswordHash   = Hash("OrgAdmin123!"),
                Role           = UserRole.OrganizationAdmin,
                OrganizationId = goldenHive.Id,
                CreatedAt      = DateTime.UtcNow
            });
        }

        if (mountainBees != null)
        {
            AddIfMissing(context, usersToAdd, new User
            {
                FirstName      = "Sofia",
                LastName       = "Petrović",
                Email          = "orgadmin@mountainbees.com",
                PasswordHash   = Hash("OrgAdmin123!"),
                Role           = UserRole.OrganizationAdmin,
                OrganizationId = mountainBees.Id,
                CreatedAt      = DateTime.UtcNow
            });

            AddIfMissing(context, usersToAdd, new User
            {
                FirstName      = "Luka",
                LastName       = "Novak",
                Email          = "orgadmin2@mountainbees.com",
                PasswordHash   = Hash("OrgAdmin123!"),
                Role           = UserRole.OrganizationAdmin,
                OrganizationId = mountainBees.Id,
                CreatedAt      = DateTime.UtcNow
            });
        }

        // ── User ──────────────────────────────────────────────────────────────
        // Regular beekeepers — two per organisation.

        if (goldenHive != null)
        {
            AddIfMissing(context, usersToAdd, new User
            {
                FirstName      = "Tom",
                LastName       = "Meadows",
                Email          = "user1@goldenhive.com",
                PasswordHash   = Hash("User123!"),
                Role           = UserRole.Beekeeper,
                OrganizationId = goldenHive.Id,
                CreatedAt      = DateTime.UtcNow
            });

            AddIfMissing(context, usersToAdd, new User
            {
                FirstName      = "Laura",
                LastName       = "Bloom",
                Email          = "user2@goldenhive.com",
                PasswordHash   = Hash("User123!"),
                Role           = UserRole.Beekeeper,
                OrganizationId = goldenHive.Id,
                CreatedAt      = DateTime.UtcNow
            });
        }

        if (mountainBees != null)
        {
            AddIfMissing(context, usersToAdd, new User
            {
                FirstName      = "Ivan",
                LastName       = "Petrov",
                Email          = "user1@mountainbees.com",
                PasswordHash   = Hash("User123!"),
                Role           = UserRole.Beekeeper,
                OrganizationId = mountainBees.Id,
                CreatedAt      = DateTime.UtcNow
            });

            AddIfMissing(context, usersToAdd, new User
            {
                FirstName      = "Ana",
                LastName       = "Kovač",
                Email          = "user2@mountainbees.com",
                PasswordHash   = Hash("User123!"),
                Role           = UserRole.Beekeeper,
                OrganizationId = mountainBees.Id,
                CreatedAt      = DateTime.UtcNow
            });
        }

        if (usersToAdd.Count > 0)
        {
            context.Users.AddRange(usersToAdd);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Seeds 6 starter learning topics (SPEC-06) — Development only, same policy as demo accounts.
    /// Production content is authored by SystemAdmin. No-ops when any topic already exists.
    /// Inserted directly (not via the service), so no publish notifications are fired.
    /// </summary>
    public static async Task SeedLearningTopicsAsync(MelariumDbContext context)
    {
        if (await context.LearningTopics.AnyAsync()) return;

        var now = DateTime.UtcNow;
        LearningTopic Topic(string title, LearningCategory category, int[]? months, string summary, string body) => new()
        {
            Title        = title,
            Category     = category,
            Months       = months,
            Summary      = summary,
            BodyMarkdown = body,
            IsPublished  = true,
            PublishedAt  = now,
            CreatedAt    = now,
        };

        context.LearningTopics.AddRange(
            Topic("Kalendar radova u julu", LearningCategory.SezonskiRadovi, [7],
                "Vrcanje kasnih paša, kontrola varoe nakon vrcanja i početak pripreme zajednica za jesen.",
                """
                ## Glavni radovi

                - Izvrcati zrele nastavke kasnih paša (lipa, kesten, livada) — ne ostavljati med da kristalizira u saću.
                - Odmah nakon zadnjeg vrcanja napraviti **monitoring varoe** (vidi posebnu temu) i planirati ljetni tretman.
                - Provjeriti maticu: u julu zajednica još mora imati kompaktno leglo. Bezmatak sada znači slabu zimsku pčelu.

                ## Na šta paziti

                - **Grabež**: paša slabi, otvore smanjiti, ne držati košnice dugo otvorene, ne prosipati sirup.
                - **Pregrijavanje**: osigurati sjenu i vodu; jaka zajednica na suncu troši med na hlađenje.

                ## Priprema za avgust

                Julski poslovi određuju zimu: zdrava i brojna avgustovska pčela je uslov preživljavanja.
                Sve što varoi dozvolite u julu, naplatiće se u oktobru.
                """),

            Topic("Prepoznavanje i monitoring varoe", LearningCategory.BolestiINametnici, [6, 7, 8],
                "Kako pouzdano procijeniti zaraženost varoom: podnjača, šećerna metoda i prag za tretman.",
                """
                ## Zašto monitoring, a ne osjećaj

                Vidljive varoe na pčelama znače **jaku** zarazu — tada je već kasno. Zaraženost se mjeri, ne procjenjuje od oka.

                ## Metode

                1. **Pregled podnjače (prirodni pad)** — umetnuti čistu podlogu 3 dana; > 5–10 varoa dnevno sredinom ljeta = tretman.
                2. **Šećerna metoda** — ~300 pčela (pola čaše) iz plodišta u teglu s kašikom šećera u prahu, protresti, izbrojati varoe kroz mrežicu. Preko 3 % (9+ varoa) = tretirati odmah.
                3. **Pregled trutovskog legla** — viljuškom izvaditi kukuljice: varoa se vidi golim okom.

                ## Kada mjeriti

                Najkasnije odmah nakon zadnjeg vrcanja, pa kontrola 3 sedmice poslije tretmana.
                Rezultate upisujte u aplikaciju (Tretmani) — evidencija je i zakonska obaveza.
                """),

            Topic("Sprječavanje rojenja", LearningCategory.SezonskiRadovi, [4, 5, 6],
                "Rojevni nagon: kako ga prepoznati na vrijeme i šta zaista pomaže — prostor, mlada matica, ventilacija.",
                """
                ## Znakovi rojevnog raspoloženja

                - Matičnjaci na donjim ivicama okvira (rojevni, ne prisilni!),
                - "brada" pčela na letu bez velike vrućine,
                - zajednica prestaje graditi satne osnove.

                ## Šta pomaže (redom po važnosti)

                1. **Prostor na vrijeme** — proširiti plodište/dodati nastavak *prije* nego zagusti.
                2. **Mlada matica** — zajednice s maticom do 2 godine roje se znatno rjeđe.
                3. **Ventilacija i sjena** — pregrijano plodište je okidač.
                4. **Skidanje rojevnih matičnjaka** kupuje najviše 7–9 dana — bez rješavanja uzroka ne vrijedi.

                ## Ako je nagon već jak

                Napraviti umjetni roj ("prisilno rojenje" pod vašom kontrolom): stara matica + 2–3 okvira legla u novu košnicu.
                Zajednica koja je krenula u rojenje rijetko odustaje — bolje ga usmjeriti nego izgubiti pola pčela na granu.
                """),

            Topic("Priprema zajednica za zimu", LearningCategory.SezonskiRadovi, [8, 9, 10],
                "Zimska pčela, rezerve hrane, utopljavanje i zaštita od miševa — šta uraditi od avgusta do oktobra.",
                """
                ## Avgust — najvažniji mjesec pčelarske godine

                Pčele izležene u avgustu/septembru su **zimske pčele**. Da bi bile zdrave:
                - varoa mora biti tretirana odmah nakon zadnjeg vrcanja,
                - zajednica mora imati polena i prostora za leglo.

                ## Rezerve hrane

                - LR dvonastavna zajednica: **15–18 kg** meda/sirupa; AŽ: 12–15 kg.
                - Prihranu gustim sirupom (3:2) završiti do kraja septembra — pčele ga moraju stići preraditi i poklopiti.

                ## Oktobar — završni radovi

                - Suziti leto i postaviti **češalj protiv miševa**.
                - Skinuti prazne nastavke, zajednicu svesti na prostor koji pokriva.
                - Osigurati krov od vjetra i nagib košnice naprijed (kondenzat mora van).

                Zimu ne preživljava najjača zajednica, nego **najzdravija**.
                """),

            Topic("Prihrana pčela — kada i čime", LearningCategory.Osnove, null,
                "Šećerni sirup, pogača i medno-šećerno tijesto: omjeri, namjena i najčešće greške u prihrani.",
                """
                ## Vrste prihrane

                | Prihrana | Omjer | Namjena |
                |---|---|---|
                | Rijetki sirup | 1:1 | proljetna stimulacija legla |
                | Gusti sirup | 3:2 | dopuna zimskih rezervi (avg–sep) |
                | Pogača (fondan) | — | zimska nužda i rana stimulacija |
                | Proteinska pogača | — | polen deficit, jačanje u rano proljeće |

                ## Pravila koja štede zajednice

                - Prihranjivati **uveče** — manje grabeži.
                - Nikad ne prihranjivati tokom paše s nastavcima za med (sirup završi u medu!).
                - Količinu prilagoditi snazi: slaboj zajednici malo i često.
                - Sirup koji stoji danima fermentira — praviti svježe.

                ## Evidencija

                Svaku prihranu bilježite u aplikaciji (Ishrana) — historija prihrane uz preglede daje
                jasnu sliku razvoja zajednice kroz sezonu.
                """),

            Topic("Higijena i dezinfekcija opreme", LearningCategory.Oprema, null,
                "Plamenik, soda, sunce: kako čistiti košnice, okvire i pribor da bolesti ne putuju pčelinjakom.",
                """
                ## Zašto je higijena pola zdravlja

                Spore nozemoze i američke gnjiloće prenose se **opremom** češće nego pčelama.
                Alat koji ide iz košnice u košnicu je najčešći "vektor" na pčelinjaku.

                ## Praksa po materijalu

                - **Drvene košnice i okviri**: sastrugati vosak/propolis, pa plamenikom do slabog smeđenja drveta.
                - **AŽ i nastavci koji ne smiju na plamen**: vrela 3–5 % otopina kaustične sode (zaštitne rukavice i naočale!), pa temeljito ispiranje.
                - **Kovinski alat (dlijeto, viljuška)**: između zajednica prebrisati alkoholom ili opaliti.
                - **Rukavice**: kožne su nemoguće za dezinfekciju — bolje jednokratne ili pranje platnenih.

                ## Saće

                Staro tamno saće (3+ godine) je rezervoar patogena — pretapati, ne "štedjeti".
                Godišnje zamijeniti trećinu saća u plodištu.
                """)
        );

        await context.SaveChangesAsync();
    }

    private static void AddIfMissing(MelariumDbContext context, List<User> batch, User user)
    {
        if (!context.Users.Any(u => u.Email == user.Email) &&
            batch.All(u => u.Email != user.Email))
            batch.Add(user);
    }

    private static string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password);
}
