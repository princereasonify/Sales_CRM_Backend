using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Infrastructure.Services;

namespace SalesCRM.Infrastructure.Data;

public static class DbSeeder
{
    // Sentinel: if this exact SCA user exists with this password, the DB is already in the target state.
    private const string SentinelEmail = "pankti@reasonify.in";
    private const string SentinelPassword = "Pankti@123";

    public static async Task SeedAsync(AppDbContext context)
    {
        // Idempotent — runs every startup, only inserts boards that are missing.
        // Lives outside the sentinel guard so existing DBs get the master data too.
        await SeedBoardsAsync(context);

        var sentinel = context.Users.FirstOrDefault(u => u.Email == SentinelEmail);
        if (sentinel != null && VerifyPassword(SentinelPassword, sentinel.PasswordHash))
            return;

        await WipeAllAsync(context);

        // Region
        var gujarat = new Region { Name = "Gujarat" };
        context.Regions.Add(gujarat);
        await context.SaveChangesAsync();

        // Zone
        var ahmedabad = new Zone { Name = "Ahmedabad", RegionId = gujarat.Id };
        context.Zones.Add(ahmedabad);
        await context.SaveChangesAsync();

        // Users
        var users = new[]
        {
            new User
            {
                Name = "Pankti Parekh",
                Email = "pankti@reasonify.in",
                PasswordHash = AuthService.HashPassword("Pankti@123"),
                Role = UserRole.SCA,
                Avatar = "PP",
            },
            new User
            {
                Name = "Shaishav K",
                Email = "shaishav@reasonify.in",
                PasswordHash = AuthService.HashPassword("Shsishav@123"),
                Role = UserRole.RH,
                RegionId = gujarat.Id,
                Avatar = "SK",
            },
            new User
            {
                Name = "Mani Gupta",
                Email = "mani@singularity-learn.com",
                PasswordHash = AuthService.HashPassword("Mania@123"),
                Role = UserRole.ZH,
                RegionId = gujarat.Id,
                ZoneId = ahmedabad.Id,
                Avatar = "MG",
            },
            new User
            {
                Name = "Bhavya Neema",
                Email = "bhavya@reasonify.in",
                PasswordHash = AuthService.HashPassword("Bhavya@123"),
                Role = UserRole.FO,
                RegionId = gujarat.Id,
                ZoneId = ahmedabad.Id,
                Avatar = "BN",
            },
        };
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
    }

