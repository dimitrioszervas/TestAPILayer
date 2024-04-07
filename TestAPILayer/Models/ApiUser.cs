using Microsoft.AspNetCore.Identity;

namespace TestAPILayer.Models
{
    public class ApiUser : IdentityUser
    {
        public const byte ADMIN = 0;
        public const byte USER = 1;
        public override string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public byte Type { get; set; }
    }
}
