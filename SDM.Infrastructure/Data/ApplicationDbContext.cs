using Microsoft.EntityFrameworkCore;
using SDM.Domain.Entities;

namespace SDM.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options): base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        public DbSet<Role> Roles => Set<Role>();

        // MDM entities
        public DbSet<SDM.Domain.Entities.Device> Devices => Set<SDM.Domain.Entities.Device>();

        public DbSet<SDM.Domain.Entities.DeviceCommand> DeviceCommands => Set<SDM.Domain.Entities.DeviceCommand>();

        public DbSet<SDM.Domain.Entities.DeviceHeartbeat> DeviceHeartbeats => Set<SDM.Domain.Entities.DeviceHeartbeat>();

        public DbSet<SDM.Domain.Entities.DeviceGroup> DeviceGroups => Set<SDM.Domain.Entities.DeviceGroup>();

        public DbSet<SDM.Domain.Entities.Policy> Policies => Set<SDM.Domain.Entities.Policy>();

        public DbSet<SDM.Domain.Entities.DevicePushToken> DevicePushTokens => Set<SDM.Domain.Entities.DevicePushToken>();

        public DbSet<SDM.Domain.Entities.EnrollmentToken> EnrollmentTokens => Set<SDM.Domain.Entities.EnrollmentToken>();

        public DbSet<SDM.Domain.Entities.AuditLog> AuditLogs => Set<SDM.Domain.Entities.AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var superAdminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var adminRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var operatorRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var viewerRoleId = Guid.Parse("44444444-4444-4444-4444-444444444444");

            modelBuilder.Entity<Role>().HasData(
                new Role
                {
                    Id = superAdminRoleId,
                    Name = "SuperAdmin"
                },
                new Role
                {
                    Id = adminRoleId,
                    Name = "Admin"
                },
                new Role
                {
                    Id = operatorRoleId,
                    Name = "Operator"
                },
                new Role
                {
                    Id = viewerRoleId,
                    Name = "Viewer"
                });

            modelBuilder.Entity<User>()
                .HasIndex(x => x.Email)
                .IsUnique();

            // Device indexes & relationships
            modelBuilder.Entity<SDM.Domain.Entities.Device>()
                .HasIndex(d => d.DeviceIdentifier)
                .IsUnique();

            modelBuilder.Entity<SDM.Domain.Entities.DevicePushToken>()
                .HasIndex(t => new { t.DeviceId, t.Token });

            modelBuilder.Entity<SDM.Domain.Entities.DeviceCommand>()
                .HasOne(c => c.Device)
                .WithMany(d => d.Commands)
                .HasForeignKey(c => c.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SDM.Domain.Entities.DeviceCommand>()
                .Property<int>("RetryCount")
                .HasDefaultValue(0);

            modelBuilder.Entity<SDM.Domain.Entities.DeviceCommand>()
                .Property<int>("MaxRetries")
                .HasDefaultValue(5);

            modelBuilder.Entity<SDM.Domain.Entities.AuditLog>()
                .HasIndex(a => a.Timestamp);

            modelBuilder.Entity<SDM.Domain.Entities.DeviceHeartbeat>()
                .HasOne(h => h.Device)
                .WithMany(d => d.Heartbeats)
                .HasForeignKey(h => h.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

        }


    }
}
