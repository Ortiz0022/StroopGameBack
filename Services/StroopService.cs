using Microsoft.EntityFrameworkCore;
using StroobGame.AppDataBase;
using StroobGame.Entities;

namespace StroobGame.Services
{
    public class StroopService : IGameService
    {
        private readonly AppDbContext _db;
        private readonly Random _rng = new();

        // Catálogo local de colores (mapea con el front)
        private static readonly (int Id, string Name, string Hex)[] COLORS = new[]
        {
            (1,"Blanco","#FFFFFF"),
            (2,"Negro" ,"#000000"),
            (3,"Rojo"  ,"#FF0000"),
            (4,"Verde" ,"#00FF00"),
            (5,"Azul"  ,"#0000FF"),
        };

        public StroopService(AppDbContext db) => _db = db;

        public async Task<GameSession> StartAsync(Guid roomId, int roundsPerPlayer)
        {
            // ANTES: devolvías la sesión playing existente → causa juegos “pegados”.
            // Ahora: si hay una playing, la damos por finalizada y limpiamos residuos mínimos.
            var existing = await _db.GameSessions.FirstOrDefaultAsync(g => g.RoomId == roomId && g.State == "playing");
            if (existing != null)
            {
                existing.State = "finished";
                await _db.SaveChangesAsync();
            }

            var players = await _db.RoomPlayers
                .Where(p => p.RoomId == roomId)
                .OrderBy(p => p.SeatOrder)
                .ToListAsync();

            if (players.Count == 0)
                throw new InvalidOperationException("La sala no tiene jugadores.");

            var gs = new GameSession
            {
                RoomId = roomId,
                State = "playing",
                RoundsPerPlayer = Math.Clamp(roundsPerPlayer, 1, 10),
                CurrentSeat = 0,
                CurrentPlayerUserId = players[0].UserId,
                CurrentPlayerRoundsPlayed = 0,
                CurrentRoundId = null
            };
            _db.GameSessions.Add(gs);

            foreach (var p in players)
            {
                _db.GamePlayers.Add(new GamePlayer
                {
                    GameSessionId = gs.Id,
                    UserId = p.UserId,
                    Username = p.Username,
                    Score = 0
                });

                var stats = await _db.UserStats.FirstOrDefaultAsync(s => s.UserId == p.UserId);
                if (stats == null)
                    _db.UserStats.Add(new UserStats { UserId = p.UserId });
            }

            // Marcar usuarios como jugando
            var userIds = players.Select(p => p.UserId).ToList();
            var users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToListAsync();
            foreach (var usr in users) usr.IsPlaying = true;

            // NUEVO: marca sala como iniciada y guarda Id de sesión activa.
            var room = await _db.Rooms.FirstAsync(r => r.Id == roomId);
            room.Started = true;
            room.ActiveGameSessionId = gs.Id;

            await _db.SaveChangesAsync();
            return gs;
        }

        public async Task<(Round round, int remainingForThisPlayer)> CreateOrNextRoundForCurrentAsync(Guid roomId)
        {
            var gs = await _db.GameSessions.FirstAsync(g => g.RoomId == roomId && g.State == "playing");

            if (gs.CurrentPlayerRoundsPlayed >= gs.RoundsPerPlayer)
                throw new InvalidOperationException("El turno actual ya terminó, debe cambiar de jugador.");

            // palabra = color random (TEXTO), tinta = otro color random (puede coincidir)
            // REGLA: el jugador debe elegir el color que DICE la palabra (no la tinta).
            var wordColor = COLORS[_rng.Next(COLORS.Length)];  // ← correcta (semántico)
            var inkColor = COLORS[_rng.Next(COLORS.Length)];  // solo para pintar el texto

            // Construimos exactamente 2 opciones:
            //  - Correcta: color de la palabra (wordColor)
            //  - Distractor: preferimos la tinta; si coincide con la correcta, elegimos otro distinto al azar
            var correct = wordColor;

            (int Id, string Name, string Hex) distractor;
            if (inkColor.Id != correct.Id)
            {
                distractor = inkColor; // usar tinta como distractor
            }
            else
            {
                distractor = COLORS.Where(c => c.Id != correct.Id)
                                   .OrderBy(_ => _rng.Next())
                                   .First();
            }

            // Creamos el round
            var round = new Round
            {
                GameSessionId = gs.Id,
                Word = wordColor.Name,  // lo que DICE la palabra
                InkHex = inkColor.Hex,  // con qué color se pinta
            };
            _db.Rounds.Add(round);
            await _db.SaveChangesAsync(); // obtiene Id

            // Insertamos EXACTAMENTE 2 opciones, barajadas
            var pair = new List<(int Id, string Name, string Hex, bool IsCorrect)>
            {
                (correct.Id,    correct.Name,    correct.Hex,    true),
                (distractor.Id, distractor.Name, distractor.Hex, false)
            }.OrderBy(_ => _rng.Next()).ToList();

            for (int i = 0; i < pair.Count; i++)
            {
                var c = pair[i];
                _db.RoundOptions.Add(new RoundOption
                {
                    RoundId = round.Id,
                    IsCorrect = c.IsCorrect,   // ← ahora es correcta la del color de la palabra
                    Order = i + 1,
                    ColorId = c.Id
                });
            }

            gs.CurrentRoundId = round.Id;
            await _db.SaveChangesAsync();
            await _db.Entry(round).Collection(r => r.Options).LoadAsync();

            var remaining = Math.Max(0, gs.RoundsPerPlayer - gs.CurrentPlayerRoundsPlayed - 1);
            return (round, remaining);
        }

