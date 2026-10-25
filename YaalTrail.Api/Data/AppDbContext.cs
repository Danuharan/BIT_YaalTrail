using Microsoft.EntityFrameworkCore;
using YaalTrail.Api.Models;

namespace YaalTrail.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<PlaceImage> PlaceImages => Set<PlaceImage>();
    public DbSet<OpeningHour> OpeningHours => Set<OpeningHour>();
    public DbSet<VisitPlan> VisitPlans => Set<VisitPlan>();
    public DbSet<PlanItem> PlanItems => Set<PlanItem>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(100);
            e.Property(c => c.Description).HasMaxLength(500);
            e.Property(c => c.IconRef).HasMaxLength(200);
        });

        modelBuilder.Entity<Place>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Latitude).HasColumnType("decimal(9,6)");
            e.Property(p => p.Longitude).HasColumnType("decimal(9,6)");
            e.Property(p => p.DistanceKm).HasColumnType("decimal(6,2)");
            e.Property(p => p.EntryFee).HasColumnType("decimal(10,2)");

            // A category cannot be deleted while places still use it.
            e.HasOne(p => p.Category)
             .WithMany(c => c.Places)
             .HasForeignKey(p => p.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);

            // FR-48: last admin who changed the place; deleting the admin keeps the place.
            e.HasOne(p => p.MaintainedByAdmin)
             .WithMany()
             .HasForeignKey(p => p.MaintainedByAdminId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PlaceImage>(e =>
        {
            e.Property(i => i.ImageUrl).IsRequired().HasMaxLength(500);
            e.Property(i => i.Caption).HasMaxLength(200);

            e.HasOne(i => i.Place)
             .WithMany(p => p.Images)
             .HasForeignKey(i => i.PlaceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OpeningHour>(e =>
        {
            e.HasOne(o => o.Place)
             .WithMany(p => p.OpeningHours)
             .HasForeignKey(o => o.PlaceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VisitPlan>(e =>
        {
            e.Property(v => v.ShareToken).IsRequired().HasMaxLength(64);
            e.HasIndex(v => v.ShareToken).IsUnique();
        });

        modelBuilder.Entity<PlanItem>(e =>
        {
            e.HasOne(pi => pi.VisitPlan)
             .WithMany(v => v.Items)
             .HasForeignKey(pi => pi.VisitPlanId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(pi => pi.PlaceNameSnapshot).IsRequired().HasMaxLength(200);

            // Deleting a place keeps the plan; the item just loses its link
            // and is shown as "unavailable" using PlaceNameSnapshot.
            e.HasOne(pi => pi.Place)
             .WithMany()
             .HasForeignKey(pi => pi.PlaceId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AdminUser>(e =>
        {
            e.Property(a => a.Email).IsRequired().HasMaxLength(256);
            e.Property(a => a.PasswordHash).IsRequired();
            e.Property(a => a.DisplayName).IsRequired().HasMaxLength(100);
            e.HasIndex(a => a.Email).IsUnique();
        });
    }
}
