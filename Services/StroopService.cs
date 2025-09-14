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
            var existing = await _db.GameSessions.FirstOrDefaultAsync(g => g.RoomId == roomId && g.State == "playing");
            if (existing != null) return existing;

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
                RoundsPerPlayer = Math.Clamp(roundsPerPlayer, 5, 100),
                CurrentSeat = 0,
                CurrentPlayerUserId = players[0].UserId,
                CurrentPlayerRoundsPlayed = 0
            };
            _db.GameSessions.Add(gs);

            // Inicializar GamePlayers con los jugadores de la sala
            foreach (var p in players)
            {
                _db.GamePlayers.Add(new GamePlayer
                {
                    GameSessionId = gs.Id,
                    UserId = p.UserId,
                    Username = p.Username,
                    Score = 0
                });

                // Asegura stats histórico
                var stats = await _db.UserStats.FirstOrDefaultAsync(s => s.UserId == p.UserId);
                if (stats == null)
                    _db.UserStats.Add(new UserStats { UserId = p.UserId });
            }

            await _db.SaveChangesAsync();
            return gs;
        }

        public async Task<(Round round, int remainingForThisPlayer)> CreateOrNextRoundForCurrentAsync(Guid roomId)
        {
            var gs = await _db.GameSessions.FirstAsync(g => g.RoomId == roomId && g.State == "playing");

            if (gs.CurrentPlayerRoundsPlayed >= gs.RoundsPerPlayer)
                throw new InvalidOperationException("El turno actual ya terminó, debe cambiar de jugador.");

            // palabra = color random (texto), tinta = otro color random (puede coincidir)
            var wordColor = COLORS[_rng.Next(COLORS.Length)];
            var inkColor = COLORS[_rng.Next(COLORS.Length)]; // regla: responde la TINTA

            // opciones: 1 correcta + 2 distractores
            var correct = inkColor;
            var distractors = COLORS.Where(c => c.Id != correct.Id).OrderBy(_ => _rng.Next()).Take(2).ToList();
            var pool = new List<(int Id, string Name, string Hex)> { correct };
            pool.AddRange(distractors);

            var round = new Round
            {
                GameSessionId = gs.Id,
                Word = wordColor.Name,
                InkHex = inkColor.Hex,
            };
            _db.Rounds.Add(round);
            await _db.SaveChangesAsync(); // obtiene Id

            int ord = 1;
            foreach (var c in pool.OrderBy(_ => _rng.Next()))
            {
                _db.RoundOptions.Add(new RoundOption
                {
                    RoundId = round.Id,
                    IsCorrect = (c.Id == correct.Id),
                    Order = ord++,
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

            // Solo el jugador al que le toca puede responder
            if (userId != gs.CurrentPlayerUserId)
                throw new InvalidOperationException("No es tu turno.");

            // Validar opción elegida
            var opt = await _db.RoundOptions.FirstAsync(o => o.RoundId == roundId && o.Id == optionId);
            var isCorrect = opt.IsCorrect;

            // Registrar answer
            var ans = new Answer
            {
                GameSessionId = gs.Id,
                RoundId = roundId,
                UserId = userId,
                IsCorrect = isCorrect,
                ResponseTimeSec = responseTimeSec,
                OptionId = optionId
            };
            _db.Answers.Add(ans);

            // Actualizar score/tiempo por juego
            var gp = await _db.GamePlayers.FirstAsync(x => x.GameSessionId == gs.Id && x.UserId == userId);
            var delta = ScoreDelta(isCorrect, responseTimeSec);
            gp.Score += delta;
            gp.TotalResponses += 1;
            gp.TotalResponseMs += (long)(responseTimeSec * 1000.0);

            // Estadísticas históricas
            var stats = await _db.UserStats.FirstAsync(s => s.UserId == userId);
            stats.TotalScore += delta;
            stats.TotalResponses += 1;
            stats.TotalResponseMs += (long)(responseTimeSec * 1000.0);

            // Avance del turno
            gs.CurrentPlayerRoundsPlayed += 1;

            bool finishedTurn = false;
            bool finishedGame = false;

            if (gs.CurrentPlayerRoundsPlayed >= gs.RoundsPerPlayer)
            {
                // terminó el turno de este jugador
                finishedTurn = true;

                // ¿hay otro jugador?
                var players = await _db.RoomPlayers.Where(p => p.RoomId == roomId).OrderBy(p => p.SeatOrder).ToListAsync();
                gs.CurrentSeat += 1;

                if (gs.CurrentSeat < players.Count)
                {
                    // pasa al siguiente
                    gs.CurrentPlayerUserId = players[gs.CurrentSeat].UserId;
                    gs.CurrentPlayerRoundsPlayed = 0;
                    gs.CurrentRoundId = null; // se generará su primer round luego
                }
                else
                {
                    // no hay más jugadores
                    finishedGame = true;
                    gs.State = "finished";

                    // cerrar GamesPlayed & BestScore
                    var allPlayers = await _db.GamePlayers.Where(x => x.GameSessionId == gs.Id).ToListAsync();
                    foreach (var p in allPlayers)
                    {
                        var s = await _db.UserStats.FirstAsync(u => u.UserId == p.UserId);
                        s.GamesPlayed += 1;
                        if (p.Score > s.BestScore) s.BestScore = p.Score;
                    }
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
            if (!isCorrect) return -1;
            if (responseTimeSec <= 1.0) return +3;
            if (responseTimeSec <= 2.0) return +2;
            return +1;
        }
    }
}
