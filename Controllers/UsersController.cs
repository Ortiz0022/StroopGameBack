using Microsoft.AspNetCore.Mvc;
using StroobGame.Services;

namespace StroobGame.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _users;
        public UsersController(IUserService users) => _users = users;

        public record RegisterDto(string Username);

        // REGISTER ESTRICTO: crea y falla si existe
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            try
            {
                var u = await _users.RegisterAsync(dto.Username);
                return Ok(new { u.Id, u.Username, u.CreatedAt });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message }); // 409
            }
        }

        // LOOKUP: devuelve usuario existente por username (404 si no existe)
        [HttpGet("by-username/{username}")]
        public async Task<IActionResult> GetByUsername([FromRoute] string username)
        {
            var u = await _users.GetByUsernameAsync(username);
            if (u is null) return NotFound("Usuario no encontrado");
            return Ok(new { u.Id, u.Username, u.CreatedAt });
        }

        // OPCIONAL (si quieres simplicidad en pruebas): upsert idempotente
        [HttpPost("resolve")]
        public async Task<IActionResult> Resolve([FromBody] RegisterDto dto)
        {
            var u = await _users.ResolveAsync(dto.Username);
            return Ok(new { u.Id, u.Username, u.CreatedAt });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            var user = await _users.GetByIdAsync(id);
            if (user == null) return NotFound("Usuario no encontrado");
            return Ok(new { user.Id, user.Username, user.CreatedAt });
        }
    }
}
