using Microsoft.EntityFrameworkCore;
using StroobGame.AppDataBase;
using StroobGame.Entities;

namespace StroobGame.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _db;
        public UserService(AppDbContext db) => _db = db;

        private static string Norm(string? s) => (s ?? "").Trim();

        public async Task<User> RegisterAsync(string username)
        {
            var normalized = Norm(username);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new ArgumentException("Username requerido");

            var exists = await _db.Users
                .AnyAsync(u => u.Username.ToLower() == normalized.ToLower());
            if (exists)
                throw new InvalidOperationException("El nombre de usuario ya existe.");

            var u = new User { Username = normalized };
            _db.Users.Add(u);
            await _db.SaveChangesAsync();

            _db.UserStats.Add(new UserStats { UserId = u.Id });
            await _db.SaveChangesAsync();

            return u;
        }

        public Task<User?> GetByIdAsync(Guid userId) =>
            _db.Users.FirstOrDefaultAsync(u => u.Id == userId)!;

        public Task<User?> GetByUsernameAsync(string username)
        {
            var normalized = Norm(username);
            return _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalized.ToLower());
        }

        public async Task<User> ResolveAsync(string username)
        {
            var normalized = Norm(username);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new ArgumentException("Username requerido");

            var existing = await GetByUsernameAsync(normalized);
            if (existing != null) return existing;

            var u = new User { Username = normalized };
            _db.Users.Add(u);
            await _db.SaveChangesAsync();

            _db.UserStats.Add(new UserStats { UserId = u.Id });
            await _db.SaveChangesAsync();

            return u;
        }

        // ✅ AGREGADO: chequea si el usuario está ocupado (jugando)
        public async Task<bool> IsUserBusyAsync(string username)
        {
            var normalized = Norm(username);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized.ToLower());

            if (user is null) return false;

            // 1) Si manejas flag IsPlaying en User, úsalo primero
            if (user.IsPlaying) return true;

            // 2) Revalida por relación con sesiones "playing"
            //    Ajusta los nombres si tus DbSet difieren, la idea es:
            //    GameSessions(State="playing") -> Rooms -> RoomPlayers(UserId == user.Id)
            var isInActiveSession = await (
                from gs in _db.GameSessions.AsNoTracking()
                join room in _db.Rooms.AsNoTracking() on gs.RoomId equals room.Id
                join rp in _db.RoomPlayers.AsNoTracking() on room.Id equals rp.RoomId
                where gs.State == "playing" && rp.UserId == user.Id
                select gs.Id
            ).AnyAsync();

            return isInActiveSession;
        }
    }
}
