namespace MeetingRoomBookingSystem.Models;

//model for representing User and related information 
public class User
{
    //Unique Identifier of User in db
    public int Id { get; set; }

    // User Name or Login
    public string Name { get; set; } = string.Empty;

    //Password for User authentication
    public string Password { get; set; } = string.Empty;

    //Field for identifying admin users
    public bool IsAdmin { get; set; }
}