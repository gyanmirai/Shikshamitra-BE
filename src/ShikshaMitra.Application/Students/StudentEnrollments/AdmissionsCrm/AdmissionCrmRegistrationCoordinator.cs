using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationCoordinator(
	AdmissionCrmRegistrationIssuanceWorkflow issuance,
	AdmissionCrmRegistrationAutomationWorkflow automation) : ITransientDependency
{
	public Task<AdmissionCrmRegistrationApplicationDto> IssueDirectAsync(Guid leadId, IssueAdmissionCrmRegistrationApplicationDto input) =>
		issuance.IssueDirectAsync(leadId, input);
	public Task<AdmissionCrmRegistrationApplicationDto> IssueFromPaperAsync(Guid leadId, CreatePaperAdmissionCrmRegistrationDto input) =>
		issuance.IssueFromPaperAsync(leadId, input);
	public Task EnsureStandardIssuanceEligibleAsync(Guid leadId) => issuance.EnsureStandardEligibleAsync(leadId);
	public Task<bool> IssueApplicableFromAutomationAsync(Guid leadId, AdmissionInquiryStage stage) => automation.IssueApplicableAsync(leadId, stage);
}
