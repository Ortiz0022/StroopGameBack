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

        // LOGIN: si existe entra, si no existe lo crea
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] RegisterDto dto)
        {
            if (dto is null || string.IsNullOrWhiteSpace(dto.Username))
                return BadRequest(new { message = "Username requerido" });

            var before = await _users.GetByUsernameAsync(dto.Username);
            var u = await _users.ResolveAsync(dto.Username);

            return Ok(new
            {
                status = before == null ? "created" : "existing",
                user = new { u.Id, u.Username, u.CreatedAt }
            });
        }

        // REGISTER ESTRICTO: crea y falla si existe (déjalo si lo quieres mantener)
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
                return Conflict(new { message = ex.Message });
            }
        }

        // LOOKUP por username
        [HttpGet("by-username/{username}")]
        public async Task<IActionResult> GetByUsername([FromRoute] string username)
        {
            var u = await _users.GetByUsernameAsync(username);
            if (u is null) return NotFound("Usuario no encontrado");
            return Ok(new { u.Id, u.Username, u.CreatedAt });
        }

        // GET por Id
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            var user = await _users.GetByIdAsync(id);
            if (user == null) return NotFound("Usuario no encontrado");
            return Ok(new { user.Id, user.Username, user.CreatedAt });
        }
    }
}
