namespace StroobGame.Entities
{
    public class ChatMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RoomId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    }
}