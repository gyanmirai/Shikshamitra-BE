using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

public sealed class StudentRecordTransitionEvent : CreationAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid TransitionId { get; private set; }
	public Guid ActorId { get; private set; }
	public string Action { get; private set; } = "";
	public int Stage { get; private set; }
	public string Remarks { get; private set; } = "";
	private StudentRecordTransitionEvent() { }
	public StudentRecordTransitionEvent(Guid id, Guid tenantId, Guid transitionId, Guid actorId, string action, int stage, string remarks) : base(id)
	{ TenantId = tenantId; TransitionId = transitionId; ActorId = actorId; Action = action; Stage = stage; Remarks = remarks; }
}
