using System.ComponentModel.DataAnnotations;

namespace MeetingRoomBookingSystem.Models;

//model for representing meeting Room and related information 
public class Room
{
    //Unique Identifier of Room in db
    public int Id { get; set; }

    //Title of the meeting room
    public string Title { get; set; } = string.Empty;

    //Official number of the meeting room
    //String type is used to allow different formats of it for example 101-A
    public string Number { get; set; } = string.Empty;

    //Maximal amount of people which could be present in the meeting room
    public int Capacity { get; set; }

    //Foreign key of Id of admin who create current meeting room
    public int AdminId { get; set; }

    //field for storing version of modification of booking information about room
    [ConcurrencyCheck]

    public int RoomVersion { get; set; }

}