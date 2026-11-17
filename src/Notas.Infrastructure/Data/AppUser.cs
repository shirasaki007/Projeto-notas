using Microsoft.AspNetCore.Identity;

namespace Notas.Infrastructure.Data;

public class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
