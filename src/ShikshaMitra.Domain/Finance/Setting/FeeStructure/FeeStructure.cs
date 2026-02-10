using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Setting.FeeStructure;

public class FeeStructure : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid AcademicYearId { get; private set; }
    public Guid BoardId { get; private set; }
    public Guid MediumId { get; private set; }
    public Guid ReceiptBookId { get; private set; }

    [Required]
    public string Name { get; private set; }

    public int Status { get; private set; } // 0: DRAFT (or similar), 1: PUBLISHED - using int as per doc

    public ICollection<FeeStructureStandard> FeeStructureStandards { get; private set; }

    protected FeeStructure() 
    {
        FeeStructureStandards = new List<FeeStructureStandard>();
    }

    public FeeStructure(
        Guid id, 
        Guid? tenantId, 
        Guid academicYearId, 
        Guid boardId, 
        Guid mediumId, 
        Guid receiptBookId, 
        string name, 
        int status = 0) : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        BoardId = boardId;
        MediumId = mediumId;
        ReceiptBookId = receiptBookId;
        Name = name;
        Status = status;
        FeeStructureStandards = new List<FeeStructureStandard>();
    }

    public void SetStatus(int status)
    {
        Status = status;
    }
}
