using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public interface IStudentOnboardingAppService : IApplicationService
{
	Task<PagedResultDto<StudentOnboardingCaseDto>> GetListAsync(StudentOnboardingListInput input);
	Task<StudentOnboardingFilterOptionsDto> GetFilterOptionsAsync();
	Task<StudentOnboardingAnalyticsDto> GetAnalyticsAsync();
	Task<StudentOnboardingCaseDto> GetAsync(Guid id);
	Task<IReadOnlyList<StudentOnboardingPlacementOptionDto>> GetPlacementOptionsAsync(Guid id);
	Task<IReadOnlyList<StudentIdentifierSeriesDto>> GetAvailableIdentifierSeriesAsync(Guid id);
	Task<StudentOnboardingCaseDto> RefreshReadinessAsync(Guid id, string? concurrencyStamp);
	Task<StudentOnboardingCaseDto> AssignOwnerAsync(Guid id, AssignStudentOnboardingOwnerDto input);
	Task<StudentOnboardingCaseDto> UpdateStepAsync(Guid id, string stepKey, UpdateStudentOnboardingStepDto input);
	Task<StudentOnboardingCaseDto> SetPlacementAsync(Guid id, SetStudentOnboardingPlacementDto input);
	Task<StudentOnboardingCaseDto> AllocateIdentifierAsync(Guid id, AllocateStudentOnboardingIdentifierDto input);
	Task<StudentOnboardingCaseDto> PreviewActivationAsync(Guid id);
	Task<StudentOnboardingCaseDto> ActivateAsync(Guid id, ActivateStudentOnboardingDto input);
	Task<StudentOnboardingCaseDto> CancelAsync(Guid id, CancelStudentOnboardingDto input);
	Task<IReadOnlyList<StudentOnboardingServiceHandoffDto>> GetServiceHandoffsAsync(Guid id);
	Task<StudentOnboardingServiceHandoffDto> RetryServiceHandoffAsync(Guid id, Guid handoffId);
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
