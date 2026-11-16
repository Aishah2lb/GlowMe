using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GlowMe.Models;

[Table("Availability", Schema = "glowme")]
public class Availability
{
    [Key]
    [Column("AvailabilityID")]
    public int AvailabilityID { get; set; }

    [Column("ProviderID")]
    public int ProviderID { get; set; }

    [Column("AvailableDate")]
    public DateTime AvailableDate { get; set; }

    [Column("StartTime")]
    public TimeSpan StartTime { get; set; }

    [Column("EndTime")]
    public TimeSpan EndTime { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("SlotStatus")]
    public string SlotStatus { get; set; } = "Open";

    public ServiceProvider? Provider { get; set; }
}