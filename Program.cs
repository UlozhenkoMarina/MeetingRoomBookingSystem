using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt; 
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using MeetingRoomBookingSystem.Data;
using MeetingRoomBookingSystem.Models;
using MeetingRoomBookingSystem.DTOs;

var builder = WebApplication.CreateBuilder(args);

//Database configuration
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<BookingDbContext>(options =>
        options.UseInMemoryDatabase("BookingLocalDb"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<BookingDbContext>(options =>
        options.UseSqlServer(connectionString));
}

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDefaultFiles(); 
app.UseStaticFiles();
app.UseCors();

//Built-in .NET utility for cryptographic password hashing and verification
var hasher = new PasswordHasher<User>();

// Reading JWT Secret information from the secure appsettings.Development.json 
var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? throw new InvalidOperationException("JWT Secret is missing!");
var key = Encoding.ASCII.GetBytes(jwtSecret);

//registering user
app.MapPost("/api/auth/register", async (InputUserDTO dto, BookingDbContext context) =>
{
    if (await context.Users.AnyAsync(u => u.Name == dto.Name))
        return Results.BadRequest(new { Message = "Such name is already used" });

    var user = new User {Name = dto.Name, IsAdmin = dto.IsAdmin };
    user.Password = hasher.HashPassword(user, dto.Password);

    context.Users.Add(user);
    await context.SaveChangesAsync();
    return Results.Ok(new { Message = "Successfully registered" });
});

//login and generating JWT 
app.MapPost("/api/auth/login", async (InputUserDTO dto, BookingDbContext context) =>
{
    var user = await context.Users.SingleOrDefaultAsync(u => u.Name == dto.Name);
    if (user == null || hasher.VerifyHashedPassword(user, user.Password, dto.Password) == PasswordVerificationResult.Failed)
        return Results.BadRequest(new { Message = "Incorrect password or email" });

    var tokenHandler = new JwtSecurityTokenHandler();
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.Name, user.Name),
            new Claim("IsAdmin", user.IsAdmin.ToString().ToLower())
        }),
        Expires = DateTime.UtcNow.AddDays(7),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };
    var token = tokenHandler.CreateToken(tokenDescriptor);

    return Results.Ok(new { Token = tokenHandler.WriteToken(token), IsAdmin = user.IsAdmin });
});

app.Run();
