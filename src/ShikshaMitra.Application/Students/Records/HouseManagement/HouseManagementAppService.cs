using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records.HouseManagement;

[RemoteService(false)]
public class HouseManagementAppService(
	HouseManagementQueryService queries,
	HouseManagementCommandService commands)
	: ApplicationService, IHouseManagementAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.View)]
	public Task<HouseManagementHousePageDto> GetHousesAsync(HouseManagementQuery input) => queries.GetHousesAsync(input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.View)]
	public Task<PagedResultDto<HouseManagementStudentDto>> GetStudentsAsync(HouseManagementQuery input) => queries.GetStudentsAsync(input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.View)]
	public Task<PagedResultDto<HouseManagementDivisionDto>> GetDivisionsAsync(HouseManagementQuery input) => queries.GetDivisionsAsync(input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Add)]
	public Task<HouseManagementHouseDto> CreateHouseAsync(SaveHouseRequest input) => commands.SaveHouseAsync(null, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Add)]
	public Task<HouseManagementHouseDto> UpdateHouseAsync(Guid id, SaveHouseRequest input) => commands.SaveHouseAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Delete)]
	public Task DeleteHouseAsync(Guid id, Guid academicMediumId, string concurrencyStamp) => commands.DeleteHouseAsync(id, academicMediumId, concurrencyStamp);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Add)]
	public Task<HouseAssignmentPreviewDto> PreviewAssignmentAsync(HouseAssignmentRequest input) => commands.PreviewAsync(input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Add)]
	public Task CommitAssignmentAsync(HouseAssignmentRequest input) => commands.CommitAsync(input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Add)]
	public Task ReplaceHouseLeaderAsync(Guid id, ReplaceStudentLeaderRequest input) => commands.ReplaceHouseLeaderAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Add)]
	public Task ReplaceDivisionLeaderAsync(Guid id, ReplaceStudentLeaderRequest input) => commands.ReplaceDivisionLeaderAsync(id, input);
}
