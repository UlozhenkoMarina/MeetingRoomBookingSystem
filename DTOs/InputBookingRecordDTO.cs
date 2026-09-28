namespace MeetingRoomBookingSystem.DTOs;

//DTO for receiving information about booking process
public class InputBookingRecordDTO
{
    //Foreign key of booked room
    public int RoomId { get; set; }

    //Date of booking 
    public DateTime BookingDate { get; set; }

    //24 bit mask for booked slots of time for each chosen date
    // Each slot have 60 minute duration
    public Dictionary<DateTime, int>? SlotsPerDate;
}