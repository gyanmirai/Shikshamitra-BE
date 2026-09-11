using System;
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
	Task<AdmissionCycleDto> CloseAsync(Guid id, string? concurrencyStamp = null);
	Task<AdmissionCycleDto> ArchiveAsync(Guid id, string? concurrencyStamp = null);
}
