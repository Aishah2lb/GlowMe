using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GlowMe.Models;

[Table("Payment", Schema = "glowme")]
public class Payment
{
    [Key]
    [Column("PaymentID")]
    public int PaymentID { get; set; }

    [Column("BookingID")]
    public int? BookingID { get; set; }

    [Column("Amount", TypeName = "decimal(9,2)")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("PaymentType")]
    public string PaymentType { get; set; } = string.Empty;

    [Required]
    [MaxLength(15)]
    [Column("PaymentMethod")]
    public string PaymentMethod { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    [Column("PaymentStatus")]
    public string PaymentStatus { get; set; } = "Pending";

    [Column("CommissionAmount", TypeName = "decimal(9,2)")]
    public decimal? CommissionAmount { get; set; }

    [Column("ProviderAmount", TypeName = "decimal(9,2)")]
    public decimal? ProviderAmount { get; set; }

    [MaxLength(40)]
    [Column("TransactionReference")]
    public string? TransactionReference { get; set; }

    [Column("PaidAt")]
    public DateTime? PaidAt { get; set; }

    [Column("SubscriptionID")]
    public int? SubscriptionID { get; set; }

    public Booking? Booking { get; set; }

    public Subscription? Subscription { get; set; }
}