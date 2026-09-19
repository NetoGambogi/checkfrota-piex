using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    public class UserListItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Avatar { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        public bool Active { get; set; }
    }

    public class UserListResponse
    {
        public List<UserListItem> Users { get; set; } = new();
    }
}
