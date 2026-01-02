using ClinicBooking.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Api.Infrastructure.Data;

public class ClinicDbContext : DbContext
{
    public ClinicDbContext(DbContextOptions<ClinicDbContext> options) : base(options) { }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Practitioner> Practitioners => Set<Practitioner>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ===== Patient =====
        modelBuilder.Entity<Patient>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.FullName)
                .IsRequired()
                .HasMaxLength(200);

            e.Property(x => x.Email)
                .HasMaxLength(320);

            e.Property(x => x.Phone)
                .HasMaxLength(30);

            e.HasIndex(x => x.Email);
        });

        // ===== Practitioner =====
        modelBuilder.Entity<Practitioner>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.FullName)
                .IsRequired()
                .HasMaxLength(200);

            e.Property(x => x.Specialty)
                .IsRequired()
                .HasMaxLength(100);

            e.Property(x => x.IsActive)
                .IsRequired();

            e.HasIndex(x => new { x.Specialty, x.IsActive });
        });

        // ===== Appointment =====
modelBuilder.Entity<Appointment>(e =>
{
    e.HasKey(x => x.Id);

    e.Property(x => x.Status)
        .IsRequired()
        .HasConversion<string>();

    e.Property(x => x.Notes);

    e.Property(x => x.StartUtc).IsRequired();
    e.Property(x => x.EndUtc).IsRequired();

    // ✅ RELATIONS EXPLICITES (anti PatientId1 / PractitionerId1)
    e.HasOne(x => x.Patient)
        .WithMany(p => p.Appointments)
        .HasForeignKey(x => x.PatientId)
        .OnDelete(DeleteBehavior.Restrict);

    e.HasOne(x => x.Practitioner)
        .WithMany(p => p.Appointments)
        .HasForeignKey(x => x.PractitionerId)
        .OnDelete(DeleteBehavior.Restrict);

    // Index utile
    e.HasIndex(x => new { x.PractitionerId, x.StartUtc, x.EndUtc })
        .HasDatabaseName("IX_Appointments_PractitionerId_StartUtc_EndUtc");

    e.HasIndex(x => x.PatientId)
        .HasDatabaseName("IX_Appointments_PatientId");

    // Check constraint DB
    e.ToTable(t => t.HasCheckConstraint(
        "CK_Appointments_EndAfterStart",
        "\"EndUtc\" > \"StartUtc\""
    ));
});

        base.OnModelCreating(modelBuilder);
    }
}
