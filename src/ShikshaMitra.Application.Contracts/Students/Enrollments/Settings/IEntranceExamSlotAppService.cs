using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public interface IEntranceExamSlotAppService : IApplicationService
{
	Task<PagedResultDto<EntranceExamSlotDto>> GetListAsync(GetEntranceExamSlotsInput input);

	Task<EntranceExamSlotDetailDto> GetAsync(Guid id);

	Task<EntranceExamSlotDto> CreateAsync(CreateEntranceExamSlotDto input);

	Task<EntranceExamSlotDto> UpdateAsync(Guid id, UpdateEntranceExamSlotDto input);

	Task DeleteAsync(Guid id);

	Task<PagedResultDto<AvailableExamSlotDto>> GetAvailableSlotsAsync(GetAvailableExamSlotsInput input);
}
