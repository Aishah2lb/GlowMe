using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GlowMe.Models;

[Table("Customer", Schema = "glowme")]
public class Customer
{
    [Key]
    [Column("CustomerID")]
    public int CustomerID { get; set; }

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
    [MaxLength(10)]
    [Column("PhoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("City")]
    public string City { get; set; } = string.Empty;

    [MaxLength(250)]
    [Column("Address")]
    public string? Address { get; set; }

    [Column("Latitude", TypeName = "decimal(9,6)")]
    public decimal? Latitude { get; set; }

    [Column("Longitude", TypeName = "decimal(9,6)")]
    public decimal? Longitude { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("AccountStatus")]
    public string AccountStatus { get; set; } = "Pending";

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [MaxLength(500)]
    [Column("ProfilePhotoURL")]
    public string? ProfilePhotoURL { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public ICollection<Message> Messages { get; set; } = new List<Message>();

    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    [NotMapped]
    public string AuthPassword => PasswordHash;

    public string FirstName() => FullName.Trim().Split(' ')[0].Trim();

    public string? District() =>
        !string.IsNullOrEmpty(Address) ? Address.Split(',')[0].Trim() : City;
}