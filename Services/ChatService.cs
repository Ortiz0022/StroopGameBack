using Microsoft.EntityFrameworkCore;
using StroobGame.AppDataBase;
using StroobGame.Entities;

namespace StroobGame.Services
{
    public class ChatService : IChatService
    {
        private readonly AppDbContext _db;
        public ChatService(AppDbContext db) => _db = db;

        public async Task<ChatMessage> AddAsync(Guid roomId, Guid userId, string username, string text)
        {
            var clean = (text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(clean))
                throw new ArgumentException("Mensaje vacío.");

            if (clean.Length > 400) clean = clean[..400];

            var msg = new ChatMessage
            {
                RoomId = roomId,
                UserId = userId,
                Username = username,
                Text = clean,
                SentAtUtc = DateTime.UtcNow
            };
            _db.ChatMessages.Add(msg);
            await _db.SaveChangesAsync();
            return msg;
        }

        public Task<List<ChatMessage>> GetLastAsync(Guid roomId, int take = 50) =>
            _db.ChatMessages
               .Where(m => m.RoomId == roomId)
               .OrderByDescending(m => m.SentAtUtc)
               .Take(take)
               .OrderBy(m => m.SentAtUtc)
               .ToListAsync();
    }
}
