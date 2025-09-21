using StroobGame.Entities;

namespace StroobGame.Services
{
    public interface IUserService
    {
        Task<User> RegisterAsync(string username);
        Task<User?> GetByIdAsync(Guid userId);
        Task<User?> GetByUsernameAsync(string username);
        Task<User> ResolveAsync(string username); // devuelve existente o crea
        Task<bool> IsUserBusyAsync(string username); //consulta si el usuario está actualmente jugando
    }
}
