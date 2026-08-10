using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmEventAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionCrmEventDto>> GetListAsync(Guid academicYearId, GetAdmissionCrmEventListInput input);
	Task<AdmissionCrmEventDto> GetAsync(Guid eventId);
	Task<AdmissionCrmEventDto> CreateAsync(Guid academicYearId, CreateUpdateAdmissionCrmEventDto input);
	Task<AdmissionCrmEventDto> UpdateAsync(Guid eventId, CreateUpdateAdmissionCrmEventDto input);
	Task<AdmissionCrmEventDto> PublishAsync(Guid eventId);
	Task<AdmissionCrmEventDto> CancelAsync(Guid eventId, CancelAdmissionCrmEventDto input);
	Task<AdmissionCrmEventDto> CompleteAsync(Guid eventId);
	Task<AdmissionCrmEventDto> ReserveAsync(Guid eventId, ReserveAdmissionCrmEventRegistrationDto input);
	Task<AdmissionCrmEventDto> CancelRegistrationAsync(Guid eventId, Guid registrationId, CancelAdmissionCrmEventRegistrationDto input);
	Task<AdmissionCrmEventDto> CheckInAsync(Guid eventId, Guid registrationId);
	Task<AdmissionCrmEventDto> MarkNoShowAsync(Guid eventId, Guid registrationId);
}
