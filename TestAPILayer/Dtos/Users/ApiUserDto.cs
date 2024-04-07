namespace TestAPILayer.Dtos.Users
{
    public class ApiUserDto : LoginDto
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public byte Type { get; set; }
    }
}
