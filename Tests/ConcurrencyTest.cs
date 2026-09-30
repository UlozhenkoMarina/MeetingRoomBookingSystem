using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

using MeetingRoomBookingSystem.Models;
using MeetingRoomBookingSystem.Data;
using MeetingRoomBookingSystem;


namespace MeetingRoomBookingSystem.Tests
{
    public class BookingRouteConcurrencyTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public BookingRouteConcurrencyTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                var baseDir = AppContext.BaseDirectory;
                builder.UseContentRoot(baseDir);
                builder.UseWebRoot(baseDir); 
            });
        }

        [Fact]
        public async Task Post_BookingRoute_Should_Handle_RaceCondition_With_Tokens_And_Return_Conflict()
        {

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

                var existingRoom = await context.Rooms.FirstOrDefaultAsync(r => r.Id == 1);
                if (existingRoom == null)
                {
                    var testRoom = new Room
                    {
                        Id = 1,
                        Title = "Integration Test Room",
                        Number = "101-T",
                        Capacity = 10,
                        RoomVersion = 1
                    };
                    context.Rooms.Add(testRoom);
                    await context.SaveChangesAsync();
                }
                else
                {
                    existingRoom.RoomVersion = 1;
                    await context.SaveChangesAsync();
                }
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var testSecretKey = "MeetingRoomBookingSystemSuperSecretKey2026!!!";
            var keyBytes = Encoding.UTF8.GetBytes(testSecretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("Id", "42"),
                    new Claim("IsAdmin", "false")
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
            };

            var securityToken = tokenHandler.CreateToken(tokenDescriptor);
            var validCustomerToken = tokenHandler.WriteToken(securityToken);

            var client1 = _factory.CreateClient();
            var client2 = _factory.CreateClient();

            client1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", validCustomerToken);
            client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", validCustomerToken);

            var selectedDate = DateTime.UtcNow.Date;
            var slotsObject = new Dictionary<string, int>
            {
                { selectedDate.ToString("yyyy-MM-ddTHH:mm:ssZ"), 4 } // Бронюємо слот 10:00 (маска = 4)
            };

            var payload = new
            {
                roomId = 1,
                bookingDate = selectedDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                slotsPerDate = slotsObject
            };

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var content1 = new StringContent(JsonSerializer.Serialize(payload, jsonOptions), Encoding.UTF8, "application/json");
            var content2 = new StringContent(JsonSerializer.Serialize(payload, jsonOptions), Encoding.UTF8, "application/json");

            var request1 = client1.PostAsync("/api/customer/bookings", content1);
            var request2 = client2.PostAsync("/api/customer/bookings", content2);

            var responses = await Task.WhenAll(request1, request2);

            var responseA = responses[0];
            var responseB = responses[1];

            bool safeProtectionWorked =
                (responseA.StatusCode == HttpStatusCode.OK && responseB.StatusCode == HttpStatusCode.Conflict) ||
                (responseA.StatusCode == HttpStatusCode.Conflict && responseB.StatusCode == HttpStatusCode.OK) ||
                (responseA.StatusCode == HttpStatusCode.Conflict && responseB.StatusCode == HttpStatusCode.Conflict);

            Assert.True(safeProtectionWorked,
                $"Concurrency error! Thread A return: {responseA.StatusCode}, Thread B return: {responseB.StatusCode}");
        }
    }
}
