using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionCampusAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionCampusDto>> GetListAsync(AdmissionCampusListInput input);
	Task<AdmissionCampusDto> GetAsync(Guid id);
	Task<AdmissionCampusDto> CreateAsync(CreateUpdateAdmissionCampusDto input);
	Task<AdmissionCampusDto> UpdateAsync(Guid id, CreateUpdateAdmissionCampusDto input);
	Task<AdmissionCampusDto> ActivateAsync(Guid id);
	Task<AdmissionCampusDto> DeactivateAsync(Guid id);
}
