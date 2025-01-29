using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.EntranceExam;

public interface IEntranceExamRepository :  IRepository<EntranceExam, Guid>
{
	
}