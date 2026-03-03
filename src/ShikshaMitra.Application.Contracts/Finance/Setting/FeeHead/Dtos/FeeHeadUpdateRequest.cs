using System;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.FeeHead.Dtos;

public class FeeHeadUpdateRequest
{
    public Guid FeeCategoryId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}
