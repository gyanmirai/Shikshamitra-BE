using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp.DependencyInjection;
using static ShikshaMitra.HR.Policies.HrPolicyAssignmentProjection;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAssignmentAuditWriter(HrAuditWriter auditWriter) : ITransientDependency
{
	public Task WriteCreatedAsync(HrPolicyAssignment entity) =>
		auditWriter.WriteAsync(
			entity.AcademicYearId,
			"policy_assignment",
			entity.Id,
			BuildAssignmentAuditName(entity),
			entity.IsActive ? "assigned" : "unassigned",
			$"Created {HrPolicyMapping.MapAssignmentTargetType(entity.TargetType)} policy assignment for {entity.TargetDisplayName ?? entity.TargetKey}.",
			entity.AssignmentReason,
			context: BuildAssignmentAuditContext(entity));

	public Task WriteUpdatedAsync(HrPolicyAssignment entity, HrPolicyAssignmentDto before, HrPolicyAssignmentDto dto) =>
		auditWriter.WriteAsync(
			entity.AcademicYearId,
			"policy_assignment",
			entity.Id,
			BuildAssignmentAuditName(entity),
			entity.IsActive ? "assigned" : "unassigned",
			$"Updated {HrPolicyMapping.MapAssignmentTargetType(entity.TargetType)} policy assignment for {entity.TargetDisplayName ?? entity.TargetKey}.",
			entity.AssignmentReason,
			HrAuditWriter.Diff(before, dto, "lastModificationTime", "lastModifierId"),
			BuildAssignmentAuditContext(entity));

	public Task WriteEmployeeOverrideAsync(HrPolicyAssignment entity, HrPolicyAssignmentDto before, HrPolicyAssignmentDto dto, string employeeName) =>
		auditWriter.WriteAsync(
			entity.AcademicYearId,
			"policy_assignment",
			entity.Id,
			BuildAssignmentAuditName(entity),
			"assigned",
			$"Upserted direct employee policy override for {employeeName}.",
			entity.AssignmentReason,
			HrAuditWriter.Diff(before, dto, "lastModificationTime", "lastModifierId"),
			BuildAssignmentAuditContext(entity));

	public Task WriteDeletedAsync(HrPolicyAssignment entity, HrPolicyAssignmentDto before) =>
		auditWriter.WriteAsync(
			entity.AcademicYearId,
			"policy_assignment",
			entity.Id,
			BuildAssignmentAuditName(entity),
			"unassigned",
			$"Deactivated {HrPolicyMapping.MapAssignmentTargetType(entity.TargetType)} policy assignment for {entity.TargetDisplayName ?? entity.TargetKey}.",
			entity.AssignmentReason,
			HrAuditWriter.Diff(before, HrPolicyMapping.ToDto(entity), "lastModificationTime", "lastModifierId"),
			BuildAssignmentAuditContext(entity));
}
