namespace TestAPILayer.Dtos.Users
{
    public class ResetPasswordDto
    {
        public string Email { get; set; }
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
        public string Token { get; set; }
        public string Transaction { get; set; }
    }
}
