using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GlowMe.Models;

[Table("Review", Schema = "glowme")]
public class Review
{
    [Key]
    [Column("ReviewID")]
    public int ReviewID { get; set; }

    [Column("BookingID")]
    public int BookingID { get; set; }

    [Column("AdminID")]
    public int? AdminID { get; set; }

    [Range(1, 5)]
    [Column("Rating")]
    public int Rating { get; set; }

    [MaxLength(800)]
    [Column("Comment")]
    public string? Comment { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("ReviewStatus")]
    public string ReviewStatus { get; set; } = "Visible";

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    public Booking? Booking { get; set; }

    public Admin? Admin { get; set; }
}