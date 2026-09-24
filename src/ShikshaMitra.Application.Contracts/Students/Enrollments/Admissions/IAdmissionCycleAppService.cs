using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionCycleAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionCycleDto>> GetListAsync(AdmissionCycleListInput input);
	Task<AdmissionCycleDto> GetAsync(Guid id);
	Task<AdmissionCycleDto> CreateAsync(CreateUpdateAdmissionCycleDto input);
	Task<AdmissionCycleDto> UpdateAsync(Guid id, CreateUpdateAdmissionCycleDto input);
	Task<AdmissionCycleDto> PublishAsync(Guid id, string? concurrencyStamp = null);
	Task<AdmissionCycleDto> CloseAsync(Guid id, AdmissionCycleActionDto input);
	Task<AdmissionCycleDto> ReopenAsync(Guid id, ReopenAdmissionCycleDto input);
	Task<AdmissionCycleDto> ArchiveAsync(Guid id, AdmissionCycleActionDto input);
	Task DeleteAsync(Guid id, string? concurrencyStamp = null);
	Task<AdmissionCycleDto> CloneAsync(CloneAdmissionCycleDto input);
	Task<AdmissionCycleDto> UpdateWindowAsync(Guid id, UpdateAdmissionCycleWindowDto input);
	Task<AdmissionCycleDto> ScheduleAsync(Guid id, ScheduleAdmissionCycleDto input);
	Task<AdmissionCycleValidationDto> ValidateAsync(Guid id);
	Task<AdmissionCycleImpactDto> GetImpactAsync(Guid id);
	Task<List<AdmissionCycleHistoryDto>> GetHistoryAsync(Guid id);
	Task<AdmissionCycleCapacitySummaryDto> GetCapacityAsync(Guid id);
	Task<AdmissionCycleCapacitySummaryDto> UpdateOfferingCapacityAsync(Guid id, Guid offeringId,
		UpdateAdmissionCycleOfferingCapacityDto input);
}
