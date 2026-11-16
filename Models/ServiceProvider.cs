using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using GlowMe.Support;

namespace GlowMe.Models;

[Table("ServiceProvider", Schema = "glowme")]
public class ServiceProvider
{
    [Key]
    [Column("ProviderID")]
    public int ProviderID { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("FullName")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("BusinessName")]
    public string BusinessName { get; set; } = string.Empty;

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

    [MaxLength(600)]
    [Column("Bio")]
    public string? Bio { get; set; }

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

    [Column("OffersHomeService")]
    public bool OffersHomeService { get; set; }

    [Column("AverageRating", TypeName = "decimal(2,1)")]
    public decimal? AverageRating { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("AccountStatus")]
    public string AccountStatus { get; set; } = "Pending";

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [MaxLength(500)]
    [Column("ProfilePhotoURL")]
    public string? ProfilePhotoURL { get; set; }

    [MaxLength(500)]
    [Column("LicenseURL")]
    public string? LicenseURL { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("LicenseStatus")]
    public string LicenseStatus { get; set; } = "Pending";

    public ICollection<Service> Services { get; set; } = new List<Service>();

    public ICollection<Portfolio> Portfolio { get; set; } = new List<Portfolio>();

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public ICollection<Availability> Availability { get; set; } = new List<Availability>();

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public ICollection<Message> Messages { get; set; } = new List<Message>();

    public ICollection<Favorite> FavoritedBy { get; set; } = new List<Favorite>();

    [NotMapped]
    public string AuthPassword => PasswordHash;

    public IEnumerable<Service> ActiveServices() => Services.Where(s => s.IsActive);

    public static IQueryable<ServiceProvider> Bookable(IQueryable<ServiceProvider> query)
    {
        var today = DateTime.Today;
        return query.Where(p => p.AccountStatus == "Active"
            && p.Subscriptions.Any(s => s.Status == "Active" && s.EndDate >= today));
    }

    public int CompletedBookingsCount() =>
        Bookings.Count(b => b.BookingStatus == "Completed");

    public decimal? MinActivePrice()
    {
        var active = ActiveServices().ToList();
        return active.Count > 0 ? active.Min(s => s.Price) : null;
    }

    public int VisibleReviewCount() =>
        Bookings.Count(b => b.Review != null && b.Review.ReviewStatus == "Visible");

    public IEnumerable<Portfolio> NewestPortfolio(int n) =>
        Portfolio.OrderByDescending(p => p.UploadedAt).Take(n);

    public double? DistanceFrom(double? lat, double? lng)
    {
        if (lat is null || lng is null || Latitude is null || Longitude is null)
        {
            return null;
        }

        const double r = 6371;
        var dLat = DegToRad((double)Latitude - lat.Value);
        var dLng = DegToRad((double)Longitude - lng.Value);
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
                + Math.Cos(DegToRad(lat.Value)) * Math.Cos(DegToRad((double)Latitude)) * Math.Pow(Math.Sin(dLng / 2), 2);
        return Math.Round(r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)), 1);
    }

    private static double DegToRad(double deg) => deg * Math.PI / 180.0;

    public string? District() =>
        !string.IsNullOrEmpty(Address) ? Address.Split(',')[0].Trim() : City;

    public string? CardImage()
    {
        var photo = Portfolio.OrderByDescending(p => p.UploadedAt).FirstOrDefault();
        if (photo is not null)
        {
            return photo.Url();
        }

        var cat = ActiveCategoryNames().FirstOrDefault();
        var file = cat is not null
            ? (GlowMeOptions.CategoryImages.TryGetValue(cat, out var f) ? f : "makeup.jpeg")
            : "makeup.jpeg";
        return "~/img/categories/" + file;
    }

    public Subscription? ActiveSubscription() =>
        Subscriptions.Where(s => s.Status == "Active").OrderByDescending(s => s.StartDate).FirstOrDefault();

    public bool IsBookable() => AccountStatus == "Active" && ActiveSubscription() is not null;

    public string FirstName() => FullName.Trim().Split(' ')[0].Trim();

    public IEnumerable<string> ActiveCategoryNames() =>
        ActiveServices()
            .Select(s => s.Category?.CategoryName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .Distinct();

    public static bool SupportsLicense(Data.AppDbContext db) =>
        Support.SchemaProbe.ColumnExists(db, "glowme.ServiceProvider", "LicenseURL");

    public string? LicenseUrl() => Img.Url(LicenseURL);
}