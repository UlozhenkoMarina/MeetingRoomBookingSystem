using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt; 
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

using MeetingRoomBookingSystem.Data;
using MeetingRoomBookingSystem.Models;
using MeetingRoomBookingSystem.DTOs;
using MeetingRoomBookingSystem.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null; 
});

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
builder.Services.AddSignalR();


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<BookingDbContext>();
        await context.Database.EnsureCreatedAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database initialization error: {ex.Message}");
    }
}

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
    return Results.Ok(new { message = "Successfully registered" });
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

    return Results.Ok(new { token = tokenHandler.WriteToken(token), isAdmin = user.IsAdmin });
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
        AdminId = adminId,
        RoomVersion = 1
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

//overview of list of rooms by customer 
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


//getting information about room to show it to regular customer
app.MapGet("/api/customer/booking", async (string jsonDto, DateTime date, BookingDbContext context, HttpContext httpContext) =>
{
    var authHeader = httpContext.Request.Headers["Authorization"].ToString();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        return Results.Json(new { Message = "Unauthorized" }, statusCode: 401);

    var token = authHeader.Substring(7);
    var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

    try
    {
        var jwtToken = tokenHandler.ReadJwtToken(token);
        var isAdminClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "IsAdmin")?.Value;

        if (isAdminClaim == "true")
            return Results.Json(new { Message = "Forbidden. Customers access only." }, statusCode: 403);
    }
    catch
    {
        return Results.Json(new { Message = "Unauthorized. Invalid token structure." }, statusCode: 401);
    }

    InputRoomDTO? dto;
    try
    {
        var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        dto = System.Text.Json.JsonSerializer.Deserialize<InputRoomDTO>(jsonDto, jsonOptions);
        if (dto == null) return Results.BadRequest(new { Message = "Invalid JSON DTO format." });
    }
    catch
    {
        return Results.BadRequest(new { Message = "Failed to deserialize input JSON." });
    }

    var roomFromDb = await context.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == dto.Id);
    if (roomFromDb == null)
        return Results.NotFound(new { Message = "Requested meeting room not found." });

    var roomDto = new MeetingRoomBookingSystem.DTOs.InputRoomDTO
    {
        Id = roomFromDb.Id,
        Title = roomFromDb.Title,
        Number = roomFromDb.Number,
        Capacity = roomFromDb.Capacity
    };

    var searchDate = date.Date;

    var records = await context.BookingRecords
        .AsNoTracking()
        .Where(b => b.RoomId == dto.Id && b.BookingDate.Date == searchDate) // 👈 Тепер працює залізобетонно
        .Select(b => new MeetingRoomBookingSystem.DTOs.OutputBookingRecordDTO
        {
            Id = b.Id,
            RoomId = b.RoomId,
            BookedSlots = b.BookedSlots
        })
        .ToListAsync();

    return Results.Ok(new
    {
        room = roomDto,
        bookingList = records
    });
});


