namespace SignalR_Demo.Models;

public class User
{
    public string Id {get;set;}
    public string PhoneNumber { get;  set; } = null!;
    public Guid? TenantId { get;  set; }
    public string Name { get;  set; } = null!;
    public string Email { get;  set; } = null!;
    public string PasswordHash { get;  set; } = null!;
    public UserRole? Role { get;  set; }
    public bool Disabled { get;  set; }
    public bool EmailConfirmed { get;  set; }
}

public enum UserRole
{
    SuperAdmin,
    Manager,
    Employee
}