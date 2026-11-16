using System.ComponentModel.DataAnnotations.Schema;

namespace GlowMe.Models;

[Table("Favorite", Schema = "glowme")]
public class Favorite
{
    [Column("CustomerID")]
    public int CustomerID { get; set; }

    [Column("ProviderID")]
    public int ProviderID { get; set; }

    [Column("AddedAt")]
    public DateTime AddedAt { get; set; }

    public Customer? Customer { get; set; }
    public ServiceProvider? Provider { get; set; }
}