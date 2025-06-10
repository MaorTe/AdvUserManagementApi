using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using UserManagementApi.Models;

namespace UserManagementApi.Endpoints;

public static class AuthEndpoints
{
    public static WebApplication MapAuthEndpoints(this WebApplication app) {
        var jwt = app.Configuration.GetSection("Jwt");
        var keyBytes = Encoding.UTF8.GetBytes(jwt["Key"]!);

        app.MapPost("/login", (LoginDto creds) => {
            // TODO: replace with real user validation
            if (creds.UserName != "admin" || creds.Password != "P@ssw0rd")
                return Results.Unauthorized();

            var claims = new[] { new Claim(ClaimTypes.Name, creds.UserName) };
            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(int.Parse(jwt["ExpireMinutes"]!)),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(keyBytes),
                    SecurityAlgorithms.HmacSha256)
            );

            return Results.Ok(new {
                token = new JwtSecurityTokenHandler().WriteToken(token)
            });
        })
        .AllowAnonymous()
        .WithName("Login")
        .WithTags("Auth");

        return app;
    }
}
