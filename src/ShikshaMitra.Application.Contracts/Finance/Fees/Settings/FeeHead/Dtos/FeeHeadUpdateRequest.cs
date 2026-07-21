using System;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeHeadUpdateRequest
{
    public Guid FeeCategoryId { get; set; }
    public string Name { get; set; }
    public FeeHeadModule Module { get; set; }
    public bool IsConcessionApplied { get; set; }
    public string Description { get; set; }
}
