using StroobGame.Entities;

namespace StroobGame.Services
{
    public interface IUserService
    {
        Task<User> RegisterAsync(string username);
        Task<User?> GetByIdAsync(Guid userId);
    }
}
