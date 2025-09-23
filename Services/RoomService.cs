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
            var creator = await _db.Users.FindAsync(creatorUserId)
                ?? throw new KeyNotFoundException("Usuario creador no existe");

            var code = NewCode();
            while (await _db.Rooms.AnyAsync(r => r.Code == code))
                code = NewCode();

            var room = new Room
            {
                Code = code,
                CreatorUserId = creatorUserId,
                MinPlayers = 2,
                MaxPlayers = 4,
                Started = false
            };

            _db.Rooms.Add(room);
            await _db.SaveChangesAsync();

            _db.RoomPlayers.Add(new RoomPlayer
            {
                RoomId = room.Id,
                UserId = creator.Id,
                Username = creator.Username,
                IsOwner = true,
                SeatOrder = 0
            });
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
            // FIX: debe ser >= para bloquear al 5to (si Max=4)
            if (count >= room.MaxPlayers)
                throw new InvalidOperationException("Sala llena");

            var already = await _db.RoomPlayers
                .FirstOrDefaultAsync(p => p.RoomId == room.Id && p.UserId == user.Id);

            if (already != null) return room; // idempotente

            _db.RoomPlayers.Add(new RoomPlayer
            {
                RoomId = room.Id,
                UserId = user.Id,
                Username = user.Username,
                IsOwner = (user.Id == room.CreatorUserId),
                SeatOrder = count
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

        public Task<bool> IsOwnerAsync(Guid roomId, Guid userId) =>
            _db.Rooms.AnyAsync(r => r.Id == roomId && r.CreatorUserId == userId);

        // ⬇⬇⬇ NUEVO: reset fuerte de sala y sesión
        public async Task ResetRoomAsync(Guid roomId)
        {
            // ⚠️ InMemory no soporta transacciones. Hacemos reset “best-effort” sin BeginTransaction.
            var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId);
            if (room == null) return;

            // Todas las sesiones de esa sala (puede haber históricas)
            var sessions = await _db.GameSessions
                .Where(s => s.RoomId == roomId)
                .ToListAsync();

            if (sessions.Count > 0)
            {
                var sids = sessions.Select(s => s.Id).ToList();

                // 1) Answers
                var answers = _db.Answers.Where(a => sids.Contains(a.GameSessionId));
                _db.Answers.RemoveRange(answers);

                // 2) RoundOptions y Rounds
                var rounds = await _db.Rounds.Where(r => sids.Contains(r.GameSessionId)).ToListAsync();
                var rIds = rounds.Select(r => r.Id).ToList();

                var ropts = _db.RoundOptions.Where(o => rIds.Contains(o.RoundId));
                _db.RoundOptions.RemoveRange(ropts);
                _db.Rounds.RemoveRange(rounds);

                // 3) GamePlayers
                var gplayers = _db.GamePlayers.Where(gp => sids.Contains(gp.GameSessionId));
                _db.GamePlayers.RemoveRange(gplayers);

                // 4) GameSessions (márcalas y bórralas)
                foreach (var s in sessions) s.State = "finished";
                _db.GameSessions.RemoveRange(sessions);
            }

            // 5) Limpia estado de la sala
            room.Started = false;
            room.ActiveGameSessionId = null;

            await _db.SaveChangesAsync();
        }
    }
}
