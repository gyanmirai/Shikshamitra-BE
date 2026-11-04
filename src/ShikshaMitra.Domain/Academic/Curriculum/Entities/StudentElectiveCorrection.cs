using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public class StudentElectiveCorrection : CreationAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid ElectiveGroupId { get; private set; }
	public Guid ChangedByUserId { get; private set; }
	public string BeforeJson { get; private set; } = "";
	public string AfterJson { get; private set; } = "";
	public string ImpactsJson { get; private set; } = "";
	public string? Reason { get; private set; }
	private StudentElectiveCorrection() { }
	public StudentElectiveCorrection(Guid id, Guid tenantId, Guid studentId, Guid groupId, Guid userId,
		string beforeJson, string afterJson, string impactsJson, string? reason) : base(id)
	{
		TenantId = tenantId; StudentId = studentId; ElectiveGroupId = groupId; ChangedByUserId = userId;
		BeforeJson = beforeJson; AfterJson = afterJson; ImpactsJson = impactsJson; Reason = reason;
	}
}
