using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.Portal;

[Authorize]
[RemoteService(false)]
public class MobileStudentPhotoAppService(StudentPortalPhotoAuthority authority,
	IRepository<StudentRecordPhotoDraft, Guid> drafts, IRepository<StudentRecordChange, Guid> changes,
	IRepository<StudentRecordChangeDecision, Guid> decisions, IBlobContainer blobs, StudentRecordPayloadService payloads,
	StudentRecordsPolicyResolver policies, StudentRecordChangeApplication application, IStudentPhotoChangeLock photoLock)
	: ApplicationService, IMobileStudentPhotoAppService
{
	internal const string PortalOrigin = "PortalProfilePhoto";
	public async Task<StudentPhotoInfoDto> GetInfoAsync(Guid studentId)
	{
		var student = await authority.EnsureAsync(studentId, false);
		var snapshot = StudentRecordPayloadService.Hash(await payloads.CaptureAsync(studentId, "photo"));
		var photoChanges = (await changes.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.Section == "photo" && x.RequiredApprovals == 0);
		var active = await AsyncExecuter.ToListAsync(photoChanges.Where(x => x.Status == "Approved" || x.Status == "Scheduled")
			.OrderByDescending(x => x.CreationTime).ThenByDescending(x => x.Id));
		var pending = active.FirstOrDefault(x => x.GetProperty<string>("Origin") == PortalOrigin);
		if (pending == null)
		{
			var latest = await AsyncExecuter.FirstOrDefaultAsync(photoChanges.OrderByDescending(x => x.CreationTime).ThenByDescending(x => x.Id));
			if (latest?.RequestedBy == CurrentUser.Id && latest.GetProperty<string>("Origin") == PortalOrigin) pending = latest;
		}
		return new() { CanView = true, CanUpdate = true,
			HasPhoto = !string.IsNullOrWhiteSpace(student.PhotoBlobName), Url = student.PhotoBlobName == null ? null : PhotoUrl(studentId),
			Version = student.PhotoBlobName == null ? null : snapshot, ExpectedSnapshot = snapshot,
			RequiresApproval = false, PendingChange = pending == null ? null : await MapAsync(pending) };
	}

	public async Task<StudentPhotoDownloadDto?> GetPhotoAsync(Guid studentId)
	{
		var student = await authority.EnsureAsync(studentId, false);
		if (string.IsNullOrWhiteSpace(student.PhotoBlobName)) return null;
		return await DownloadAsync(student.PhotoBlobName, ContentType(student.PhotoBlobName));
	}

	public async Task<StudentRecordPhotoDraftDto> UploadAsync(Guid studentId, UploadStudentPhotoDto input)
	{
		await authority.EnsureAsync(studentId, true);
		ValidateUpload(input);
		var id = GuidGenerator.Create();
		var extension = Path.GetExtension(input.FileName).ToLowerInvariant();
		var blob = $"student-record-photo/{CurrentTenant.Id}/{studentId}/{id:N}{extension}";
		await blobs.SaveAsync(blob, input.Content, overrideExisting: false);
		try
		{
			using var work = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
			await authority.EnsureAsync(studentId, true);
			await drafts.InsertAsync(new(id, CurrentTenant.Id, studentId, CurrentUser.GetId(), blob, ContentType(input.FileName),
				Path.GetFileName(input.FileName), new DateTimeOffset(Clock.Now.ToUniversalTime()).AddDays(30)), true);
			await work.CompleteAsync();
		}
		catch { await blobs.DeleteAsync(blob); throw; }
		return new() { Id = id, PhotoUrl = DraftUrl(studentId, id) };
	}

	public async Task<StudentPhotoDownloadDto> GetDraftAsync(Guid studentId, Guid draftId)
	{
		await authority.EnsureAsync(studentId, false);
		var draft = await drafts.FindAsync(x => x.Id == draftId && x.StudentId == studentId && x.TenantId == CurrentTenant.Id && x.UploadedBy == CurrentUser.Id)
			?? throw new AbpAuthorizationException();
		return await DownloadAsync(draft.BlobName, draft.ContentType);
	}

	public async Task<StudentPhotoChangeDto> SubmitAsync(Guid studentId, SubmitStudentPhotoChangeDto input)
	{
		Guid id;
		bool applyImmediately;
		// ReadCommitted is deliberate: after waiting for the transaction lock, reads must see the previous commit.
		using (var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.ReadCommitted }, requiresNew: true))
		{
			await photoLock.AcquireAsync(CurrentTenant.Id, studentId);
			var allowed = await authority.EnsureAsync(studentId, true);
			if (input.PhotoDraftId == Guid.Empty || string.IsNullOrWhiteSpace(input.ClientKey) || input.ClientKey.Length > 80 || input.ExpectedSnapshot?.Length != 64)
				throw new BusinessException("Students:Records:InvalidPayload");
			var json = StudentRecordPayloadService.Canonical(JsonSerializer.Serialize(new StudentRecordPhotoPayload { PhotoDraftId = input.PhotoDraftId }, StudentRecordPayloadService.JsonOptions));
			var existing = await changes.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.ClientKey == input.ClientKey);
			if (existing != null)
			{
				if (existing.RequestedBy != CurrentUser.Id || existing.Section != "photo" || existing.GetProperty<string>("Origin") != PortalOrigin ||
					StudentRecordPayloadService.Canonical(existing.PayloadJson) != json || StudentRecordPayloadService.Hash(existing.BeforeJson) != input.ExpectedSnapshot)
					throw new BusinessException("Students:Records:IdempotencyConflict");
				id = existing.Id;
				applyImmediately = existing.RequiredApprovals == 0;
			}
			else
			{
				var active = await changes.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.Section == "photo" && x.RequiredApprovals == 0 &&
					(x.Status == "Approved" || x.Status == "Scheduled"));
				if (active.Any(x => x.GetProperty<string>("Origin") == PortalOrigin)) throw new BusinessException("Students:Records:PhotoChangePending");
				await payloads.EnsurePhotoDraftAsync(studentId, CurrentUser.GetId(), json);
				var before = await payloads.CaptureAsync(studentId, "photo");
				if (StudentRecordPayloadService.Hash(before) != input.ExpectedSnapshot) throw new BusinessException("Students:Records:StaleSnapshot");
				var year = allowed.AcademicYearId ?? throw new BusinessException("Students:Records:EnrollmentMissing");
				var policy = await policies.ResolveAsync(year);
				// Retain the policy snapshot for audit; mobile photos never require school approval.
				var item = new StudentRecordChange(GuidGenerator.Create(), CurrentTenant.Id, studentId, year, CurrentUser.GetId(), "photo", "Photo",
					input.ClientKey, "Portal profile photo update", before, json, policy.Policy.ToJson(), policy.VersionId,
					"[]", 0, new DateTimeOffset(Clock.Now.ToUniversalTime()));
				item.SetProperty("Origin", PortalOrigin);
				await changes.InsertAsync(item);
				id = item.Id;
				applyImmediately = item.RequiredApprovals == 0;
			}
			await work.CompleteAsync();
		}
		if (applyImmediately) await application.ApplyOrScheduleAsync(id);
		var result = await GetChangeAsync(studentId, id);
		if (result.Status != "Applied") throw new BusinessException(result.ExceptionCode ?? "Students:Records:PhotoNotApplied");
		return result;
	}

	public async Task<StudentPhotoChangeDto> GetChangeAsync(Guid studentId, Guid changeId)
	{
		await authority.EnsureAsync(studentId, false);
		var item = await changes.FindAsync(x => x.Id == changeId && x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.Section == "photo")
			?? throw new EntityNotFoundException(typeof(StudentRecordChange), changeId);
		if (item.RequestedBy != CurrentUser.Id || item.GetProperty<string>("Origin") != PortalOrigin) throw new AbpAuthorizationException();
		return await MapAsync(item);
	}

	private async Task<StudentPhotoChangeDto> MapAsync(StudentRecordChange item)
	{
		var draft = JsonSerializer.Deserialize<StudentRecordPhotoPayload>(item.PayloadJson, StudentRecordPayloadService.JsonOptions)!.PhotoDraftId;
		var rejection = item.Status == "Rejected" ? (await decisions.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.ChangeId == item.Id && x.Decision == "Rejected")).FirstOrDefault()?.Reason : null;
		return new() { Id = item.Id, StudentId = item.StudentId, Status = item.Status, CreatedAt = item.CreationTime,
			ProposedPhotoUrl = item.RequestedBy == CurrentUser.Id && draft.HasValue ? DraftUrl(item.StudentId, draft.Value) : null,
			RejectionReason = rejection, ExceptionCode = item.ExceptionCode };
	}
	private async Task<StudentPhotoDownloadDto> DownloadAsync(string blob, string type) => new() { FileName = Path.GetFileName(blob), ContentType = type, Content = await blobs.GetAllBytesAsync(blob) };
	private static string PhotoUrl(Guid id) => $"/api/mobile/v1/students/{id}/profile/photo";
	private static string DraftUrl(Guid id, Guid draft) => $"{PhotoUrl(id)}/drafts/{draft}";
	private static string ContentType(string file) => Path.GetExtension(file).ToLowerInvariant() switch { ".png" => "image/png", ".gif" => "image/gif", ".webp" => "image/webp", _ => "image/jpeg" };
	internal static void ValidateUpload(UploadStudentPhotoDto input)
	{
		if (input.Content == null || input.Content.Length == 0) throw new BusinessException("Students:Records:InvalidPhoto");
		if (input.Content.Length > 3 * 1024 * 1024) throw new BusinessException("Students:Records:PhotoTooLarge");
		if (string.IsNullOrWhiteSpace(input.FileName) || input.FileName.Length > 255 || Path.GetFileName(input.FileName) != input.FileName ||
			input.FileName.Contains('/') || input.FileName.Contains('\\')) throw new BusinessException("Students:Records:InvalidPhoto");
		var extension = Path.GetExtension(input.FileName).ToLowerInvariant();
		if (extension is not (".jpg" or ".jpeg" or ".png") || !FileValidationService.IsMatchingDocumentContentType(extension, input.ContentType))
			throw new BusinessException("Students:Records:InvalidPhoto");
		try { FileValidationService.ValidateDocumentFileSignature(input.Content, extension); }
		catch (BusinessException) { throw new BusinessException("Students:Records:InvalidPhoto"); }
		StudentPhotoImageValidation.EnsureDecodable(input.Content, extension == ".png");
	}
}
