using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
[Authorize]
public class HrPolicySafetyAppService(
	HrPolicyImpactPreviewBuilder impactPreviewBuilder,
	HrPolicyAuditSummaryBuilder auditSummaryBuilder)
	: ApplicationService, IHrPolicySafetyAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact)]
	public Task<HrPolicyImpactPreviewDto> PreviewPolicyImpactAsync(HrPolicyImpactPreviewRequestDto input) =>
		impactPreviewBuilder.BuildAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.ViewAudit)]
	public Task<HrPolicyAuditSummaryDto> GetAuditSummaryAsync(string policyType, Guid id) =>
		auditSummaryBuilder.BuildAsync(policyType, id);
}
