using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StroobGame.AppDataBase;
using StroobGame.Services;
using System.Xml.Linq;

namespace StroobGame.Hubs
{
    public class GameHub : Hub
    {
        private readonly IRoomService _rooms;
        private readonly IChatService _chat;
        private readonly IGameService _game;
        private readonly ConnectionRegistry _connections;
        private readonly AppDbContext _db;

        public GameHub(IRoomService rooms, IChatService chat, IGameService game, ConnectionRegistry connections, AppDbContext db)
        {
            _rooms = rooms;
            _chat = chat;
            _game = game;
            _connections = connections;
            _db = db;
        }

        /// <summary>
        /// Unirse a una sala (grupo) y recibir snapshot + histórico de chat.
        /// </summary>
        public async Task JoinRoom(string roomCode, Guid userId, string username)
        {
            var room = await _rooms.GetByCodeAsync(roomCode) ?? throw new HubException("Sala no existe");
            var connId = Context.ConnectionId!;

            // Garantiza que el usuario esté en la sala (idempotente si ya estaba)
            await _rooms.JoinRoomAsync(roomCode, userId);

            await Groups.AddToGroupAsync(connId, roomCode);
            _connections.Set(connId, room.Id, roomCode, userId, username);

            // Notificar a todos en la sala
            await Clients.Group(roomCode).SendAsync("UserJoined", new { userId, username });

            // Snapshot de jugadores para todos
            var players = await _rooms.GetPlayersAsync(room.Id);
            await Clients.Group(roomCode).SendAsync("RoomUpdated", new
            {
                room.Code,
                room.Started,
                Players = players.Select(p => new { p.UserId, p.Username, p.SeatOrder, p.IsOwner })
            });

            // Histórico de chat solo para el que entra
            var last = await _chat.GetLastAsync(room.Id, 50);
            await Clients.Caller.SendAsync("ChatHistory", last.Select(m => new
            {
                m.UserId,
                m.Username,
                m.Text,
                sentAt = m.SentAtUtc
            }));
        }

        /// <summary>
        /// Salir explícitamente de la sala.
        /// </summary>
        public async Task LeaveRoom(string roomCode)
        {
            var connId = Context.ConnectionId!;
            if (_connections.TryGet(connId, out var info) && info.RoomCode == roomCode)
            {
                await Groups.RemoveFromGroupAsync(connId, roomCode);
                _connections.Remove(connId);

                await Clients.Group(roomCode).SendAsync("UserLeft", new { userId = info.UserId, username = info.Username });

                var players = await _rooms.GetPlayersAsync(info.RoomId);
                await Clients.Group(roomCode).SendAsync("RoomUpdated", new
                {
                    roomCode,
                    Players = players.Select(p => new { p.UserId, p.Username, p.SeatOrder, p.IsOwner })
                });
            }
        }

        /// <summary>
        /// Enviar mensaje de chat dentro de la sala.
        /// </summary>
        public async Task SendChat(string roomCode, Guid userId, string text)
        {
            var room = await _rooms.GetByCodeAsync(roomCode) ?? throw new HubException("Sala no existe");

            // Validar que el usuario pertenezca a la sala
            var players = await _rooms.GetPlayersAsync(room.Id);
            var player = players.FirstOrDefault(p => p.UserId == userId);
            if (player is null) throw new HubException("El usuario no pertenece a la sala.");

            // Guardar en histórico (InMemory)
            var msg = await _chat.AddAsync(room.Id, userId, player.Username, text);

            // Emitir a todos en la sala
            await Clients.Group(roomCode).SendAsync("ChatMessage", new
            {
                msg.UserId,
                msg.Username,
                msg.Text,
                sentAt = msg.SentAtUtc
            });
        }

        /// <summary>
        /// Indicador de escritura ("typing...") para otros en la sala.
        /// </summary>
        public Task Typing(string roomCode, Guid userId, bool isTyping)
            => Clients.OthersInGroup(roomCode).SendAsync("UserTyping", new { userId, isTyping });

