using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormDefinition : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; }
	public string Code { get; private set; }
	public string? Description { get; private set; }
	public int Version { get; private set; }
	public AdmissionFormStatus Status { get; private set; }
	public AdmissionFormLayoutType LayoutType { get; private set; }
	public AdmissionFormPurpose Purpose { get; private set; }
	public int FieldsPerRow { get; private set; }
	public Guid? ParentVersionId { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public Guid? PublishedBy { get; private set; }
	protected AdmissionFormDefinition()
	{
		// EF materializes these required columns after this ORM constructor.
		Code = null!;
		Name = null!;
	}
	public AdmissionFormDefinition(Guid id, Guid? tenantId, string name, string code, AdmissionFormLayoutType layoutType, int version = 1, Guid? parentVersionId = null, string? description = null, int fieldsPerRow = 1, AdmissionFormPurpose purpose = AdmissionFormPurpose.Registration) : base(id)
	{
		TenantId = tenantId;
		IsDeleted = false;
		Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim();
		Code = NormalizeCode(code);
		Description = NormalizeDescription(description);
		LayoutType = layoutType;
		Purpose = purpose;
		FieldsPerRow = NormalizeFieldsPerRow(fieldsPerRow);
		Version = version;
		ParentVersionId = parentVersionId;
		Status = AdmissionFormStatus.Draft;
	}
	public void UpdateDetails(string name, string code, AdmissionFormLayoutType layoutType, string? description = null, int fieldsPerRow = 1, AdmissionFormPurpose purpose = AdmissionFormPurpose.Registration)
	{
		if (Status != AdmissionFormStatus.Draft) throw new BusinessException(AdmissionFormErrorCodes.PublishedImmutable);
		Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim(); Code = NormalizeCode(code); LayoutType = layoutType; Purpose = purpose; Description = NormalizeDescription(description); FieldsPerRow = NormalizeFieldsPerRow(fieldsPerRow);
	}
	public void Publish(Guid? userId) { if (Status != AdmissionFormStatus.Draft) throw new BusinessException(AdmissionFormErrorCodes.PublishedImmutable); Status = AdmissionFormStatus.Published; PublishedAt = DateTime.UtcNow; PublishedBy = userId; }
	public void Archive() { if (Status == AdmissionFormStatus.Draft) throw new BusinessException(AdmissionFormErrorCodes.PublishedImmutable); Status = AdmissionFormStatus.Archived; }
	private static string NormalizeCode(string code) => Check.NotNullOrWhiteSpace(code, nameof(code)).Trim().ToUpperInvariant();
	private static string? NormalizeDescription(string? description) => string.IsNullOrWhiteSpace(description) ? null : Check.Length(description.Trim(), nameof(description), 1024);
	private static int NormalizeFieldsPerRow(int fieldsPerRow) => Check.Range(fieldsPerRow, nameof(fieldsPerRow), 1, 4);
}
