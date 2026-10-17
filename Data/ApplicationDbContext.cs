using FichApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FichApp.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<AttendanceEvent> AttendanceEvents => Set<AttendanceEvent>();
    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AttendanceEvent>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.EmployeeEmail).HasMaxLength(256).IsRequired();
            entity.Property(record => record.EmployeeName).HasMaxLength(200).IsRequired();
            entity.Property(record => record.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.HasIndex(record => new { record.EmployeeId, record.RecordedAtUtc });
        });

        builder.Entity<EmployeeProfile>(entity =>
        {
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.DisplayName).HasMaxLength(200).IsRequired();
        });
    }
}
