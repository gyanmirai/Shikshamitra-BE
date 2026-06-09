using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

internal static partial class HrPolicyMapping
{
	public static HrPolicyAssignmentTargetType ParseAssignmentTargetType(string value) => Normalize(value).Replace("-", "_") switch
	{
		"employee" or "" => HrPolicyAssignmentTargetType.Employee,
		"group" => HrPolicyAssignmentTargetType.Group,
		_ => throw new BusinessException(HRErrorCodes.HrPolicyAssignmentTargetTypeUnsupported)
	};

	public static string MapAssignmentTargetType(HrPolicyAssignmentTargetType value) => value switch
	{
		HrPolicyAssignmentTargetType.Group => "group",
		_ => "employee"
	};

	public static HrPolicyAssignmentDto ToDto(HrPolicyAssignment entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		TargetType = MapAssignmentTargetType(entity.TargetType),
		TargetKey = entity.TargetKey,
		TargetDisplayName = entity.TargetDisplayName,
		AttendancePolicyId = entity.AttendancePolicyId,
		PayrollPolicyId = entity.PayrollPolicyId,
		LeavePolicyId = entity.LeavePolicyId,
		EffectiveFrom = entity.EffectiveFrom,
		EffectiveTo = entity.EffectiveTo,
		IsActive = entity.IsActive,
		Priority = entity.Priority,
		AssignmentReason = entity.AssignmentReason,
		CreationTime = entity.CreationTime,
		CreatorId = entity.CreatorId,
		LastModificationTime = entity.LastModificationTime,
		LastModifierId = entity.LastModifierId
	};
}
