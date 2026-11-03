using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records.HouseManagement;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.Records;

[ApiController]
[Area("app")]
[RemoteService(Name = "Default")]
[Authorize]
[Route("api/students/records/house-management")]
public class HouseManagementController(IHouseManagementAppService service) : AbpController
{
	[HttpGet("houses")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.View)]
	public async Task<Result<HouseManagementHousePageDto>> GetHouses([FromQuery] HouseManagementQuery input) => Result.Success(await service.GetHousesAsync(input));
	[HttpGet("students")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.View)]
	public async Task<Result<PagedResultDto<HouseManagementStudentDto>>> GetStudents([FromQuery] HouseManagementQuery input) => Result.Success(await service.GetStudentsAsync(input));
	[HttpGet("divisions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.View)]
	public async Task<Result<PagedResultDto<HouseManagementDivisionDto>>> GetDivisions([FromQuery] HouseManagementQuery input) => Result.Success(await service.GetDivisionsAsync(input));
	[HttpPost("houses")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Add)]
	public async Task<Result<HouseManagementHouseDto>> CreateHouse([FromBody] SaveHouseRequest input) => Result.Success(await service.CreateHouseAsync(input));
	[HttpPut("houses/{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Add)]
	public async Task<Result<HouseManagementHouseDto>> UpdateHouse(Guid id, [FromBody] SaveHouseRequest input) => Result.Success(await service.UpdateHouseAsync(id, input));
	[HttpDelete("houses/{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Delete)]
	public async Task<Result> DeleteHouse(Guid id, [FromQuery] Guid academicMediumId, [FromQuery] string concurrencyStamp)
	{
		await service.DeleteHouseAsync(id, academicMediumId, concurrencyStamp);
		return Result.Success();
	}

	[HttpPost("assignments/preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Add)]
	public async Task<Result<HouseAssignmentPreviewDto>> Preview([FromBody] HouseAssignmentRequest input) => Result.Success(await service.PreviewAssignmentAsync(input));
	[HttpPost("assignments/commit")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Add)]
	public async Task<Result> Commit([FromBody] HouseAssignmentRequest input)
	{
		await service.CommitAssignmentAsync(input);
		return Result.Success();
	}

	[HttpPut("houses/{id:guid}/leader")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Add)]
	public async Task<Result> HouseLeader(Guid id, [FromBody] ReplaceStudentLeaderRequest input)
	{
		await service.ReplaceHouseLeaderAsync(id, input);
		return Result.Success();
	}

	[HttpPut("divisions/{id:guid}/leader")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Add)]
	public async Task<Result> DivisionLeader(Guid id, [FromBody] ReplaceStudentLeaderRequest input)
	{
		await service.ReplaceDivisionLeaderAsync(id, input);
		return Result.Success();
	}
}
