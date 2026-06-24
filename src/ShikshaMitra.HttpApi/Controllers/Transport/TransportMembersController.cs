using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Transport;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Transport;

[ApiController]
[Authorize]
[Route("api/transport")]
public class TransportMembersController(ITransportMemberAppService memberAppService) : AbpController
{
	[HttpGet("member-candidates")]
	[Authorize(ShikshaMitraSubModulePermissions.ApplicationManagement)]
	public async Task<Result<PagedResultDto<TransportMemberCandidateDto>>> GetMemberCandidates(
		[FromQuery] TransportMemberCandidateListRequestDto input)
	{
		var candidates = await memberAppService.GetCandidatesAsync(input);
		return Result.Success(candidates);
	}

	[HttpGet("members")]
	[Authorize(ShikshaMitraSubModulePermissions.ApplicationManagement)]
	public async Task<Result<PagedResultDto<TransportMemberDto>>> GetMembers(
		[FromQuery] TransportMemberListRequestDto input)
	{
		var members = await memberAppService.GetListAsync(input);
		return Result.Success(members);
	}

	[HttpGet("members/{memberId}")]
	[Authorize(ShikshaMitraSubModulePermissions.ApplicationManagement)]
	public async Task<Result<TransportMemberDto>> GetMember(Guid memberId, [FromQuery] Guid academicMediumId)
	{
		var member = await memberAppService.GetAsync(memberId, academicMediumId);
		return Result.Success(member);
	}

	[HttpGet("members/{memberId}/assignments")]
	[Authorize(ShikshaMitraSubModulePermissions.ApplicationManagement)]
	public async Task<Result<PagedResultDto<TransportMemberRouteAssignmentDto>>> GetMemberAssignments(
		Guid memberId,
		[FromQuery] TransportMemberAssignmentListRequestDto input)
	{
		var assignments = await memberAppService.GetAssignmentsAsync(memberId, input);
		return Result.Success(assignments);
	}

	[HttpPut("members/{memberId}/status")]
	[Authorize(ShikshaMitraSubModulePermissions.ApplicationManagement)]
	public async Task<Result<TransportMemberDto>> ChangeMemberStatus(
		Guid memberId,
		[FromQuery] Guid academicMediumId,
		[FromBody] ChangeTransportMemberStatusDto input)
	{
		var member = await memberAppService.ChangeStatusAsync(memberId, academicMediumId, input);
		return Result.Success(member);
	}
}
