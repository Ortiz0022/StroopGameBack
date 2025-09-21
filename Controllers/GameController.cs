using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StroobGame.AppDataBase;
using StroobGame.Entities;
using StroobGame.Hubs;
using StroobGame.Services;
using System.Linq;

namespace StroobGame.Controllers
{
    [ApiController]
    [Route("api/game")]
    public class GameController : ControllerBase
    {
        private readonly IRoomService _rooms;
        private readonly IGameService _game;
        private readonly AppDbContext _db;
        private readonly IHubContext<GameHub> _hub;

        public GameController(IRoomService rooms, IGameService game, AppDbContext db, IHubContext<GameHub> hub)
        {
            _rooms = rooms;
            _game = game;
            _db = db;
            _hub = hub;
        }

        private async Task BroadcastScoreboardAsync(string roomCode)
        {
            var room = await _rooms.GetByCodeAsync(roomCode) ?? throw new InvalidOperationException("Sala no existe");
            var gs = await _db.GameSessions.FirstAsync(g => g.RoomId == room.Id);

            var board = await _game.GetScoreboardAsync(room.Id);

            var totals = await _db.Answers
                .Where(a => a.GameSessionId == gs.Id)
                .GroupBy(a => a.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Correct = g.Count(x => x.IsCorrect),
                    Wrong = g.Count(x => !x.IsCorrect)
                })
                .ToListAsync();

            var payload = board.Select(r =>
            {
                var t = totals.FirstOrDefault(x => x.UserId == r.UserId);
                var correct = t?.Correct ?? 0;
                var wrong = t?.Wrong ?? 0;

                return new
                {
                    r.UserId,
                    r.Username,
                    r.Score,
                    AvgResponseMs = Math.Round(r.AvgMs),
                    TotalCorrect = correct,
                    TotalWrong = wrong
                };
            });

            await _hub.Clients.Group(roomCode).SendAsync("Scoreboard", payload);
        }

        [HttpGet("ranking/top")]
        public async Task<IActionResult> GetTop([FromQuery] int take = 10)
        {
            take = Math.Clamp(take, 1, 100);

            var rows = await _db.UserStats
                .Join(_db.Users, s => s.UserId, u => u.Id, (s, u) => new
                {
                    u.Id,
                    u.Username,
                    s.Wins,
                    s.GamesPlayed,
                    s.BestScore,
                    s.TotalResponseMs,
                    s.TotalResponses
                })
                .OrderByDescending(x => x.Wins)
                .ThenByDescending(x => x.BestScore)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();

            var result = rows.Select(x => new
            {
                x.Id,
                x.Username,
                x.Wins,
                x.GamesPlayed,
                x.BestScore,
                AvgMs = x.TotalResponses > 0 ? (double)x.TotalResponseMs / x.TotalResponses : 0.0
            });

            return Ok(result);
        }


        [HttpGet("{roomCode}/scoreboard")]
        public async Task<IActionResult> GetScoreboard([FromRoute] string roomCode)
        {
            var room = await _rooms.GetByCodeAsync(roomCode);
            if (room is null) return NotFound("Sala no existe");

            var gs = await _db.GameSessions.FirstAsync(g => g.RoomId == room.Id);
            var board = await _game.GetScoreboardAsync(room.Id);

            var totals = await _db.Answers
                .Where(a => a.GameSessionId == gs.Id)
                .GroupBy(a => a.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Correct = g.Count(x => x.IsCorrect),
                    Wrong = g.Count(x => !x.IsCorrect)
                })
                .ToListAsync();

            var response = board.Select(r =>
            {
                var t = totals.FirstOrDefault(x => x.UserId == r.UserId);
                var correct = t?.Correct ?? 0;
                var wrong = t?.Wrong ?? 0;
                return new
                {
                    r.UserId,
                    r.Username,
                    r.Score,
                    AvgResponseMs = Math.Round(r.AvgMs),
                    TotalCorrect = correct,
                    TotalWrong = wrong
                };
            });