    // Comprehensive list of Indian education boards — central, all 28 state boards, key open / vocational, and international boards used in India.
    private static async Task SeedBoardsAsync(AppDbContext context)
    {
        var seed = new (string Name, string ShortCode, string Category)[]
        {
            // Central
            ("Central Board of Secondary Education",                                            "CBSE",   "Central"),
            ("Council for the Indian School Certificate Examinations",                          "CISCE",  "Central"),
            ("National Institute of Open Schooling",                                            "NIOS",   "Central"),

            // International (commonly used in India)
            ("International Baccalaureate",                                                     "IB",     "International"),
            ("Cambridge Assessment International Education",                                    "CAIE",   "International"),
            ("International General Certificate of Secondary Education",                        "IGCSE",  "International"),

            // State boards (28 states)
            ("Andhra Pradesh Board of Secondary Education",                                     "APBSE",  "State"),
            ("Board of Intermediate Education, Andhra Pradesh",                                 "BIEAP",  "State"),
            ("Arunachal Pradesh Board of School Education",                                     "APBSE-AR","State"),
            ("Board of Secondary Education, Assam",                                             "SEBA",   "State"),
            ("Assam Higher Secondary Education Council",                                        "AHSEC",  "State"),
            ("Bihar School Examination Board",                                                  "BSEB",   "State"),
            ("Chhattisgarh Board of Secondary Education",                                       "CGBSE",  "State"),
            ("Goa Board of Secondary and Higher Secondary Education",                           "GBSHSE", "State"),
            ("Gujarat Secondary and Higher Secondary Education Board",                          "GSEB",   "State"),
            ("Board of School Education Haryana",                                               "BSEH",   "State"),
            ("Himachal Pradesh Board of School Education",                                      "HPBOSE", "State"),
            ("Jharkhand Academic Council",                                                      "JAC",    "State"),
            ("Karnataka School Examination and Assessment Board",                               "KSEAB",  "State"),
            ("Department of Pre-University Education, Karnataka",                               "PUE-KA", "State"),
            ("Kerala Board of Public Examinations",                                             "KBPE",   "State"),
            ("Madhya Pradesh Board of Secondary Education",                                     "MPBSE",  "State"),
            ("Maharashtra State Board of Secondary and Higher Secondary Education",             "MSBSHSE","State"),
            ("Board of Secondary Education, Manipur",                                           "BSEM",   "State"),
            ("Meghalaya Board of School Education",                                             "MBOSE",  "State"),
            ("Mizoram Board of School Education",                                               "MBSE",   "State"),
            ("Nagaland Board of School Education",                                              "NBSE",   "State"),
            ("Board of Secondary Education, Odisha",                                            "BSE-OD", "State"),
            ("Council of Higher Secondary Education, Odisha",                                   "CHSE-OD","State"),
            ("Punjab School Education Board",                                                   "PSEB",   "State"),
            ("Board of Secondary Education, Rajasthan",                                         "RBSE",   "State"),
            ("Sikkim Board of School Examination",                                              "SBSE",   "State"),
            ("Tamil Nadu State Board of School Examination",                                    "TNSBSE", "State"),
            ("Telangana Board of Secondary Education",                                          "TSBSE",  "State"),
            ("Board of Intermediate Education, Telangana",                                      "TSBIE",  "State"),
            ("Tripura Board of Secondary Education",                                            "TBSE",   "State"),
            ("Uttar Pradesh Madhyamik Shiksha Parishad",                                        "UPMSP",  "State"),
            ("Uttarakhand Board of School Education",                                           "UBSE",   "State"),
            ("West Bengal Board of Secondary Education",                                        "WBBSE",  "State"),
            ("West Bengal Council of Higher Secondary Education",                               "WBCHSE", "State"),

            // Union Territories
            ("Jammu and Kashmir State Board of School Education",                               "JKBOSE", "State"),
            ("Directorate of Education, Delhi",                                                 "DOE-DL", "State"),
            ("Puducherry Board of Higher Secondary Examinations",                               "PBHSE",  "State"),

            // Madrasah / specialised
            ("West Bengal Board of Madrasah Education",                                         "WBBME",  "State"),
            ("Madhyamik Shiksha Mandal Madhya Pradesh — Open Schooling",                        "MPSOS",  "State"),
        };

        var existingNames = new HashSet<string>(context.Boards.Select(b => b.Name));
        var toInsert = seed
            .Where(s => !existingNames.Contains(s.Name))
            .Select(s => new Board { Name = s.Name, ShortCode = s.ShortCode, Category = s.Category })
            .ToList();

        if (toInsert.Count == 0) return;
        context.Boards.AddRange(toInsert);
        await context.SaveChangesAsync();
    }

    // TRUNCATE every table in the model with RESTART IDENTITY CASCADE. Postgres-only.
    private static async Task WipeAllAsync(AppDbContext context)
    {
        var tableNames = context.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .Select(n => $"\"{n}\"");

        var joined = string.Join(", ", tableNames);
        var sql = $"TRUNCATE TABLE {joined} RESTART IDENTITY CASCADE;";
        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split('.');
            if (parts.Length != 2) return false;
            var salt = Convert.FromBase64String(parts[0]);
            var hash = Convert.FromBase64String(parts[1]);
            using var hmac = new System.Security.Cryptography.HMACSHA256(salt);
            var computed = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return computed.SequenceEqual(hash);
        }
        catch { return false; }
    }
}
