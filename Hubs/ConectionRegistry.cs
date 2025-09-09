namespace StroobGame.Hubs
{
    public class ConnectionRegistry
    {
        // connId -> (roomId, roomCode, userId, username)
        private readonly Dictionary<string, (Guid RoomId, string RoomCode, Guid UserId, string Username)> _map
            = new(StringComparer.Ordinal);

        private readonly object _lock = new();

        public void Set(string connId, Guid roomId, string roomCode, Guid userId, string username)
        {
            lock (_lock) _map[connId] = (roomId, roomCode, userId, username);
        }

        public bool TryGet(string connId, out (Guid RoomId, string RoomCode, Guid UserId, string Username) info)
        {
            lock (_lock) return _map.TryGetValue(connId, out info);
        }

        public void Remove(string connId)
        {
            lock (_lock) _map.Remove(connId);
        }
    }
}
