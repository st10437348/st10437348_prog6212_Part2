namespace CMCS.Models
{
    public class UserAccount
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role { get; set; } = "";
    }
}

