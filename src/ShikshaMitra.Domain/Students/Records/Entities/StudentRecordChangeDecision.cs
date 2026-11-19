using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

public class StudentRecordChangeDecision : CreationAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ChangeId { get; private set; }
	public Guid ActorId { get; private set; }
	public int Stage { get; private set; }
	public string Decision { get; private set; } = string.Empty;
	public string Reason { get; private set; } = string.Empty;
	protected StudentRecordChangeDecision() { }
	public StudentRecordChangeDecision(Guid id, Guid? tenantId, Guid changeId, Guid actor, int stage,
		string decision, string reason) : base(id)
	{
		TenantId = tenantId; ChangeId = changeId; ActorId = actor; Stage = stage;
		Decision = Check.NotNullOrWhiteSpace(decision, nameof(decision), 20);
		Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 2000).Trim();
	}
}
