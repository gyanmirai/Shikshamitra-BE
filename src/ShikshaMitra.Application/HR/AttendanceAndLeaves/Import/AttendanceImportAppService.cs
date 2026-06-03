using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Import;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
public class AttendanceImportAppService(
	AttendanceImportPreviewWorkflow previewWorkflow,
	AttendanceImportConfirmWorkflow confirmWorkflow,
	AttendanceImportTemplateService templateService)
	: ApplicationService, IAttendanceImportAppService
{
	public Task<AttendanceImportPreviewResponseDto> PreviewAsync(string fileName, string? contentType, byte[] fileContent) =>
		previewWorkflow.PreviewAsync(fileName, fileContent);

	public Task<AttendanceImportConfirmResponseDto> ConfirmAsync(AttendanceImportConfirmRequestDto input) =>
		confirmWorkflow.ConfirmAsync(input);

	public Task<AttendanceImportTemplateDto> GetTemplateAsync() =>
		templateService.GetTemplateAsync();
}
