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
    var user = await context.Users.FirstOrDefaultAsync(u => u.Name == dto.Name);
    if (user == null || hasher.VerifyHashedPassword(user, user.Password, dto.Password) == PasswordVerificationResult.Failed)
        return Results.BadRequest(new { Message = "Incorrect password or email" });

    var tokenHandler = new JwtSecurityTokenHandler();
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.Name, user.Name),
            new Claim("Id", user.Id.ToString()),
            new Claim("IsAdmin", user.IsAdmin.ToString().ToLower())
        }),
        Expires = DateTime.UtcNow.AddDays(7),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };
    var token = tokenHandler.CreateToken(tokenDescriptor);

    return Results.Ok(new { Token = tokenHandler.WriteToken(token), IsAdmin = user.IsAdmin });
});

//adding room by administrator
app.MapPost("/api/admin/bookings", async (System.Text.Json.JsonElement json, BookingDbContext context, HttpContext httpContext) =>
{
    var authHeader = httpContext.Request.Headers["Authorization"].ToString();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        return Results.Json(new { Message = "Unauthorized" }, statusCode: 401);

    var token = authHeader.Substring(7);
    var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
    int adminId;

    try
    {
        tokenHandler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        }, out var validatedToken);

        var jwtToken = (System.IdentityModel.Tokens.Jwt.JwtSecurityToken)validatedToken;
        var isAdminClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "IsAdmin")?.Value;

        if (isAdminClaim != "true")
            return Results.Json(new { Message = "Forbidden. Admin access required." }, statusCode: 403);

        var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "Id")?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Results.Json(new { Message = "Unauthorized. Invalid token structure." }, statusCode: 401);
        }

        adminId = int.Parse(userIdClaim);
    }
    catch
    {
        return Results.Json(new { Message = "Unauthorized. Invalid token." }, statusCode: 401);
    }

    var jsonOptions = new System.Text.Json.JsonSerializerOptions
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        IncludeFields = true
    };

    var rawJsonText = json.GetRawText();

    var roomDto = System.Text.Json.JsonSerializer.Deserialize<MeetingRoomBookingSystem.DTOs.InputRoomDTO>(rawJsonText, jsonOptions);
    var bookingDto = System.Text.Json.JsonSerializer.Deserialize<MeetingRoomBookingSystem.DTOs.InputBookingRecordDTO>(rawJsonText, jsonOptions);

    if (roomDto == null || bookingDto == null)
    {
        return Results.BadRequest(new { Message = "Invalid payload format. Failed to parse DTOs." });
    }

    var roomExists = await context.Rooms.AnyAsync(r => r.Title == roomDto.Title || r.Number == roomDto.Number);
    if (roomExists)
    {
        return Results.Conflict(new { Message = $"Conflict: A meeting room with title '{roomDto.Title}' or number '{roomDto.Number}' already exists." });
    }

    var newRoom = new Room
    {
        Title = roomDto.Title,
        Number = roomDto.Number,
        Capacity = roomDto.Capacity,
        AdminId = adminId
    };

    context.Rooms.Add(newRoom);
    await context.SaveChangesAsync();

    var fieldInfo = typeof(MeetingRoomBookingSystem.DTOs.InputBookingRecordDTO)
        .GetField("SlotsPerDate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    var slotsPerDate = fieldInfo?.GetValue(bookingDto) as Dictionary<DateTime, int> ?? bookingDto.SlotsPerDate;

    if (slotsPerDate != null)
    {
        foreach (var kvp in slotsPerDate)
        {
            var targetDate = kvp.Key.Date;
            var requestedSlots = kvp.Value;

            if (requestedSlots == 0) continue;

            var newBooking = new BookingRecord
            {
                RoomId = newRoom.Id,
                UserId = adminId,
                BookingDate = targetDate,
                BookedSlots = requestedSlots
            };

            context.BookingRecords.Add(newBooking);
        }
    }

    await context.SaveChangesAsync();

    return Results.Ok(new { Message = $"Room '{newRoom.Title}' ({roomDto.Number}) and its flexible slots published successfully!", RoomId = newRoom.Id });
});

app.MapGet("/api/customer/rooms", async (BookingDbContext context, HttpContext httpContext) =>
{
    var authHeader = httpContext.Request.Headers["Authorization"].ToString();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        return Results.Json(new { Message = "Unauthorized" }, statusCode: 401);

    var token = authHeader.Substring(7);
    var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

    try
    {
        tokenHandler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        }, out var validatedToken);

        var jwtToken = (System.IdentityModel.Tokens.Jwt.JwtSecurityToken)validatedToken;
        var isAdminClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "IsAdmin")?.Value;

        if (isAdminClaim == "true")
            return Results.Json(new { Message = "Forbidden. Customers access only." }, statusCode: 403);
    }
    catch
    {
        return Results.Json(new { Message = "Unauthorized. Invalid token." }, statusCode: 401);
    }

    var rooms = await context.Rooms.AsNoTracking().ToListAsync();
    return Results.Ok(rooms);
});


app.Run();
