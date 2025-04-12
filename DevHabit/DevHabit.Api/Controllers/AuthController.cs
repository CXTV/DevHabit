using System.Runtime.ConstrainedExecution;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Auth;
using DevHabit.Api.DTOs.Users;
using DevHabit.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using DevHabit.Api.Services;
using DevHabit.Api.Settings;
using Microsoft.Extensions.Options;

namespace DevHabit.Api.Controllers;

[ApiController]
[Route("auth")]
[AllowAnonymous]
public sealed class AuthController(
    UserManager<IdentityUser> userManager,
    ApplicationIdentityDbContext identityDbContext,
    ApplicationDbContext applicationDbContext,
    TokenProvider tokenProvider,
    IOptions<JwtAuthOptions> options
    ) : ControllerBase
{
    private readonly JwtAuthOptions _jwtAuthOptions = options.Value;


    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterUserDto registerUserDto)
    {
        //1. 开启事务（在 IdentityDb 上下文中）
        await using IDbContextTransaction transaction = await identityDbContext.Database.BeginTransactionAsync();
        //2. 在 ApplicationDb 上下文中使用相同的连接
        applicationDbContext.Database.SetDbConnection(identityDbContext.Database.GetDbConnection());
        await applicationDbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var identityUser = new IdentityUser
        {
            Email = registerUserDto.Email,
            UserName = registerUserDto.Email
        };

        IdentityResult identityResult = await userManager.CreateAsync(identityUser, registerUserDto.Password);

        if (!identityResult.Succeeded)
        {

            var extensions = new Dictionary<string, object?> { { "errors", identityResult.Errors } };
            return Problem(
                detail: "Unable to register user",
                statusCode: StatusCodes.Status500InternalServerError,
                extensions: extensions
            );
        }

        User user = registerUserDto.ToEntity();

        user.IdentityId = identityUser.Id;
        applicationDbContext.Users.Add(user);
        await applicationDbContext.SaveChangesAsync();


        var tokenRequest = new TokenRequest(identityUser.Id, identityUser.Email);
        AccessTokensDto accessTokens = tokenProvider.Create(tokenRequest);

        var refreshToken = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = identityUser.Id,
            Token = accessTokens.RefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtAuthOptions.RefreshTokenExpirationInDays)
        };

        identityDbContext.RefreshTokens.Add(refreshToken);

        //3. 提交事务
        await transaction.CommitAsync();
        return Ok(accessTokens);
    }


    [HttpPost("login")]
    public async Task<ActionResult<AccessTokensDto>> Login(LoginUserDto loginUserDto)
    {
        IdentityUser? identityUser = await userManager.FindByEmailAsync(loginUserDto.Email);

        if (identityUser is null || !await userManager.CheckPasswordAsync(identityUser, loginUserDto.Password))
        {
            return Unauthorized();
        }

        var tokenRequest = new TokenRequest(identityUser.Id, identityUser.Email!);
        AccessTokensDto accessTokens = tokenProvider.Create(tokenRequest);

        var refreshToken = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = identityUser.Id,
            Token = accessTokens.RefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtAuthOptions.RefreshTokenExpirationInDays)
        };

        identityDbContext.RefreshTokens.Add(refreshToken);

        return Ok(accessTokens);
    }


    [HttpPost("refresh")]
    public async Task<ActionResult<AccessTokensDto>> Refresh(RefreshTokenDto refreshTokenDto)
    {
        RefreshToken? refreshToken = await identityDbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshTokenDto.RefreshToken);

        if (refreshToken is null)
        {
            return Unauthorized();
        }

        if (refreshToken.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Unauthorized();
        }

        var tokenRequest = new TokenRequest(refreshToken.User.Id, refreshToken.User.Email!);
        AccessTokensDto accessTokens = tokenProvider.Create(tokenRequest);

        refreshToken.Token = accessTokens.RefreshToken;
        refreshToken.ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtAuthOptions.RefreshTokenExpirationInDays);

        await identityDbContext.SaveChangesAsync();

        return Ok(accessTokens);
    }

}
