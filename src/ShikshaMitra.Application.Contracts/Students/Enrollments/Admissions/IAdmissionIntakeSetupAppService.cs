using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionIntakeSetupAppService : IApplicationService
{
	Task<List<AdmissionIntakeSetupDto>> GetListAsync(bool includeArchived = false);
	Task<AdmissionIntakeSetupDto> CreateAsync(CreateAdmissionIntakeSetupDto input);
	Task<AdmissionIntakeSetupDto> UpdateAsync(Guid id, UpdateAdmissionIntakeSetupDto input);
	Task<AdmissionIntakeSetupUsageDto> GetUsageAsync(Guid id);
	Task<AdmissionIntakeSetupDto> ArchiveAsync(Guid id, ArchiveAdmissionIntakeSetupDto input);
	Task<AdmissionIntakeSetupDto> RestoreAsync(Guid id, RestoreAdmissionIntakeSetupDto input);
}
