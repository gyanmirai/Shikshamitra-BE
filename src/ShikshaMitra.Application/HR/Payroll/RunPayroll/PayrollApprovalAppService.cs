using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Permission = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

[RemoteService(false)]
[Authorize(Permission.View)]
public class PayrollApprovalAppService(PayrollApprovalSettingsWorkflow settings, PayrollSubmissionWorkflow submissions, PayrollApprovalAccess access)
	: ApplicationService, IPayrollApprovalAppService
{
	public virtual Task<PayrollApprovalSettingsDto> GetSettingsAsync() => settings.GetAsync();

	[Authorize(Permission.ManageWorkflow)]
	public virtual Task<List<PayrollApproverDto>> GetApproversAsync(string? search) => access.GetApproversAsync(search);

	[Authorize(Permission.ManageWorkflow)]
	public virtual Task<PayrollApprovalSettingsDto> ConfigureAsync(ConfigurePayrollApprovalCommand command) =>
		settings.ConfigureAsync(command);

	[Authorize(Permission.ManageWorkflow)]
	public virtual Task<PayrollSectionOptionsDto> GetSectionOptionsAsync() => settings.GetSectionOptionsAsync();

	[Authorize(Permission.ManageWorkflow)]
	public virtual Task<List<PayrollApproverDto>> GetSectionReviewersAsync(Guid stageId, Guid roleId) =>
		access.GetSectionReviewersAsync(stageId, roleId);

	[Authorize(Permission.ManageWorkflow)]
	public virtual Task<PayrollApprovalSettingsDto> ConfigureSectionsAsync(ConfigurePayrollSectionsCommand command) =>
		settings.ConfigureSectionsAsync(command);

	[Authorize(Permission.ManageWorkflow)]
	public virtual Task<PayrollApprovalSettingsDto> ChangeModeAsync(string action, PayrollApprovalSettingsCommand command) =>
		settings.ChangeModeAsync(action, command);

	public virtual Task<List<PayrollSubmissionDto>> GetListAsync(PayrollPreviewMonthInput input) => submissions.GetListAsync(input);
	public virtual Task<PayrollSubmissionDetailDto> GetAsync(Guid id) => submissions.GetAsync(id);

	[Authorize(Permission.Submit)]
	public virtual Task<PayrollSubmissionDto> SubmitAsync(SubmitPayrollCommand command) => submissions.SubmitAsync(command);

	[Authorize(Permission.Submit)]
	public virtual Task<List<PayrollSubmissionDto>> SubmitSectionsAsync(SubmitPayrollCommand command) =>
		submissions.SubmitSectionsAsync(command);

	public virtual Task<PayrollSubmissionDto> ResolveAsync(Guid id, string action, ResolvePayrollSubmissionCommand command) =>
		submissions.ResolveAsync(id, action, command);
}
