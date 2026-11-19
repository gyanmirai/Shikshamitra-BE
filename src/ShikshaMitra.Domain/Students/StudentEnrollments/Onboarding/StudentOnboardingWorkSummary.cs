using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public static class StudentOnboardingWorkSummary
{
	public static string? PrimaryBlocker(IEnumerable<StudentOnboardingStep> steps, IEnumerable<StudentOnboardingServiceHandoff> requiredHandoffs)
	{
		var pending = steps.Where(step => step.Key != StudentOnboardingStepKeys.FinalReadiness && step.Required &&
			step.Status is not (StudentOnboardingStepStatus.Completed or StudentOnboardingStepStatus.Waived or StudentOnboardingStepStatus.NotRequired)).ToList();
		var label = pending.OrderBy(step => step.Status == StudentOnboardingStepStatus.Blocked ? 0 : 1).ThenBy(step => step.DisplayOrder).FirstOrDefault()?.Label;
		return label ?? (requiredHandoffs.FirstOrDefault(h => h.Status != StudentOnboardingServiceHandoffStatus.Acknowledged)?.ServiceKey switch
		{
			"finance-first-payment" => "Fee assignment and first payment",
			"student-account" => "Student portal account",
			"guardian-account" => "Guardian portal account",
			null => null,
			_ => "Required service setup"
		});
	}
}
