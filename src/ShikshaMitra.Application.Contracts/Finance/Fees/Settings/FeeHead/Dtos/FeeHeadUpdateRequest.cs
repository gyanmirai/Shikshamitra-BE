using System;

namespace ShikshaMitra.Finance.Fees.Settings.FeeHead.Dtos;

public class FeeHeadUpdateRequest
{
    public Guid FeeCategoryId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}
