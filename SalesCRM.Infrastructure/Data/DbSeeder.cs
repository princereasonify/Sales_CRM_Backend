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
