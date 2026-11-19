using System;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeHeadUpdateRequest
{
    public Guid FeeCategoryId { get; set; }
    public Guid IncomeCategoryId { get; set; }
    public string Name { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.
    public FeeHeadModule? Module { get; set; }
    public bool IsConcessionApplied { get; set; }
    public string Description { get; set; } = null!;
}
