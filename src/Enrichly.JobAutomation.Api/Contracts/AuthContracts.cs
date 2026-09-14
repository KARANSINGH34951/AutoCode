using System.ComponentModel.DataAnnotations;
namespace Enrichly.JobAutomation.Api.Contracts;
public sealed record RegisterRequest([param: Required, EmailAddress, StringLength(320)] string Email, [param: Required, MinLength(8), MaxLength(100)] string Password);
public sealed record LoginRequest([param: Required, EmailAddress] string Email, [param: Required] string Password);
public sealed record AuthResponse(Guid UserId, string Email, string Token);
