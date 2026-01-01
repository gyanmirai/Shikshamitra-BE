using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public interface IStudentManagementAppService : IApplicationService
{
	Task<(List<StudentRecordDto> StudentRecords, ArrangementTypeEnum? ArrangementType, ArrangementByEnum? ArrangementBy)> GetStudentRecords(Guid divisionId);
	Task AssignHouseToStudent(Guid studentId, Guid houseId);
	Task AssignHouseGlobalLeaderAsync(Guid studentId);
	Task AssignHouseDivisionLeaderAsync(Guid studentId);
	Task DeassignHouseFromStudent(Guid studentId);
	Task DeassignHouseGlobalLeaderAsync(Guid studentId);
	Task DeassignHouseDivisionLeaderAsync(Guid studentId);

	Task StoreGrNumbers(List<UpdateStudentGrNumberDto> updateStudentGrNumberDto);
	Task<List<GrProjectionDto>> GetGrProjections(Guid divisionId);
	Task<List<RollNumberProjectionDto>> GetRollNumberProjections(Guid divisionId, ArrangementTypeEnum arrangementType, ArrangementByEnum arrangementBy);
	Task StoreRollNumbers(Guid divisionId, List<UpdateStudentRollNumberDto> updateStudentRollNumberDto, ArrangementTypeEnum? arrangementType, ArrangementByEnum? arrangementBy);
	Task ChangeDivision(List<ChangeStudentDivisionDto> changeStudentDivisionDto);

}
