using HO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HO.Infrastructure.Persistence.Configurations;

public class HoUserConfiguration : IEntityTypeConfiguration<HoUser>
{
    public void Configure(EntityTypeBuilder<HoUser> b)
    {
        b.HasKey(u => u.UserId);
        b.HasIndex(u => u.Username).IsUnique();
        b.HasIndex(u => u.Email).IsUnique();
        b.Property(u => u.Username).HasMaxLength(50).IsRequired();
        b.Property(u => u.FullName).HasMaxLength(100).IsRequired();
        b.Property(u => u.Email).HasMaxLength(200).IsRequired();
        b.Property(u => u.PasswordHash).HasMaxLength(200).IsRequired();
        b.Property(u => u.Role).HasMaxLength(30).IsRequired();
        b.Property(u => u.LastLoginIp).HasMaxLength(50);
        b.Property(u => u.CreatedBy).HasMaxLength(100);
    }
}
