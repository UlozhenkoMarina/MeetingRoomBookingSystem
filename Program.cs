using Microsoft.EntityFrameworkCore;
using MeetingRoomBookingSystem.Data;

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

app.UseAuthorization();

app.MapControllers();

app.Run();
