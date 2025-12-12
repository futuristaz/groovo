using System.ComponentModel;

namespace Groovo.DTOs;

public enum UserRole
{
    [Description("Regular User")]
    User = 0,
    [Description("Content Author")]
    Author = 1,
    [Description("Administrator")]
    Admin = 2
}