            return Ok(response);
        }


        private async Task BroadcastTurnChangedAsync(string roomCode)
        {
            var room = await _rooms.GetByCodeAsync(roomCode) ?? throw new InvalidOperationException("Sala no existe");
            var (uid, uname) = await _game.GetCurrentPlayerAsync(room.Id);
            await _hub.Clients.Group(roomCode).SendAsync("TurnChanged", new { UserId = uid, Username = uname });
        }

        private async Task BroadcastNewRoundAsync(string roomCode, Round round, int remainingForThisPlayer)
        {
            await _hub.Clients.Group(roomCode).SendAsync("NewRound", new
            {
                RoundId = round.Id,
                round.Word,
                round.InkHex,
                Options = round.Options.OrderBy(o => o.Order).Select(o => new
                {
                    o.Id,
                    o.IsCorrect,
                    o.Order,
                    o.ColorId
                }),
                RemainingForThisPlayer = Math.Max(0, remainingForThisPlayer)
            });
        }

        // 🚀 Inicia partida por turnos
        // POST /api/game/{roomCode}/start?roundsPerPlayer=4&userId=GUID
        [HttpPost("{roomCode}/start")]
        public async Task<IActionResult> Start([FromRoute] string roomCode, [FromQuery] int roundsPerPlayer = 4, [FromQuery] Guid userId = default)
        {
            var room = await _rooms.GetByCodeAsync(roomCode);
            if (room is null) return NotFound("Sala no existe");
            if (userId == Guid.Empty) return BadRequest("userId requerido");

            if (room.CreatorUserId != userId)
                return Forbid("Solo el creador de la sala puede iniciar el juego.");

            var players = await _rooms.GetPlayersAsync(room.Id);
            if (players.Count < room.MinPlayers || players.Count > room.MaxPlayers)
                return BadRequest($"El juego requiere entre {room.MinPlayers} y {room.MaxPlayers} jugadores.");

            await _rooms.MarkStartedAsync(room.Id);

            var gs = await _game.StartAsync(room.Id, roundsPerPlayer);

            await _hub.Clients.Group(roomCode).SendAsync("GameStarted", new
            {
                GameId = gs.Id,
                RoomId = room.Id,
                RoundsPerPlayer = gs.RoundsPerPlayer
            });

            await BroadcastScoreboardAsync(roomCode);
            await BroadcastTurnChangedAsync(roomCode);

            var created = await _game.CreateOrNextRoundForCurrentAsync(room.Id);
            await BroadcastNewRoundAsync(roomCode, created.round, created.remainingForThisPlayer);

            return Ok(new { gs.Id, gs.RoundsPerPlayer, Started = true });
        }

        // 👀 Round actual (por turnos)
        // GET /api/game/{roomCode}/round
        [HttpGet("{roomCode}/round")]
        public async Task<IActionResult> GetCurrentRound([FromRoute] string roomCode)
        {
            var room = await _rooms.GetByCodeAsync(roomCode);
            if (room is null) return NotFound("Sala no existe");

            var gs = await _db.GameSessions.FirstOrDefaultAsync(g => g.RoomId == room.Id && g.State == "playing");
            if (gs is null) return NotFound("No hay juego activo.");

            if (gs.CurrentRoundId is null)
                return Ok(new { HasRound = false, RemainingForThisPlayer = gs.RoundsPerPlayer - gs.CurrentPlayerRoundsPlayed });

            var round = await _db.Rounds.Include(r => r.Options).FirstAsync(r => r.Id == gs.CurrentRoundId.Value);
            var remaining = Math.Max(0, gs.RoundsPerPlayer - gs.CurrentPlayerRoundsPlayed);

            return Ok(new
            {
                HasRound = true,
                RoundId = round.Id,
                round.Word,
                round.InkHex,
                Options = round.Options.OrderBy(o => o.Order).Select(o => new
                {
                    o.Id,
                    o.IsCorrect,
                    o.Order,
                    o.ColorId
                }),
                RemainingForThisPlayer = remaining
            });
        }

        public record SubmitDto(Guid UserId, int RoundId, int OptionId, double ResponseTimeSec);

        // 🖱️ Responder round por turnos
        // POST /api/game/{roomCode}/answer
        [HttpPost("{roomCode}/answer")]
        public async Task<IActionResult> Submit([FromRoute] string roomCode, [FromBody] SubmitDto dto)
        {
            var room = await _rooms.GetByCodeAsync(roomCode);
            if (room is null) return NotFound("Sala no existe");

            var (gp, delta, finishedTurn, finishedGame) =
                await _game.SubmitAnswerTurnAsync(room.Id, dto.UserId, dto.RoundId, dto.OptionId, dto.ResponseTimeSec);

            await _hub.Clients.Group(roomCode).SendAsync("ScoreUpdated", new
            {
                UserId = dto.UserId,
                Score = gp.Score,
                Delta = delta,
                IsCorrect = delta > 0
            });

            await BroadcastScoreboardAsync(roomCode);

            if (finishedGame)
            {
                var winner = await _game.GetWinnerAsync(room.Id);
                if (winner is not null)
                {
                    await _hub.Clients.Group(roomCode).SendAsync("Winner", new
                    {
                        winner.Value.UserId,
                        winner.Value.Username,
                        winner.Value.Score,
                        AvgResponseMs = Math.Round(winner.Value.AvgMs)
                    });
                }

                var board = await _game.GetScoreboardAsync(room.Id);
                await _hub.Clients.Group(roomCode).SendAsync("GameFinished",
                    board.Select(r => new
                    {
                        r.UserId,
                        r.Username,
                        r.Score,
                        AvgResponseMs = Math.Round(r.AvgMs)
                    }));

                return Ok(new { Finished = true });
            }

            if (finishedTurn)
            {
                await BroadcastTurnChangedAsync(roomCode);
            }

            var created = await _game.CreateOrNextRoundForCurrentAsync(room.Id);
            await BroadcastNewRoundAsync(roomCode, created.round, created.remainingForThisPlayer);

            return Ok(new { Finished = false, NextRoundId = created.round.Id });
        }
    }
}
