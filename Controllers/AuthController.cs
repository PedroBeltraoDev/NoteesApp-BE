using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NotesApp.Api.DTOs;
using NotesApp.Api.Extensions;
using NotesApp.Api.Services;

namespace NotesApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponseDto<AuthResponseDto>>> Register([FromBody] RegisterDto dto)
    {
        var response = await _authService.RegisterAsync(dto);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponseDto<AuthResponseDto>.Ok(response, "Conta criada com sucesso"));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponseDto<AuthResponseDto>>> Login([FromBody] LoginDto dto)
    {
        var response = await _authService.LoginAsync(dto);
        return Ok(ApiResponseDto<AuthResponseDto>.Ok(response, "Login realizado com sucesso"));
    }

    /// <summary>Troca o refresh token por um novo par de tokens (o refresh token antigo deixa de valer).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponseDto<AuthResponseDto>>> Refresh([FromBody] RefreshRequestDto dto)
    {
        var response = await _authService.RefreshAsync(dto.RefreshToken);
        return Ok(ApiResponseDto<AuthResponseDto>.Ok(response));
    }

    /// <summary>Revoga o refresh token informado. Idempotente.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponseDto<object?>>> Logout([FromBody] RefreshRequestDto dto)
    {
        await _authService.LogoutAsync(dto.RefreshToken);
        return Ok(ApiResponseDto<object?>.Ok(null, "Logout realizado com sucesso"));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> Me()
    {
        var user = await _authService.GetCurrentUserAsync(User.GetUserId());
        return Ok(ApiResponseDto<UserDto>.Ok(user));
    }
}
