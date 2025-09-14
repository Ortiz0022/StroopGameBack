using System;

namespace StroobGame.Entities
{
    // Estadísticas históricas por usuario (persisten entre partidas)
    public class UserStats
    {
        public int Id { get; set; }              // Identity en InMemory
        public Guid UserId { get; set; }         // FK lógica a User

        public int TotalScore { get; set; } = 0; // Suma de todos los puntos históricos
        public int GamesPlayed { get; set; } = 0;
        public int BestScore { get; set; } = 0;

        public long TotalResponseMs { get; set; } = 0; // suma de tiempos (ms)
        public int TotalResponses { get; set; } = 0;   // cantidad de respuestas

        // Promedio de respuesta (ms) calculado al vuelo
        public double AvgResponseMs =>
            TotalResponses > 0 ? (double)TotalResponseMs / TotalResponses : 0;
    }
}
