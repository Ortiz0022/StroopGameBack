namespace StroobGame.Entities
{
    public class Room
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public required string Code { get; set; } // Ej: "12345"
        public Guid CreatorUserId { get; set; }
        public int MinPlayers { get; set; } = 2; // 2–4
        public int MaxPlayers { get; set; } = 6;
        public bool Started { get; set; } = false;
        public List<RoomPlayer> Players { get; set; } = new();
        public Guid? ActiveGameSessionId { get; set; }
    }
}
