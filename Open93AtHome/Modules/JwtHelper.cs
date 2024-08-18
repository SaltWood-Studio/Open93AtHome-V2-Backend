using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Open93AtHome.Modules;

public class JwtHelper
{
    private static readonly Lazy<JwtHelper> _instance = new Lazy<JwtHelper>(() => new JwtHelper());
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private RSA _rsa;
    private RsaSecurityKey _securityKey;

    // Private constructor
    private JwtHelper()
    {
        this._rsa = RSA.Create(4096);
        this._securityKey = new RsaSecurityKey(_rsa);
        _tokenHandler = new JwtSecurityTokenHandler();
        // GenerateRSAKeys(); // Generate RSA keys on initialization
    }

    public static JwtHelper Instance => _instance.Value;

    // RSA Keys Property
    public RSA RsaKey
    {
        get => this._rsa;
        set => this._rsa = value;
    }

    private void GenerateRSAKeys()
    {
        _rsa = RSA.Create(2048); // Generate a new RSA key pair with 2048-bit key length
        _securityKey = new RsaSecurityKey(_rsa);
    }

    public string GenerateToken(string username, string issuer, string audience, int expiration) => GenerateToken(issuer, audience, [
        new Claim(JwtRegisteredClaimNames.UniqueName, username)
    ], expiration);

    public string GenerateToken(string issuer, string audience, Claim[] claims, int expiration)
    {
        var credentials = new SigningCredentials(_securityKey, SecurityAlgorithms.RsaSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expiration), // Set the expiration time as needed
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token, string issuer, string audience)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = _securityKey
        };

        try
        {
            var principal = _tokenHandler.ValidateToken(token, tokenValidationParameters, out var validatedToken);
            return principal;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
