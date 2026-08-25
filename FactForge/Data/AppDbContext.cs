using FactForge.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactForge.Data;

public class AppDbContext : DbContext
{
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<Slide> Slides => Set<Slide>();
    public DbSet<SlideOption> SlideOptions => Set<SlideOption>();
    public DbSet<QuizSession> QuizSessions => Set<QuizSession>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerAnswer> PlayerAnswers => Set<PlayerAnswer>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Quiz>()
            .HasMany(q => q.Slides)
            .WithOne(s => s.Quiz!)
            .HasForeignKey(s => s.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Quiz>()
            .HasMany(q => q.Sessions)
            .WithOne(s => s.Quiz!)
            .HasForeignKey(s => s.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Slide>()
            .HasMany(s => s.Options)
            .WithOne(o => o.Slide!)
            .HasForeignKey(o => o.SlideId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<QuizSession>()
            .HasMany(s => s.Players)
            .WithOne(p => p.Session!)
            .HasForeignKey(p => p.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Player>()
            .HasMany(p => p.Answers)
            .WithOne(a => a.Player!)
            .HasForeignKey(a => a.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade: deleting a quiz (or an individual slide) also removes whatever
        // historical answers were recorded against it, so "delete quiz" always
        // succeeds instead of failing with a dangling-reference constraint.
        modelBuilder.Entity<PlayerAnswer>()
            .HasOne(a => a.Slide)
            .WithMany()
            .HasForeignKey(a => a.SlideId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<QuizSession>()
            .HasIndex(s => s.JoinCode);
    }
}
