using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Compliance;

public interface IStudentIdentityComplianceAppService : IApplicationService
{
	Task<StudentIdentityComplianceDto> GetAsync(Guid studentId);
	Task<StudentIdentityComplianceDto> UpdateAsync(Guid studentId, UpdateStudentIdentityComplianceDto input);
	Task<StudentIdentityComplianceDto> UpdateAadhaarVerificationStatusAsync(Guid studentId, UpdateAadhaarVerificationStatusDto input);
	Task<StudentIdentityComplianceDto> UpdateApaarConsentAsync(Guid studentId, UpdateApaarConsentDto input);
}
