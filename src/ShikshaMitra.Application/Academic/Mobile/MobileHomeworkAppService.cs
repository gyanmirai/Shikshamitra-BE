using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Academic.Mobile;

[Authorize]
[RemoteService(false)]
public class MobileHomeworkAppService(
	MobileAcademicContextAppService context,
	IRepository<HomeworkAssignmentEntity, Guid> homework,
	IRepository<AssignmentEntity, Guid> assignments,
	IRepository<StudentSubmissionEntity, Guid> submissions,
	MobileHomeworkSubmissionService writer,
	IRepository<Subject, Guid> subjects,
	MobileAttachmentStore files, IRepository<AssessmentRubric, Guid> rubrics,
	IRepository<RubricEvaluation, Guid> evaluations, IRepository<IdentityUser, Guid> users) : ApplicationService, IMobileHomeworkAppService
{
	private record Work(Guid Id, Guid SubjectId, string Title, string Description, string Type, SubmissionType SubmissionType,
		DateTime Assigned, DateTime Due, bool Closed, decimal? TotalMarks, string? Attachments, Guid? CreatorId);

	private async Task<List<Work>> ReadWorksAsync(MobilePlacement p)
	{
		var selectedSubjects = await context.GetSubjectsAsync(p);
		// Older learning tasks may reference the class subject assignment instead of the subject master.
		var subjectIds = selectedSubjects.ToDictionary(x => x.Id, x => x.SubjectId);
		foreach (var subject in selectedSubjects) subjectIds[subject.SubjectId] = subject.SubjectId;
		var selected = subjectIds.Keys.ToArray();
		// ResolveAsync already validates that this class belongs to the selected academic year.
		// Web homework creation historically left AcademicYearId null.
		var hq = (await homework.WithDetailsAsync(x => x.DivisionSchedules)).Where(x => x.TenantId == CurrentTenant.Id &&
			x.StandardId == p.StandardId && (!x.AcademicYearId.HasValue || x.AcademicYearId == p.Year.Id) && Enumerable.Contains(selected, x.SubjectId) &&
			((!x.DivisionSchedules.Any() && x.DivisionId == p.DivisionId && x.IsPublished) ||
			 x.DivisionSchedules.Any(s => s.DivisionId == p.DivisionId && s.IsPublished && (!s.PublishedAt.HasValue || s.PublishedAt <= Clock.Now))));
		var aq = (await assignments.WithDetailsAsync(x => x.DivisionSchedules)).Where(x => x.TenantId == CurrentTenant.Id &&
			x.StandardId == p.StandardId && (!x.AcademicYearId.HasValue || x.AcademicYearId == p.Year.Id) && Enumerable.Contains(selected, x.SubjectId) &&
			x.DivisionSchedules.Any(s => s.DivisionId == p.DivisionId && s.IsPublished && (!s.PublishedAt.HasValue || s.PublishedAt <= Clock.Now)));
		var hs = await AsyncExecuter.ToListAsync(hq);
		var ass = await AsyncExecuter.ToListAsync(aq);
		return hs.Select(x => new Work(x.Id, subjectIds[x.SubjectId], x.Title, x.Description, "homework", x.SubmissionType,
			x.DivisionSchedules.FirstOrDefault(s => s.DivisionId == p.DivisionId)?.PublishedAt ?? x.CreationTime,
			x.DivisionSchedules.FirstOrDefault(s => s.DivisionId == p.DivisionId)?.DueDate ?? x.DueDate ?? x.CreationTime,
			x.Status == HomeworkStatus.Closed, null, x.AttachmentsJson ?? x.DocumentUrl, x.CreatorId))
			.Concat(ass.Select(x => new Work(x.Id, subjectIds[x.SubjectId], x.Title, x.Description, "assignment", x.SubmissionType,
				x.DivisionSchedules.First(s => s.DivisionId == p.DivisionId).PublishedAt ?? x.CreationTime,
				x.DivisionSchedules.First(s => s.DivisionId == p.DivisionId).DueDate, x.Status == AssignmentStatus.Closed, x.TotalMarks, x.AttachmentsJson, x.CreatorId)))
			.OrderBy(x => x.Due).ThenBy(x => x.Id).ToList();
	}

	public async Task<List<MobileHomeworkSubjectDto>> GetSubjectsAsync(MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		var assigned = await context.GetSubjectsAsync(p);
		var ids = assigned.Select(x => x.SubjectId).Distinct().ToArray();
		var names = (await subjects.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, x.Id)))
			.ToDictionary(x => x.Id, x => x.Name);
		return assigned.Where(x => names.ContainsKey(x.SubjectId))
			.Select(x => new MobileHomeworkSubjectDto(x.SubjectId, names[x.SubjectId], x.SubjectCode))
			.OrderBy(x => x.Name).ThenBy(x => x.Id).ToList();
	}

	public async Task<MobilePage<MobileHomeworkDto>> GetListAsync(MobileListInput input)
	{
		var p = await context.ResolveAsync(input);
		var works = await ReadWorksAsync(p);
		var ids = works.Select(x => x.Id).ToArray();
		var responses = await submissions.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id &&
			((x.HomeworkAssignmentId.HasValue && Enumerable.Contains(ids, x.HomeworkAssignmentId.Value)) ||
			 (x.AssignmentId.HasValue && Enumerable.Contains(ids, x.AssignmentId.Value))));
		var subjectIds = works.Select(x => x.SubjectId).Distinct().ToArray();
		var names = (await subjects.GetListAsync(x => Enumerable.Contains(subjectIds, x.Id))).ToDictionary(x => x.Id, x => x.Name);
		var actorIds = works.Where(x => x.CreatorId.HasValue).Select(x => x.CreatorId!.Value).Distinct().ToArray();
		var actors = (await users.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(actorIds, x.Id))).ToDictionary(x => x.Id,
			x => string.Join(" ", new[] { x.Name, x.Surname }.Where(n => !string.IsNullOrWhiteSpace(n))));
		var codes = (await context.GetSubjectsAsync(p)).ToDictionary(x => x.SubjectId, x => x.SubjectCode);
		var rows = works.Select(w => {
			var dto = Map(w, responses.SingleOrDefault(s => (s.AssignmentId ?? s.HomeworkAssignmentId) == w.Id), names.GetValueOrDefault(w.SubjectId, ""), p);
			dto.TeacherName = w.CreatorId.HasValue ? actors.GetValueOrDefault(w.CreatorId.Value, "") : "";
			dto.SubjectCode = codes.GetValueOrDefault(w.SubjectId, "");
			dto.Description = "";
			return dto;
		}).AsEnumerable();
		if (input.SubjectId.HasValue) rows = rows.Where(x => x.SubjectId == input.SubjectId);
		if (!string.IsNullOrWhiteSpace(input.Search)) rows = rows.Where(x => x.Title.Contains(input.Search.Trim(), StringComparison.OrdinalIgnoreCase));
		if (!string.IsNullOrEmpty(input.Status)) rows = rows.Where(x => x.Status == input.Status);
		if (input.Tab == "completed") rows = rows.Where(x => x.Status is "completed" or "checked" or "submitted");
		if (input.Tab == "pending") rows = rows.Where(x => x.Status is "pending" or "draft" or "late" or "submittedEditable");
		if (input.Tab == "today" || input.Date.HasValue)
		{
			var date = input.Date ?? DateOnly.FromDateTime(MobileAcademicContextAppService.SchoolToday);
			rows = rows.Where(x => SchoolDate(x.AssignedDate) == date || SchoolDate(x.DueDate) == date);
		}
		return MobileAcademicContextAppService.Page(rows, input, p);
	}

	private MobileHomeworkDto Map(Work w, StudentSubmissionEntity? s, string subjectName, MobilePlacement p)
	{
		var editable = MobileSubmissionRules.CanEdit(s, w.Due, Clock.Now, w.Closed);
		var supported = w.SubmissionType is SubmissionType.FileUpload or SubmissionType.OnlineText or SubmissionType.InPersonSubmission or SubmissionType.NoSubmissionRequired;
		var canSubmit = supported && MobileSubmissionRules.CanSubmit(s, w.Closed);
		var submitted = s?.SubmissionDate != null;
		var status = s?.Status switch { SubmissionStatus.Draft => "draft", SubmissionStatus.Reviewed => "checked",
			SubmissionStatus.Excused => "completed",
			SubmissionStatus.NeedsReview when s.SubmissionDate.HasValue => "submitted",
			SubmissionStatus.Submitted or SubmissionStatus.Late => editable ? "submittedEditable" : "submitted",
			_ => Clock.Now > w.Due ? "late" : "pending" };
		return new() { Id = w.Id, SubjectId = w.SubjectId, SubjectName = subjectName, Title = w.Title, Description = w.Description,
			Type = w.Type, SubmissionType = w.SubmissionType switch { SubmissionType.FileUpload => "fileUpload", SubmissionType.OnlineText => "online", SubmissionType.NoSubmissionRequired => "noSubmissionRequired", _ => "inPerson" },
			AssignedDate = w.Assigned, DueDate = w.Due, Status = status, CanSubmit = canSubmit,
			CanSaveDraft = canSubmit, CanEdit = editable && supported && submitted,
			SubmittedDate = s?.SubmissionDate?.ToUniversalTime(), TextAnswer = s?.SubmittedContent, TextAnswerHtml = s?.SubmittedContentHtml,
			Acknowledged = s?.Acknowledged ?? false,
			Revision = s?.ConcurrencyStamp ?? "0", SavedAt = s?.MobileSavedAt?.ToUniversalTime(), UpdatedAt = s?.MobileUpdatedAt?.ToUniversalTime(),
			SubmissionReasonCode = !supported ? MobileSubmissionErrorCodes.SubmissionNotSupported : w.Closed ? MobileSubmissionErrorCodes.SubmissionLocked :
				submitted && Clock.Now > w.Due ? MobileSubmissionErrorCodes.DeadlinePassed : !canSubmit && !editable ? MobileSubmissionErrorCodes.SubmissionLocked : null,
			Marks = s?.Status == SubmissionStatus.Reviewed ? s.Marks : null, MaxMarks = w.TotalMarks,
			Feedback = s?.Status == SubmissionStatus.Reviewed ? s.Feedback : null, ReviewedOn = s?.Status == SubmissionStatus.Reviewed ? s.ReviewedAt : null,
			SubmissionAttachments = MobileAttachmentStore.Read(s?.MobileAttachmentsJson).Select(x => MobileAttachmentStore.Map(x, DownloadRoute(w.Id, x.Id, p))).ToList() };
	}
	private static DateOnly SchoolDate(DateTime value) => DateOnly.FromDateTime(value.ToUniversalTime().AddHours(5.5));

	public async Task<MobileHomeworkDto> GetAsync(Guid id, MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		var w = (await ReadWorksAsync(p)).SingleOrDefault(x => x.Id == id) ?? throw new EntityNotFoundException(typeof(HomeworkAssignmentEntity), id);
		var s = await submissions.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id && (x.HomeworkAssignmentId == id || x.AssignmentId == id));
		var subject = await subjects.GetAsync(w.SubjectId);
		var dto = Map(w, s, subject.Name, p);
		dto.SubjectCode = (await context.GetSubjectsAsync(p)).Single(x => x.SubjectId == w.SubjectId).SubjectCode;
		var actor = w.Type == "assignment" ? (await assignments.GetAsync(id)).CreatorId : (await homework.GetAsync(id)).CreatorId;
		if (actor.HasValue) {
			var teacher = await users.FindAsync(x => x.TenantId == CurrentTenant.Id && x.Id == actor);
			dto.TeacherName = string.Join(" ", new[] { teacher?.Name, teacher?.Surname }.Where(x => !string.IsNullOrWhiteSpace(x)));
		}
		var rubric = await AsyncExecuter.SingleOrDefaultAsync((await rubrics.WithDetailsAsync(x => x.Criteria)).Where(x => x.TenantId == CurrentTenant.Id &&
			x.Status == RubricStatus.Published && !x.IsTemplate && (w.Type == "assignment" ? x.AssignmentId == id : x.HomeworkId == id)));
		if (rubric != null) {
			var evaluation = s == null ? null : await AsyncExecuter.SingleOrDefaultAsync((await evaluations.WithDetailsAsync(x => x.Scores)).Where(x =>
				x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id && x.StudentSubmissionId == s.Id && x.RubricId == rubric.Id && x.Status == RubricEvaluationStatus.Approved));
			dto.Rubric = rubric.Criteria.OrderBy(x => x.DisplayOrder).Select(x => new MobileRubricScoreDto(x.Title, rubric.GetCriterionMaximum(x),
				evaluation?.Scores.SingleOrDefault(score => score.CriterionId == x.Id)?.Marks)).ToList();
			if (evaluation != null) dto.Feedback = evaluation.Remarks;
		}
		var attachments = AssignmentAttachmentSerializer.ReadLegacyList(w.Attachments) ?? [];
		dto.Attachments = attachments.Select((a, i) => new MobileAttachmentDto(AttachmentId(id, i), a.Name, DownloadRoute(id, AttachmentId(id, i), p), Mime(a.Name), a.Size)).ToList();
		return dto;
	}

	public Task<MobileHomeworkDto> SaveAsync(Guid id, MobileSubmissionInput input, bool draft, bool replace) =>
		writer.SaveAsync(id, input, draft, replace, () => GetAsync(id, input));

	public Task<MobileAttachmentDto> UploadAsync(Guid id, MobileAcademicInput input, IRemoteStreamContent file, Guid? clientUploadId = null) =>
		writer.UploadAsync(id, input, file, clientUploadId, () => GetAsync(id, input));

	public Task DeleteAttachmentAsync(Guid id, Guid attachmentId, MobileAcademicInput input) =>
		writer.DeleteAsync(id, attachmentId, input, () => GetAsync(id, input));

	public async Task<MobileFileDto> DownloadAsync(Guid id, Guid attachmentId, MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		var w = (await ReadWorksAsync(p)).SingleOrDefault(x => x.Id == id) ?? throw new EntityNotFoundException(typeof(HomeworkAssignmentEntity), id);
		var s = await submissions.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id && (x.HomeworkAssignmentId == id || x.AssignmentId == id));
		var saved = MobileAttachmentStore.Read(s?.MobileAttachmentsJson).SingleOrDefault(x => x.Id == attachmentId);
		if (saved != null) return await files.DownloadAsync(saved);
		var resources = AssignmentAttachmentSerializer.ReadLegacyList(w.Attachments) ?? [];
		for (var i = 0; i < resources.Count; i++)
			if (AttachmentId(id, i) == attachmentId) return await files.DownloadPublishedAsync(resources[i].Url, resources[i].Name, Mime(resources[i].Name));
		return await files.DownloadAsync((await files.ResolveAsync(p.Student.Id, id, [attachmentId])).Single());
	}

	private static Guid AttachmentId(Guid id, int index) { var bytes = id.ToByteArray(); bytes[0] ^= 0x80; BitConverter.GetBytes(index).CopyTo(bytes, 12); return new(bytes); }
	private static string DownloadRoute(Guid id, Guid attachment, MobilePlacement p) => $"/api/mobile/v1/academics/homework/{id}/attachments/{attachment}/download?studentId={p.Student.Id}&academicYearId={p.Year.Id}";
	private static string Mime(string name) => System.IO.Path.GetExtension(name).ToLowerInvariant() switch { ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document", _ => "application/octet-stream" };
}
