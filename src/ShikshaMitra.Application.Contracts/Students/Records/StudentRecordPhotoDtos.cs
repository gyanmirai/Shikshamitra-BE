using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public class StudentRecordPhotoPayload
{
	[Required] public Guid? PhotoDraftId { get; set; }
}
public class StudentRecordPhotoDraftDto
{
	public Guid Id { get; set; }
	public string PhotoUrl { get; set; } = string.Empty;
}
public interface IStudentRecordPhotoAppService : IApplicationService
{
	Task<StudentRecordPhotoDraftDto> UploadAsync(Guid studentId, UploadStudentPhotoDto input);
	Task<StudentPhotoDownloadDto> GetAsync(Guid studentId, Guid draftId);
}
