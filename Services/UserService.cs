using Microsoft.EntityFrameworkCore;
using StroobGame.AppDataBase;
using StroobGame.Entities;

namespace StroobGame.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _db;
        public UserService(AppDbContext db) => _db = db;

        public async Task<User> RegisterAsync(string username)
        {
            var normalized = (username ?? "").Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                throw new ArgumentException("Username requerido");

            // Validación manual (InMemory no aplica restricciones únicas reales)
            var exists = await _db.Users
                .AnyAsync(u => u.Username.ToLower() == normalized.ToLower());
            if (exists)
                throw new InvalidOperationException("El nombre de usuario ya existe.");

            var u = new User { Username = normalized };
            _db.Users.Add(u);
            await _db.SaveChangesAsync();
            return u;
        }

        public Task<User?> GetByIdAsync(Guid userId) =>
            _db.Users.FirstOrDefaultAsync(u => u.Id == userId)!;
    }
}
