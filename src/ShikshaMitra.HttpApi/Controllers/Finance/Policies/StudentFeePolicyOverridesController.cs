using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Finance.Policies;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/finance/students/{studentId:guid}/fee-structures/{feeStructureId:guid}/policy-override")]
[Authorize]
public class StudentFeePolicyOverridesController(IStudentFeePolicyOverrideAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<StudentFeePolicyOverrideDto?> GetAsync(Guid studentId, Guid feeStructureId) =>
		appService.GetOverrideAsync(studentId, feeStructureId);

	[HttpGet("effective-rules")]
	public Task<EffectiveCollectionRulesDto> GetEffectiveRulesAsync(Guid studentId, Guid feeStructureId) =>
		appService.GetEffectiveRulesAsync(studentId, feeStructureId);

	[HttpPut]
	public Task<StudentFeePolicyOverrideDto> SetAsync(Guid studentId, Guid feeStructureId, [FromBody] CreateUpdateStudentFeePolicyOverrideDto input) =>
		appService.SetOverrideAsync(studentId, feeStructureId, input);
}
