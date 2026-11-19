using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records.HouseManagement;

public interface IHouseManagementAppService : IApplicationService
{
	Task<HouseManagementHousePageDto> GetHousesAsync(HouseManagementQuery input);
	Task<PagedResultDto<HouseManagementStudentDto>> GetStudentsAsync(HouseManagementQuery input);
	Task<PagedResultDto<HouseManagementDivisionDto>> GetDivisionsAsync(HouseManagementQuery input);
	Task<HouseManagementHouseDto> CreateHouseAsync(SaveHouseRequest input);
	Task<HouseManagementHouseDto> UpdateHouseAsync(Guid id, SaveHouseRequest input);
	Task DeleteHouseAsync(Guid id, Guid academicMediumId, string concurrencyStamp);
	Task<HouseAssignmentPreviewDto> PreviewAssignmentAsync(HouseAssignmentRequest input);
	Task CommitAssignmentAsync(HouseAssignmentRequest input);
	Task ReplaceHouseLeaderAsync(Guid id, ReplaceStudentLeaderRequest input);
	Task ReplaceDivisionLeaderAsync(Guid id, ReplaceStudentLeaderRequest input);
}
