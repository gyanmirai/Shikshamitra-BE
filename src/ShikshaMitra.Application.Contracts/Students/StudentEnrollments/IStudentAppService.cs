using ShikshaMitra.Students.StudentDetails;
using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments;
public interface IStudentAppService
{
	Task<StudentGeneralInformationDto> GetStudentGeneralInformationAsync(Guid studentId);
	Task<StudentGeneralInformationDto> UpdateStudentGeneralInformationAsync(Guid studentId, CreateUpdateStudentGeneralInformationDto updateStudentGeneralInfoDto);
}
