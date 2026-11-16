using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GlowMe.Support;

namespace GlowMe.Models;

[Table("Portfolio", Schema = "glowme")]
public class Portfolio
{
    [Key]
    [Column("PortfolioID")]
    public int PortfolioID { get; set; }

    [Column("ProviderID")]
    public int ProviderID { get; set; }

    [Required]
    [MaxLength(500)]
    [Column("PhotoURL")]
    public string PhotoURL { get; set; } = string.Empty;

    [MaxLength(120)]
    [Column("Caption")]
    public string? Caption { get; set; }

    [Column("UploadedAt")]
    public DateTime UploadedAt { get; set; }

    public ServiceProvider? Provider { get; set; }

    public string Url() => Img.Url(PhotoURL) ?? string.Empty;
}