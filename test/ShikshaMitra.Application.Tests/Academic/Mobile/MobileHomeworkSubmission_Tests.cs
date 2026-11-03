using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
	[Theory]
	[InlineData(false, SubmissionType.InPersonSubmission, false)]
	[InlineData(false, SubmissionType.NoSubmissionRequired, true)]
	[InlineData(true, SubmissionType.InPersonSubmission, true)]
	[InlineData(true, SubmissionType.NoSubmissionRequired, false)]
	public Task Completion_acknowledgment_persists_and_exposes_the_submitted_response(bool assignment, SubmissionType type, bool attachFile) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		Guid id;
		if (assignment)
		{
			f.Assignment.SubmissionType = type;
			await GetRequiredService<IRepository<AssignmentEntity, Guid>>().UpdateAsync(f.Assignment, true); id = f.Assignment.Id;
		}
		else
		{
			var work = new HomeworkAssignmentEntity(Guid.NewGuid(), f.Enrollment.StandardId!.Value, f.Enrollment.DivisionId!.Value,
				f.SubjectId, "Complete this work", "Practice", Guid.NewGuid(), type, DateTime.UtcNow.AddDays(1), tenantId, f.Year.Id)
				{ HomeworkAssignmentTypeId = null, IsPublished = true };
			await Insert(work); id = work.Id;
		}
		var service = GetRequiredService<IMobileHomeworkAppService>();
		var input = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id,
			TextAnswerHtml = "<p><br></p>", ExpectedRevision = "0", ClientRequestId = Guid.NewGuid() };
		(await service.GetAsync(id, input)).CanSubmit.ShouldBeTrue();
		var draft = await service.SaveAsync(id, input, true, false);
		draft.Acknowledged.ShouldBeFalse(); draft.SubmittedDate.ShouldBeNull();
		input.ExpectedRevision = draft.Revision; input.ClientRequestId = Guid.NewGuid();
		(await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(id, input, false, false))).Code.ShouldBe(MobileSubmissionErrorCodes.AcknowledgmentRequired);
		if (attachFile)
		{
			var file = await service.UploadAsync(id, input, new RemoteStreamContent(new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\ncompleted")), "evidence.pdf", "application/pdf"), Guid.NewGuid());
			input.AttachmentIds = [file.Id];
		}
		input.Acknowledged = true; input.ClientRequestId = Guid.NewGuid();
		var submitted = await service.SaveAsync(id, input, false, false);
		submitted.Acknowledged.ShouldBeTrue(); submitted.SubmittedDate.ShouldNotBeNull(); submitted.CanSubmit.ShouldBeFalse(); submitted.CanEdit.ShouldBeTrue();
		submitted.SubmissionAttachments.Count.ShouldBe(attachFile ? 1 : 0);
		(await service.SaveAsync(id, input, false, false)).Revision.ShouldBe(submitted.Revision);
		input.Acknowledged = false;
		(await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(id, input, false, false))).Code.ShouldBe(MobileSubmissionErrorCodes.IdempotencyConflict);
		var detail = await service.GetAsync(id, input);
		detail.Acknowledged.ShouldBeTrue(); detail.SubmittedDate.ShouldBe(submitted.SubmittedDate);
		var history = await GetRequiredService<IRepository<MobileSubmissionSnapshot, Guid>>().GetListAsync(x => x.TaskId == id);
		history.Single().Acknowledged.ShouldBeTrue();
	});

	[Theory]
	[InlineData(false, SubmissionType.OnlineText)]
	[InlineData(false, SubmissionType.FileUpload)]
	[InlineData(true, SubmissionType.OnlineText)]
	[InlineData(true, SubmissionType.FileUpload)]
	public Task Overdue_work_allows_one_first_submission_but_no_edit(bool assignment, SubmissionType type) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		var due = DateTime.UtcNow.AddDays(-1);
		Guid taskId;
		if (assignment)
		{
			f.Assignment.SubmissionType = type;
			f.Assignment.DivisionSchedules.Single().DueDate = due;
			await GetRequiredService<IRepository<AssignmentEntity, Guid>>().UpdateAsync(f.Assignment, true);
			taskId = f.Assignment.Id;
		}
		else
		{
			var homework = new HomeworkAssignmentEntity(Guid.NewGuid(), f.Enrollment.StandardId!.Value, f.Enrollment.DivisionId!.Value,
				f.SubjectId, "Overdue homework", "Practice", Guid.NewGuid(), type, due, tenantId, f.Year.Id)
				{ HomeworkAssignmentTypeId = null, IsPublished = true };
			await Insert(homework); taskId = homework.Id;
		}
		var service = GetRequiredService<IMobileHomeworkAppService>();
		var input = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id,
			TextAnswerHtml = "<p>Completed late</p>", ExpectedRevision = "0", ClientRequestId = Guid.NewGuid() };
		var detail = await service.GetAsync(taskId, input);
		detail.Status.ShouldBe("late"); detail.CanSubmit.ShouldBeTrue(); detail.CanSaveDraft.ShouldBeTrue();
		detail.CanEdit.ShouldBeFalse(); detail.SubmissionReasonCode.ShouldBeNull();
		var draft = await service.SaveAsync(taskId, input, true, false);
		input.ExpectedRevision = draft.Revision; input.ClientRequestId = Guid.NewGuid();
		var file = await service.UploadAsync(taskId, input, new RemoteStreamContent(
			new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\nlate work")), "late.pdf", "application/pdf"), Guid.NewGuid());
		input.AttachmentIds = [file.Id];
		var first = await service.SaveAsync(taskId, input, false, false);
		first.Status.ShouldBe("submitted"); first.CanSubmit.ShouldBeFalse(); first.CanSaveDraft.ShouldBeFalse(); first.CanEdit.ShouldBeFalse();
		first.SubmissionReasonCode.ShouldBe(MobileSubmissionErrorCodes.DeadlinePassed);
		(await service.SaveAsync(taskId, input, false, false)).Revision.ShouldBe(first.Revision);
		input.ExpectedRevision = first.Revision; input.ClientRequestId = Guid.NewGuid();
		await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(taskId, input, false, false));
		await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(taskId, input, false, true));
		await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(taskId, input, true, false));
		var current = await service.GetAsync(taskId, input);
		current.Revision.ShouldBe(first.Revision); current.SubmittedDate.ShouldBe(first.SubmittedDate);
		var row = await GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().GetAsync(x => x.StudentId == f.Student.Id &&
			(x.HomeworkAssignmentId == taskId || x.AssignmentId == taskId));
		row.Status.ShouldBe(SubmissionStatus.Late);
	});

	[Fact]
	public Task Html_drafts_retry_safely_and_stale_revisions_never_overwrite() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		var service = GetRequiredService<IMobileHomeworkAppService>();
		var input = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id,
			TextAnswerHtml = "<p>Hello <strong>world</strong><script>alert(1)</script><img src='https://example.test/x'></p>",
			ExpectedRevision = "0", ClientRequestId = Guid.NewGuid() };
		var first = await service.SaveAsync(f.Assignment.Id, input, true, false);
		first.Status.ShouldBe("draft"); first.TextAnswer.ShouldBe("Hello world");
		first.TextAnswerHtml.ShouldNotBeNull().ShouldNotContain("script"); first.TextAnswerHtml.ShouldNotBeNull().ShouldNotContain("img");
		first.TextAnswerHtml.ShouldContain("<strong>world</strong>");
		first.SubmittedDate.ShouldBeNull(); first.SavedAt.ShouldNotBeNull(); first.Revision.ShouldNotBe("0");
		var replay = await service.SaveAsync(f.Assignment.Id, input, true, false);
		replay.Revision.ShouldBe(first.Revision); replay.SavedAt.ShouldBe(first.SavedAt);
		input.TextAnswerHtml = "<p>Changed</p>";
		(await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(f.Assignment.Id, input, true, false))).Code.ShouldBe("MobileAcademics:IdempotencyConflict");
		input.ClientRequestId = Guid.NewGuid();
		(await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(f.Assignment.Id, input, true, false))).Code.ShouldBe("MobileAcademics:SubmissionConflict");
		(await service.GetAsync(f.Assignment.Id, input)).TextAnswer.ShouldBe("Hello world");
	});

	[Fact]
	public Task Final_submission_keeps_first_timestamp_and_protects_previous_files() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		var service = GetRequiredService<IMobileHomeworkAppService>();
		var uploadId = Guid.NewGuid();
		async Task<MobileAttachmentDto> Upload() => await service.UploadAsync(f.Assignment.Id,
			new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id },
			new RemoteStreamContent(new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\nsolution")), "answer.pdf", "application/pdf"), uploadId);
		var attachment = await Upload();
		(await Upload()).Id.ShouldBe(attachment.Id);
		var input = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id,
			TextAnswerHtml = "<p>Original answer</p>", AttachmentIds = [attachment.Id], ExpectedRevision = "0", ClientRequestId = Guid.NewGuid() };
		var first = await service.SaveAsync(f.Assignment.Id, input, false, false);
		first.CanSubmit.ShouldBeFalse(); first.CanEdit.ShouldBeTrue(); first.CanSaveDraft.ShouldBeFalse();
		(await service.SaveAsync(f.Assignment.Id, input, false, false)).Revision.ShouldBe(first.Revision);
		input.ClientRequestId = Guid.NewGuid(); input.ExpectedRevision = first.Revision;
		await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(f.Assignment.Id, input, true, false));
		input.TextAnswerHtml = "<p>Replacement</p>"; input.AttachmentIds = [];
		var next = await service.SaveAsync(f.Assignment.Id, input, false, true);
		next.SubmittedDate.ShouldBe(first.SubmittedDate); next.Revision.ShouldNotBe(first.Revision);
		var history = await GetRequiredService<IRepository<MobileSubmissionSnapshot, Guid>>().GetListAsync(x => x.TaskId == f.Assignment.Id);
		history.Count.ShouldBe(2); history.ShouldContain(x => x.AttachmentsJson!.Contains(attachment.Id.ToString()));
		(await Should.ThrowAsync<BusinessException>(() => service.DeleteAttachmentAsync(f.Assignment.Id, attachment.Id, input)))
			.Code.ShouldBe("MobileAcademics:FinalAttachmentProtected");
	});

	[Fact]
	public Task Teacher_review_changes_revision_and_blocks_pending_edits() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		var service = GetRequiredService<IMobileHomeworkAppService>();
		var input = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id,
			TextAnswerHtml = "<p>Answer</p>", ExpectedRevision = "0", ClientRequestId = Guid.NewGuid() };
		var first = await service.SaveAsync(f.Assignment.Id, input, false, false);
		var repository = GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>();
		var row = await repository.GetAsync(x => x.StudentId == f.Student.Id && x.AssignmentId == f.Assignment.Id);
		row.UpdateFeedback(10, 20, "Reviewed", null, SubmissionStatus.Reviewed, f.User.Id);
		await repository.UpdateAsync(row, true);
		(await service.GetAsync(f.Assignment.Id, input)).Revision.ShouldNotBe(first.Revision);
		input.ExpectedRevision = first.Revision; input.ClientRequestId = Guid.NewGuid();
		(await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(f.Assignment.Id, input, false, true)))
			.Code.ShouldBe("MobileAcademics:SubmissionConflict");
	});

	[Theory]
	[InlineData(SubmissionType.OnlineText)]
	[InlineData(SubmissionType.FileUpload)]
	public Task Homework_respects_teacher_requirements_and_empty_drafts(SubmissionType type) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		var homework = new HomeworkAssignmentEntity(Guid.NewGuid(), f.Enrollment.StandardId!.Value, f.Enrollment.DivisionId!.Value,
			f.SubjectId, "Homework", "Practice", Guid.NewGuid(), type, DateTime.UtcNow.AddDays(1), tenantId, f.Year.Id)
			{ HomeworkAssignmentTypeId = null, IsPublished = true };
		await Insert(homework);
		var service = GetRequiredService<IMobileHomeworkAppService>();
		var input = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id,
			TextAnswerHtml = "<p><br></p>", ExpectedRevision = "0", ClientRequestId = Guid.NewGuid() };
		var draft = await service.SaveAsync(homework.Id, input, true, false);
		draft.Status.ShouldBe("draft"); draft.TextAnswer.ShouldBe("");
		input.ExpectedRevision = draft.Revision; input.ClientRequestId = Guid.NewGuid();
		(await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(homework.Id, input, false, false)))
			.Code.ShouldBe("MobileAcademics:SubmissionRequired");
		input.ClientRequestId = Guid.NewGuid();
		if (type == SubmissionType.OnlineText) input.TextAnswerHtml = "<p>Completed</p>";
		else
		{
			var file = await service.UploadAsync(homework.Id, input, new RemoteStreamContent(
				new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\nhomework")), "work.pdf", "application/pdf"));
			input.AttachmentIds = [file.Id];
		}
		(await service.SaveAsync(homework.Id, input, false, false)).Status.ShouldBe("submittedEditable");
	});

	[Fact]
	public Task Receipt_replay_still_denies_revoked_student_links() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = SignIn(f.User.Id);
		var service = GetRequiredService<IMobileHomeworkAppService>();
		var input = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id,
			TextAnswerHtml = "<p>Draft</p>", ExpectedRevision = "0", ClientRequestId = Guid.NewGuid() };
		await service.SaveAsync(f.Assignment.Id, input, true, false);
		await GetRequiredService<IRepository<ShikshaMitra.Students.Portal.StudentPortalAccountLink, Guid>>().DeleteAsync(f.Link, true);
		await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(() => service.SaveAsync(f.Assignment.Id, input, true, false));
	});

	[Fact]
	public Task Invalid_metadata_and_duplicate_attachments_never_create_a_response() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		var service = GetRequiredService<IMobileHomeworkAppService>();
		var input = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id, TextAnswerHtml = "<p>Answer</p>" };
		(await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(f.Assignment.Id, input, true, false))).Code.ShouldBe("MobileAcademics:RequestMetadataRequired");
		input.ExpectedRevision = "0"; input.ClientRequestId = Guid.NewGuid();
		var id = Guid.NewGuid(); input.AttachmentIds = [id, id];
		(await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(f.Assignment.Id, input, true, false))).Code.ShouldBe("MobileAcademics:InvalidAttachments");
		(await service.GetAsync(f.Assignment.Id, input)).Revision.ShouldBe("0");
	});
}
