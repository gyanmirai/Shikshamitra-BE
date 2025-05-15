using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.ClassTeacher;

public interface IClassTeacherAppService
{
	Task AssignOrUpdateTeachersAsync(List<AssignClassTeacherDto> assignments, Guid academicMediumId);
	Task<List<ClassTeacherDto>> GetAllClassTeachersAsync(Guid educationMediumId);
}
