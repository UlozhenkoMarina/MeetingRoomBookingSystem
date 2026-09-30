using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace MeetingRoomBookingSystem.Hubs
{
    public class BookingHub : Hub
    {
        public async Task SendBookingUpdate(int roomId, string bookingDate)
        {
            await Clients.Others.SendAsync("ReceiveBookingUpdate", roomId, bookingDate);
        }
    }
}