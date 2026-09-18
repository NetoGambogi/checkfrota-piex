using System;
using System.Collections.Generic;
using System.Text;

namespace checkfrota_front.Models
{
    public class UserModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Avatar { get; set; }

        public string? CreatedAt { get; set; }
    }

    // Models/LoginResponse.cs
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public UserModel User { get; set; } = new();
    }
}
