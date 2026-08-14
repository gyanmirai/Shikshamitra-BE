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
	public int Version { get; private set; }
	public AdmissionFormStatus Status { get; private set; }
	public AdmissionFormLayoutType LayoutType { get; private set; }
	public Guid? ParentVersionId { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public Guid? PublishedBy { get; private set; }
	protected AdmissionFormDefinition() { }
	public AdmissionFormDefinition(Guid id, Guid? tenantId, string name, string code, AdmissionFormLayoutType layoutType, int version = 1, Guid? parentVersionId = null) : base(id)
	{
		TenantId = tenantId;
		Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim();
		Code = NormalizeCode(code);
		LayoutType = layoutType;
		Version = version;
		ParentVersionId = parentVersionId;
		Status = AdmissionFormStatus.Draft;
	}
	public void UpdateDetails(string name, string code, AdmissionFormLayoutType layoutType)
	{
		if (Status != AdmissionFormStatus.Draft) throw new BusinessException(AdmissionFormErrorCodes.PublishedImmutable);
		Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim(); Code = NormalizeCode(code); LayoutType = layoutType;
	}
	public void Publish(Guid? userId) { if (Status != AdmissionFormStatus.Draft) throw new BusinessException(AdmissionFormErrorCodes.PublishedImmutable); Status = AdmissionFormStatus.Published; PublishedAt = DateTime.UtcNow; PublishedBy = userId; }
	public void Archive() { if (Status == AdmissionFormStatus.Draft) throw new BusinessException(AdmissionFormErrorCodes.PublishedImmutable); Status = AdmissionFormStatus.Archived; }
	private static string NormalizeCode(string code) => Check.NotNullOrWhiteSpace(code, nameof(code)).Trim().ToUpperInvariant();
}
