using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GlowMe.Models;

[Table("SubscriptionPlan", Schema = "glowme")]
public class SubscriptionPlan
{
    [Key]
    [Column("PlanID")]
    public int PlanID { get; set; }

    [Required]
    [MaxLength(60)]
    [Column("PlanName")]
    public string PlanName { get; set; } = string.Empty;

    [MaxLength(300)]
    [Column("Description")]
    public string? Description { get; set; }

    [Column("Price", TypeName = "decimal(8,2)")]
    public decimal Price { get; set; }

    [Column("DurationDays")]
    public int DurationDays { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}