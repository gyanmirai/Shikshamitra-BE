using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionOfferingAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionOfferingListDto>> GetListAsync(AdmissionOfferingListInput input);
	Task<AdmissionOfferingDto> GetAsync(Guid id);
	Task<AdmissionOfferingDto> CreateAsync(Guid admissionCycleId, CreateAdmissionOfferingDto input);
	Task<AdmissionOfferingDto> UpdateAsync(Guid id, UpdateAdmissionOfferingDto input);
	Task<AdmissionOfferingDto> ActivateAsync(Guid id, string concurrencyStamp);
	Task<AdmissionOfferingDto> DeactivateAsync(Guid id, string concurrencyStamp);
	Task DeleteAsync(Guid id, string concurrencyStamp);
	Task<AdmissionOfferingDto> CloneAsync(Guid id, CloneAdmissionOfferingDto input);
	Task<AdmissionOfferingConfigurationValidationDto> ValidateAsync(ValidateAdmissionOfferingDto input);
	Task<AdmissionOfferingAvailabilityDto> GetAvailabilityAsync(Guid id);
	Task<BulkAdmissionOfferingResultDto> BulkCreateAsync(Guid admissionCycleId, BulkCreateAdmissionOfferingsDto input);
}
