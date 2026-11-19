using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Mobile;
using ShikshaMitra.Common.Responses;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Academic.Mobile;

public sealed partial class MobileAcademicPostgresApi_Tests
{
	private async Task CheckCompletionAcknowledgmentAsync(HttpClient client, Fixture f, MobileAcademicPostgresFactory factory, Guid tenantId)
	{
		foreach (var type in new[] { SubmissionType.InPersonSubmission, SubmissionType.NoSubmissionRequired })
		{
			Guid id;
			using (var scope = factory.Services.CreateScope())
			{
				using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
				using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
				var work = new HomeworkAssignmentEntity(Guid.NewGuid(), f.Enrollment.StandardId!.Value, f.Enrollment.DivisionId!.Value,
					f.SubjectId, "Completion", "Finish this work", Guid.NewGuid(), type, DateTime.UtcNow.AddDays(-1), tenantId, f.Year.Id)
					{ HomeworkAssignmentTypeId = null, IsPublished = true };
				await scope.ServiceProvider.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().InsertAsync(work, true);
				id = work.Id; await uow.CompleteAsync();
			}
			var path = $"/api/mobile/v1/academics/homework/{id}";
			var query = $"?studentId={f.Student.Id}&academicYearId={f.Year.Id}";
			var input = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id, ExpectedRevision = "0", ClientRequestId = Guid.NewGuid() };
			using var missing = await client.PostAsJsonAsync(path + "/submission", input, JsonOptions);
			missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await missing.Content.ReadAsStringAsync());
			if (type == SubmissionType.NoSubmissionRequired)
			{
				using var form = new MultipartFormDataContent();
				var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\ncompletion"));
				file.Headers.ContentType = new("application/pdf"); form.Add(file, "file", "completed.pdf");
				form.Add(new StringContent(Guid.NewGuid().ToString()), "clientUploadId");
				using var uploaded = await client.PostAsync(path + "/submission/attachments" + query, form);
				uploaded.StatusCode.ShouldBe(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
				input.AttachmentIds = [(await uploaded.Content.ReadFromJsonAsync<Result<MobileAttachmentDto>>(JsonOptions))!.Data!.Id];
			}
			input.Acknowledged = true; input.ClientRequestId = Guid.NewGuid();
			using var saved = await client.PostAsJsonAsync(path + "/submission", input, JsonOptions);
			saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
			var result = (await saved.Content.ReadFromJsonAsync<Result<MobileHomeworkDto>>(JsonOptions))!.Data!;
			result.Acknowledged.ShouldBeTrue(); result.SubmittedDate.ShouldNotBeNull(); result.CanSubmit.ShouldBeFalse(); result.CanEdit.ShouldBeFalse();
			using var detailResponse = await client.GetAsync(path + query);
			var detail = (await detailResponse.Content.ReadFromJsonAsync<Result<MobileHomeworkDto>>(JsonOptions))!.Data!;
			detail.Acknowledged.ShouldBeTrue(); detail.SubmissionAttachments.Count.ShouldBe(type == SubmissionType.NoSubmissionRequired ? 1 : 0);
			using var retry = await client.PostAsJsonAsync(path + "/submission", input, JsonOptions);
			(await retry.Content.ReadFromJsonAsync<Result<MobileHomeworkDto>>(JsonOptions))!.Data!.Revision.ShouldBe(result.Revision);
			input.ExpectedRevision = detail.Revision; input.ClientRequestId = Guid.NewGuid();
			using var edit = await client.PutAsJsonAsync(path + "/submission", input, JsonOptions);
			edit.IsSuccessStatusCode.ShouldBeFalse();
		}
	}

	private async Task CheckHomeworkSubmissionsAsync(HttpClient client, Fixture f, MobileAcademicPostgresFactory factory, Guid tenantId)
	{
		var path = $"/api/mobile/v1/academics/homework/{f.Assignment.Id}";
		var query = $"?studentId={f.Student.Id}&academicYearId={f.Year.Id}";
		var key = Guid.NewGuid();
		async Task<HttpResponseMessage> Upload(string content)
		{
			using var form = new MultipartFormDataContent();
			var bytes = new ByteArrayContent(Encoding.ASCII.GetBytes(content));
			bytes.Headers.ContentType = new("application/pdf");
			form.Add(bytes, "file", "answer.pdf"); form.Add(new StringContent(key.ToString()), "clientUploadId");
			return await client.PostAsync(path + "/submission/attachments" + query, form);
		}
		using var uploaded = await Upload("%PDF-1.4\nanswer");
		uploaded.StatusCode.ShouldBe(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
		var attachment = (await uploaded.Content.ReadFromJsonAsync<Result<MobileAttachmentDto>>(JsonOptions))!.Data!;
		using var uploadedAgain = await Upload("%PDF-1.4\nanswer");
		(await uploadedAgain.Content.ReadFromJsonAsync<Result<MobileAttachmentDto>>(JsonOptions))!.Data!.Id.ShouldBe(attachment.Id);
		using var changedUpload = await Upload("%PDF-1.4\nchanged");
		changedUpload.StatusCode.ShouldBe(HttpStatusCode.Conflict);

		var input = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id,
			TextAnswerHtml = "<p><strong>Solution</strong></p>", AttachmentIds = [attachment.Id], ExpectedRevision = "0", ClientRequestId = Guid.NewGuid() };
		using var draftResponse = await client.PutAsJsonAsync(path + "/submission/draft", input, JsonOptions);
		draftResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await draftResponse.Content.ReadAsStringAsync());
		var draft = (await draftResponse.Content.ReadFromJsonAsync<Result<MobileHomeworkDto>>(JsonOptions))!.Data!;
		using var draftRetry = await client.PutAsJsonAsync(path + "/submission/draft", input, JsonOptions);
		(await draftRetry.Content.ReadFromJsonAsync<Result<MobileHomeworkDto>>(JsonOptions))!.Data!.Revision.ShouldBe(draft.Revision);
		input.ExpectedRevision = draft.Revision; input.ClientRequestId = Guid.NewGuid();
		factory.Barrier.Arm();
		var finals = await Task.WhenAll(client.PostAsJsonAsync(path + "/submission", input, JsonOptions),
			client.PostAsJsonAsync(path + "/submission", input, JsonOptions)).WaitAsync(TimeSpan.FromSeconds(60));
		MobileHomeworkDto submitted;
		try
		{
			factory.Barrier.Connections.Count.ShouldBe(2);
			foreach (var response in finals) response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			submitted = (await finals[0].Content.ReadFromJsonAsync<Result<MobileHomeworkDto>>(JsonOptions))!.Data!;
			(await finals[1].Content.ReadFromJsonAsync<Result<MobileHomeworkDto>>(JsonOptions))!.Data!.Revision.ShouldBe(submitted.Revision);
		}
		finally { foreach (var response in finals) response.Dispose(); }
		using var rejectedDraft = await client.PutAsJsonAsync(path + "/submission/draft", new MobileSubmissionInput {
			StudentId = f.Student.Id, AcademicYearId = f.Year.Id, TextAnswer = "Cannot revert submission" }, JsonOptions);
		rejectedDraft.IsSuccessStatusCode.ShouldBeFalse();

		var edit1 = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id, TextAnswerHtml = "<p>First edit</p>",
			ExpectedRevision = submitted.Revision, ClientRequestId = Guid.NewGuid() };
		var edit2 = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id, TextAnswerHtml = "<p>Second edit</p>",
			ExpectedRevision = submitted.Revision, ClientRequestId = Guid.NewGuid() };
		factory.Barrier.Arm();
		var edits = await Task.WhenAll(client.PutAsJsonAsync(path + "/submission", edit1, JsonOptions),
			client.PutAsJsonAsync(path + "/submission", edit2, JsonOptions)).WaitAsync(TimeSpan.FromSeconds(60));
		MobileHomeworkDto latest;
		try
		{
			edits.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1, string.Join("\n", await Task.WhenAll(edits.Select(r => r.Content.ReadAsStringAsync()))));
			edits.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
			latest = (await edits.Single(r => r.IsSuccessStatusCode).Content.ReadFromJsonAsync<Result<MobileHomeworkDto>>(JsonOptions))!.Data!;
			latest.SubmittedDate.ShouldBe(submitted.SubmittedDate);
		}
		finally { foreach (var response in edits) response.Dispose(); }
		using var protectedFile = await client.DeleteAsync(path + $"/submission/attachments/{attachment.Id}" + query);
		protectedFile.IsSuccessStatusCode.ShouldBeFalse();

		// Pause a real response UPDATE after reading its revision, commit teacher review on another connection,
		// then release the stale update. The EF concurrency predicate must reject it.
		const string candidate = "<p>Racing with teacher review</p>";
		factory.Barrier.ArmReview(candidate);
		var pending = client.PutAsJsonAsync(path + "/submission", new MobileSubmissionInput { StudentId = f.Student.Id,
			AcademicYearId = f.Year.Id, TextAnswerHtml = candidate, ExpectedRevision = latest.Revision, ClientRequestId = Guid.NewGuid() }, JsonOptions);
		await factory.Barrier.ReviewReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
		try
		{
			using var scope = factory.Services.CreateScope();
			using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>();
			var row = await repository.GetAsync(x => x.StudentId == f.Student.Id && x.AssignmentId == f.Assignment.Id);
			row.UpdateFeedback(18, 20, "Reviewed during edit", null, SubmissionStatus.Reviewed, f.User.Id);
			await repository.UpdateAsync(row, true);
			await uow.CompleteAsync();
		}
		finally { factory.Barrier.ReviewCommitted.TrySetResult(); }
		using var stale = await pending;
		stale.StatusCode.ShouldBe(HttpStatusCode.Conflict, await stale.Content.ReadAsStringAsync());
		using var after = await client.GetAsync(path + query);
		var reviewed = (await after.Content.ReadFromJsonAsync<Result<MobileHomeworkDto>>(JsonOptions))!.Data!;
		reviewed.Status.ShouldBe("checked"); reviewed.CanEdit.ShouldBeFalse(); reviewed.TextAnswerHtml.ShouldBe(latest.TextAnswerHtml);
	}
}
