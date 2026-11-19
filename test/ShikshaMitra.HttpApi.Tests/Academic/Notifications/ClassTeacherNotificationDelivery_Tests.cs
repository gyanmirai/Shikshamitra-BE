using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Performance;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;
using ClassTeacherRecord = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Notifications;

[Collection(ApiTestCollection.Name)]
public sealed class ClassTeacherNotificationDelivery_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Replaced = AcademicNotificationEventKeys.ClassTeacherReplaced;
	private static readonly Guid School = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid Actor = Guid.Parse(TestAuthHandler.TestUserId);

	[Fact]
	public Task A_saved_draft_preserves_the_previous_teacher_for_a_confirmed_notice() => RunAsync(concurrent: false);

	internal async Task RunAsync(bool concurrent)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var stageId = Guid.Empty;
		var classId = Guid.Empty;
		var divisionId = Guid.Empty;
		var previousTeacherId = Guid.Empty;
		var nextTeacherId = Guid.Empty;
		var nextUserId = Guid.Empty;
		var reviewer = Guid.NewGuid();
		var key = $"Notifications:Academic:Tenants:{School:N}:ClassTeacherReviewerUserId";
		var config = Factory.Services.GetRequiredService<IConfiguration>();
		var previous = config[key];
		config[key] = reviewer.ToString();
		try
		{
			await InSchoolAsync(async services =>
			{
				var stage = await InsertAsync(services, await services.GetRequiredService<EducationMediumStageManager>()
					.CreateAsync(Factory.SeededMediumId, "Class notice stage"));
				stageId = stage.Id;
				var standard = await InsertAsync(services, await services.GetRequiredService<StandardManager>()
					.CreateAsync(stageId, "Class notice grade", 10, null));
				classId = standard.Id;
				var division = await InsertAsync(services, await services.GetRequiredService<DivisionManager>().CreateAsync(classId, "A", 25));
				divisionId = division.Id;
				var teacher = (await services.GetRequiredService<IEmployeeRepository>().GetEmployeeByUserIdAsync(Actor))!;
				previousTeacherId = teacher.Id;
				var next = (await services.GetRequiredService<IRepository<Employee, Guid>>().GetListAsync(x =>
					x.TenantId == School && x.UserId.HasValue && x.UserId != Actor && x.RoleId == teacher.RoleId &&
					x.EmployeeStatusType == EmployeeStatusType.Active && x.EmployeeGroup == EmployeeGroupType.Teaching)).First();
				nextTeacherId = next.Id;
				nextUserId = next.UserId!.Value;
				await InsertAsync(services, new ClassTeacherRecord(Guid.NewGuid(), School, classId, divisionId, teacher.Id,
					Factory.SeededMediumId, educationMediumStageId: stageId));
				await InsertAsync(services, new IdentityUser(reviewer, $"class-review-{reviewer:N}"[..31], "class-review@example.test", School));
				await InsertAsync(services, new UserScopeAssignment(Guid.NewGuid(), School, userId: reviewer));

				var kinds = new[] { "AssignedTeacher", "PreviousTeacher", "ConfiguredReviewer" };
				var templates = new Guid[kinds.Length];
				for (var i = 0; i < kinds.Length; i++)
				{
					var header = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School) { TemplateName = "Class notice " + kinds[i] });
					templates[i] = header.Id;
					var version = new CommunicationTemplateRevision(Guid.NewGuid(), School, header.Id, 1, Replaced, kinds[i],
						CommunicationChannelType.InApp, "en", "Class teacher assignment", "Open your school notifications.", 1);
					version.Publish(Actor, DateTimeOffset.UtcNow);
					await InsertAsync(services, version);
				}
				var rule = new NotificationRule(Guid.NewGuid(), School, Replaced, NotificationRuleScope.Create());
				var settings = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, kinds,
					[CommunicationChannelType.InApp], templates, NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
				rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
					settings, services.GetRequiredService<AcademicNotificationCatalogue>().Get(Replaced)), rule.ConcurrencyStamp);
				await InsertAsync(services, rule);
			});

			async Task SaveAsync(bool draft)
			{
				using var response = await Client.PutAsJsonAsync("/api/class-management/class-teacher-assignments", new
				{
					organizationId = stageId, saveAsDraft = draft,
					assignments = new[] { new { classId, divisionId, teacherId = nextTeacherId } }
				}, JsonOptions);
				response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			}

			await SaveAsync(true);
			await InSchoolAsync(async services =>
				(await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x => x.SourceId == divisionId))
					.ShouldBeEmpty());
			if (concurrent) await Task.WhenAll(SaveAsync(false), SaveAsync(false));
			else
			{
				await SaveAsync(false);
				await SaveAsync(false);
			}

			Guid? operation = null;
			await InSchoolAsync(async services =>
			{
				var intent = (await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
					x.SourceId == divisionId && x.EventKey == Replaced)).ShouldHaveSingleItem();
				operation = intent.CampaignId;
				var source = await services.GetRequiredService<NotificationIntentSourceReader>()
					.LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
				source.Runtime.SourceScope.DivisionId.ShouldBe(divisionId);
				source.CurrentRecipients.ToHashSet().SetEquals(
				[
					new("AssignedTeacher", UserId: nextUserId, EmployeeId: nextTeacherId),
					new("PreviousTeacher", UserId: Actor, EmployeeId: previousTeacherId),
					new("ConfiguredReviewer", UserId: reviewer)
				]).ShouldBeTrue();
				source.Snapshot!.RecipientContent!.ShouldHaveSingleItem().Variables.Keys.ShouldBe(["school_name"]);
			});

			using var viewer = Factory.CreateClient();
			viewer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.Academics.Notification.View);
			using var denied = await viewer.GetAsync($"/api/academic/notifications/operations/{operation}");
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			using var allowed = await Client.GetAsync($"/api/academic/notifications/operations/{operation}");
			allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());
		}
		finally { config[key] = previous; }
	}

	private static Task<TEntity> InsertAsync<TEntity>(IServiceProvider services, TEntity row) where TEntity : class, IEntity<Guid> =>
		services.GetRequiredService<IRepository<TEntity, Guid>>().InsertAsync(row, autoSave: true);

	private async Task InSchoolAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(School, "Synthetic school");
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(services);
		await unit.CompleteAsync();
	}
}

[Collection(ApiTestCollection.Name)]
public sealed class ClassTeacherNotificationPostgres_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Concurrent_confirmations_preserve_the_same_durable_assignment_revision()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		await new ClassTeacherNotificationDelivery_Tests(fixture).RunAsync(concurrent: true);
	}
}
