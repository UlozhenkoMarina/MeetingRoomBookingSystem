namespace MeetingRoomBookingSystem.DTOs;

//DTO for returning information about room object without sensitive information
public class OutputRoomDTO
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

}