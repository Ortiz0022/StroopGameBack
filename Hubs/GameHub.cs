using Microsoft.AspNetCore.SignalR;
using StroobGame.Services;

namespace StroobGame.Hubs
{
    public class GameHub : Hub
    {
        private readonly IRoomService _rooms;
        private readonly IChatService _chat;
        private readonly ConnectionRegistry _connections;

        public GameHub(IRoomService rooms, IChatService chat, ConnectionRegistry connections)
        {
            _rooms = rooms;
            _chat = chat;
            _connections = connections;
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
    }
}
