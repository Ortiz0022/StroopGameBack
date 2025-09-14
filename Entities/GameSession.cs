namespace StroobGame.Entities
{
    public class GameSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RoomId { get; set; }
        public string State { get; set; } = "playing"; // "playing" | "finished"

        // 🎯 MODO POR TURNOS
        public int RoundsPerPlayer { get; set; } = 4;     // cuántas palabras juega cada jugador
        public int CurrentSeat { get; set; } = 0;          // índice en Room.Players (ordenados por SeatOrder)
        public Guid CurrentPlayerUserId { get; set; }      // id del jugador al que le toca
        public int CurrentPlayerRoundsPlayed { get; set; } = 0; // cuántas ya jugó el actual

        public int? CurrentRoundId { get; set; }           // round activo mostrado al jugador actual
    }
}
