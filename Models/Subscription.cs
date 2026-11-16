using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GlowMe.Models;

[Table("Subscription", Schema = "glowme")]
public class Subscription
{
    [Key]
    [Column("SubscriptionID")]
    public int SubscriptionID { get; set; }

    [Column("ProviderID")]
    public int ProviderID { get; set; }

    [Column("PlanID")]
    public int PlanID { get; set; }

    [Column("StartDate")]
    public DateTime StartDate { get; set; }

    [Column("EndDate")]
    public DateTime EndDate { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("Status")]
    public string Status { get; set; } = "Active";

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    public ServiceProvider? Provider { get; set; }

    public SubscriptionPlan? Plan { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}