using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmPlaybookAppService : IApplicationService
{
	Task<AdmissionCrmPlaybookDto> CreateAsync(Guid academicYearId, CreateAdmissionCrmPlaybookDto input);
	Task<PagedResultDto<AdmissionCrmPlaybookDto>> GetListAsync(
		Guid academicYearId,
		GetAdmissionCrmPlaybookListInput input);
	Task<AdmissionCrmPlaybookDto> GetAsync(Guid id);
	Task<AdmissionCrmPlaybookDto> UpdateAsync(Guid id, UpdateAdmissionCrmPlaybookDto input);
	Task<AdmissionCrmPlaybookDto> PublishAsync(Guid id);
	Task<AdmissionCrmPlaybookDto> CreateNewVersionAsync(Guid id);
	Task<AdmissionCrmPlaybookDto> RetireAsync(Guid id);
	Task<AdmissionCrmPlaybookResolutionDto> ResolveAsync(
		Guid academicYearId,
		ResolveAdmissionCrmPlaybookDto input);
	Task<AdmissionCrmSlotGenerationResultDto> GenerateSlotsAsync(
		Guid id,
		GenerateAdmissionCrmPlaybookSlotsDto input);
	Task<AdmissionCrmSlotRecommendationDto> RecommendSlotAsync(
		Guid id,
		RecommendAdmissionCrmPlaybookSlotDto input);
	Task<AdmissionCrmExamResultPreviewDto> PreviewExamResultAsync(
		Guid id,
		PreviewAdmissionCrmExamResultDto input);
}
