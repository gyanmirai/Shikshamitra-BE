using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Policies;

public class AttendancePolicyImpactGuard(
	IHrPolicySafetyAppService policySafetyAppService,
	HrPolicyImpactConfirmationService confirmationService)
	: ApplicationService
{
	public Task EnsureUpdateConfirmedAsync(Guid policyId, Guid academicYearId, CreateUpdateAttendancePolicyDto input) =>
		EnsureConfirmedAsync(policyId, academicYearId, "update", input);

	public Task EnsureArchiveConfirmedAsync(Guid policyId, Guid academicYearId) =>
		EnsureConfirmedAsync(policyId, academicYearId, "archive");

	public Task EnsureSetDefaultConfirmedAsync(Guid policyId, Guid academicYearId) =>
		EnsureConfirmedAsync(policyId, academicYearId, "set-default");

	private async Task EnsureConfirmedAsync(
		Guid policyId,
		Guid academicYearId,
		string action,
		CreateUpdateAttendancePolicyDto? input = null)
	{
		var preview = await policySafetyAppService.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "attendance",
			PolicyId = policyId,
			AcademicYearId = academicYearId,
			Action = action,
			AttendancePolicy = input
		});
		await confirmationService.EnsureConfirmedAsync(preview);
	}
}
