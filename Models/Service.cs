using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GlowMe.Models;

[Table("Service", Schema = "glowme")]
public class Service
{
    [Key]
    [Column("ServiceID")]
    public int ServiceID { get; set; }

    [Column("ProviderID")]
    public int ProviderID { get; set; }

    [Column("CategoryID")]
    public int CategoryID { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("ServiceName")]
    public string ServiceName { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("Description")]
    public string? Description { get; set; }

    [Column("Price", TypeName = "decimal(8,2)")]
    public decimal Price { get; set; }

    [Column("DurationMinutes")]
    public int DurationMinutes { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    public ServiceProvider? Provider { get; set; }

    public Category? Category { get; set; }

    public ICollection<BookingService> BookingServices { get; set; } = new List<BookingService>();
}