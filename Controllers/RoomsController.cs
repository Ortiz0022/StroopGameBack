using Microsoft.AspNetCore.Mvc;
using StroobGame.Services;

namespace StroobGame.Controllers
{
    [ApiController]
    [Route("api/rooms")]
    public class RoomsController : ControllerBase
    {
        private readonly IRoomService _rooms;
        private readonly IChatService _chat;

        public RoomsController(IRoomService rooms, IChatService chat)
        {
            _rooms = rooms;
            _chat = chat;
        }

        // ========== CREATE ==========
        // Body: GUID en JSON (entre comillas)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Guid creatorUserId)
        {
            var room = await _rooms.CreateRoomAsync(creatorUserId);
            return Ok(new { room.Id, room.Code, room.MinPlayers, room.MaxPlayers });
        }

        // ========== JOIN ==========
        public record JoinDto(string RoomCode, Guid UserId);

        [HttpPost("join")]
        public async Task<IActionResult> Join([FromBody] JoinDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RoomCode)) return BadRequest("roomCode requerido.");
            if (dto.UserId == Guid.Empty) return BadRequest("userId requerido.");

            try
            {
                var room = await _rooms.JoinRoomAsync(dto.RoomCode, dto.UserId);
                var players = await _rooms.GetPlayersAsync(room.Id);
                return Ok(new
                {
                    room.Code,
                    Players = players.Select(p => new { p.UserId, p.Username, p.SeatOrder, p.IsOwner })
                });
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        // ========== PLAYERS SNAPSHOT ==========
        [HttpGet("{roomCode}/players")]
        public async Task<IActionResult> Players([FromRoute] string roomCode)
        {
            var room = await _rooms.GetByCodeAsync(roomCode);
            if (room is null) return NotFound("Sala no existe");
            var players = await _rooms.GetPlayersAsync(room.Id);
            return Ok(players.Select(p => new { p.UserId, p.Username, p.SeatOrder, p.IsOwner }));
        }

        // ========== MARK STARTED (opcional si lo usas) ==========
        [HttpPost("{roomCode}/start")]
        public async Task<IActionResult> Start([FromRoute] string roomCode)
        {
            var room = await _rooms.GetByCodeAsync(roomCode);
            if (room is null) return NotFound("Sala no existe");
            if (!await _rooms.CanStartAsync(room.Id)) return BadRequest("No se puede iniciar aún.");
            await _rooms.MarkStartedAsync(room.Id);
            return Ok(new { started = true });
        }

        // ========== CHAT HISTORY ==========
        [HttpGet("{roomCode}/messages")]
        public async Task<IActionResult> GetMessages([FromRoute] string roomCode, [FromQuery] int take = 50)
        {
            var room = await _rooms.GetByCodeAsync(roomCode);
            if (room is null) return NotFound("Sala no existe");

            var msgs = await _chat.GetLastAsync(room.Id, Math.Clamp(take, 1, 200));
            return Ok(msgs.Select(m => new { m.UserId, m.Username, m.Text, sentAt = m.SentAtUtc }));
        }

        // ========== NEW: VOLVER A LA SALA / RESET ==========
        [HttpPost("{roomCode}/return")]
        public async Task<IActionResult> ReturnToLobby([FromRoute] string roomCode)
        {
            var room = await _rooms.GetByCodeAsync(roomCode);
            if (room is null) return NotFound("Sala no existe");

            await _rooms.ResetRoomAsync(room.Id);
            return Ok(new { ok = true });
        }
    }
}
