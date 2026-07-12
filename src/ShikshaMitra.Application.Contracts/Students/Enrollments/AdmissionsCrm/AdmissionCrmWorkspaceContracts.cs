using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public enum AdmissionCrmIntegrationCapabilityState
{
	NotConfigured = 0,
	Manual = 1,
	Configured = 2
}

public class AdmissionCrmStaffLookupDto
{
	public Guid Id { get; set; }
	public string DisplayName { get; set; } = string.Empty;
	public string UserName { get; set; } = string.Empty;
	public string? Email { get; set; }
}

public class AdmissionCrmIntegrationCapabilityDto
{
	public string Code { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public AdmissionCrmIntegrationCapabilityState State { get; set; }
	public string OperationalMode { get; set; } = string.Empty;
	public string Detail { get; set; } = string.Empty;
}

public interface IAdmissionCrmWorkspaceAppService : IApplicationService
{
	Task<IReadOnlyList<AdmissionCrmStaffLookupDto>> GetStaffAsync(string? filter = null, string? ids = null, int maxResultCount = 20);
	Task<IReadOnlyList<AdmissionCrmIntegrationCapabilityDto>> GetIntegrationCapabilitiesAsync(Guid academicYearId);
}
