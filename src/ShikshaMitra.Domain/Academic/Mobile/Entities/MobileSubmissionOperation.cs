using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Mobile;

/// <summary>A durable receipt. Inserted in the same transaction as its successful operation.</summary>
public class MobileSubmissionOperation : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ActorId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid TaskId { get; private set; }
	public string Operation { get; private set; } = "";
	public Guid RequestId { get; private set; }
	public string PayloadHash { get; private set; } = "";
	public string ResponseJson { get; private set; } = "";
	public DateTimeOffset CreatedAt { get; private set; }

	private MobileSubmissionOperation() { }
	public MobileSubmissionOperation(Guid id, Guid tenantId, Guid actorId, Guid studentId, Guid yearId,
		Guid taskId, string operation, Guid requestId, string hash, string response) : base(id)
	{
		TenantId = tenantId; ActorId = actorId; StudentId = studentId; AcademicYearId = yearId;
		TaskId = taskId; Operation = operation; RequestId = requestId; PayloadHash = hash;
		ResponseJson = response; CreatedAt = DateTimeOffset.UtcNow;
	}
}
