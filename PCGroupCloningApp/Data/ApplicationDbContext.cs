// Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using PCGroupCloningApp.Models;

namespace PCGroupCloningApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<ServiceAccount> ServiceAccounts { get; set; }

        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<OUConfiguration> OUConfigurations { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Tilføj denne ServiceAccount konfiguration:
            modelBuilder.Entity<ServiceAccount>(entity =>
            {
                entity.Property(e => e.Domain)
                    .HasMaxLength(50);

                entity.Property(e => e.Username)
                    .HasMaxLength(100);

                entity.Property(e => e.EncryptedPassword)
                    .HasMaxLength(1000);

                entity.Property(e => e.UpdatedBy)
                    .HasMaxLength(100);
            });

            modelBuilder.Entity<OUConfiguration>(entity =>
            {
                entity.Property(e => e.RetiredComputersOU)
                    .HasMaxLength(500);
                entity.Property(e => e.UpdatedBy)
                    .HasMaxLength(100);
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.Property(e => e.Timestamp)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.Username)
                    .HasMaxLength(100);
                entity.Property(e => e.Operation)
                    .HasMaxLength(50);
                entity.Property(e => e.SourceComputer)
                    .HasMaxLength(100);
                entity.Property(e => e.TargetComputer)
                    .HasMaxLength(100);
            });
        }
    }
}