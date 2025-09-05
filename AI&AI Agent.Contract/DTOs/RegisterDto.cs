namespace AI_AI_Agent.Contract.DTOs
{
    public class RegisterDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
        public string Email { get; set; }
        public bool isPersistance { get; set; }
    }
}
