using StroobGame.Entities;

namespace StroobGame.Services
{
    public interface IChatService
    {
        Task<ChatMessage> AddAsync(Guid roomId, Guid userId, string username, string text);
        Task<List<ChatMessage>> GetLastAsync(Guid roomId, int take = 50);
    }
}