//customer room booking with using optimistic approach of solving concurrency issue
app.MapPost("/api/customer/bookings", async (System.Text.Json.JsonElement json, BookingDbContext context, HttpContext httpContext) =>
{
    var authHeader = httpContext.Request.Headers["Authorization"].ToString();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        return Results.Json(new { Message = "Unauthorized" }, statusCode: 401);

    var token = authHeader.Substring(7);
    var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
    int userId;

    try
    {
        var jwtToken = tokenHandler.ReadJwtToken(token);
        var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "Id")?.Value;
        var isAdminClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "IsAdmin")?.Value;

        if (string.IsNullOrEmpty(userIdClaim)) return Results.Json(new { Message = "Unauthorized" }, statusCode: 401);
        if (isAdminClaim == "true") return Results.Json(new { Message = "Forbidden. Customers only." }, statusCode: 403);

        userId = int.Parse(userIdClaim);
    }
    catch
    {
        return Results.Json(new { Message = "Unauthorized. Invalid token structure." }, statusCode: 401);
    }

    var jsonOptions = new System.Text.Json.JsonSerializerOptions
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        IncludeFields = true
    };

    var dto = System.Text.Json.JsonSerializer.Deserialize<InputBookingRecordDTO>(json.GetRawText(), jsonOptions);
    if (dto == null) return Results.BadRequest(new { Message = "Invalid request payload." });

    var fieldInfo = typeof(MeetingRoomBookingSystem.DTOs.InputBookingRecordDTO)
        .GetField("SlotsPerDate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var slotsPerDate = fieldInfo?.GetValue(dto) as Dictionary<DateTime, int> ?? dto.SlotsPerDate;

    if (slotsPerDate == null || slotsPerDate.Count == 0)
        return Results.BadRequest(new { Message = "No slots provided." });

    var room = await context.Rooms.FirstOrDefaultAsync(r => r.Id == dto.RoomId);
    if (room == null) return Results.NotFound(new { Message = "Room not found." });

    foreach (var kvp in slotsPerDate)
    {
        var targetDate = kvp.Key.Date;
        var requestedSlots = kvp.Value;
        if (requestedSlots == 0) continue;

        var allDayRecords = await context.BookingRecords
            .AsNoTracking()
            .Where(b => b.RoomId == dto.RoomId && b.BookingDate.Date == targetDate)
            .ToListAsync();

        int combinedDayMask = 0;
        foreach (var record in allDayRecords)
        {
            combinedDayMask |= record.BookedSlots;
        }

        if ((combinedDayMask & requestedSlots) != 0)
        {
            return Results.Conflict(new { Message = $"Conflict: One or more selected hours on {targetDate:yyyy-MM-dd} are already taken by other users!" });
        }

        var newBooking = new BookingRecord
        {
            RoomId = dto.RoomId,
            UserId = userId,
            BookingDate = targetDate,
            BookedSlots = requestedSlots
        };
        context.BookingRecords.Add(newBooking);
    }

    room.RoomVersion++;


    try
    {
        await context.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict(new { Message = "Concurrency Conflict: The schedule for this room was modified by another server thread. Transaction rolled back." });
    }

    var hubContext = (IHubContext<BookingHub>)
    httpContext.RequestServices.GetRequiredService(typeof(IHubContext<BookingHub>));

    foreach (var kvp in slotsPerDate)
    {
        if (kvp.Value == 0) continue;
        await hubContext.Clients.All.SendAsync("ReceiveBookingUpdate", dto.RoomId, kvp.Key.ToString("yyyy-MM-dd"));
    }

    return Results.Ok(new { Message = "All selected slots reserved successfully with true distributed protection!" });
});


//getting information about reserved by user rooms
app.MapGet("/api/customer/reserved_rooms", async (BookingDbContext context, HttpContext httpContext) =>
{
    var authHeader = httpContext.Request.Headers["Authorization"].ToString();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        return Results.Json(new { Message = "Unauthorized" }, statusCode: 401);

    var token = authHeader.Substring(7);
    var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
    int userId;

    try
    {
        var jwtToken = tokenHandler.ReadJwtToken(token);
        var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "Id")?.Value;
        var isAdminClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "IsAdmin")?.Value;

        if (string.IsNullOrEmpty(userIdClaim)) return Results.Json(new { Message = "Unauthorized" }, statusCode: 401);
        if (isAdminClaim == "true") return Results.Json(new { Message = "Forbidden. Customers access only." }, statusCode: 403);

        userId = int.Parse(userIdClaim);
    }
    catch
    {
        return Results.Json(new { Message = "Unauthorized. Invalid token structure." }, statusCode: 401);
    }

    var customerBookings = await context.BookingRecords
        .AsNoTracking()
        .Where(b => b.UserId == userId)
        .Join(context.Rooms,
            booking => booking.RoomId,
            room => room.Id,
            (booking, room) => new
            {
                Id = booking.Id,
                RoomId = booking.RoomId,
                RoomTitle = room.Title,
                RoomNumber = room.Number,
                BookingDate = booking.BookingDate,
                BookedSlots = booking.BookedSlots
            })
        .OrderByDescending(b => b.BookingDate)
        .ToListAsync();

    var resultList = customerBookings.Select(b => new
    {
        record = new OutputBookingRecordDTO
        {
            Id = b.Id,
            RoomId = b.RoomId,
            BookedSlots = b.BookedSlots
        },
        roomTitle = b.RoomTitle,
        roomNumber = b.RoomNumber,
        bookingDate = b.BookingDate.ToString("yyyy-MM-dd")
    }).ToList();

    return Results.Ok(resultList);
});


app.MapHub<BookingHub>("/bookingHub");


app.Run();

//for testing
public partial class Program { }
