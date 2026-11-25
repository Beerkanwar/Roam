using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Roam.Domain.Users;

namespace Roam.Infrastructure.Identity;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Roam.Infrastructure.Persistence.ApplicationDbContext>();

        // Several instances may start at once; serialize seeding so they don't race to create the same
        // role/admin (which would fail one instance's startup on a unique-index violation).
        await Roam.Infrastructure.Scaling.PostgresAdvisoryLock.RunExclusiveAsync(
            db, Roam.Infrastructure.Scaling.PostgresAdvisoryLock.SeedingLockId,
            _ => SeedCoreAsync(scope.ServiceProvider, configuration), CancellationToken.None);
    }

    private static async Task SeedCoreAsync(IServiceProvider scopedServices, IConfiguration configuration)
    {
        var roleManager = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scopedServices.GetService<ILoggerFactory>()?.CreateLogger(typeof(DatabaseSeeder).FullName!);

        // 1. Ensure "Admin" role exists
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        // 2. Check bootstrap admin
        var adminEmail = configuration["Bootstrap:AdminEmail"] ?? configuration["BOOTSTRAP_ADMIN_EMAIL"];
        var adminPassword = configuration["Bootstrap:AdminPassword"] ?? configuration["BOOTSTRAP_ADMIN_PASSWORD"];

        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                if (!string.IsNullOrWhiteSpace(adminPassword) && adminPassword.Length >= 8)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true,
                        DisplayName = "System Administrator",
                        AccountStatus = AccountStatus.Active,
                        VolunteerStatus = VolunteerStatus.Approved,
                        AgeCategory = AgeCategory.Adult
                    };

                    var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                    if (createResult.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                    }
                    else
                    {
                        logger?.LogError("Bootstrap admin account could not be created: {Errors}",
                            string.Join("; ", createResult.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    logger?.LogWarning("Bootstrap admin email is set but the bootstrap password is missing or shorter than 8 characters; admin account was not created.");
                }
            }
            // Safeguard: Never auto-promote pre-existing accounts to Admin at restart
        }

        // 3. Auto-approve specified volunteer emails only if already email-confirmed
        var volunteerEmailsRaw = configuration["Bootstrap:VolunteerEmails"] ?? configuration["BOOTSTRAP_VOLUNTEER_EMAILS"];
        if (!string.IsNullOrWhiteSpace(volunteerEmailsRaw))
        {
            var emails = volunteerEmailsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var email in emails)
            {
                var volUser = await userManager.FindByEmailAsync(email);
                // Safeguard: Only approve volunteer status if the user's email has already been verified
                if (volUser != null && volUser.EmailConfirmed && volUser.VolunteerStatus != VolunteerStatus.Approved)
                {
                    volUser.VolunteerStatus = VolunteerStatus.Approved;
                    await userManager.UpdateAsync(volUser);
                }
            }
        }
    }
}
