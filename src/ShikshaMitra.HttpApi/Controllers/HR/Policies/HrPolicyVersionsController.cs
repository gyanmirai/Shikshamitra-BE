using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Policies;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/{policyType}/policies/{policyId:guid}/versions")]
[Authorize]
public class HrPolicyVersionsController(IHrPolicyVersionAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<HrPolicyVersionDto>> GetListAsync(
		string policyType,
		Guid policyId,
		[FromQuery] PagedAndSortedResultRequestDto input) =>
		appService.GetListAsync(policyType, policyId, input);

	[HttpGet("{versionId:guid}")]
	public Task<HrPolicyVersionDto> GetAsync(string policyType, Guid policyId, Guid versionId) =>
		appService.GetAsync(policyType, policyId, versionId);

	[HttpGet("compare")]
	public Task<HrPolicyVersionCompareDto> CompareAsync(
		string policyType,
		Guid policyId,
		[FromQuery] Guid fromVersionId,
		[FromQuery] Guid toVersionId) =>
		appService.CompareAsync(policyType, policyId, fromVersionId, toVersionId);

	[HttpGet("{versionId:guid}/restore-preview")]
	public Task<HrPolicyImpactPreviewDto> GetRestorePreviewAsync(
		string policyType,
		Guid policyId,
		Guid versionId) =>
		appService.GetRestorePreviewAsync(policyType, policyId, versionId);

	[HttpPost("{versionId:guid}/restore-draft")]
	public Task<object> RestoreDraftAsync(
		string policyType,
		Guid policyId,
		Guid versionId,
		[FromBody] HrPolicyRestoreDraftRequestDto input) =>
		appService.RestoreDraftAsync(policyType, policyId, versionId, input);
}
