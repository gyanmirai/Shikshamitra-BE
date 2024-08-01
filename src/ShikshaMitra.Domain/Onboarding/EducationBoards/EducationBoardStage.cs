using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Onboarding.EducationBoards;

public class EducationBoardStage : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid EducationBoardId { get; set; }
    public string Name { get; set; }

    // Navigation property
    public EducationBoard EducationBoard { get; set; }

}
