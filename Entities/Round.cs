namespace StroobGame.Entities
{
    public class Round
    {
        public int Id { get; set; }                  // identity en memoria
        public Guid GameSessionId { get; set; }
        public string Word { get; set; } = "";       // texto (nombre del color)
        public string InkHex { get; set; } = "";     // tinta (hex)
        public List<RoundOption> Options { get; set; } = new();
    }
}
