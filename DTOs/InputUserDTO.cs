namespace MeetingRoomBookingSystem.DTOs;

//DTO for receiving user credentials
public class InputUserDTO
{

    // User Name or Login
    public string Name { get; set; } = string.Empty;

    //Password for User authentication
    public string Password { get; set; } = string.Empty;

}
