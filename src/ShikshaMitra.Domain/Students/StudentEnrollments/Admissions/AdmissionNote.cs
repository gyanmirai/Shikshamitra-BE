using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionNote : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; } public Guid ApplicantId { get; private set; } public string Text { get; private set; } public DateTime? LastEditedAt { get; private set; }
	protected AdmissionNote()
	{
		// EF materializes these required columns after this ORM constructor.
		Text = null!;
	}
	public AdmissionNote(Guid id, Guid? tenantId, Guid applicantId, string text) : base(id) { TenantId = tenantId; ApplicantId = applicantId; Text = Check.NotNullOrWhiteSpace(text, nameof(text)).Trim(); }
	public void UpdateText(string text) { Text = Check.NotNullOrWhiteSpace(text, nameof(text)).Trim(); LastEditedAt = DateTime.UtcNow; }
}
