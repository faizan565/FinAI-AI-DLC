using System.Security.Claims;
using FinAI.Api.Contracts;
using FinAI.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAntiforgery antiforgery) : ControllerBase
{
    private const int MaxCredentialLength = 256;
    private const string DuplicateUserNameCode = "DuplicateUserName";
    private const string InvalidCredentialsTitle = "Invalid username or password.";

    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult GetCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfTokenResponse(tokens.RequestToken ?? string.Empty));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] CredentialsRequest request)
    {
        var errors = ValidateCredentials(request);
        if (errors is not null)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var userName = request.UserName.Trim();
        var user = new ApplicationUser { UserName = userName };
        var result = await userManager.CreateAsync(user, request.Password);
        if (result.Succeeded)
        {
            return Created("/api/v1/auth/me", new UserResponse(user.Id, userName));
        }

        if (result.Errors.Any(error => error.Code == DuplicateUserNameCode))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Username is already taken.");
        }

        return ValidationProblem(new ValidationProblemDetails(ToValidationErrors(result.Errors)));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] CredentialsRequest request)
    {
        var errors = ValidateCredentials(request);
        if (errors is not null)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var userName = request.UserName.Trim();
        var result = await signInManager.PasswordSignInAsync(userName, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return InvalidCredentials();
        }

        var user = await userManager.FindByNameAsync(userName);
        return user is null
            ? InvalidCredentials()
            : Ok(new UserResponse(user.Id, userName));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = User.Identity?.Name;
        return id is null || userName is null
            ? Unauthorized()
            : Ok(new UserResponse(id, userName));
    }

    private IActionResult InvalidCredentials() =>
        Problem(statusCode: StatusCodes.Status401Unauthorized, title: InvalidCredentialsTitle);

    private static Dictionary<string, string[]>? ValidateCredentials(CredentialsRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var userName = (request.UserName ?? string.Empty).Trim();
        if (userName.Length == 0 || userName.Length > MaxCredentialLength)
        {
            errors[nameof(CredentialsRequest.UserName)] = new[] { $"Username must be between 1 and {MaxCredentialLength} characters." };
        }

        var password = request.Password ?? string.Empty;
        if (password.Length == 0 || password.Length > MaxCredentialLength)
        {
            errors[nameof(CredentialsRequest.Password)] = new[] { $"Password must be between 1 and {MaxCredentialLength} characters." };
        }

        return errors.Count > 0 ? errors : null;
    }

    private static Dictionary<string, string[]> ToValidationErrors(IEnumerable<IdentityError> errors) =>
        errors
            .GroupBy(error => error.Code.StartsWith("Password", StringComparison.Ordinal)
                ? nameof(CredentialsRequest.Password)
                : nameof(CredentialsRequest.UserName))
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
}
