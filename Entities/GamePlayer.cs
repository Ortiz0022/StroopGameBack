namespace StroobGame.Entities
{
    // Score por JUEGO
    public class GamePlayer
    {
        public int Id { get; set; }
        public Guid GameSessionId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = "";
        public int Score { get; set; } = 0;
        public long TotalResponseMs { get; set; } = 0;
        public int TotalResponses { get; set; } = 0;
    }
}
