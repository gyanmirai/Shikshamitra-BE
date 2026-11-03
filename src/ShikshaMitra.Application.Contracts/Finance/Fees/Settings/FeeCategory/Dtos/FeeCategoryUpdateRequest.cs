using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeCategoryUpdateRequest
{
    [Required]
    public string Name { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

    public string Description { get; set; } = null!;

    public List<FeeHeadItemRequest> FeeHeads { get; set; } = new();
}
