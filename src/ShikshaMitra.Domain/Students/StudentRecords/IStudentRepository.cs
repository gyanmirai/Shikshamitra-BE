using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentRecords;

public interface IStudentRepository : IRepository<Student, Guid>
{
	Task<Student?> GetLastStudentbyGrNoForGSEBAsync(Guid educationBoardId, Guid educationMediumId, Guid educationMediumStageId);
	Task<Student?> GetLastStudentbyGrNoForCBSEAsync(Guid educationBoardId);
	Task<Student?> GetLastStudentbyRollNoAsync(Guid divisionId);
}
