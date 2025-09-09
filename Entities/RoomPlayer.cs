namespace StroobGame.Entities
{
    public class RoomPlayer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RoomId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = "";
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public bool IsOwner { get; set; } = false;
        public int SeatOrder { get; set; } // 0..N-1
    }
}
