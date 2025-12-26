using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public interface IStudentManagementAppService : IApplicationService
{
	Task<List<StudentRecordDto>> GetStudentRecords(Guid divisionId);
	Task AssignHouseToStudent(Guid studentId, Guid houseId);
	Task AssignHouseGlobalLeaderAsync(Guid studentId);
	Task AssignHouseDivisionLeaderAsync(Guid studentId);
	Task DeassignHouseFromStudent(Guid studentId);
	Task DeassignHouseGlobalLeaderAsync(Guid studentId);
	Task DeassignHouseDivisionLeaderAsync(Guid studentId);

}
