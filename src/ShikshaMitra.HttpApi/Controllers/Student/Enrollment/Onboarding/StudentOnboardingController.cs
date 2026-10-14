using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

[ApiController]
[Authorize]
[Route("api/student/onboarding")]
public class StudentOnboardingController(IStudentOnboardingAppService service) : AbpController
{
	[HttpPost("{id:guid}/requirements/review")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public async Task<Result<StudentOnboardingCaseDto>> ReviewRequirementsAsync(Guid id, [FromQuery] string? concurrencyStamp) => Result.Success(await service.ReviewRequirementsAsync(id, concurrencyStamp));
	[HttpPost("{id:guid}/gr-number")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	public async Task<Result<StudentOnboardingCaseDto>> AssignGrNumberAsync(Guid id, AssignStudentOnboardingGrNumberDto input) => Result.Success(await service.AssignGrNumberAsync(id, input));

	[HttpPost("{id:guid}/roll-number")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	public async Task<Result<StudentOnboardingCaseDto>> AssignRollNumberAsync(Guid id, AssignStudentOnboardingRollNumberDto input) => Result.Success(await service.AssignRollNumberAsync(id, input));

	[HttpPost("identifier-locks")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	public async Task<Result> SetIdentifierLockAsync(SetStudentIdentifierLockDto input)
	{
		await service.SetIdentifierLockAsync(input); return Result.Success();
	}

	[HttpGet("{id:guid}/fee-structure")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<Result<Admissions.AdmissionFeeSelectionDto>> GetFeeStructureAsync(Guid id) => Result.Success(await service.GetFeeStructureAsync(id));

	[HttpGet("{id:guid}/fee-structure/candidates")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<Result<List<Admissions.AdmissionFeeStructureCandidateDto>>> GetFeeStructureCandidatesAsync(Guid id) => Result.Success(await service.GetFeeStructureCandidatesAsync(id));

	[HttpPut("{id:guid}/fee-structure")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public async Task<Result<Admissions.AdmissionFeeSelectionDto>> SaveFeeStructureAsync(Guid id, SaveStudentOnboardingFeeStructureDto input) => Result.Success(await service.SaveFeeStructureAsync(id, input));
	[HttpGet("{id:guid}/portal-accounts")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<Result<IReadOnlyList<StudentPortalAccountDto>>> GetPortalAccountsAsync(Guid id) =>
		Result.Success(await service.GetPortalAccountsAsync(id));

	[HttpPost("{id:guid}/portal-accounts")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[Authorize(Volo.Abp.Identity.IdentityPermissions.Users.Create)]
	public async Task<Result<StudentPortalAccountDto>> ProvisionPortalAccountAsync(Guid id, ProvisionStudentPortalAccountDto input) =>
		Result.Success(await service.ProvisionPortalAccountAsync(id, input));

	[HttpGet("{id:guid}/credentials")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<Result<StudentCredentialsWorkspaceDto>> GetCredentialsAsync(Guid id) =>
		Result.Success(await service.GetCredentialsAsync(id));

	[HttpPost("{id:guid}/credentials")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public async Task<Result<StudentCredentialsWorkspaceDto>> SendCredentialsAsync(Guid id, SendStudentCredentialsDto input) =>
		Result.Success(await service.SendCredentialsAsync(id, input));

	// Case-only policy correction; never provisions accounts or sends an email.
	[HttpPost("{id:guid}/guardian-credential-email/enable")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<Result<StudentOnboardingCaseDto>> EnableGuardianCredentialEmailAsync(Guid id, EnableGuardianCredentialEmailDto input) =>
		Result.Success(await service.EnableGuardianCredentialEmailAsync(id, input));

	[HttpGet("portal/my-students")]
	public async Task<Result<IReadOnlyList<StudentPortalAccountDto>>> GetMyPortalStudentsAsync() =>
		Result.Success(await service.GetMyPortalStudentsAsync());

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<Result<PagedResultDto<StudentOnboardingCaseDto>>> GetListAsync(
		[FromQuery] StudentOnboardingListInput input) => Result.Success(await service.GetListAsync(input));

	[HttpGet("filter-options")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<Result<StudentOnboardingFilterOptionsDto>> GetFilterOptionsAsync([FromQuery] Guid? academicYearId = null) =>
		Result.Success(await service.GetFilterOptionsAsync(academicYearId));

	[HttpGet("settings/offerings")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<Result<IReadOnlyList<OnboardingAvailableOfferingDto>>> GetAvailableOfferingsAsync([FromQuery] Guid? academicYearId = null) => Result.Success(await service.GetAvailableOfferingsAsync(academicYearId));

	[HttpGet("settings/campuses")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<Result<IReadOnlyList<StudentOnboardingCampusDto>>> GetAvailableCampusesAsync() =>
		Result.Success(await service.GetAvailableCampusesAsync());

	[HttpGet("analytics")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<Result<StudentOnboardingAnalyticsDto>> GetAnalyticsAsync() =>
		Result.Success(await service.GetAnalyticsAsync());

	[HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<Result<StudentOnboardingCaseDto>> GetAsync(Guid id) => Result.Success(await service.GetAsync(id));

	[HttpGet("{id:guid}/placement-options")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateSeat)]
	public async Task<Result<IReadOnlyList<StudentOnboardingPlacementOptionDto>>> GetPlacementOptionsAsync(Guid id) =>
		Result.Success(await service.GetPlacementOptionsAsync(id));

	[HttpGet("{id:guid}/identifier-series")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	public async Task<Result<IReadOnlyList<StudentIdentifierSeriesDto>>> GetAvailableIdentifierSeriesAsync(Guid id) =>
		Result.Success(await service.GetAvailableIdentifierSeriesAsync(id));

	[HttpPost("{id:guid}/readiness/refresh")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public async Task<Result<StudentOnboardingCaseDto>> RefreshReadinessAsync(
		Guid id, [FromQuery] string? concurrencyStamp) =>
		Result.Success(await service.RefreshReadinessAsync(id, concurrencyStamp));

	[HttpPut("{id:guid}/owner")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public async Task<Result<StudentOnboardingCaseDto>> AssignOwnerAsync(
		Guid id, AssignStudentOnboardingOwnerDto input) => Result.Success(await service.AssignOwnerAsync(id, input));

	[HttpPut("{id:guid}/admission-route")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public async Task<Result<StudentOnboardingCaseDto>> SetAdmissionRouteAsync(Guid id, SetStudentOnboardingRouteDto input) =>
		Result.Success(await service.SetAdmissionRouteAsync(id, input));

	[HttpPut("{id:guid}/steps/{stepKey}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public async Task<Result<StudentOnboardingCaseDto>> UpdateStepAsync(
		Guid id, string stepKey, UpdateStudentOnboardingStepDto input) =>
		Result.Success(await service.UpdateStepAsync(id, stepKey, input));

	[HttpPost("{id:guid}/placement")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateSeat)]
	public async Task<Result<StudentOnboardingCaseDto>> SetPlacementAsync(
		Guid id, SetStudentOnboardingPlacementDto input) => Result.Success(await service.SetPlacementAsync(id, input));

	[HttpPost("{id:guid}/identifiers")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	public async Task<Result<StudentOnboardingCaseDto>> AllocateIdentifierAsync(
		Guid id, AllocateStudentOnboardingIdentifierDto input) =>
		Result.Success(await service.AllocateIdentifierAsync(id, input));

	[HttpPost("{id:guid}/roll-preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	public async Task<Result<StudentOnboardingRollPreviewDto>> PreviewRollAsync(Guid id, StudentOnboardingRollPreviewInput input) => Result.Success(await service.PreviewRollAsync(id, input));

	[HttpGet("{id:guid}/activation-preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<Result<StudentOnboardingCaseDto>> PreviewActivationAsync(Guid id) =>
		Result.Success(await service.PreviewActivationAsync(id));

	[HttpPost("{id:guid}/activate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Activate)]
	public async Task<Result<StudentOnboardingCaseDto>> ActivateAsync(
		Guid id, ActivateStudentOnboardingDto input) => Result.Success(await service.ActivateAsync(id, input));

	[HttpPost("{id:guid}/cancel")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Cancel)]
	public async Task<Result<StudentOnboardingCaseDto>> CancelAsync(
		Guid id, CancelStudentOnboardingDto input) => Result.Success(await service.CancelAsync(id, input));

	[HttpGet("{id:guid}/service-handoffs")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<Result<IReadOnlyList<StudentOnboardingServiceHandoffDto>>> GetServiceHandoffsAsync(Guid id) =>
		Result.Success(await service.GetServiceHandoffsAsync(id));

	[HttpPost("{id:guid}/service-handoffs/{handoffId:guid}/retry")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Repair)]
	public async Task<Result<StudentOnboardingServiceHandoffDto>> RetryServiceHandoffAsync(Guid id, Guid handoffId) =>
		Result.Success(await service.RetryServiceHandoffAsync(id, handoffId));

	[HttpPost("{id:guid}/service-consent/confirm")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public async Task<Result<StudentOnboardingCaseDto>> ConfirmServiceConsentAsync(
		Guid id, ConfirmStudentOnboardingServiceConsentDto input) =>
		Result.Success(await service.ConfirmServiceConsentAsync(id, input));

	[HttpGet("settings/policies")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<Result<IReadOnlyList<StudentOnboardingPolicyDto>>> GetPoliciesAsync([FromQuery] Guid academicYearId) =>
		Result.Success(await service.GetPoliciesAsync(academicYearId));

	[HttpPost("settings/policies")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<Result<StudentOnboardingPolicyDto>> CreatePolicyAsync(UpsertStudentOnboardingPolicyDto input) =>
		Result.Success(await service.CreatePolicyAsync(input));

	[HttpPut("settings/policies/{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<Result<StudentOnboardingPolicyDto>> UpdatePolicyAsync(
		Guid id, UpsertStudentOnboardingPolicyDto input) => Result.Success(await service.UpdatePolicyAsync(id, input));

	[HttpGet("settings/configurations")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<Result<IReadOnlyList<StudentOnboardingSettingsDto>>> GetSettingsAsync(
		[FromQuery] Guid? academicYearId) => Result.Success(await service.GetSettingsAsync(academicYearId));

	[HttpPut("settings/configurations")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<Result<StudentOnboardingSettingsDto>> SaveSettingsAsync(
		SaveStudentOnboardingSettingsDto input) => Result.Success(await service.SaveSettingsAsync(input));

	[HttpDelete("settings/configurations/{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<Result> DeleteSettingsAsync(Guid id, [FromQuery] string? concurrencyStamp)
	{
		await service.DeleteSettingsAsync(id, concurrencyStamp);
		return Result.Success();
	}

	[HttpGet("settings/identifier-series")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<Result<IReadOnlyList<StudentIdentifierSeriesDto>>> GetIdentifierSeriesAsync(
		[FromQuery] Guid academicYearId) => Result.Success(await service.GetIdentifierSeriesAsync(academicYearId));

	[HttpPost("settings/identifier-series")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<Result<StudentIdentifierSeriesDto>> CreateIdentifierSeriesAsync(
		CreateStudentIdentifierSeriesDto input) => Result.Success(await service.CreateIdentifierSeriesAsync(input));

	[HttpPost("settings/identifier-series/{id:guid}/deactivate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<Result<StudentIdentifierSeriesDto>> DeactivateIdentifierSeriesAsync(
		Guid id, [FromQuery] string? concurrencyStamp) =>
		Result.Success(await service.DeactivateIdentifierSeriesAsync(id, concurrencyStamp));

	[HttpGet("settings/historical-reconciliation")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Repair)]
	public async Task<Result<HistoricalAdmissionReconciliationDto>> GetHistoricalReconciliationAsync() =>
		Result.Success(await service.GetHistoricalReconciliationAsync());

	[HttpPost("settings/historical-reconciliation/backfill")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Repair)]
	public async Task<Result<HistoricalAdmissionReconciliationDto>> BackfillHistoricalAdmissionsAsync() =>
		Result.Success(await service.BackfillHistoricalAdmissionsAsync());
}
