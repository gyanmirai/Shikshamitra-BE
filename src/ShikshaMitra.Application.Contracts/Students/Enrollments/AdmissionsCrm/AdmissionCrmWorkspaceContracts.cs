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
	public Guid EmployeeId { get; set; }
	public string EmployeeCode { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string UserName { get; set; } = string.Empty;
	public string? Email { get; set; }
	public bool IsOwnerEligible { get; set; }
}

public class AdmissionCrmIntegrationCapabilityDto
{
	public string Code { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public AdmissionCrmIntegrationCapabilityState State { get; set; }
	public string OperationalMode { get; set; } = string.Empty;
	public string Detail { get; set; } = string.Empty;
}

public class AdmissionCrmApplicantStepDto
{
	public AdmissionCrmApplicantStepKey Step { get; set; }
	public string Title { get; set; } = string.Empty;
	public AdmissionCrmApplicantStepStatus Status { get; set; }
	public bool IsRequired { get; set; }
	public string ReasonCode { get; set; } = string.Empty;
	public string Reason { get; set; } = string.Empty;
	public string? ActionCode { get; set; }
	public DateTime? CompletedAt { get; set; }
}

public class AdmissionCrmApplicantProgressDto
{
	public Guid LeadId { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid? AdmissionYearSetupId { get; set; }
	public int AdmissionYearSetupVersion { get; set; }
	public AdmissionCrmPipelineColumnKey PipelineColumn { get; set; }
	public AdmissionCrmClosedOutcome? ClosedOutcome { get; set; }
	public AdmissionCrmApplicantStepKey CurrentStep { get; set; }
	public string NextAction { get; set; } = string.Empty;
	public string? NextActionCode { get; set; }
	public IReadOnlyList<AdmissionCrmApplicantStepDto> Steps { get; set; } = [];
}

public interface IAdmissionCrmWorkspaceAppService : IApplicationService
{
	Task<AdmissionCrmApplicantProgressDto> GetApplicantProgressAsync(Guid leadId);
	Task<IReadOnlyList<AdmissionCrmStaffLookupDto>> GetStaffAsync(
		string? filter = null,
		string? ids = null,
		int maxResultCount = 20,
		Guid? academicYearId = null,
		bool ownerOnly = false,
		bool reviewerOnly = false);
	Task<IReadOnlyList<AdmissionCrmIntegrationCapabilityDto>> GetIntegrationCapabilitiesAsync(Guid academicYearId);
}
