using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionOfferingAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionOfferingDto>> GetListAsync(AdmissionOfferingListInput input);
	Task<AdmissionOfferingDto> GetAsync(Guid id);
	Task<AdmissionOfferingDto> CreateAsync(Guid admissionCycleId, CreateUpdateAdmissionOfferingDto input);
	Task<AdmissionOfferingDto> UpdateAsync(Guid id, CreateUpdateAdmissionOfferingDto input);
	Task<AdmissionOfferingDto> SetActiveAsync(Guid id, bool isActive);
}
