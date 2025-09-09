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
        // Crea sala: el body es un GUID en JSON (entre comillas)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Guid creatorUserId)
        {
            var room = await _rooms.CreateRoomAsync(creatorUserId);
            return Ok(new { room.Id, room.Code, room.MinPlayers, room.MaxPlayers });
        }

        public record JoinDto(string RoomCode, Guid UserId);

        // Unirse a sala: requiere userId de un usuario ya registrado (logeado)
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

        [HttpGet("{roomCode}/players")]
        public async Task<IActionResult> Players([FromRoute] string roomCode)
        {
            var room = await _rooms.GetByCodeAsync(roomCode);
            if (room is null) return NotFound("Sala no existe");
            var players = await _rooms.GetPlayersAsync(room.Id);
            return Ok(players.Select(p => new { p.UserId, p.Username, p.SeatOrder, p.IsOwner }));
        }

        [HttpPost("{roomCode}/start")]
        public async Task<IActionResult> Start([FromRoute] string roomCode)
        {
            var room = await _rooms.GetByCodeAsync(roomCode);
            if (room is null) return NotFound("Sala no existe");
            if (!await _rooms.CanStartAsync(room.Id)) return BadRequest("No se puede iniciar aún.");
            await _rooms.MarkStartedAsync(room.Id);
            return Ok(new { started = true });
        }

        [HttpGet("{roomCode}/messages")]
        public async Task<IActionResult> GetMessages([FromRoute] string roomCode, [FromQuery] int take = 50)
        {
            var room = await _rooms.GetByCodeAsync(roomCode);
            if (room is null) return NotFound("Sala no existe");

            var msgs = await _chat.GetLastAsync(room.Id, Math.Clamp(take, 1, 200));
            return Ok(msgs.Select(m => new { m.UserId, m.Username, m.Text, sentAt = m.SentAtUtc }));
        }
    }
}
