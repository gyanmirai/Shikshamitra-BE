using System.Collections.Generic;

namespace ShikshaMitra.HR.Policies;

internal sealed record HrPolicyAssignmentResolution(
	HrPolicyAssignment? Assignment,
	IReadOnlyList<HrPolicyResolutionStep> Steps);
