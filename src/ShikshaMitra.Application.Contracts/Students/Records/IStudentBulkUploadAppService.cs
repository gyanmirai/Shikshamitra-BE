using System.Threading.Tasks;

namespace ShikshaMitra.Students.Records;

public interface IStudentBulkUploadAppService
{
	Task<StudentBulkUploadResultDto> UploadAsync(StudentBulkUploadFileDto input);
}
