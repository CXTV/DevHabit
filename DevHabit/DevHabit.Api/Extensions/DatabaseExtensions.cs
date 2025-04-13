using DevHabit.Api.Database;
using DevHabit.Api.Database.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Extensions;

public static class DatabaseExtensions
{
    public static void ApplyMigrations(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        /// 使用 ApplicationDbContext 进行数据库迁移
        using ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // 使用 ApplicationIdentityDbContext 进行身份验证相关的数据库迁移
        using ApplicationIdentityDbContext identityDbContext = scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>();

        try
        {
            dbContext.Database.Migrate(); 
            app.Logger.LogInformation("Database migrations applied successfully.");
            identityDbContext.Database.Migrate(); // 如果需要对身份验证数据库进行迁移，可以取消注释
            app.Logger.LogInformation("Identity database migrations successfully.");

        }
        catch (Exception e)
        {
            app.Logger.LogError(e, "An error occurred while applying database migrations.");
            throw;
        }
    }

    public static void SeedHabits(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        using ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            app.Logger.LogInformation("Start seeding");
            HabitsSeed.Seed(dbContext); // 调用 Seed 方法
            dbContext.SaveChanges();
            app.Logger.LogInformation("Database seeded successfully.");
        }
        catch (Exception e)
        {
            app.Logger.LogError(e, "An error occurred while seeding the database.");
            throw;
        }
    }


    public static async Task SeedInitialDataAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        RoleManager<IdentityRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        try
        {
            if (!await roleManager.RoleExistsAsync("Admin")) await roleManager.CreateAsync(new IdentityRole("Admin"));
            if (!await roleManager.RoleExistsAsync("Member")) await roleManager.CreateAsync(new IdentityRole("Member"));
            app.Logger.LogInformation("Successfully created roles.");
        }
        catch (Exception e)
        {
            app.Logger.LogError(e, "An error occurred while seeding initial data.");
            throw;
        }
    }
}
