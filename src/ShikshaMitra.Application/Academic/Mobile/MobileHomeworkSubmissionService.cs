using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Validation;

namespace ShikshaMitra.Academic.Mobile;

[Authorize, RemoteService(false), DisableValidation, DisableAuditing]
public class MobileHomeworkSubmissionService(
	MobileAcademicContextAppService context, IMobileSubmissionLock submissionLock,
	IRepository<StudentSubmissionEntity, Guid> submissions, StudentSubmissionManager manager,
	IRepository<MobileSubmissionOperation, Guid> operations, IRepository<MobileSubmissionSnapshot, Guid> snapshots,
	MobileAttachmentStore files, MobileAnswerSanitizer sanitizer) : ApplicationService
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	[UnitOfWork(isTransactional: true)]
	public virtual async Task<MobileHomeworkDto> SaveAsync(Guid id, MobileSubmissionInput input, bool draft, bool replace,
		Func<Task<MobileHomeworkDto>> read)
	{
		var p = await context.ResolveAsync(input);
		await submissionLock.AcquireAsync(CurrentTenant.Id!.Value, p.Student.Id, id);
		var detail = await read(); // Revalidate publication and student access before receipt replay.
		if (input.AttachmentIds == null || input.AttachmentIds.Count > 5 || input.AttachmentIds.Distinct().Count() != input.AttachmentIds.Count)
			throw new BusinessException(MobileSubmissionErrorCodes.InvalidAttachments);
		ValidateRequestMetadata(input);
		var operation = draft ? "draft" : replace ? "resubmit" : "submit";
		// Keep existing receipt hashes valid when the additive flag is omitted/false.
		var hash = Hash(JsonSerializer.Serialize(input.Acknowledged
			? (object)new { input.TextAnswer, input.TextAnswerHtml, input.AttachmentIds, input.ExpectedRevision, input.Acknowledged }
			: new { input.TextAnswer, input.TextAnswerHtml, input.AttachmentIds, input.ExpectedRevision }, Json));
		var replay = await ReplayAsync<MobileHomeworkDto>(p, id, operation, input.ClientRequestId, hash);
		if (replay != null) return replay;
		if (input.ExpectedRevision != null && detail.Revision != input.ExpectedRevision)
			throw new BusinessException(MobileSubmissionErrorCodes.SubmissionConflict);
		if (draft ? !detail.CanSaveDraft : replace ? !detail.CanEdit : !detail.CanSubmit)
			throw new BusinessException(detail.SubmissionReasonCode ?? MobileSubmissionErrorCodes.SubmissionConflict);
		var s = await submissions.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id &&
			(x.HomeworkAssignmentId == id || x.AssignmentId == id));
		var answer = sanitizer.Sanitize(input);
		var type = detail.SubmissionType switch { "online" => SubmissionType.OnlineText, "fileUpload" => SubmissionType.FileUpload,
			"inPerson" => SubmissionType.InPersonSubmission, "noSubmissionRequired" => SubmissionType.NoSubmissionRequired,
			_ => throw new BusinessException(MobileSubmissionErrorCodes.SubmissionNotSupported) };
		MobileSubmissionRules.Validate(s, detail.DueDate, Clock.Now, false, type, draft, replace, answer.Text, input.AttachmentIds.Count, input.Acknowledged);
		var attachments = await files.ResolveAsync(p.Student.Id, id, input.AttachmentIds, s?.MobileAttachmentsJson);
		if (s?.SubmissionDate == null) await files.RetainDraftAccessAsync(p.Student.Id, id, s?.MobileAttachmentsJson);
		if (s?.SubmissionDate != null && !await snapshots.AnyAsync(x => x.SubmissionId == s.Id && x.Revision == s.ConcurrencyStamp))
			await snapshots.InsertAsync(new MobileSubmissionSnapshot(GuidGenerator.Create(), s));
		s ??= detail.Type == "assignment"
			? await manager.CreateOrGetSubmissionForAssignmentAsync(id, p.Student.Id, p.DivisionId, detail.DueDate, CurrentTenant.Id)
			: await manager.CreateOrGetSubmissionAsync(id, p.Student.Id, p.DivisionId, detail.DueDate, CurrentTenant.Id);
		// Legacy writers do not use the mobile lock; the manager may have found their just-created row.
		MobileSubmissionRules.Validate(s, detail.DueDate, Clock.Now, false, type, draft, replace, answer.Text, input.AttachmentIds.Count, input.Acknowledged);
		s.SaveMobileResponse(answer.Text, MobileAttachmentStore.Serialize(attachments), Path.GetFileName(attachments.FirstOrDefault()?.BlobName),
			attachments.FirstOrDefault()?.Name, draft, new DateTimeOffset(Clock.Now.ToUniversalTime()), answer.Html, input.Acknowledged);
		try { await submissions.UpdateAsync(s, autoSave: true); }
		catch (AbpDbConcurrencyException exception) { throw new BusinessException(MobileSubmissionErrorCodes.SubmissionConflict, innerException: exception); }
		if (!draft) await snapshots.InsertAsync(new MobileSubmissionSnapshot(GuidGenerator.Create(), s));
		var result = await read();
		await RememberAsync(p, id, operation, input.ClientRequestId, hash, result);
		return result;
	}

	[UnitOfWork(isTransactional: true)]
	public virtual async Task<MobileAttachmentDto> UploadAsync(Guid id, MobileAcademicInput input, IRemoteStreamContent file,
		Guid? requestId, Func<Task<MobileHomeworkDto>> read)
	{
		if (requestId == Guid.Empty) throw new BusinessException(MobileSubmissionErrorCodes.InvalidRequestId);
		var p = await context.ResolveAsync(input);
		await submissionLock.AcquireAsync(CurrentTenant.Id!.Value, p.Student.Id, id);
		var detail = await read();
		using var content = new MemoryStream();
		var buffer = new byte[81920];
		var source = file.GetStream();
		int count;
		while ((count = await source.ReadAsync(buffer)) > 0)
		{
			if (content.Length + count > 5 * 1024 * 1024) throw new BusinessException(MobileSubmissionErrorCodes.SubmissionTooLarge);
			await content.WriteAsync(buffer.AsMemory(0, count));
		}
		var hash = Hash(JsonSerializer.Serialize(new { file.FileName, file.ContentType, Digest = Convert.ToHexString(SHA256.HashData(content.ToArray())) }, Json));
		var replay = await ReplayAsync<MobileAttachmentDto>(p, id, "upload", requestId, hash);
		if (replay != null) return replay;
		if (!detail.CanSubmit && !detail.CanEdit) throw new BusinessException(detail.SubmissionReasonCode ?? MobileSubmissionErrorCodes.SubmissionLocked);
		content.Position = 0;
		using var stream = new RemoteStreamContent(content, file.FileName, file.ContentType);
		var item = await files.UploadAsync(p.Student.Id, id, stream);
		var route = $"/api/mobile/v1/academics/homework/{id}/attachments/{item.Id}/download?studentId={p.Student.Id}&academicYearId={p.Year.Id}";
		var result = MobileAttachmentStore.Map(item, route);
		await RememberAsync(p, id, "upload", requestId, hash, result);
		return result;
	}

	[UnitOfWork(isTransactional: true)]
	public virtual async Task DeleteAsync(Guid id, Guid attachmentId, MobileAcademicInput input, Func<Task<MobileHomeworkDto>> read)
	{
		var p = await context.ResolveAsync(input);
		await submissionLock.AcquireAsync(CurrentTenant.Id!.Value, p.Student.Id, id);
		var detail = await read();
		if (!detail.CanSubmit && !detail.CanEdit) throw new BusinessException(detail.SubmissionReasonCode ?? MobileSubmissionErrorCodes.SubmissionLocked);
		var key = attachmentId.ToString();
		var blobKey = attachmentId.ToString("N");
		if (await snapshots.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id && x.TaskId == id &&
			x.AttachmentsJson != null && (x.AttachmentsJson.Contains(key) || x.AttachmentsJson.Contains(blobKey))))
			throw new BusinessException(MobileSubmissionErrorCodes.FinalAttachmentProtected);
		var s = await submissions.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id &&
			(x.HomeworkAssignmentId == id || x.AssignmentId == id));
		await files.DeleteAsync(p.Student.Id, id, attachmentId, s?.MobileAttachmentsJson);
	}

	private static void ValidateRequestMetadata(MobileSubmissionInput input)
	{
		if (input.ClientRequestId == Guid.Empty || (input.ExpectedRevision != null && input.ExpectedRevision != "0" &&
			!Guid.TryParse(input.ExpectedRevision, out _))) throw new BusinessException(MobileSubmissionErrorCodes.InvalidRequestId);
		if ((input.Acknowledged || input.TextAnswerHtml != null || input.ExpectedRevision != null || input.ClientRequestId != null) &&
			(input.ExpectedRevision == null || input.ClientRequestId == null))
			throw new BusinessException(MobileSubmissionErrorCodes.RequestMetadataRequired);
	}

	private async Task<T?> ReplayAsync<T>(MobilePlacement p, Guid id, string operation, Guid? requestId, string hash) where T : class
	{
		if (!requestId.HasValue) return null;
		var receipt = await operations.FindAsync(x => x.TenantId == CurrentTenant.Id && x.ActorId == CurrentUser.Id &&
			x.StudentId == p.Student.Id && x.AcademicYearId == p.Year.Id && x.TaskId == id && x.Operation == operation && x.RequestId == requestId);
		if (receipt == null) return null;
		if (receipt.PayloadHash != hash) throw new BusinessException(MobileSubmissionErrorCodes.IdempotencyConflict);
		return JsonSerializer.Deserialize<T>(receipt.ResponseJson, Json)!;
	}

	private async Task RememberAsync<T>(MobilePlacement p, Guid id, string operation, Guid? requestId, string hash, T response)
	{
		if (requestId.HasValue) await operations.InsertAsync(new MobileSubmissionOperation(GuidGenerator.Create(), CurrentTenant.Id!.Value,
			CurrentUser.Id!.Value, p.Student.Id, p.Year.Id, id, operation, requestId.Value, hash, JsonSerializer.Serialize(response, Json)), autoSave: true);
	}
	private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
