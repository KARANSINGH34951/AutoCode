using Enrichly.JobAutomation.Api.Contracts;
using Enrichly.JobAutomation.Api.Services;
using Enrichly.JobAutomation.Domain.Entities;
using Enrichly.JobAutomation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Enrichly.JobAutomation.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(JobAutomationDbContext db, JwtTokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken)) return Conflict(new { message = "An account with this email already exists." });
        var user = new User { Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return Created("", new AuthResponse(user.Id, user.Email, tokens.Create(user)));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) return Unauthorized(new { message = "Invalid email or password." });
        return Ok(new AuthResponse(user.Id, user.Email, tokens.Create(user)));
    }
}
