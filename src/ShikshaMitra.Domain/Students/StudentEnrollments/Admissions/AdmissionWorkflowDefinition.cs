using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowDefinition : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string Code { get; private set; } = string.Empty;
	public int Version { get; private set; }
	public AdmissionWorkflowStatus Status { get; private set; }
	public Guid? ParentVersionId { get; private set; }
	public string? Description { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public Guid? PublishedBy { get; private set; }

	protected AdmissionWorkflowDefinition()
	{
		// Keep the invariant true for every construction path, including the
		// protected path used by serializers/ORM tooling.
		IsDeleted = false;
	}

	public AdmissionWorkflowDefinition(
		Guid id,
		Guid? tenantId,
		string name,
		string code,
		int version = 1,
		Guid? parentVersionId = null,
		string? description = null) : base(id)
	{
		TenantId = tenantId;
		// FullAuditedAggregateRoot does not initialize the soft-delete flag in
		// every ABP persistence path. Set it explicitly so new definitions never
		// send NULL to the non-nullable PostgreSQL column.
		IsDeleted = false;
		SetDetails(name, code, description);
		Version = version;
		ParentVersionId = parentVersionId;
		Status = AdmissionWorkflowStatus.Draft;
		AddLocalEvent(new AdmissionWorkflowCreatedEvent(Id, TenantId));
	}

	public void UpdateDetails(string name, string code, string? description = null)
	{
		EnsureDraft();
		SetDetails(name, code, description);
	}

	public void RecordVersionCreated(Guid sourceWorkflowDefinitionId) =>
		AddLocalEvent(new AdmissionWorkflowVersionCreatedEvent(sourceWorkflowDefinitionId, Id, Version, TenantId));

	public void Publish(Guid? publishedBy)
	{
		EnsureDraft();
		Status = AdmissionWorkflowStatus.Published;
		PublishedAt = DateTime.UtcNow;
		PublishedBy = publishedBy;
		AddLocalEvent(new AdmissionWorkflowPublishedEvent(Id, Version, TenantId));
	}

	public void Archive()
	{
		if (Status != AdmissionWorkflowStatus.Published)
		{
			throw new BusinessException(AdmissionWorkflowErrorCodes.PublishedImmutable);
		}

		Status = AdmissionWorkflowStatus.Archived;
		AddLocalEvent(new AdmissionWorkflowArchivedEvent(Id, Version, TenantId));
	}

	public void EnsureDraft()
	{
		if (Status != AdmissionWorkflowStatus.Draft)
		{
			throw new BusinessException(AdmissionWorkflowErrorCodes.PublishedImmutable);
		}
	}

	private void SetDetails(string name, string code, string? description)
	{
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionWorkflowConsts.MaxNameLength).Trim();
		Code = NormalizeCode(code);
		Description = string.IsNullOrWhiteSpace(description)
			? null
			: Check.Length(description.Trim(), nameof(description), AdmissionWorkflowConsts.MaxDescriptionLength);
	}

	private static string NormalizeCode(string code) => Check.NotNullOrWhiteSpace(code, nameof(code)).Trim().ToUpperInvariant();
}
