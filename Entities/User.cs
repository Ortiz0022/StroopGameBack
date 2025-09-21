namespace StroobGame.Entities
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public required string Username { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        //bloquea el login si está en partida
        public bool IsPlaying { get; set; } = false;
    }
}
