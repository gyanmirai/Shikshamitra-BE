using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Academic.Notifications;

public sealed partial class AcademicNotificationEndToEndApi_Tests
{
	private static async Task VerifyEmergencyClosureDeliveryAsync(DeliveryApiFactory fixture, HttpClient staff,
		SchoolSample sample, AcademicNotificationScopeDto scope, HttpClient primary, HttpClient student)
	{
		const string route = "/api/academic/calendar-days";
		const string declared = AcademicNotificationEventKeys.CalendarEmergencyClosureDeclared;
		const string changed = AcademicNotificationEventKeys.CalendarEmergencyClosureChanged;
		const string closureTitle = "Hosted weather closure";
		const string reopenedTitle = "Hosted closure lifted before dispatch";
		const string internalNote = "staff-only emergency plan";
		var date = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromMinutes(330)).AddDays(2);
		var outsideGuardianId = Guid.NewGuid();
		await InSchoolAsync(fixture, async services =>
		{
			var calendar = new AcademicCalendar(Guid.NewGuid(), School, fixture.SeededMediumId, fixture.SeededAcademicYearId,
				"Hosted closure calendar", date.Date.AddDays(-7), date.Date.AddDays(7));
			calendar.Publish(Actor);
			await InsertAsync(services, calendar);
			await SeedGuardianInAnotherClassAsync(fixture, services, outsideGuardianId);
		});
		using var outsideGuardian = PersonalClient(fixture, outsideGuardianId, "Guardian");
		var mediumScope = scope with { ClassId = null, DivisionId = null };
		await PublishGuardianInboxRuleAsync(staff, declared, mediumScope);
		await PublishGuardianInboxRuleAsync(staff, changed, mediumScope);

		var closure = await DeclareAsync(date, closureTitle);
		var entries = await staff.GetFromJsonAsync<PagedResultDto<EmergencyClosureListItemDto>>(
			$"{route}/emergency-closures?academicMediumId={fixture.SeededMediumId}&maxResultCount=100");
		var saved = entries!.Items.Single(x => x.Id == closure.Id);
		saved.NotifyParents.ShouldBeTrue();
		saved.NotifyStudents.ShouldBeFalse();
		saved.NotifyTeachers.ShouldBeFalse();

		await RunQueueAsync(fixture);
		var notice = (await InboxAsync(primary)).Items.Where(x => x.Body.Contains(closureTitle, StringComparison.Ordinal)).ShouldHaveSingleItem();
		notice.Body.ShouldContain("closure is in effect");
		notice.Body.ShouldNotContain(internalNote);
		(await InboxAsync(student)).Items.ShouldNotContain(x => x.Body.Contains(closureTitle, StringComparison.Ordinal));
		(await InboxAsync(outsideGuardian)).Items.ShouldBeEmpty();
		var declaredHistory = await ReadHistoryAsync(declared, closure.Id);
		declaredHistory.Items.Where(x => x.Status == NotificationDispatchState.Delivered)
			.Select(x => x.RecipientUserId).ShouldBe([(Guid?)sample.Primary, sample.UnrelatedGuardian], ignoreOrder: true);

		var reopened = await DeclareAsync(date.AddDays(1), reopenedTitle);
		using var reopenResponse = await staff.PostAsync($"{route}/{reopened.Id}/cancel-emergency-closure", null);
		reopenResponse.IsSuccessStatusCode.ShouldBeTrue(await reopenResponse.Content.ReadAsStringAsync());

		await RunQueueAsync(fixture);
		await RunQueueAsync(fixture);
		var reopening = (await InboxAsync(primary)).Items.Where(x => x.Body.Contains(reopenedTitle, StringComparison.Ordinal)).ShouldHaveSingleItem();
		reopening.Body.ShouldContain("The emergency closure has been lifted.");
		reopening.Body.ShouldNotContain(internalNote);
		(await InboxAsync(outsideGuardian)).Items.ShouldBeEmpty();
		(await ReadHistoryAsync(declared, reopened.Id)).Items.ShouldNotContain(x => x.Status == NotificationDispatchState.Delivered);
		(await ReadHistoryAsync(changed, reopened.Id)).Items.Where(x => x.Status == NotificationDispatchState.Delivered)
			.Select(x => x.RecipientUserId).ShouldBe([(Guid?)sample.Primary, sample.UnrelatedGuardian], ignoreOrder: true);
		await InSchoolAsync(fixture, async services =>
		{
			var intents = services.GetRequiredService<IRepository<NotificationIntent, Guid>>();
			var original = (await intents.GetListAsync(x => x.SourceId == reopened.Id && x.EventKey == declared)).ShouldHaveSingleItem();
			original.Status.ShouldBe(NotificationIntentState.Cancelled);
		});

		async Task<AcademicCalendarDayDto> DeclareAsync(DateTimeOffset day, string title) =>
			(await PostAsync<AcademicCalendarDayDto[]>(staff, $"{route}/emergency-closure", new CreateEmergencyClosureDto
			{
				AcademicMediumId = fixture.SeededMediumId, Date = day, Title = title, Description = internalNote,
				StandardIds = [fixture.SeededStandardId], NotifyParents = true, AutoScanImpacts = false
			})).ShouldHaveSingleItem();

		async Task<PagedResultDto<AcademicNotificationHistoryItemDto>> ReadHistoryAsync(string eventKey, Guid sourceId) =>
			(await staff.GetFromJsonAsync<PagedResultDto<AcademicNotificationHistoryItemDto>>(
				$"{Root}/history?eventKey={eventKey}&sourceType=Academic.Calendar&sourceId={sourceId}&maxResultCount=100"))!;
	}

	private static async Task SeedGuardianInAnotherClassAsync(DeliveryApiFactory fixture, IServiceProvider services, Guid guardianId)
	{
		const string email = "outside-closure-class@example.invalid";
		var standard = await InsertAsync(services, await services.GetRequiredService<StandardManager>()
			.CreateAsync(fixture.SeededMediumStageId, "Outside closure class", 12, null));
		var division = await InsertAsync(services, await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 25));
		var learner = new Student(Guid.NewGuid(), School, "Closure", null, "Comparison", fixture.SeededAcademicYearId,
			fixture.SeededBoardId, fixture.SeededMediumId, fixture.SeededMediumStageId, standard.Id, division.Id,
			status: StudentLifeCycleStatusEnum.Enrolled);
		learner.UpdateFamilyInfo(null, new("Comparison guardian", null, null, email, null, null, null, null, null,
			null, null, null, null), null, []);
		StudentFamilyContactSelection.Select(learner, "mother");
		await InsertAsync(services, learner);
		await InsertAsync(services, StudentEnrollment.Create(Guid.NewGuid(), School, learner.Id, fixture.SeededAcademicYearId, standard.Id, division.Id));
		var guardian = new IdentityUser(guardianId, $"closure-{guardianId:N}", email, School);
		guardian.SetEmailConfirmed(true);
		await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(guardian, autoSave: true);
		await InsertAsync(services, new StudentPortalAccountLink(Guid.NewGuid(), School, learner.Id, guardian.Id, "guardian", "mother", Actor));
	}
}
