using DevHabit.Api.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Database.Configurations;


public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasMaxLength(500);
        builder.Property(t => t.UserId).HasMaxLength(500);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(50);

        builder.Property(t => t.Description).HasMaxLength(500);
        //同一个用户下标签名唯一，不同用户可以有相同标签名
        builder.HasIndex(t => new { t.UserId,t.Name }).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(h => h.UserId);

    }
}
