namespace StroobGame.Entities
{
    public class RoundOption
    {
        public int Id { get; set; }              // identity en memoria
        public int RoundId { get; set; }
        public bool IsCorrect { get; set; }
        public int Order { get; set; }           // para ordenar en el front
        public int ColorId { get; set; }         // referencia a catálogo
    }
}
