using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GlowMe.Data;
using GlowMe.Support;

namespace GlowMe.Models;

[Table("Category", Schema = "glowme")]
public class Category
{
    [Key]
    [Column("CategoryID")]
    public int CategoryID { get; set; }

    [Column("AdminID")]
    public int AdminID { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("CategoryName")]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(300)]
    [Column("Description")]
    public string? Description { get; set; }

    [MaxLength(500)]
    [Column("ImageURL")]
    public string? ImageURL { get; set; }

    public Admin? Admin { get; set; }

    public ICollection<Service> Services { get; set; } = new List<Service>();

    public string Image() =>
        GlowMeOptions.CategoryImages.TryGetValue(CategoryName, out var f) ? f : "makeup.jpeg";

    public string PictureUrl()
    {
        var v = ImageURL;
        if (!string.IsNullOrWhiteSpace(v))
        {
            return Img.Url(v)!;
        }
        return "~/img/categories/" + Image();
    }

    public static bool SupportsImage(AppDbContext db) =>
        SchemaProbe.ColumnExists(db, "glowme.Category", "ImageURL");
}