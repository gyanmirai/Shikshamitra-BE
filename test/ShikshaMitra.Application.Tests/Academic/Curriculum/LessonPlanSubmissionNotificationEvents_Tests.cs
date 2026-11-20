using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public sealed class LessonPlanSubmissionNotificationEvents_Tests : SubjectTeacherReassignmentTestBase
{
	[Theory]
	[InlineData("submit")]
	[InlineData("create")]
	[InlineData("bulk")]
	public async Task Submission_uses_the_configured_scoped_reviewer(string action)
	{
		using var school = Tenant.Change(TenantId, "Synthetic school");
		const string submitted = AcademicNotificationEventKeys.LessonPlanSubmitted;
		var settings = GetRequiredService<IConfiguration>();
		var key = $"Notifications:Academic:Tenants:{TenantId:N}:LessonPlanReviewerUserId";
		var before = settings[key];
		settings[key] = ActorId.ToString();
		try
		{
			var planId = Guid.NewGuid();
			var secondPlanId = Guid.NewGuid();
			Guid teacherUser = default, year = default;
			await WithUnitOfWorkAsync(async () =>
			{
				await SeedAllocationAsync();
				var reviewer = await GetRequiredService<IIdentityUserRepository>().GetAsync(ActorId);
				var roles = GetRequiredService<IIdentityRoleRepository>();
				var role = await roles.FindByNormalizedNameAsync("ADMIN") ??
					await roles.InsertAsync(new IdentityRole(Guid.NewGuid(), "admin", TenantId), autoSave: true);
				reviewer.AddRole(role.Id);
				await GetRequiredService<IIdentityUserRepository>().UpdateAsync(reviewer, autoSave: true);
				var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
				var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
				year = board.AcademicYearId;
				teacherUser = (await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(OldTeacher)).UserId!.Value;
				if (action != "create")
					await Plans.InsertAsync(new LessonPlan(planId, TenantId, year, OldTeacher, StandardId, DivisionId,
						SubjectId, ChapterId, DateTime.UtcNow.Date.AddDays(1), "Lesson plan to submit", 30), autoSave: true);
				if (action == "bulk")
					await Plans.InsertAsync(new LessonPlan(secondPlanId, TenantId, year, OldTeacher, StandardId, DivisionId,
						SubjectId, ChapterId, DateTime.UtcNow.Date.AddDays(1), "Second lesson plan to submit", 30), autoSave: true);

				var template = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = submitted };
				await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(template, autoSave: true);
				var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, submitted, "ConfiguredReviewer",
					CommunicationChannelType.InApp, "en", "Lesson plan waiting for review", "Open your school inbox.", 1);
				revision.Publish(ActorId, DateTimeOffset.UtcNow);
				await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
				var rule = new NotificationRule(Guid.NewGuid(), TenantId, submitted, NotificationRuleScope.Create());
				var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, ["ConfiguredReviewer"],
					[CommunicationChannelType.InApp], [template.Id], NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
				rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
					config, GetRequiredService<AcademicNotificationCatalogue>().Get(submitted)), rule.ConcurrencyStamp);
				await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
			});

			using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
				[new Claim(AbpClaimTypes.UserId, teacherUser.ToString()), new Claim(AbpClaimTypes.Role, "admin")], "Test")));
			await WithTransactionalUnitOfWorkAsync(async () =>
			{
				if (action == "bulk")
				{
					await Service.BulkSubmitAsync(new BulkSubmitLessonPlansDto { LessonPlanIds = [planId, secondPlanId] });
					(await Plans.GetAsync(planId)).Status.ShouldBe(LessonPlanStatus.Submitted);
					(await Plans.GetAsync(secondPlanId)).Status.ShouldBe(LessonPlanStatus.Submitted);
					return;
				}

				var plan = action == "create"
					? await Service.CreateAsync(new CreateLessonPlanDto
					{
						AcademicYearId = year, TeacherId = OldTeacher, StandardId = StandardId, DivisionId = DivisionId,
						SubjectId = SubjectId, ChapterId = ChapterId, PlannedDate = DateTimeOffset.UtcNow.AddDays(1),
						Topic = "Create and submit lesson plan", PlannedCompletionPercentage = 30, Status = LessonPlanStatus.Submitted
					})
					: await Service.SubmitAsync(planId);
				plan.Status.ShouldBe(LessonPlanStatus.Submitted);
				planId = plan.Id;
			});

			await WithUnitOfWorkAsync(async () =>
			{
				var intent = (await GetRequiredService<IRepository<NotificationIntent, Guid>>()
					.GetListAsync(x => x.TenantId == TenantId && x.EventKey == submitted && (action == "bulk" || x.SourceId == planId))).ShouldHaveSingleItem();
				var snapshot = await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
				snapshot.Runtime.SourceScope.DivisionId.ShouldBe(DivisionId);
				snapshot.CurrentRecipients.ShouldBe([new("ConfiguredReviewer", UserId: ActorId)]);
			});
		}
		finally { settings[key] = before; }
	}
}
