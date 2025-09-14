using StroobGame.Entities;

namespace StroobGame.Services
{
    public interface IGameService
    {
        // Inicia partida por turnos: fija jugador inicial y RoundsPerPlayer
        Task<GameSession> StartAsync(Guid roomId, int roundsPerPlayer);

        // Crea (o recrea) el round del jugador actual. Devuelve lo que le queda a este jugador.
        Task<(Round round, int remainingForThisPlayer)> CreateOrNextRoundForCurrentAsync(Guid roomId);

        // Procesa la respuesta del jugador actual.
        // finishedTurn: terminó sus N rondas. finishedGame: no hay más jugadores.
        Task<(GamePlayer updated, int delta, bool finishedTurn, bool finishedGame)>
            SubmitAnswerTurnAsync(Guid roomId, Guid userId, int roundId, int optionId, double responseTimeSec);

        // Lecturas auxiliares
        Task<List<(Guid UserId, string Username, int Score, double AvgMs)>> GetScoreboardAsync(Guid roomId);
        Task<(Guid UserId, string Username, int Score, double AvgMs)?> GetWinnerAsync(Guid roomId);
        Task<(Guid UserId, string Username)> GetCurrentPlayerAsync(Guid roomId);
    }
}
