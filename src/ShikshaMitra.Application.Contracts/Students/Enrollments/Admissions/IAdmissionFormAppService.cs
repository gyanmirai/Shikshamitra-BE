using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
namespace ShikshaMitra.Students.StudentEnrollments.Admissions;
public interface IAdmissionFormAppService : IApplicationService { Task<PagedResultDto<AdmissionFormDto>> GetListAsync(AdmissionFormListInput input); Task<AdmissionFormDto> GetAsync(Guid id); Task<AdmissionFormDto> CreateAsync(CreateUpdateAdmissionFormDto input); Task<AdmissionFormDto> UpdateAsync(Guid id, CreateUpdateAdmissionFormDto input); Task<AdmissionFormDto> CloneAsync(Guid id); Task<AdmissionFormValidationResultDto> ValidateAsync(Guid id); Task<AdmissionFormDto> PublishAsync(Guid id); Task<AdmissionFormDto> PreviewAsync(Guid id); }
