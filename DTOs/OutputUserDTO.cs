namespace MeetingRoomBookingSystem.DTOs;

//DTO for returning user object without sensitive information

public class OutputUserDTO
{

    // User Name or Login
    public string Name { get; set; } = string.Empty;

}