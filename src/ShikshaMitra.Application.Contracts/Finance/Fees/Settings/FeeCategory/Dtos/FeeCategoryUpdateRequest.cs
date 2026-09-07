using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeCategoryUpdateRequest
{
    [Required]
    public string Name { get; set; }

    public string Description { get; set; }

    public string ConcurrencyStamp { get; set; }
}
