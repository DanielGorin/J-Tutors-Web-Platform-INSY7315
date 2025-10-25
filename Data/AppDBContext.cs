#nullable enable
using Microsoft.EntityFrameworkCore;
using J_Tutors_Web_Platform.Models.Admins;
using J_Tutors_Web_Platform.Models.AppFiles;
using J_Tutors_Web_Platform.Models.Events;
using J_Tutors_Web_Platform.Models.Points;
using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Subjects;
using J_Tutors_Web_Platform.Models.Users;

namespace J_Tutors_Web_Platform.Data
{
    
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Tables in the database
        public DbSet<User> Users => Set<User>();
        public DbSet<Admin> Admins => Set<Admin>();
        public DbSet<Subject> Subjects => Set<Subject>();
        public DbSet<PricingRule> PricingRules => Set<PricingRule>();
        public DbSet<AvailabilityBlock> AvailabilityBlocks => Set<AvailabilityBlock>();
        public DbSet<TutoringSession> TutoringSessions => Set<TutoringSession>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<EventParticipation> EventParticipations => Set<EventParticipation>();
        public DbSet<AppFile> Files => Set<AppFile>();
        public DbSet<FileShareAccess> FileAccesses => Set<FileShareAccess>();
        public DbSet<PointsReceipt> PointsReceipts => Set<PointsReceipt>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Prevent duplicate file access entries
            modelBuilder.Entity<FileShareAccess>()
                .HasIndex(x => new { x.FileID, x.UserID })
                .IsUnique();

            // Event participation setup
            modelBuilder.Entity<EventParticipation>()
                .HasOne(ep => ep.Event)
                .WithMany(e => e.Participations)
                .HasForeignKey(ep => ep.EventID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EventParticipation>()
                .HasOne(ep => ep.User)
                .WithMany(u => u.EventParticipations)
                .HasForeignKey(ep => ep.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            // Tutoring session setup
            modelBuilder.Entity<TutoringSession>()
                .HasOne(ts => ts.User)
                .WithMany(u => u.TutoringSessions)
                .HasForeignKey(ts => ts.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TutoringSession>()
                .HasOne(ts => ts.Admin)
                .WithMany(a => a.TutoringSessions)
                .HasForeignKey(ts => ts.AdminID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TutoringSession>()
                .HasOne(ts => ts.Subject)
                .WithMany(s => s.TutoringSessions)
                .HasForeignKey(ts => ts.SubjectID)
                .OnDelete(DeleteBehavior.Restrict);

            // Pricing rule setup
            modelBuilder.Entity<PricingRule>()
                .HasOne(pr => pr.Subject)
                .WithMany(s => s.PricingRules)
                .HasForeignKey(pr => pr.SubjectID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PricingRule>()
                .HasOne(pr => pr.Admin)
                .WithMany(a => a.PricingRules)
                .HasForeignKey(pr => pr.AdminID)
                .OnDelete(DeleteBehavior.Cascade);

            // File and access setup
            modelBuilder.Entity<AppFile>()
                .HasOne(f => f.Admin)
                .WithMany(a => a.Files)
                .HasForeignKey(f => f.AdminID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FileShareAccess>()
                .HasOne(fa => fa.File)
                .WithMany(f => f.FileAccesses)
                .HasForeignKey(fa => fa.FileID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FileShareAccess>()
                .HasOne(fa => fa.User)
                .WithMany(u => u.FileAccesses)
                .HasForeignKey(fa => fa.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            // Points receipt setup
            modelBuilder.Entity<PointsReceipt>()
                .HasOne(pr => pr.User)
                .WithMany(u => u.PointsReceipts)
                .HasForeignKey(pr => pr.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PointsReceipt>()
                .HasOne(pr => pr.Admin)
                .WithMany(a => a.IssuedPointsReceipts)
                .HasForeignKey(pr => pr.AdminID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PointsReceipt>()
                .HasOne(pr => pr.EventParticipation)
                .WithMany()
                .HasForeignKey(pr => pr.EventParticipationID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PointsReceipt>()
                .HasOne(pr => pr.Session)
                .WithMany()
                .HasForeignKey(pr => pr.SessionID)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
