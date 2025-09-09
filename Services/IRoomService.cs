using StroobGame.Entities;

namespace StroobGame.Services
{
    public interface IRoomService
    {
        Task<Room> CreateRoomAsync(Guid creatorUserId);
        Task<Room?> GetByCodeAsync(string code);
        Task<Room> JoinRoomAsync(string code, Guid userId); // ← usa userId existente
        Task<List<RoomPlayer>> GetPlayersAsync(Guid roomId);
        Task<bool> CanStartAsync(Guid roomId);
        Task<Room> MarkStartedAsync(Guid roomId);
    }
}
