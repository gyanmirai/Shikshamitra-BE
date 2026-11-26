using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
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
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_BiometricAttendance.Add)]
	public Task<AttendanceImportPreviewResponseDto> PreviewAsync(string fileName, string? contentType, byte[] fileContent) =>
		previewWorkflow.PreviewAsync(fileName, contentType, fileContent);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_BiometricAttendance.Add)]
	public Task<AttendanceImportConfirmResponseDto> ConfirmAsync(AttendanceImportConfirmRequestDto input) =>
		confirmWorkflow.ConfirmAsync(input);

	public Task<AttendanceImportTemplateDto> GetTemplateAsync() =>
		templateService.GetTemplateAsync();
}
