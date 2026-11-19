using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Examination;

[Collection(ApiTestCollection.Name)]
public sealed class ResultNotificationPrivacy_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Published = AcademicNotificationEventKeys.ResultPublished;
	private const string Route = "/api/academics/results/publish";
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid Publisher = Guid.Parse(TestAuthHandler.TestUserId);

	[Fact]
	public async Task Real_result_publication_targets_the_eligible_roster_with_generic_content()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var resultId = Guid.NewGuid();
		var learner = Guid.NewGuid();
		const string confidentialGrade = "PRIVATE-A";
		await InSchoolAsync(async services =>
		{
			var year = await InsertAsync(services, await services.GetRequiredService<AcademicYearManager>().CreateAsync(
				$"Result {Guid.NewGuid():N}"[..30], DateTime.UtcNow.Date.AddMonths(-2), DateTime.UtcNow.Date.AddMonths(4)));
			var board = await InsertAsync(services, await services.GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE));
			var medium = await InsertAsync(services, await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "Result medium"));
			var stage = await InsertAsync(services, await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Result stage"));
			var standard = await InsertAsync(services, await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Result class", 12, null));
			var divisions = services.GetRequiredService<DivisionManager>();
			var group = await InsertAsync(services, await divisions.CreateAsync(standard.Id, "A", 25));
			var other = await InsertAsync(services, await divisions.CreateAsync(standard.Id, "B", 25));
			var type = await InsertAsync(services, new ExamType(Guid.NewGuid(), TenantId, "Result exam type", null, medium.Id));
			var exam = new Exam(Guid.NewGuid(), type.Id, "Synthetic exam", standard.Id, TenantId);
			exam.AddDivision(Guid.NewGuid(), group.Id);
			exam.ChangeStatus(ExamStatus.Completed);
			await InsertAsync(services, exam);
			await InsertAsync(services, new ExamResultHeader(resultId, exam.Id, group.Id, Publisher, TenantId));
			foreach (var (student, division, status) in new[]
			{
				(learner, group.Id, ExamStudentResultStatus.Pass),
				(Guid.NewGuid(), group.Id, ExamStudentResultStatus.Withheld),
				(Guid.NewGuid(), other.Id, ExamStudentResultStatus.Pass)
			})
			{
				await InsertAsync(services, new Student(student, TenantId, "Synthetic", null, "Learner", year.Id, board.Id,
					medium.Id, stage.Id, standard.Id, division, status: StudentLifeCycleStatusEnum.Enrolled));
				await InsertAsync(services, StudentEnrollment.Create(Guid.NewGuid(), TenantId, student, year.Id, standard.Id, division));
				await InsertAsync(services, new ExamResultDetail(Guid.NewGuid(), resultId, exam.Id, student, group.Id,
					100, 78, 78, confidentialGrade, 2, 2, status, TenantId));
			}

			var templateId = Guid.NewGuid();
			await InsertAsync(services, new CommunicationTemplate(templateId, TenantId) { TemplateName = "Result ready" });
			var template = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, templateId, 1, Published, "Guardian",
				CommunicationChannelType.InApp, "en", "Result available", "A result is ready in ShikshaMitra.", 1);
			template.Publish(Publisher, DateTimeOffset.UtcNow);
			await InsertAsync(services, template);
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, Published,
				NotificationRuleScope.Create(year.Id, board.Id, medium.Id, standard.Id, group.Id));
			var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
				[CommunicationChannelType.InApp], [templateId], NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>()
				.Serialize(configuration, services.GetRequiredService<AcademicNotificationCatalogue>().Get(Published)), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);
			(await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x => x.SourceId == resultId)).ShouldBeEmpty();
		});

		using var publish = await Client.PostAsJsonAsync(Route, new { resultHeaderId = resultId }, JsonOptions);
		publish.IsSuccessStatusCode.ShouldBeTrue(await publish.Content.ReadAsStringAsync());
		using var duplicate = await Client.PostAsJsonAsync(Route, new { resultHeaderId = resultId }, JsonOptions);
		duplicate.IsSuccessStatusCode.ShouldBeFalse();
		Guid? operationId = null;
		await InSchoolAsync(async services =>
		{
			var rows = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
				x.TenantId == TenantId && x.SourceId == resultId && x.EventKey == Published);
			var intent = rows.ShouldHaveSingleItem();
			operationId = intent.CampaignId;
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			var source = await reader.LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
			source.CurrentRecipients.ShouldHaveSingleItem().ShouldBe(new("Student", StudentId: learner));
			source.Snapshot!.Variables.Values.ShouldNotContain(x => x.Value == confidentialGrade || x.Value == "78");
			source.Snapshot.Variables.Keys.ShouldNotContain(key =>
				key.Contains("rank", StringComparison.Ordinal) || key.Contains("marks", StringComparison.Ordinal) ||
				key.Contains("grade", StringComparison.Ordinal));
			var enrollments = services.GetRequiredService<IRepository<StudentEnrollment, Guid>>();
			var enrollment = (await enrollments.GetListAsync(x => x.TenantId == TenantId && x.StudentId == learner)).Single();
			enrollment.Deactivate();
			await enrollments.UpdateAsync(enrollment, autoSave: true);
			(await reader.LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
		});
		using var viewer = Factory.CreateClient();
		viewer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.Academics.Notification.View);
		using var denied = await viewer.GetAsync($"/api/academic/notifications/operations/{operationId}");
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	private static Task<TEntity> InsertAsync<TEntity>(IServiceProvider services, TEntity entity)
		where TEntity : class, IEntity<Guid> =>
		services.GetRequiredService<IRepository<TEntity, Guid>>().InsertAsync(entity, autoSave: true);

	private async Task InSchoolAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var school = services.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(services);
		await unit.CompleteAsync();
	}
}
