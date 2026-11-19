using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

[Collection(RubricScoringApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Academic)]
public class HomeworkCreationApi_Tests(RubricScoringApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData(SubmissionType.OnlineText)]
	[InlineData(SubmissionType.FileUpload)]
	[InlineData(SubmissionType.InPersonSubmission)]
	[InlineData(SubmissionType.NoSubmissionRequired)]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Create_Should_Return_A_Draft_That_Can_Be_Read_And_Published(SubmissionType submissionType)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var (typeId, subjectId) = await CreateContextAsync();
		var publishAt = new DateTimeOffset(2026, 11, 4, 11, 10, 0, TimeSpan.FromMinutes(330));
		var dueAt = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.FromMinutes(330));
		using var form = CreateForm(typeId, subjectId, submissionType, publishAt, dueAt);
		using var response = await Client.PostAsync($"/api/academic/homeworks?academicMediumId={Factory.SeededMediumId}", form);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		var created = (await response.Content.ReadFromJsonAsync<HomeworkCreateResponseDto>(JsonOptions))!.Data;
		created.Id.ShouldNotBe(Guid.Empty);
		created.Status.ShouldBe((int) HomeworkStatus.Draft);
		created.SubmissionType.ShouldBe((int) submissionType);
		created.SubmittedCount.ShouldBe(0);
		created.LateCount.ShouldBe(0);
		created.CheckedCount.ShouldBe(0);
		created.EvaluationPendingCount.ShouldBe(0);
		created.DivisionSchedules.ShouldHaveSingleItem();
		created.DivisionSchedules[0].PublishAt.ShouldBe(publishAt);
		created.DivisionSchedules[0].DueAt.ShouldBe(dueAt);

		await SeedSubmissionsAsync(created.Id, dueAt.UtcDateTime);
		var detail = (await GetAndReadAsync<HomeworkResponseDto>($"/api/academic/homeworks/{created.Id}")).Data;
		detail.SubmittedCount.ShouldBe(3);
		detail.LateCount.ShouldBe(1);
		detail.CheckedCount.ShouldBe(1);
		detail.EvaluationPendingCount.ShouldBe(3);
		var list = await GetAndReadAsync<HomeworkListResponseDto>(
			$"/api/academic/homeworks?academicMediumId={Factory.SeededMediumId}&standardId={Factory.SeededStandardId}&divisionId={Factory.SeededDivisionId}&subjectId={subjectId}");
		list.Items.Single(row => row.Id == created.Id).SubmittedCount.ShouldBe(3);

		using var published = await Client.PatchAsJsonAsync($"/api/academic/homeworks/{created.Id}/status",
			new HomeworkStatusUpdateDto { Status = (int) HomeworkStatus.Published }, JsonOptions);
		published.StatusCode.ShouldBe(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
		var reloaded = (await GetAndReadAsync<HomeworkResponseDto>($"/api/academic/homeworks/{created.Id}")).Data;
		reloaded.Status.ShouldBe((int) HomeworkStatus.Published);
		reloaded.DivisionSchedules[0].IsPublished.ShouldBeTrue();
		reloaded.DivisionSchedules[0].DueAt.ShouldBe(dueAt);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Create_Should_Reject_A_Due_Date_Before_Publish_And_Anonymous_Requests()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var (typeId, subjectId) = await CreateContextAsync();
		var publishAt = new DateTimeOffset(2026, 11, 4, 11, 10, 0, TimeSpan.FromMinutes(330));
		using var invalidForm = CreateForm(typeId, subjectId, SubmissionType.InPersonSubmission, publishAt, publishAt.AddMinutes(-1));
		using var invalid = await Client.PostAsync($"/api/academic/homeworks?academicMediumId={Factory.SeededMediumId}", invalidForm);
		invalid.IsSuccessStatusCode.ShouldBeFalse();
		((int) invalid.StatusCode).ShouldBeLessThan(500);
		(await invalid.Content.ReadAsStringAsync()).ShouldContain("Due Date must be after Publish Date");
		using var anonymous = CreateAnonymousClient();
		using var validForm = CreateForm(typeId, subjectId, SubmissionType.InPersonSubmission, publishAt, publishAt.AddDays(1));
		using var denied = await anonymous.PostAsync($"/api/academic/homeworks?academicMediumId={Factory.SeededMediumId}", validForm);
		denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	private async Task<(Guid TypeId, Guid SubjectId)> CreateContextAsync()
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var type = await scope.ServiceProvider.GetRequiredService<HomeworkTypeManager>()
			.CreateAsync(Factory.SeededMediumId, $"Creation {Guid.NewGuid():N}"[..30]);
		var subject = new Subject(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), "Creation regression subject", "#24597a", Factory.SeededMediumId);
		await scope.ServiceProvider.GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);
		await uow.CompleteAsync();
		return (type.Id, subject.Id);
	}

	private MultipartFormDataContent CreateForm(Guid typeId, Guid subjectId, SubmissionType submissionType,
		DateTimeOffset publishAt, DateTimeOffset dueAt)
	{
		return new MultipartFormDataContent
		{
			{ new StringContent(Factory.SeededStandardId.ToString()), "StandardId" },
			{ new StringContent(subjectId.ToString()), "SubjectId" },
			{ new StringContent(typeId.ToString()), "HomeworkTypeId" },
			{ new StringContent("Homework creation regression"), "Title" },
			{ new StringContent("Complete the exercise in class."), "Description" },
			{ new StringContent(((int) submissionType).ToString(CultureInfo.InvariantCulture)), "SubmissionType" },
			{ new StringContent(Factory.SeededDivisionId.ToString()), "DivisionSchedules[0].DivisionId" },
			{ new StringContent(publishAt.ToString("O", CultureInfo.InvariantCulture)), "DivisionSchedules[0].PublishAt" },
			{ new StringContent(dueAt.ToString("O", CultureInfo.InvariantCulture)), "DivisionSchedules[0].DueAt" }
		};
	}

	private async Task SeedSubmissionsAsync(Guid homeworkId, DateTime dueAt)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = currentTenant.Change(tenantId);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>();
		foreach (var status in new[] { SubmissionStatus.Submitted, SubmissionStatus.Late, SubmissionStatus.Reviewed,
			SubmissionStatus.NeedsReview, SubmissionStatus.Draft, SubmissionStatus.NotSubmitted })
		{
			await repository.InsertAsync(new StudentSubmissionEntity(Guid.NewGuid(), homeworkId, Guid.NewGuid(),
				Factory.SeededDivisionId, dueAt, tenantId) { Status = status }, autoSave: true);
		}
		await repository.InsertAsync(new StudentSubmissionEntity(Guid.NewGuid(), homeworkId, Guid.NewGuid(),
			Factory.SeededDivisionId, dueAt, tenantId) { HomeworkAssignmentId = null, Status = SubmissionStatus.Submitted }, autoSave: true);
		using (currentTenant.Change(Guid.NewGuid()))
		{
			await repository.InsertAsync(new StudentSubmissionEntity(Guid.NewGuid(), homeworkId, Guid.NewGuid(),
				Factory.SeededDivisionId, dueAt, currentTenant.Id) { Status = SubmissionStatus.Submitted }, autoSave: true);
		}
		await uow.CompleteAsync();
	}
}
