using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentRecordPhotoAppService(IRepository<StudentRecordPhotoDraft, Guid> drafts, IBlobContainer blobs,
	StudentRecordsAccessService access, StudentRecordReadGuard readGuard,
	IRepository<StudentRecordChange, Guid> changes) : ApplicationService, IStudentRecordPhotoAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create)]
	public Task<StudentRecordPhotoDraftDto> UploadAsync(Guid studentId, UploadStudentPhotoDto input) => UploadCoreAsync(studentId, input, false);

	internal Task<StudentRecordPhotoDraftDto> UploadForAdmissionReviewAsync(Guid studentId, UploadStudentPhotoDto input) =>
		UploadCoreAsync(studentId, input, true);

	private async Task<StudentRecordPhotoDraftDto> UploadCoreAsync(Guid studentId, UploadStudentPhotoDto input, bool joinAdmissionWork)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create);
		await access.EnsureWritableAsync(studentId);
		await readGuard.EnsureAsync(studentId, "photo");
		if (input.Content == null || input.Content.Length == 0) throw new BusinessException("Students:Records:InvalidPhoto");
		if (input.Content.Length > 3 * 1024 * 1024) throw new BusinessException("Students:Records:PhotoTooLarge");
		FileValidationService.ValidateProfilePictureUpload(input.FileName, input.Content);
		var extension = Path.GetExtension(input.FileName).ToLowerInvariant();
		var contentType = extension switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", ".gif" => "image/gif", _ => throw new BusinessException("Students:Records:InvalidPhoto") };
		if (extension is ".jpg" or ".jpeg" or ".png") FileValidationService.ValidateDocumentFileSignature(input.Content, extension);
		if (extension == ".gif" && (input.Content.Length < 6 || !System.Text.Encoding.ASCII.GetString(input.Content, 0, 6).StartsWith("GIF8", StringComparison.Ordinal)))
			throw new BusinessException("Students:Records:InvalidPhoto");
		if (extension == ".webp" && (input.Content.Length < 12 || System.Text.Encoding.ASCII.GetString(input.Content, 0, 4) != "RIFF" || System.Text.Encoding.ASCII.GetString(input.Content, 8, 4) != "WEBP"))
			throw new BusinessException("Students:Records:InvalidPhoto");
		if (!FileValidationService.IsMatchingDocumentContentType(extension, contentType) && extension is not (".webp" or ".gif"))
			throw new BusinessException("Students:Records:InvalidPhoto");
		var id = GuidGenerator.Create();
		var blob = $"student-record-photo/{CurrentTenant.Id}/{studentId}/{id:N}{extension}";
		await blobs.SaveAsync(blob, input.Content, overrideExisting: false);
		try
		{
			using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: !joinAdmissionWork);
			await drafts.InsertAsync(new(id, CurrentTenant.Id, studentId, CurrentUser.GetId(), blob, contentType,
				Path.GetFileName(input.FileName), new DateTimeOffset(Clock.Now.ToUniversalTime()).AddDays(30)), autoSave: true);
			await work.CompleteAsync();
		}
		catch { await blobs.DeleteAsync(blob); throw; }
		return new() { Id = id, PhotoUrl = $"/api/students/records/changes/students/{studentId}/photo-drafts/{id}" };
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View)]
	public async Task<StudentPhotoDownloadDto> GetAsync(Guid studentId, Guid draftId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View);
		await readGuard.EnsureAsync(studentId, "photo");
		var draft = await drafts.GetAsync(x => x.Id == draftId && x.StudentId == studentId && x.TenantId == CurrentTenant.Id);
		if (draft.UploadedBy != CurrentUser.Id && !await changes.AnyAsync(c => c.TenantId == CurrentTenant.Id && c.StudentId == studentId && c.Section == "photo" && c.PayloadJson.Contains(draftId.ToString())))
			throw new Volo.Abp.Authorization.AbpAuthorizationException();
		return new() { FileName = draft.FileName, ContentType = draft.ContentType, Content = await blobs.GetAllBytesAsync(draft.BlobName) };
	}
}
