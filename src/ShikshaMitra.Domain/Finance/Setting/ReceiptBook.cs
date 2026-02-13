using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Setting;

public class ReceiptBook : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid AcademicYearId { get; private set; }
	public AcademicYear AcademicYear { get; }


	public Guid BoardId { get; private set; }
	public EducationBoard EducationBoard { get; }

	public Guid MediumId { get; private set; }
	public EducationMedium EducationMedium { get; }
	public Guid MediumStageId { get; private set; }
	public EducationMediumStage EducationMediumStage { get; }

    [Required]
    public string Name { get; private set; }
    public string Prefix { get; private set; }
    public string Suffix { get; private set; }
    
    public long StartingNumber { get; private set; }
    public long EndingNumber { get; private set; }
    
    public string Description { get; private set; }

    public bool IsActive { get; private set; }

    private ReceiptBook() { }

    public ReceiptBook(
        Guid id, 
        Guid? tenantId, 
        Guid academicYearId, 
        Guid boardId, 
        Guid mediumId, 
        Guid mediumStageId, 
        string name, 
        string prefix, 
        string suffix, 
        long startingNumber, 
        long endingNumber, 
        string description, 
        bool isActive = true) : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        BoardId = boardId;
        MediumId = mediumId;
        MediumStageId = mediumStageId;
        Name = name;
        Prefix = prefix;
        Suffix = suffix;
        StartingNumber = startingNumber;
        EndingNumber = endingNumber;
        Description = description;
        IsActive = isActive;
    }

    public void Update(
        Guid boardId,
        Guid mediumId,
        Guid mediumStageId,
        string name,
        string prefix,
        string suffix,
        long startingNumber,
        long endingNumber,
        string description)
    {
        BoardId = boardId;
        MediumId = mediumId;
        MediumStageId = mediumStageId;
        Name = name;
        Prefix = prefix;
        Suffix = suffix;
        StartingNumber = startingNumber;
        EndingNumber = endingNumber;
        Description = description;
    }

    public void SetActiveStatus(bool isActive)
    {
        IsActive = isActive;
    }
}
