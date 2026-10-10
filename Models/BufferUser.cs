namespace Uttu.Models;

public class BufferUser
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Plan { get; set; } = "Free";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Timezone { get; set; } = "America/Sao_Paulo";
}
