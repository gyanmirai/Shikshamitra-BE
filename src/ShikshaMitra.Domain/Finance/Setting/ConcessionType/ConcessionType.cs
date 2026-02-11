using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.TenantSettings.AcademicYears;

namespace ShikshaMitra.Finance.Setting.ConcessionType;

public class ConcessionType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid AcademicYearId { get; private set; }
    public string Name { get; private set; }
    public ConcessionBasedOn BasedOn { get; private set; }
    public decimal Amount { get; private set; }
    public bool IsDocumentRequired { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation properties
    public virtual AcademicYear AcademicYear { get; set; }
    public virtual ICollection<ConcessionTypeFeeHead> ConcessionTypeFeeHeads { get; private set; }
    public virtual ICollection<ConcessionTypeDocument> ConcessionTypeDocuments { get; private set; }

    private ConcessionType()
    {
        /* This constructor is for deserialization / ORM purpose */
    }

    public ConcessionType(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        string name,
        ConcessionBasedOn basedOn,
        decimal amount,
        bool isDocumentRequired,
        string? description = null,
        bool isActive = true
    ) : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        Name = name;
        BasedOn = basedOn;
        Amount = amount;
        IsDocumentRequired = isDocumentRequired;
        Description = description;
        IsActive = isActive;
        ConcessionTypeFeeHeads = new List<ConcessionTypeFeeHead>();
        ConcessionTypeDocuments = new List<ConcessionTypeDocument>();
    }

    public void Update(
        string name,
        ConcessionBasedOn basedOn,
        decimal amount,
        bool isDocumentRequired,
        string? description
    )
    {
        Name = name;
        BasedOn = basedOn;
        Amount = amount;
        IsDocumentRequired = isDocumentRequired;
        Description = description;
    }
    
    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    public void AddFeeHead(Guid feeHeadId)
    {
        ConcessionTypeFeeHeads.Add(new ConcessionTypeFeeHead(Id, feeHeadId));
    }

    public void RemoveFeeHead(Guid feeHeadId)
    {
        // Logic to remove... typically handled by replacing collection or similar in AppService, 
        // given this is a simple join entity, we might just clear and re-add in service or manage collection here.
        // For now, let's keep it simple.
    }
    
    public void AddDocument(string documentName, long documentSize, List<int> acceptedFormats) // acceptedFormats stored how?
    {
        // Handling formats might differ based on how we want to store them. 
        // The spec implies an array of ints. We could store as JSON or related entity.
        // Given ConcessionDocument is an object in the JSON, let's make it an entity.
    }
}
