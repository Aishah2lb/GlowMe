using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GlowMe.Models;

[Table("Admin", Schema = "glowme")]
public class Admin
{
    [Key]
    [Column("AdminID")]
    public int AdminID { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("FullName")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    [Column("Email")]
    public string Email { get; set; } = string.Empty;

    [JsonIgnore]
    [Required]
    [MaxLength(255)]
    [Column("PasswordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("AdminRole")]
    public string AdminRole { get; set; } = "Moderator";

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Review> ModeratedReviews { get; set; } = new List<Review>();

    [NotMapped]
    public string AuthPassword => PasswordHash;

    public string FirstName() => FullName.Trim().Split(' ')[0].Trim();
}