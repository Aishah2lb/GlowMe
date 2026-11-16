using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GlowMe.Models;

[Table("Message", Schema = "glowme")]
public class Message
{
    [Key]
    [Column("MessageID")]
    public int MessageID { get; set; }

    [Column("CustomerID")]
    public int CustomerID { get; set; }

    [Column("ProviderID")]
    public int ProviderID { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("SenderType")]
    public string SenderType { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    [Column("MessageText")]
    public string MessageText { get; set; } = string.Empty;

    [Column("IsRead")]
    public bool IsRead { get; set; }

    [Column("SentAt")]
    public DateTime SentAt { get; set; }

    public Customer? Customer { get; set; }

    public ServiceProvider? Provider { get; set; }
}