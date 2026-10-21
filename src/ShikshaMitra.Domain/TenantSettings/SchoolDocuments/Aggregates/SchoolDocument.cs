using System;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.SchoolDocuments;

public class SchoolDocument : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; } = "";
	public SchoolDocumentCategory Category { get; private set; }
	public string? Notes { get; private set; }
	public string UploadedBy { get; private set; } = "";

	protected SchoolDocument() { }

	public SchoolDocument(Guid id, Guid tenantId, string name, SchoolDocumentCategory category, string? notes, string uploadedBy) : base(id)
	{
		if (tenantId == Guid.Empty) throw new BusinessException(SchoolDocumentConsts.InvalidDetails);
		TenantId = tenantId;
		UploadedBy = uploadedBy;
		SetDetails(name, category, notes);
	}

	public void UpdateDetails(string name, SchoolDocumentCategory category, string? notes, string expectedStamp)
	{
		if (ConcurrencyStamp != expectedStamp) throw new AbpDbConcurrencyException();
		SetDetails(name, category, notes);
	}

	private void SetDetails(string name, SchoolDocumentCategory category, string? notes)
	{
		if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > SchoolDocumentConsts.MaxNameLength ||
			!Enum.IsDefined(category) || notes?.Length > SchoolDocumentConsts.MaxNotesLength)
			throw new BusinessException(SchoolDocumentConsts.InvalidDetails);
		Name = name.Trim();
		Category = category;
		Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
	}
}
