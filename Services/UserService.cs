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
    }
}
