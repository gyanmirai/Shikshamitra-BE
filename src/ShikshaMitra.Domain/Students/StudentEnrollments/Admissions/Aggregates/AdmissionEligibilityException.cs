using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

// A request and its eventual decision are separate immutable records.
public class AdmissionEligibilityException : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid StageInstanceId { get; private set; }
	public Guid EvaluationId { get; private set; }
	public Guid? RequestId { get; private set; }
	public string Decision { get; private set; } = string.Empty;
	public string Reason { get; private set; } = string.Empty;
	public Guid ActorId { get; private set; }
	public DateTime OccurredAt { get; private set; }
	protected AdmissionEligibilityException() { }
	public AdmissionEligibilityException(Guid id, Guid? tenantId, AdmissionEligibilityEvaluation evaluation, string reason,
		Guid actor, DateTime at, AdmissionEligibilityException? request = null, string decision = "pending", bool independent = true) : base(id)
	{
		if (evaluation.Outcome != "ineligible" || decision is not ("pending" or "approved" or "rejected") ||
			(decision == "pending") != (request == null) || request != null && (request.Decision != "pending" || request.EvaluationId != evaluation.Id || independent && request.ActorId == actor))
			throw new BusinessException(AdmissionEligibilityErrorCodes.ExceptionNotAllowed);
		var validatedReason = Check.Length(Check.NotNullOrWhiteSpace(reason, nameof(reason)).Trim(), nameof(reason), 1024)!;
		TenantId = tenantId; ApplicationId = evaluation.ApplicationId; StageInstanceId = evaluation.StageInstanceId;
		EvaluationId = evaluation.Id; RequestId = request?.Id; Decision = decision; Reason = validatedReason;
		ActorId = Check.NotDefaultOrNull<Guid>(actor, nameof(actor)); OccurredAt = at;
	}
}