        /// <summary>
        /// Limpieza automática cuando se cierra el tab o se cae la conexión.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connId = Context.ConnectionId!;
            if (_connections.TryGet(connId, out var info))
            {
                await Groups.RemoveFromGroupAsync(connId, info.RoomCode);
                _connections.Remove(connId);

                await Clients.Group(info.RoomCode).SendAsync("UserLeft", new { userId = info.UserId, username = info.Username });

                var players = await _rooms.GetPlayersAsync(info.RoomId);
                await Clients.Group(info.RoomCode).SendAsync("RoomUpdated", new
                {
                    info.RoomCode,
                    Players = players.Select(p => new { p.UserId, p.Username, p.SeatOrder, p.IsOwner })
                });
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task BroadcastTurnChanged(string roomCode)
        {
            var room = await _rooms.GetByCodeAsync(roomCode) ?? throw new HubException("Sala no existe");
            var (uid, uname) = await _game.GetCurrentPlayerAsync(room.Id);

            // TurnChanged: el front deshabilita inputs si no es su userId
            await Clients.Group(roomCode).SendAsync("TurnChanged", new
            {
                UserId = uid,
                Username = uname
            });
        }

        // 🚀 StartGame por turnos
        public async Task StartGame(string roomCode, int roundsPerPlayer)
        {
            var room = await _rooms.GetByCodeAsync(roomCode) ?? throw new HubException("Sala no existe");
            var players = await _rooms.GetPlayersAsync(room.Id);
            if (players.Count < room.MinPlayers || players.Count > room.MaxPlayers)
                throw new HubException($"El juego requiere entre {room.MinPlayers} y {room.MaxPlayers} jugadores.");

            await _rooms.MarkStartedAsync(room.Id);

            var gs = await _game.StartAsync(room.Id, roundsPerPlayer);

            // Notificar inicio
            await Clients.Group(roomCode).SendAsync("GameStarted", new
            {
                GameId = gs.Id,
                RoomId = room.Id,
                RoundsPerPlayer = gs.RoundsPerPlayer
            });

            // Scoreboard inicial
            await BroadcastScoreboard(roomCode);

            // Anuncia de quién es el turno
            await BroadcastTurnChanged(roomCode);

            // Primer round del jugador actual
            var created = await _game.CreateOrNextRoundForCurrentAsync(room.Id);
            await Clients.Group(roomCode).SendAsync("NewRound", new
            {
                RoundId = created.round.Id,
                created.round.Word,
                created.round.InkHex,
                Options = created.round.Options.OrderBy(o => o.Order).Select(o => new
                {
                    o.Id,
                    o.IsCorrect,
                    o.Order,
                    o.ColorId
                }),
                RemainingForThisPlayer = created.remainingForThisPlayer
            });
        }

        // 🖱️ Responder en modo por turnos
        public async Task SubmitAnswer(string roomCode, Guid userId, int roundId, int optionId, double responseTimeSec)
        {
            var room = await _rooms.GetByCodeAsync(roomCode) ?? throw new HubException("Sala no existe");

            var (gp, delta, finishedTurn, finishedGame) =
                await _game.SubmitAnswerTurnAsync(room.Id, userId, roundId, optionId, responseTimeSec);

            // update individual + scoreboard en vivo
            await Clients.Group(roomCode).SendAsync("ScoreUpdated", new
            {
                UserId = userId,
                Score = gp.Score,
                Delta = delta,
                IsCorrect = delta > 0
            });

            await BroadcastScoreboard(roomCode);

            if (finishedGame)
            {
                var winner = await _game.GetWinnerAsync(room.Id);
                if (winner is not null)
                {
                    await Clients.Group(roomCode).SendAsync("Winner", new
                    {
                        winner.Value.UserId,
                        winner.Value.Username,
                        winner.Value.Score,
                        AvgResponseMs = Math.Round(winner.Value.AvgMs)
                    });
                }

                var board = await _game.GetScoreboardAsync(room.Id);
                await Clients.Group(roomCode).SendAsync("GameFinished",
                    board.Select(r => new
                    {
                        r.UserId,
                        r.Username,
                        r.Score,
                        AvgResponseMs = Math.Round(r.AvgMs)
                    }));
                return;
            }

            if (finishedTurn)
            {
                // Cambió de jugador
                await BroadcastTurnChanged(roomCode);

                // Crea primer round del nuevo jugador
                var created = await _game.CreateOrNextRoundForCurrentAsync(room.Id);
                await Clients.Group(roomCode).SendAsync("NewRound", new
                {
                    RoundId = created.round.Id,
                    created.round.Word,
                    created.round.InkHex,
                    Options = created.round.Options.OrderBy(o => o.Order).Select(o => new
                    {
                        o.Id,
                        o.IsCorrect,
                        o.Order,
                        o.ColorId
                    }),
                    RemainingForThisPlayer = created.remainingForThisPlayer
                });
            }
            else
            {
                // Sigue el mismo jugador: crea siguiente round
                var created = await _game.CreateOrNextRoundForCurrentAsync(room.Id);
                await Clients.Group(roomCode).SendAsync("NewRound", new
                {
                    RoundId = created.round.Id,
                    created.round.Word,
                    created.round.InkHex,
                    Options = created.round.Options.OrderBy(o => o.Order).Select(o => new
                    {
                        o.Id,
                        o.IsCorrect,
                        o.Order,
                        o.ColorId
                    }),
                    RemainingForThisPlayer = created.remainingForThisPlayer
                });
            }
        }

        // 🔁 Tu método BroadcastScoreboard ya existente (no cambia)
        private async Task BroadcastScoreboard(string roomCode)
        {
            var room = await _rooms.GetByCodeAsync(roomCode) ?? throw new HubException("Sala no existe");
            var gs = await _db.GameSessions.FirstAsync(g => g.RoomId == room.Id);

            // Score + promedio (como ya hacías)
            var board = await _game.GetScoreboardAsync(room.Id);

            // NUEVO: contar aciertos/errores por usuario en la partida actual
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

            // Mezcla score + contadores
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

            await Clients.Group(roomCode).SendAsync("Scoreboard", payload);
        }

    }
}

