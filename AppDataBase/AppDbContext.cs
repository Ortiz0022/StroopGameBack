using Microsoft.EntityFrameworkCore;
using StroobGame.Entities;

namespace StroobGame.AppDataBase
{
    public class AppDbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseInMemoryDatabase(databaseName: "ProjectDataBase");

        public DbSet<User> Users { get; set; }
        public DbSet<UserStats> UserStats { get; set; }

        public DbSet<Room> Rooms { get; set; }
        public DbSet<RoomPlayer> RoomPlayers { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

        public DbSet<GameSession> GameSessions { get; set; }
        public DbSet<GamePlayer> GamePlayers { get; set; }
        public DbSet<Round> Rounds { get; set; }
        public DbSet<RoundOption> RoundOptions { get; set; }
        public DbSet<Answer> Answers { get; set; }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            // ===== Users / Stats =====
            mb.Entity<User>().HasIndex(u => u.Username).IsUnique();
            mb.Entity<UserStats>().HasIndex(s => s.UserId).IsUnique();

            // ===== Rooms / Players =====
            mb.Entity<RoomPlayer>().HasIndex(rp => new { rp.RoomId, rp.UserId }).IsUnique();
            mb.Entity<Room>()
                .HasMany(r => r.Players)
                .WithOne()
                .HasForeignKey(rp => rp.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            mb.Entity<RoomPlayer>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(rp => rp.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<ChatMessage>().HasIndex(m => m.RoomId);
            mb.Entity<ChatMessage>().HasIndex(m => m.SentAtUtc);

            // ===== Game =====
            mb.Entity<GameSession>().HasIndex(g => g.RoomId);
            mb.Entity<GamePlayer>().HasIndex(gp => new { gp.GameSessionId, gp.UserId }).IsUnique();

            mb.Entity<Round>()
              .HasMany(r => r.Options)
              .WithOne()
              .HasForeignKey(o => o.RoundId)
              .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<Answer>().HasIndex(a => new { a.GameSessionId, a.RoundId, a.UserId });
        }
    }
}
