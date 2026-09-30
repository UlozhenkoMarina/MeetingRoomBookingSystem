using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MeetingRoomBookingSystem.Models;


//model for representing Booking events and related information 
public class BookingRecord
{

    //Unique Identifier of Booking Room in db
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    //Foreign key of booked room
    public int RoomId { get; set; }

    //Foreign key of user who made booking
    public int UserId { get; set; }

    //Date of booking 
    public DateTime BookingDate { get; set; }

    //24 bit mask for booked slots of time
    // Each slot have 60 minute duration
    public int BookedSlots { get; set; }
}