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

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var u = await _users.RegisterAsync(dto.Username);
            return Ok(new { u.Id, u.Username });
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