        public async Task<(GamePlayer updated, int delta, bool finishedTurn, bool finishedGame)>
        SubmitAnswerTurnAsync(Guid roomId, Guid userId, int roundId, int optionId, double responseTimeSec)
        {
            var gs = await _db.GameSessions.FirstAsync(g => g.RoomId == roomId && g.State == "playing");

            if (userId != gs.CurrentPlayerUserId)
                throw new InvalidOperationException("No es tu turno.");

            var opt = await _db.RoundOptions.FirstAsync(o => o.RoundId == roundId && o.Id == optionId);
            var isCorrect = opt.IsCorrect;

            _db.Answers.Add(new Answer
            {
                GameSessionId = gs.Id,
                RoundId = roundId,
                UserId = userId,
                IsCorrect = isCorrect,
                ResponseTimeSec = responseTimeSec,
                OptionId = optionId
            });

            var gp = await _db.GamePlayers.FirstAsync(x => x.GameSessionId == gs.Id && x.UserId == userId);
            var delta = isCorrect ? 1 : 0;
            gp.Score += delta;
            gp.TotalResponses += 1;
            gp.TotalResponseMs += (long)(responseTimeSec * 1000.0);

            var stats = await _db.UserStats.FirstAsync(s => s.UserId == userId);
            stats.TotalScore += delta;
            stats.TotalResponses += 1;
            stats.TotalResponseMs += (long)(responseTimeSec * 1000.0);

            gs.CurrentPlayerRoundsPlayed += 1;

            bool finishedTurn = false;
            bool finishedGame = false;

            if (gs.CurrentPlayerRoundsPlayed >= gs.RoundsPerPlayer)
            {
                finishedTurn = true;

                var players = await _db.RoomPlayers
                    .Where(p => p.RoomId == roomId)
                    .OrderBy(p => p.SeatOrder)
                    .ToListAsync();

                gs.CurrentSeat += 1;

                if (gs.CurrentSeat < players.Count)
                {
                    gs.CurrentPlayerUserId = players[gs.CurrentSeat].UserId;
                    gs.CurrentPlayerRoundsPlayed = 0;
                    gs.CurrentRoundId = null;
                }
                else
                {
                    // FIN DEL JUEGO
                    finishedGame = true;
                    gs.State = "finished";

                    var allPlayers = await _db.GamePlayers
                        .Where(x => x.GameSessionId == gs.Id)
                        .ToListAsync();

                    foreach (var p in allPlayers)
                    {
                        var s = await _db.UserStats.FirstAsync(u => u.UserId == p.UserId);
                        s.GamesPlayed += 1;
                        if (p.Score > s.BestScore) s.BestScore = p.Score;
                    }

                    var winnerRow = allPlayers
                        .OrderByDescending(p => p.Score)
                        .ThenBy(p => p.TotalResponseMs)
                        .ThenBy(p => p.UserId)
                        .First();

                    var winStats = await _db.UserStats.FirstAsync(u => u.UserId == winnerRow.UserId);
                    winStats.Wins += 1;

                    // Marcar usuarios como NO jugando
                    var uids = players.Select(p => p.UserId).ToList();
                    var usrs = await _db.Users.Where(u => uids.Contains(u.Id)).ToListAsync();
                    foreach (var usr in usrs) usr.IsPlaying = false;

                    // NUEVO: deja la sala lista para otra partida
                    var room = await _db.Rooms.FirstAsync(r => r.Id == roomId);
                    room.Started = false;
                    room.ActiveGameSessionId = null;
                }
            }

            await _db.SaveChangesAsync();
            return (gp, delta, finishedTurn, finishedGame);
        }

        public async Task<(Guid UserId, string Username)> GetCurrentPlayerAsync(Guid roomId)
        {
            var gs = await _db.GameSessions.FirstAsync(g => g.RoomId == roomId);
            var u = await _db.GamePlayers
                .Where(gp => gp.GameSessionId == gs.Id && gp.UserId == gs.CurrentPlayerUserId)
                .Select(gp => new { gp.UserId, gp.Username })
                .FirstAsync();
            return (u.UserId, u.Username);
        }

        public async Task<List<(Guid UserId, string Username, int Score, double AvgMs)>> GetScoreboardAsync(Guid roomId)
        {
            var gs = await _db.GameSessions.FirstAsync(g => g.RoomId == roomId);
            var rows = await _db.GamePlayers
                .Where(gp => gp.GameSessionId == gs.Id)
                .Select(gp => new
                {
                    gp.UserId,
                    gp.Username,
                    gp.Score,
                    AvgMs = gp.TotalResponses > 0 ? (double)gp.TotalResponseMs / gp.TotalResponses : 0.0
                })
                .ToListAsync();

            return rows
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.AvgMs)
                .Select(r => (r.UserId, r.Username, r.Score, r.AvgMs))
                .ToList();
        }

        public async Task<(Guid UserId, string Username, int Score, double AvgMs)?> GetWinnerAsync(Guid roomId)
        {
            var board = await GetScoreboardAsync(roomId);
            return board.FirstOrDefault();
        }

        private int ScoreDelta(bool isCorrect, double responseTimeSec)
        {
            return isCorrect ? 1 : 0;   // solo suma por acierto
        }
    }
}
