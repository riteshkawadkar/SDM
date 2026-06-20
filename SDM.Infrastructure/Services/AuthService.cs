using Microsoft.EntityFrameworkCore;
using SDM.Application.DTOs.Auth;
using SDM.Application.Interfaces;
using SDM.Domain.Entities;
using SDM.Infrastructure.Data;

namespace SDM.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(ApplicationDbContext db, IJwtTokenGenerator jwtTokenGenerator)
    {
        _db = db;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == request.Email);

        if (user == null)
            throw new SDM.Application.Exceptions.AuthenticationException("Invalid credentials");

        var valid = BCrypt.Net.BCrypt.Verify(
            request.Password,
            user.PasswordHash);

        if (!valid)
            throw new SDM.Application.Exceptions.AuthenticationException("Invalid credentials");

        var token = _jwtTokenGenerator.GenerateToken(user);

        return new LoginResponse
        {
            Token = token
        };
    }

    public async Task RegisterAsync(RegisterRequest request)
    {
        var exists = await _db.Users
            .AnyAsync(x => x.Email == request.Email);

        if (exists)
            throw new Exception("Email already exists");

        // Ensure new user has a valid RoleId (Roles are seeded by migrations)
        var defaultRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Viewer");
        if (defaultRole == null)
        {
            // Fallback: if expected role not found, pick any existing role
            defaultRole = await _db.Roles.FirstOrDefaultAsync();
            if (defaultRole == null)
                throw new Exception("No roles found in the database. Ensure migrations and seed data have been applied.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedOn = DateTime.UtcNow,
            RoleId = defaultRole.Id
        };

        _db.Users.Add(user);

        await _db.SaveChangesAsync();
    }
}