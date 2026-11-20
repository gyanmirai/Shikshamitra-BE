using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Calendar;

public sealed class AcademicEmergencyClosureNotificationEvents_Tests : SubjectTeacherReassignmentTestBase
{
	private const string Declared = AcademicNotificationEventKeys.CalendarEmergencyClosureDeclared;
	private const string Changed = AcademicNotificationEventKeys.CalendarEmergencyClosureChanged;

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Changing_a_class_closure_suppresses_the_prior_notice_and_notifies_its_scope(bool reschedule)
	{
		using var school = Tenant.Change(TenantId, "Synthetic school");
		using var actor = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
			[new Claim(AbpClaimTypes.UserId, ActorId.ToString()), new Claim(AbpClaimTypes.Role, "admin"),
				new Claim(AbpClaimTypes.TenantId, TenantId.ToString())], "Test")));
		var date = Today.AddDays(2);
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var assignments = GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var assignment = await assignments.GetAsync(AssignmentId);
			assignment.ChangeStatus(SubjectStatus.Published);
			await assignments.UpdateAsync(assignment, autoSave: true);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			var calendar = new AcademicCalendar(Guid.NewGuid(), TenantId, AcademicMediumId, board.AcademicYearId, "Term calendar",
				date.Date.AddDays(-20), date.Date.AddDays(30), isDefault: true);
			calendar.Publish(ActorId);
			await GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(calendar, autoSave: true);
			foreach (var key in new[] { Declared, Changed })
			{
				var template = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = key };
				await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(template, autoSave: true);
				var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, key, "AssignedTeacher",
					CommunicationChannelType.InApp, "en", "School closure", "Open the school inbox.", 1);
				revision.Publish(ActorId, DateTimeOffset.UtcNow);
				await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
				var rule = new NotificationRule(Guid.NewGuid(), TenantId, key,
					NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, StandardId, DivisionId));
				var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
					["AssignedTeacher"], [CommunicationChannelType.InApp], [template.Id],
					NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
				rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
					config, GetRequiredService<AcademicNotificationCatalogue>().Get(key)), rule.ConcurrencyStamp);
				await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
			}
		});

		var service = GetRequiredService<IAcademicCalendarAppService>();
		Guid closure = default;
		await WithTransactionalUnitOfWorkAsync(async () =>
		{
			var created = (await service.CreateEmergencyClosureAsync(new CreateEmergencyClosureDto
			{
				AcademicMediumId = AcademicMediumId, Date = date, Title = "Campus closed",
				StandardIds = [StandardId], NotifyTeachers = true, AutoScanImpacts = false
			})).ShouldHaveSingleItem();
			created.Status.ShouldBe(CalendarDayStatus.Active);
			created.DivisionId.ShouldBeNull();
			created.StandardId.ShouldBe(StandardId);
			closure = created.Id;
		});
		var notices = GetRequiredService<IRepository<NotificationIntent, Guid>>();
		var reader = GetRequiredService<NotificationIntentSourceReader>();
		NotificationIntent original = null!;
		await WithUnitOfWorkAsync(async () =>
		{
			original = (await notices.GetListAsync(x => x.SourceId == closure && x.EventKey == Declared)).ShouldHaveSingleItem();
			var source = await reader.LoadAsync(original, DateTimeOffset.UtcNow, CancellationToken.None);
			source.Runtime.SourceScope.DivisionId.ShouldBe(DivisionId);
			source.CurrentRecipients.Select(x => x.EmployeeId!.Value).ShouldBe([OldTeacher, OtherTeacher], ignoreOrder: true);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var result = await service.GetEmergencyClosuresAsync(AcademicMediumId, CalendarDayStatus.Active);
			var closureRow = result.Items.Single(x => x.Id == closure);
			var payload = JsonSerializer.SerializeToElement(closureRow, JsonSerializerOptions.Web);
			payload.TryGetProperty("notifyTeachers", out var teachers).ShouldBeTrue("The edit form needs the saved audiences.");
			teachers.GetBoolean().ShouldBeTrue();
			payload.GetProperty("notifyParents").GetBoolean().ShouldBeFalse();
			payload.GetProperty("notifyStudents").GetBoolean().ShouldBeFalse();
		});
		if (reschedule)
		{
			await WithTransactionalUnitOfWorkAsync(async () =>
			{
				var updated = await service.UpdateEmergencyClosureAsync(closure, new UpdateEmergencyClosureDto
				{
					StartDate = date.AddDays(1), Title = "Campus closed", StandardIds = [StandardId],
					NotifyTeachers = true, AutoScanImpacts = false, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(12)
				});
				updated.Status.ShouldBe(CalendarDayStatus.Active);
			});
		}
		else
		{
			await WithTransactionalUnitOfWorkAsync(() => service.CancelEmergencyClosureAsync(closure));
		}
		await WithUnitOfWorkAsync(async () =>
		{
			(await reader.LoadAsync(original, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
			var changed = (await notices.GetListAsync(x => x.SourceId == closure && x.EventKey == Changed)).ShouldHaveSingleItem();
			var source = await reader.LoadAsync(changed, DateTimeOffset.UtcNow, CancellationToken.None);
			source.Runtime.SourceScope.DivisionId.ShouldBe(DivisionId);
			source.CurrentRecipients.Select(x => x.EmployeeId!.Value).ShouldBe([OldTeacher, OtherTeacher], ignoreOrder: true);
			source.Snapshot!.Variables["event_date"].Value.ShouldBe((reschedule ? date.AddDays(1) : date).ToString("yyyy-MM-dd"));
			if (reschedule)
				source.Snapshot.Variables["change_summary"].Value.ShouldContain("10:00");
		});
	}
}
