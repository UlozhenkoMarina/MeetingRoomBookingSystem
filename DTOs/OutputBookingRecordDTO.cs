namespace MeetingRoomBookingSystem.DTOs;

//DTO for returning information about booking process 
public class OutputBookingRecordDTO
{

    //Unique Identifier of Booking Room in db
    public int Id { get; set; }

    //Foreign key of booked room
    public int RoomId { get; set; }

    //Date of booking 
    public DateTime BookingDate { get; set; }

    //24 bit mask for booked slots of time
    // Each slot have 60 minute duration
    public int BookedSlots { get; set; }
}