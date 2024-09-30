using Microsoft.AspNetCore.Identity;

namespace TypingGameApp.API.Models
{
    public class AppUser : IdentityUser
    {
        public DateTime RegisteredOn { get; set; }
    }
}
