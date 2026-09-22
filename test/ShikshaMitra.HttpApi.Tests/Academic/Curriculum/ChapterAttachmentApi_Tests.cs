using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Academic)]
public class ChapterAttachmentApi_Tests : ShikshaMitraApiTestBase
{
	public ChapterAttachmentApi_Tests(ShikshaMitraApiTestFactory factory)
		: base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_And_Update_Chapter_With_Multipart_Attachments()
	{
		var (subjectId, assignedSubjectId) = await SeedSubjectAssignmentAsync();
		var startDate = DateTimeOffset.UtcNow.Date.AddDays(7);
		var createInput = new CreateChapterDto
		{
			AssignedSubjectId = assignedSubjectId,
			ChapterNo = 1,
			ChapterName = "Multipart chapter",
			StartDate = startDate,
			EndDate = startDate.AddDays(2),
			Documents = []
		};

		using (var createContent = BuildMultipartContent(createInput, "chapter-notes.pdf", "first attachment"))
		{
			var createResponse = await Client.PostAsync(
				$"/api/academic/curriculum/chapters/with-attachments?standardId={Factory.SeededStandardId}&subjectId={subjectId}",
				createContent);
			createResponse.EnsureSuccessStatusCode();
		}

		var chapters = await Client.GetFromJsonAsync<List<ChapterDto>>(
			$"/api/academic/curriculum/chapters?standardId={Factory.SeededStandardId}&subjectId={subjectId}",
			JsonOptions);
		var created = chapters.ShouldNotBeNull().Single();
		created.Documents.Count.ShouldBe(1);
		created.Documents[0].ShouldEndWith(".pdf");

		var updateInput = new CreateUpdateChapterDto
		{
			AssignedSubjectId = assignedSubjectId,
			ChapterNo = created.ChapterNumber,
			ChapterName = "Multipart chapter updated",
			StartDate = startDate,
			EndDate = startDate.AddDays(3),
			Documents = [created.Documents[0]]
		};

		using var updateContent = BuildMultipartContent(updateInput, "chapter-diagram.png", "second attachment");
		var updateResponse = await Client.PutAsync(
			$"/api/academic/curriculum/chapters/{created.Id}/with-attachments",
			updateContent);
		updateResponse.EnsureSuccessStatusCode();

		var updated = await updateResponse.Content.ReadFromJsonAsync<ChapterDto>(JsonOptions);
		updated.ShouldNotBeNull();
		updated.ChapterName.ShouldBe("Multipart chapter updated");
		updated.Documents.Count.ShouldBe(2);
		updated.Documents.ShouldContain(created.Documents[0]);
		updated.Documents.ShouldContain(document => document.EndsWith(".png", StringComparison.Ordinal));
	}

	private async Task<(Guid SubjectId, Guid AssignedSubjectId)> SeedSubjectAssignmentAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var dbContext = scope.ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var unitOfWork = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var subject = new Subject(
				Guid.NewGuid(),
				tenantId,
				$"Multipart subject {Guid.NewGuid():N}",
				"#112233",
				Factory.SeededMediumId);
			var assignedSubject = new AssignedSubject(
				Guid.NewGuid(),
				tenantId,
				Factory.SeededStandardId,
				subject.Id,
				"MPART");

			dbContext.Add(subject);
			dbContext.Add(assignedSubject);
			await dbContext.SaveChangesAsync();
			await unitOfWork.CompleteAsync();

			return (subject.Id, assignedSubject.Id);
		}
	}

	private static MultipartFormDataContent BuildMultipartContent<T>(
		T payload,
		string fileName,
		string fileContents)
	{
		var content = new MultipartFormDataContent();
		content.Add(
			new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
			"Payload");

		var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(fileContents));
		fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
		content.Add(fileContent, "Files", fileName);
		return content;
	}
}
