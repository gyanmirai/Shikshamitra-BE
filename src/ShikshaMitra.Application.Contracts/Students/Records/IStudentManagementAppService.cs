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
	Task<List<AcademicTermDto>> GetAcademicTermsAsync(Guid academicYearId);
	Task<AcademicTermDto> CreateAcademicTermAsync(CreateAcademicTermDto input);
	Task<AcademicTermDto> SetCurrentAcademicTermAsync(Guid academicTermId);
	Task<List<RollNumberProjectionDto>> GetRollNumberProjections(Guid divisionId, Guid? academicTermId, ArrangementTypeEnum arrangementType, ArrangementByEnum arrangementBy);
	Task<RollNumberSaveResultDto> StoreRollNumbers(Guid divisionId, Guid? academicTermId, List<UpdateStudentRollNumberDto> updateStudentRollNumberDto, ArrangementTypeEnum? arrangementType, ArrangementByEnum? arrangementBy);
	Task ChangeDivision(List<ChangeStudentDivisionDto> changeStudentDivisionDto);

}
