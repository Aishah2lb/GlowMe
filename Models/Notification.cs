using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GlowMe.Models;

[Table("Notification", Schema = "glowme")]
public class Notification
{
    [Key]
    [Column("NotificationID")]
    public int NotificationID { get; set; }

    [Column("CustomerID")]
    public int? CustomerID { get; set; }

    [Column("ProviderID")]
    public int? ProviderID { get; set; }

    [Required]
    [MaxLength(25)]
    [Column("NotificationType")]
    public string NotificationType { get; set; } = string.Empty;

    [Required]
    [MaxLength(400)]
    [Column("Message")]
    public string Message { get; set; } = string.Empty;

    [Column("IsRead")]
    public bool IsRead { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    public Customer? Customer { get; set; }

    public ServiceProvider? Provider { get; set; }
}