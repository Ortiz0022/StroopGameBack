// using...
using StroobGame.Entities;
using System.Threading.Tasks;

namespace StroobGame.Services
{
    public interface IRoomService
    {
        Task<Room> CreateRoomAsync(Guid creatorUserId);
        Task<Room?> GetByCodeAsync(string code);
        Task<Room> JoinRoomAsync(string code, Guid userId);
        Task<List<RoomPlayer>> GetPlayersAsync(Guid roomId);
        Task<bool> CanStartAsync(Guid roomId);
        Task<Room> MarkStartedAsync(Guid roomId);

        // NUEVO
        Task<bool> IsOwnerAsync(Guid roomId, Guid userId);

        // NUEVO: deja la sala como recién creada (sin sesión activa ni rondas colgando)
        Task ResetRoomAsync(Guid roomId);
    }
}
