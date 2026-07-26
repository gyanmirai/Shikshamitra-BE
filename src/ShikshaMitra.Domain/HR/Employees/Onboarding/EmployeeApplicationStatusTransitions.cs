namespace ShikshaMitra.HR.Employees.Onboarding;

internal static class EmployeeApplicationStatusTransitions
{
	public static EmployeeApplicationStatus[] AllowedNextStatuses(
		EmployeeApplicationStatus current) =>
		current switch
		{
			EmployeeApplicationStatus.InvitationSent => [
				EmployeeApplicationStatus.Rejected,
				EmployeeApplicationStatus.Withdrawn
			],
			EmployeeApplicationStatus.Applied => [
				EmployeeApplicationStatus.Screening,
				EmployeeApplicationStatus.Interviewing,
				EmployeeApplicationStatus.Offered,
				EmployeeApplicationStatus.ReadyForOnboarding,
				EmployeeApplicationStatus.Rejected,
				EmployeeApplicationStatus.Withdrawn
			],
			EmployeeApplicationStatus.Screening => [
				EmployeeApplicationStatus.Interviewing,
				EmployeeApplicationStatus.Offered,
				EmployeeApplicationStatus.ReadyForOnboarding,
				EmployeeApplicationStatus.Rejected,
				EmployeeApplicationStatus.Withdrawn
			],
			EmployeeApplicationStatus.Interviewing => [
				EmployeeApplicationStatus.Offered,
				EmployeeApplicationStatus.ReadyForOnboarding,
				EmployeeApplicationStatus.Rejected,
				EmployeeApplicationStatus.Withdrawn
			],
			EmployeeApplicationStatus.Offered => [
				EmployeeApplicationStatus.ReadyForOnboarding,
				EmployeeApplicationStatus.Rejected,
				EmployeeApplicationStatus.Withdrawn
			],
			EmployeeApplicationStatus.ReadyForOnboarding => [
				EmployeeApplicationStatus.Rejected,
				EmployeeApplicationStatus.Withdrawn
			],
			_ => []
		};
}
