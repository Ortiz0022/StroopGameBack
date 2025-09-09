using Microsoft.EntityFrameworkCore;
using StroobGame.Entities;

namespace StroobGame.AppDataBase
{
    public class AppDbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(databaseName: "ProjectDataBase");
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<RoomPlayer> RoomPlayers { get; set; }

        public DbSet<ChatMessage> ChatMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            // Unicidad de username (en InMemory NO se aplica fuerte; igual validamos en servicio)
            mb.Entity<User>().HasIndex(u => u.Username).IsUnique();

            // Un usuario no puede estar dos veces en la misma sala
            mb.Entity<RoomPlayer>().HasIndex(rp => new { rp.RoomId, rp.UserId }).IsUnique();

            // Relaciones
            mb.Entity<Room>()
              .HasMany(r => r.Players)
              .WithOne()
              .HasForeignKey(rp => rp.RoomId)
              .OnDelete(DeleteBehavior.Cascade);

            // FK explícita RoomPlayer -> User (recomendado)
            mb.Entity<RoomPlayer>()
              .HasOne<User>()
              .WithMany()
              .HasForeignKey(rp => rp.UserId)
              .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<ChatMessage>().HasIndex(m => m.RoomId);
            mb.Entity<ChatMessage>().HasIndex(m => m.SentAtUtc);
        }
    }
}
