using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Roam.Domain.Users;

namespace Roam.Infrastructure.Identity;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

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
                }
            }
            // Safeguard C2: Never auto-promote pre-existing accounts to Admin at restart
        }

        // 3. Auto-approve specified volunteer emails only if already email-confirmed
        var volunteerEmailsRaw = configuration["Bootstrap:VolunteerEmails"] ?? configuration["BOOTSTRAP_VOLUNTEER_EMAILS"];
        if (!string.IsNullOrWhiteSpace(volunteerEmailsRaw))
        {
            var emails = volunteerEmailsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var email in emails)
            {
                var volUser = await userManager.FindByEmailAsync(email);
                // Safeguard C2: Only approve volunteer status if the user's email has already been verified
                if (volUser != null && volUser.EmailConfirmed && volUser.VolunteerStatus != VolunteerStatus.Approved)
                {
                    volUser.VolunteerStatus = VolunteerStatus.Approved;
                    await userManager.UpdateAsync(volUser);
                }
            }
        }
    }
}
