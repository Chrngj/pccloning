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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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