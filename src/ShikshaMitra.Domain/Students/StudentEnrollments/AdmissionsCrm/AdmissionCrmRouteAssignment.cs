using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRouteAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid RouteDefinitionId { get; private set; }
	public int RouteDefinitionVersion { get; private set; }
	public AdmissionCrmRouteType RouteType { get; private set; }
	public AdmissionCrmRouteEligibilityStatus Status { get; private set; }
	public string RouteDataJson { get; private set; } = "{}";
	public string EvidenceJson { get; private set; } = "[]";
	public Guid? PreviousAssignmentId { get; private set; }
	public string AssignmentReason { get; private set; } = string.Empty;
	public bool IsPrimary { get; private set; }
	public Guid AssignedByUserId { get; private set; }
	public DateTime AssignedAt { get; private set; }
	public Guid? EvaluatedByUserId { get; private set; }
	public DateTime? EvaluatedAt { get; private set; }
	public string? EvaluationReason { get; private set; }

	protected AdmissionCrmRouteAssignment() { }

	public AdmissionCrmRouteAssignment(Guid id, Guid? tenantId, Guid academicYearId, Guid leadId,
		Guid routeDefinitionId, int routeDefinitionVersion, AdmissionCrmRouteType routeType,
		string routeDataJson, string evidenceJson, Guid? previousAssignmentId, string assignmentReason,
		Guid assignedByUserId, DateTime assignedAt) : base(id)
	{
		TenantId = tenantId; AcademicYearId = academicYearId; LeadId = leadId;
		RouteDefinitionId = routeDefinitionId; RouteDefinitionVersion = routeDefinitionVersion; RouteType = routeType;
		ValidateJson(routeDataJson, JsonValueKind.Object, "RouteData"); ValidateJson(evidenceJson, JsonValueKind.Array, "Evidence");
		RouteDataJson = routeDataJson; EvidenceJson = evidenceJson; PreviousAssignmentId = previousAssignmentId;
		AssignmentReason = Check.NotNullOrWhiteSpace(assignmentReason, nameof(assignmentReason), AdmissionCrmConsts.MaxNotesLength);
		AssignedByUserId = assignedByUserId; AssignedAt = assignedAt.ToUniversalTime();
		Status = AdmissionCrmRouteEligibilityStatus.PendingReview; IsPrimary = true;
	}

	public void Supersede()
	{
		IsPrimary = false; Status = AdmissionCrmRouteEligibilityStatus.Superseded;
	}

	public void Evaluate(AdmissionCrmRouteEligibilityStatus status, string reason, Guid userId, DateTime at)
	{
		if (status is AdmissionCrmRouteEligibilityStatus.PendingReview or AdmissionCrmRouteEligibilityStatus.Superseded)
			throw new BusinessException("AdmissionCrm:InvalidRouteEligibilityDecision");
		Status = status; EvaluationReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		EvaluatedByUserId = userId; EvaluatedAt = at.ToUniversalTime();
	}

	private static void ValidateJson(string value, JsonValueKind kind, string field)
	{
		using var document = JsonDocument.Parse(Check.NotNullOrWhiteSpace(value, field, AdmissionCrmConsts.MaxRouteDataJsonLength));
		if (document.RootElement.ValueKind != kind) throw new BusinessException("AdmissionCrm:InvalidRouteAssignmentJson").WithData("Field", field);
	}
}
