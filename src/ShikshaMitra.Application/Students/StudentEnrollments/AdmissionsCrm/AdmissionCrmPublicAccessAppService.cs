using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[AllowAnonymous]
[RemoteService(false)]
[ExposeServices(typeof(IAdmissionCrmPublicAccessService), IncludeSelf = true)]
public class AdmissionCrmPublicAccessAppService(
	AdmissionCrmPublicOtpWorkflow otpWorkflow,
	AdmissionCrmPublicStatusWorkflow statusWorkflow,
	AdmissionCrmPublicWithdrawalWorkflow withdrawalWorkflow) : ApplicationService, IAdmissionCrmPublicAccessService
{
	public Task<AdmissionApplicantStatusOtpChallengeDto> IssueStatusOtpAsync(Guid tenantId, string token) =>
		otpWorkflow.IssueStatusAsync(tenantId, token);
	public Task<AdmissionApplicantStatusOtpChallengeDto> IssueFamilyAccessOtpAsync(
		Guid tenantId, StartAdmissionFamilyAccessDto input) => otpWorkflow.IssueFamilyAsync(tenantId, input);
	[UnitOfWork(IsDisabled = true)]
	public Task<AdmissionApplicantStatusDto> VerifyFamilyAccessOtpAsync(
		Guid tenantId, VerifyAdmissionFamilyAccessDto input) => otpWorkflow.VerifyFamilyAsync(tenantId, input);
	[UnitOfWork(IsDisabled = true)]
	public Task<AdmissionApplicantStatusDto> VerifyStatusOtpAsync(
		Guid tenantId, string token, VerifyAdmissionApplicantStatusOtpDto input) =>
		otpWorkflow.VerifyStatusAsync(tenantId, token, input);
	public Task<AdmissionApplicantStatusDto> GetVerifiedStatusAsync(
		Guid tenantId, string token, string accessSessionToken, string? language = null) =>
		statusWorkflow.GetAsync(tenantId, token, accessSessionToken, language);
	[UnitOfWork(IsDisabled = true)]
	public Task<AdmissionApplicantStatusDto> UpdateCommunicationPreferencesAsync(
		Guid tenantId, string token, UpdatePublicAdmissionCommunicationPreferencesDto input) =>
		statusWorkflow.UpdatePreferencesAsync(tenantId, token, input);
	[UnitOfWork(IsDisabled = true)]
	public Task<AdmissionApplicantStatusDto> WithdrawApplicationAsync(
		Guid tenantId, string token, WithdrawPublicAdmissionApplicationDto input) =>
		withdrawalWorkflow.WithdrawAsync(tenantId, token, input);
}
