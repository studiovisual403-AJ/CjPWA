using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Models;

namespace SmartOrderSystem.Data
{
    public static class DevelopmentAdminSeeder
    {
        public static async Task SeedAsync(
            IServiceProvider services,
            IConfiguration configuration,
            ILogger logger)
        {
            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            if (await context.Admins.AnyAsync())
            {
                logger.LogInformation("Development admin seed skipped because an Admin already exists.");
                return;
            }

            var email = configuration["AdminSeed:Email"]?.Trim();
            var password = configuration["AdminSeed:Password"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                logger.LogWarning(
                    "Development admin seed skipped because AdminSeed:Email and AdminSeed:Password are not configured.");
                return;
            }

            if (password.Length < 8)
            {
                logger.LogWarning("Development admin seed skipped because AdminSeed:Password is too short.");
                return;
            }

            context.Admins.Add(new Admin
            {
                full_name = "Development Administrator",
                email = email,
                password_hash = BCrypt.Net.BCrypt.HashPassword(password)
            });

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            logger.LogInformation("Development admin seed created an Admin account for the configured email.");
        }
    }
}
