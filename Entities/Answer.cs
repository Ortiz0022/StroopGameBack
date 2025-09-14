namespace StroobGame.Entities
{
    public class Answer
    {
        public int Id { get; set; }
        public Guid GameSessionId { get; set; }
        public int RoundId { get; set; }
        public Guid UserId { get; set; }
        public bool IsCorrect { get; set; }
        public double ResponseTimeSec { get; set; }
        public int OptionId { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
