using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GlowMe.Models;

[Table("BookingService", Schema = "glowme")]
public class BookingService
{
    [Column("BookingID")]
    public int BookingID { get; set; }

    [Column("ServiceID")]
    public int ServiceID { get; set; }

    [Column("Quantity")]
    public int Quantity { get; set; } = 1;

    [Column("PriceAtBooking", TypeName = "decimal(8,2)")]
    public decimal PriceAtBooking { get; set; }

    public Booking? Booking { get; set; }
    public Service? Service { get; set; }
}