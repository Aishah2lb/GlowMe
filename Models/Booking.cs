using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GlowMe.Support;

namespace GlowMe.Models;

[Table("Booking", Schema = "glowme")]
public class Booking
{
    [Key]
    [Column("BookingID")]
    public int BookingID { get; set; }

    [Column("CustomerID")]
    public int CustomerID { get; set; }

    [Column("ProviderID")]
    public int ProviderID { get; set; }

    [Column("BookingDate")]
    public DateTime BookingDate { get; set; }

    [Column("StartTime")]
    public TimeSpan StartTime { get; set; }

    [Column("EndTime")]
    public TimeSpan EndTime { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("LocationType")]
    public string LocationType { get; set; } = string.Empty;

    [Required]
    [MaxLength(250)]
    [Column("LocationAddress")]
    public string LocationAddress { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    [Column("PaymentOption")]
    public string PaymentOption { get; set; } = string.Empty;

    [Column("TotalAmount", TypeName = "decimal(9,2)")]
    public decimal TotalAmount { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("BookingStatus")]
    public string BookingStatus { get; set; } = "Pending";

    [MaxLength(500)]
    [Column("Notes")]
    public string? Notes { get; set; }

    [MaxLength(300)]
    [Column("CancellationReason")]
    public string? CancellationReason { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }

    public Customer? Customer { get; set; }

    public ServiceProvider? Provider { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public Review? Review { get; set; }

    public ICollection<BookingService> BookingServices { get; set; } = new List<BookingService>();

    [NotMapped]
    public IEnumerable<Service> Services =>
        BookingServices.Where(bs => bs.Service is not null).Select(bs => bs.Service!);

    public string Ref() => "#BK-" + BookingID;

    public decimal DepositAmount() =>
        Math.Round(TotalAmount * (decimal)GlowMeOptions.DepositRate, 2);

    public decimal PaidAmount() =>
        Payments
            .Where(p => p.PaymentStatus == "Paid"
                        && (p.PaymentType == "Deposit" || p.PaymentType == "Full payment" || p.PaymentType == "Remaining balance"))
            .Sum(p => p.Amount);

    public decimal RemainingAmount() => Math.Round(TotalAmount - PaidAmount(), 2);
}