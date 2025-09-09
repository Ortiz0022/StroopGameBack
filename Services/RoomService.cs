using Microsoft.EntityFrameworkCore;
using StroobGame.AppDataBase;
using StroobGame.Entities;

namespace StroobGame.Services
{
    public class RoomService : IRoomService
    {
        private readonly AppDbContext _db;
        private readonly Random _rnd = new();
        public RoomService(AppDbContext db) => _db = db;

        private string NewCode() => _rnd.Next(10000, 99999).ToString();

        public async Task<Room> CreateRoomAsync(Guid creatorUserId)
        {
            // El creador debe existir (está logeado)
            var creator = await _db.Users.FindAsync(creatorUserId)
                ?? throw new KeyNotFoundException("Usuario creador no existe");

            var code = NewCode();
            while (await _db.Rooms.AnyAsync(r => r.Code == code))
                code = NewCode();

            var room = new Room
            {
                Code = code,
                CreatorUserId = creatorUserId,
                MinPlayers = 2,  // 2–4 como pediste
                MaxPlayers = 4
            };

            _db.Rooms.Add(room);
            await _db.SaveChangesAsync();
            return room;
        }

        public Task<Room?> GetByCodeAsync(string code) =>
            _db.Rooms.Include(r => r.Players).FirstOrDefaultAsync(r => r.Code == code);

        public async Task<Room> JoinRoomAsync(string code, Guid userId)
        {
            var room = await GetByCodeAsync(code)
                ?? throw new KeyNotFoundException("Sala no existe");

            var user = await _db.Users.FindAsync(userId)
                ?? throw new KeyNotFoundException("Usuario no existe");

            if (room.Started)
                throw new InvalidOperationException("La sala ya inició");

            var count = await _db.RoomPlayers.CountAsync(p => p.RoomId == room.Id);
            if (count >= room.MaxPlayers)
                throw new InvalidOperationException("Sala llena");

            var already = await _db.RoomPlayers
                .FirstOrDefaultAsync(p => p.RoomId == room.Id && p.UserId == user.Id);

            if (already != null) return room; // ya estaba dentro

            _db.RoomPlayers.Add(new RoomPlayer
            {
                RoomId = room.Id,
                UserId = user.Id,
                Username = user.Username,
                IsOwner = (count == 0 && user.Id == room.CreatorUserId),
                SeatOrder = count // 0..N-1
            });

            await _db.SaveChangesAsync();
            return room;
        }

        public Task<List<RoomPlayer>> GetPlayersAsync(Guid roomId) =>
            _db.RoomPlayers.Where(p => p.RoomId == roomId)
                           .OrderBy(p => p.SeatOrder)
                           .ToListAsync();

        public async Task<bool> CanStartAsync(Guid roomId)
        {
            var room = await _db.Rooms.Include(r => r.Players)
                                      .FirstOrDefaultAsync(r => r.Id == roomId)
                       ?? throw new KeyNotFoundException("Sala no existe");

            var c = room.Players.Count;
            return c >= room.MinPlayers && c <= room.MaxPlayers && !room.Started;
        }

        public async Task<Room> MarkStartedAsync(Guid roomId)
        {
            var room = await _db.Rooms.FirstAsync(r => r.Id == roomId);
            room.Started = true;
            await _db.SaveChangesAsync();
            return room;
        }
    }
}
