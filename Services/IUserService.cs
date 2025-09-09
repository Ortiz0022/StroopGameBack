using StroobGame.Entities;

namespace StroobGame.Services
{
    public interface IUserService
    {
        Task<User> RegisterAsync(string username);
        Task<User?> GetByIdAsync(Guid userId);

        // NUEVOS:
        Task<User?> GetByUsernameAsync(string username);
        Task<User> ResolveAsync(string username); // idempotente (devuelve existente o crea)
    }
}
