using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowDefinition : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; }
	public string Code { get; private set; }
	public int Version { get; private set; }
	public AdmissionWorkflowStatus Status { get; private set; }
	public Guid? ParentVersionId { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public Guid? PublishedBy { get; private set; }

	protected AdmissionWorkflowDefinition() { }

	public AdmissionWorkflowDefinition(Guid id, Guid? tenantId, string name, string code, int version = 1, Guid? parentVersionId = null) : base(id)
	{
		TenantId = tenantId;
		Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim();
		Code = NormalizeCode(code);
		Version = version;
		ParentVersionId = parentVersionId;
		Status = AdmissionWorkflowStatus.Draft;
	}

	public void UpdateDetails(string name, string code)
	{
		if (Status != AdmissionWorkflowStatus.Draft) throw new BusinessException(AdmissionWorkflowErrorCodes.PublishedImmutable);
		Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim();
		Code = NormalizeCode(code);
	}

	public void Publish(Guid? publishedBy)
	{
		if (Status != AdmissionWorkflowStatus.Draft) throw new BusinessException(AdmissionWorkflowErrorCodes.PublishedImmutable);
		Status = AdmissionWorkflowStatus.Published;
		PublishedAt = DateTime.UtcNow;
		PublishedBy = publishedBy;
	}

	public void Archive()
	{
		if (Status == AdmissionWorkflowStatus.Draft) throw new BusinessException(AdmissionWorkflowErrorCodes.PublishedImmutable);
		Status = AdmissionWorkflowStatus.Archived;
	}

	private static string NormalizeCode(string code) => Check.NotNullOrWhiteSpace(code, nameof(code)).Trim().ToUpperInvariant();
}
