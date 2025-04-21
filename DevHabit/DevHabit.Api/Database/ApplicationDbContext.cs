using DevHabit.Api.Database.Configurations;
using DevHabit.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Database;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Habit> Habits { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<HabitTag> HabitTags { get; set; }
    //添加用户表
    public DbSet<User> Users { get; set; }
    //添加GitHubAccessToken表
    public DbSet<GitHubAccessToken> GitHubAccessTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schemas.Application);

        //自动加载所有DbSet<T>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

    }
}
