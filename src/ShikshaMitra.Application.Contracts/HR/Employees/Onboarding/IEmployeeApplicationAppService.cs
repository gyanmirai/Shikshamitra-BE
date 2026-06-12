using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.Onboarding;

public interface IEmployeeApplicationAppService : IApplicationService
{
	Task<PagedResultDto<EmployeeApplicationListItemDto>> GetListAsync(GetEmployeeApplicationListInput input);
	Task<EmployeeApplicationInviteDto> CreateAsync(CreateEmployeeApplicationInviteDto input);
	Task<EmployeeApplicationDto> GetAsync(Guid id);
	Task<EmployeeApplicationDto> UpdateStatusAsync(Guid id, EmployeeApplicationStatusUpdateDto input);
	Task<EmployeeApplicationInviteDto> RegenerateLinkAsync(Guid id);
	Task<EmployeeApplicationFinalizeResultDto> FinalizeAsync(Guid id, FinalizeEmployeeApplicationDto input);
	Task<EmployeeApplicationFileDto?> GetResumeAsync(Guid id);
	Task<EmployeeApplicationFileDto?> GetPhotoAsync(Guid id);
	Task DeleteAsync(Guid id);
	Task<PublicEmployeeApplicationDto> GetPublicAsync(string token);
	Task<EmployeeApplicationDto> SubmitPublicAsync(
		string token,
		PublicEmployeeApplicationSubmitDto input,
		EmployeeApplicationFileInput? resume,
		EmployeeApplicationFileInput? photo);
}
