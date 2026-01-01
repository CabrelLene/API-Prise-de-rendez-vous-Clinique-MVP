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
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Patient>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.Phone).HasMaxLength(30);
        });

        modelBuilder.Entity<Practitioner>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Specialty).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Appointment>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.Patient)
                .WithMany(x => x.Appointments)
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Practitioner)
                .WithMany(x => x.Appointments)
                .HasForeignKey(x => x.PractitionerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.PractitionerId, x.StartUtc, x.EndUtc });
        });
    }
}
