using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public interface IStudentOnboardingAppService : IApplicationService
{
	Task<PagedResultDto<StudentOnboardingCaseDto>> GetListAsync(StudentOnboardingListInput input);
	Task<StudentOnboardingFilterOptionsDto> GetFilterOptionsAsync(Guid? academicYearId = null);
	Task<IReadOnlyList<StudentOnboardingCampusDto>> GetAvailableCampusesAsync();
	Task<IReadOnlyList<OnboardingAvailableOfferingDto>> GetAvailableOfferingsAsync(Guid? academicYearId);
	Task<StudentOnboardingAnalyticsDto> GetAnalyticsAsync();
	Task<StudentOnboardingCaseDto> GetAsync(Guid id);
	Task EnsureCaseAccessAsync(Guid id);
	Task<IReadOnlyList<StudentOnboardingPlacementOptionDto>> GetPlacementOptionsAsync(Guid id);
	Task<IReadOnlyList<StudentIdentifierSeriesDto>> GetAvailableIdentifierSeriesAsync(Guid id);
	Task<StudentOnboardingCaseDto> RefreshReadinessAsync(Guid id, string? concurrencyStamp);
	Task<StudentOnboardingCaseDto> ReviewRequirementsAsync(Guid id, string? concurrencyStamp);
	Task<StudentOnboardingCaseDto> AssignOwnerAsync(Guid id, AssignStudentOnboardingOwnerDto input);
	Task<StudentOnboardingCaseDto> SetAdmissionRouteAsync(Guid id, SetStudentOnboardingRouteDto input);
	Task<StudentOnboardingCaseDto> UpdateStepAsync(Guid id, string stepKey, UpdateStudentOnboardingStepDto input);
	Task<StudentOnboardingCaseDto> SetPlacementAsync(Guid id, SetStudentOnboardingPlacementDto input);
	Task<StudentOnboardingCaseDto> AllocateIdentifierAsync(Guid id, AllocateStudentOnboardingIdentifierDto input);
	Task<StudentOnboardingCaseDto> AssignGrNumberAsync(Guid id, AssignStudentOnboardingGrNumberDto input);
	Task<StudentOnboardingCaseDto> AssignRollNumberAsync(Guid id, AssignStudentOnboardingRollNumberDto input);
	Task SetIdentifierLockAsync(SetStudentIdentifierLockDto input);
	Task<ShikshaMitra.Students.StudentEnrollments.Admissions.AdmissionFeeSelectionDto> GetFeeStructureAsync(Guid id);
	Task<List<ShikshaMitra.Students.StudentEnrollments.Admissions.AdmissionFeeStructureCandidateDto>> GetFeeStructureCandidatesAsync(Guid id);
	Task<ShikshaMitra.Students.StudentEnrollments.Admissions.AdmissionFeeSelectionDto> SaveFeeStructureAsync(Guid id, SaveStudentOnboardingFeeStructureDto input);
	Task<StudentOnboardingRollPreviewDto> PreviewRollAsync(Guid id, StudentOnboardingRollPreviewInput input);
	Task<StudentOnboardingCaseDto> PreviewActivationAsync(Guid id);
	Task<StudentOnboardingCaseDto> ActivateAsync(Guid id, ActivateStudentOnboardingDto input);
	Task<StudentOnboardingCaseDto> CancelAsync(Guid id, CancelStudentOnboardingDto input);
	Task<IReadOnlyList<StudentOnboardingServiceHandoffDto>> GetServiceHandoffsAsync(Guid id);
	Task<StudentOnboardingServiceHandoffDto> RetryServiceHandoffAsync(Guid id, Guid handoffId);
	Task<StudentOnboardingCaseDto> ConfirmServiceConsentAsync(Guid id, ConfirmStudentOnboardingServiceConsentDto input);
	Task<IReadOnlyList<StudentPortalAccountDto>> GetPortalAccountsAsync(Guid id);
	Task<StudentPortalAccountDto> ProvisionPortalAccountAsync(Guid id, ProvisionStudentPortalAccountDto input);
	Task<StudentCredentialsWorkspaceDto> GetCredentialsAsync(Guid id);
	Task<StudentCredentialsWorkspaceDto> SendCredentialsAsync(Guid id, SendStudentCredentialsDto input);
	Task<StudentOnboardingCaseDto> EnableGuardianCredentialEmailAsync(Guid id, EnableGuardianCredentialEmailDto input);
	Task<IReadOnlyList<StudentPortalAccountDto>> GetMyPortalStudentsAsync();
	Task<IReadOnlyList<StudentOnboardingPolicyDto>> GetPoliciesAsync(Guid academicYearId);
	Task<StudentOnboardingPolicyDto> CreatePolicyAsync(UpsertStudentOnboardingPolicyDto input);
	Task<StudentOnboardingPolicyDto> UpdatePolicyAsync(Guid id, UpsertStudentOnboardingPolicyDto input);
	Task<IReadOnlyList<StudentOnboardingSettingsDto>> GetSettingsAsync(Guid? academicYearId);
	Task<StudentOnboardingSettingsDto> SaveSettingsAsync(SaveStudentOnboardingSettingsDto input);
	Task DeleteSettingsAsync(Guid id, string? concurrencyStamp);
	Task<IReadOnlyList<StudentIdentifierSeriesDto>> GetIdentifierSeriesAsync(Guid academicYearId);
	Task<StudentIdentifierSeriesDto> CreateIdentifierSeriesAsync(CreateStudentIdentifierSeriesDto input);
	Task<StudentIdentifierSeriesDto> DeactivateIdentifierSeriesAsync(Guid id, string? concurrencyStamp);
	Task<HistoricalAdmissionReconciliationDto> GetHistoricalReconciliationAsync();
	Task<HistoricalAdmissionReconciliationDto> BackfillHistoricalAdmissionsAsync();
}
