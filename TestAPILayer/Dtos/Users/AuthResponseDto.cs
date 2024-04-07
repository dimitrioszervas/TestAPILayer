namespace TestAPILayer.Dtos.Users
{
    public class AuthResponseDto
    {
        public string LoginResponse { get; set; }
        public string UserID { get; set; }
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
    }
}
