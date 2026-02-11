using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.FeeHead.Dtos;

public class FeeHeadResponse : EntityDto<Guid>
{
    public Guid AcademicYearId { get; set; }
    public Guid FeeCategoryId { get; set; }
    public string FeeCategoryName { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
