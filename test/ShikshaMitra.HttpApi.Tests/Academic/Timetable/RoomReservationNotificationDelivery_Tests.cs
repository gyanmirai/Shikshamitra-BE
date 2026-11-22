using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.Common;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Timetable;

[Collection(ApiTestCollection.Name)]
public sealed class RoomReservationNotificationDelivery_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Route = "/api/academics/timetable/room-reservations";
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid RequesterId = Guid.Parse(TestAuthHandler.TestUserId);

	[Fact]
	public async Task Confirmed_reservation_actions_queue_one_requester_notice_per_real_transition()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var roomId = Guid.Empty;
		var reviewerId = Guid.Empty;
		Employee[] teachers = [];
		await InSchoolAsync(async services =>
		{
			roomId = (await services.GetRequiredService<IRepository<Room, Guid>>().GetListAsync(x => x.TenantId == TenantId))
				.OrderBy(x => x.Id).First().Id;
			var admin = (await services.GetRequiredService<IRepository<IdentityRole, Guid>>()
				.GetListAsync(x => x.TenantId == TenantId && x.NormalizedName == "ADMIN")).Single();
			reviewerId = (await services.GetRequiredService<IRepository<IdentityUser, Guid>>().GetListAsync(x =>
				x.TenantId == TenantId && x.Id != RequesterId && x.IsActive && x.Roles.Any(role => role.RoleId == admin.Id)))
				.First().Id;
			var employees = services.GetRequiredService<IRepository<Employee, Guid>>();
			var requester = (await employees.GetListAsync(x => x.TenantId == TenantId && x.UserId == RequesterId)).Single();
			teachers = (await employees.GetListAsync(x => x.TenantId == TenantId && x.UserId.HasValue &&
				x.UserId != RequesterId && x.RoleId == requester.RoleId && x.EmployeeStatusType == EmployeeStatusType.Active))
				.DistinctBy(x => x.UserId).Take(2).ToArray();
			teachers.Length.ShouldBe(2);
			var templates = services.GetRequiredService<IRepository<CommunicationTemplate, Guid>>();
			var revisions = services.GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>();
			var rules = services.GetRequiredService<IRepository<NotificationRule, Guid>>();
			var catalogue = services.GetRequiredService<AcademicNotificationCatalogue>();
			var codec = services.GetRequiredService<NotificationRuleConfigurationCodec>();
			foreach (var key in new[] { AcademicNotificationEventKeys.RoomApproved, AcademicNotificationEventKeys.RoomChanged,
				AcademicNotificationEventKeys.RoomCancelled, AcademicNotificationEventKeys.RoomApprovalRequested,
				AcademicNotificationEventKeys.RoomRejected })
			{
				var selector = key == AcademicNotificationEventKeys.RoomApprovalRequested ? "ConfiguredReviewer" : "Requester";
				var template = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = key };
				await templates.InsertAsync(template, autoSave: true);
				var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, key, selector,
					CommunicationChannelType.InApp, "en", "Room reservation", "Open the school inbox.", 1);
				revision.Publish(RequesterId, DateTimeOffset.UtcNow);
				await revisions.InsertAsync(revision, autoSave: true);
				var selectors = new[] { selector };
				var templateIds = new[] { template.Id };
				if (key is AcademicNotificationEventKeys.RoomChanged or AcademicNotificationEventKeys.RoomCancelled)
				{
					var staffTemplate = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = key + " for teachers" };
					await templates.InsertAsync(staffTemplate, autoSave: true);
					var staffRevision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, staffTemplate.Id, 1, key, "AssignedTeacher",
						CommunicationChannelType.InApp, "en", "Room reservation", "Open the school inbox.", 1);
					staffRevision.Publish(RequesterId, DateTimeOffset.UtcNow);
					await revisions.InsertAsync(staffRevision, autoSave: true);
					selectors = [selector, "AssignedTeacher"];
					templateIds = [template.Id, staffTemplate.Id];
				}
				var rule = new NotificationRule(Guid.NewGuid(), TenantId, key, NotificationRuleScope.Create());
				var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, selectors,
					[CommunicationChannelType.InApp], templateIds, NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
				rule.Configure(true, codec.Serialize(config, catalogue.Get(key)), rule.ConcurrencyStamp);
				await rules.InsertAsync(rule, autoSave: true);
			}
		});

		var input = new CreateRoomReservationDto
		{
			RoomId = roomId, Date = DateTime.UtcNow.Date.AddDays(45), StartTime = TimeSpan.FromHours(9),
			EndTime = TimeSpan.FromHours(10), Purpose = "Synthetic meeting", ReservationType = RoomReservationType.Meeting
		};
		var reservation = await PostAndReadAsync<RoomReservationDto>(Route, input);
		reservation.Status.ShouldBe(RoomReservationStatus.Approved);
		var approved = await CheckAsync(reservation.Id, AcademicNotificationEventKeys.RoomApproved);
		using (var deniedClient = Factory.CreateClient())
		{
			deniedClient.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
				ShikshaMitraSubmodulePermissions.Academics.Notification.View);
			using var denied = await deniedClient.GetAsync($"/api/academic/notifications/operations/{approved!.CampaignId}");
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		}

		input.Remarks = "Private booking notes";
		await UpdateAsync();
		await CheckAsync(reservation.Id, AcademicNotificationEventKeys.RoomChanged, expected: 0);
		input.StartTime = TimeSpan.FromHours(9.5);
		await UpdateAsync();
		await UpdateAsync();
		var changed = await CheckAsync(reservation.Id, AcademicNotificationEventKeys.RoomChanged);
		await InSchoolAsync(async services =>
		{
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			(await reader.LoadAsync(approved!, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
		});

		using var cancelled = await Client.PostAsJsonAsync($"{Route}/{reservation.Id}/cancel", "Synthetic cancellation", JsonOptions);
		cancelled.EnsureSuccessStatusCode();
		await CheckAsync(reservation.Id, AcademicNotificationEventKeys.RoomCancelled);
		await InSchoolAsync(async services =>
		{
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			(await reader.LoadAsync(changed!, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
		});

		input.Date = input.Date.AddDays(2);
		input.TeacherId = teachers[0].Id;
		input.AcademicMediumId = Factory.SeededMediumId;
		input.StandardId = Factory.SeededStandardId;
		input.DivisionId = Factory.SeededDivisionId;
		var assigned = await PostAndReadAsync<RoomReservationDto>(Route, input);
		input.TeacherId = teachers[1].Id;
		await UpdateAsync(assigned.Id);
		var withTeacherChange = await CheckAsync(assigned.Id, AcademicNotificationEventKeys.RoomChanged,
			targets: [new("Requester", UserId: RequesterId), Teacher(0), Teacher(1)]);
		await InSchoolAsync(async services =>
		{
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			var changedBooking = await reader.LoadAsync(withTeacherChange!, DateTimeOffset.UtcNow, CancellationToken.None);
			changedBooking.Runtime.SourceScope.ShouldBe(new(Factory.SeededAcademicYearId, Factory.SeededBoardId,
				Factory.SeededMediumId, Factory.SeededStandardId, Factory.SeededDivisionId));
			var former = changedBooking.Snapshot!.RecipientContent!.ShouldHaveSingleItem();
			former.Recipient.ShouldBe(Teacher(0));
			former.Variables.Keys.ShouldNotContain("room_name");
			former.Variables.Keys.ShouldNotContain("start_time");
		});
		input.TeacherId = null;
		input.AcademicMediumId = null;
		input.StandardId = null;
		input.DivisionId = null;

		input.Date = input.Date.AddDays(2);
		var affected = await PostAndReadAsync<RoomReservationDto>(Route, input);
		await InSchoolAsync(async services =>
		{
			var calendar = new AcademicCalendar(Guid.NewGuid(), TenantId, Factory.SeededMediumId, Factory.SeededAcademicYearId,
				"Room closure fixture", input.Date, input.Date.AddDays(1));
			calendar.Publish(RequesterId);
			await services.GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(calendar, autoSave: true);
			await services.GetRequiredService<IRepository<AcademicCalendarDay, Guid>>().InsertAsync(
				new(Guid.NewGuid(), TenantId, calendar.Id, input.Date, DayType.EmergencyClosure, "Synthetic closure"), autoSave: true);
		});
		await CheckAsync(affected.Id, AcademicNotificationEventKeys.RoomCancelled);

		var settings = Factory.Services.GetRequiredService<IConfiguration>();
		var reviewerKey = $"Notifications:Academic:Tenants:{TenantId:N}:RoomReviewerUserId";
		var previousReviewer = settings[reviewerKey];
		Client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "teacher");
		try
		{
			input.Date = input.Date.AddDays(3);
			settings[reviewerKey] = RequesterId.ToString();
			var selfReview = await PostAndReadAsync<RoomReservationDto>(Route, input);
			selfReview.Status.ShouldBe(RoomReservationStatus.PendingApproval);
			await CheckAsync(selfReview.Id, AcademicNotificationEventKeys.RoomApprovalRequested, expected: 0);

			input.Date = input.Date.AddDays(2);
			settings[reviewerKey] = reviewerId.ToString();
			var pending = await PostAndReadAsync<RoomReservationDto>(Route, input);
			pending.Status.ShouldBe(RoomReservationStatus.PendingApproval);
			var review = await CheckAsync(pending.Id, AcademicNotificationEventKeys.RoomApprovalRequested,
				targets: [new("ConfiguredReviewer", UserId: reviewerId)]);
			settings[reviewerKey] = RequesterId.ToString();
			await InSchoolAsync(async services =>
			{
				var reader = services.GetRequiredService<NotificationIntentSourceReader>();
				(await reader.LoadAsync(review!, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
			});
			settings[reviewerKey] = reviewerId.ToString();
			using var reviewer = CreateClientAs(reviewerId);
			using var rejected = await reviewer.PostAsJsonAsync($"{Route}/{pending.Id}/reject", new { reason = "Private reason" }, JsonOptions);
			rejected.IsSuccessStatusCode.ShouldBeTrue(await rejected.Content.ReadAsStringAsync());
			await CheckAsync(pending.Id, AcademicNotificationEventKeys.RoomRejected);
			await InSchoolAsync(async services =>
			{
				var reader = services.GetRequiredService<NotificationIntentSourceReader>();
				(await reader.LoadAsync(review!, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
			});
		}
		finally
		{
			settings[reviewerKey] = previousReviewer;
			Client.DefaultRequestHeaders.Remove(TestAuthHandler.TestRolesHeader);
		}

		input.Date = input.Date.AddDays(4);
		input.IsRecurring = true;
		input.RecurringConfig = new() { RepeatType = "daily", UntilDate = input.Date.AddDays(2), ExcludeHolidays = false };
		var series = await PostAndReadAsync<RoomReservationDto>(Route, input);
		await CheckAsync(series.Id, AcademicNotificationEventKeys.RoomApproved);
		using var seriesCancelled = await Client.PostAsJsonAsync($"{Route}/{series.Id}/cancel", "Synthetic series cancellation", JsonOptions);
		seriesCancelled.EnsureSuccessStatusCode();
		await CheckAsync(series.Id, AcademicNotificationEventKeys.RoomCancelled);
		await InSchoolAsync(async services =>
		{
			var repository = services.GetRequiredService<IRepository<RoomReservation, Guid>>();
			var savedSeries = await repository.GetAsync(series.Id);
			var occurrences = await repository.GetListAsync(x => x.RecurringConfigId == savedSeries.RecurringConfigId && x.Id != series.Id);
			occurrences.Count.ShouldBeGreaterThan(0);
			occurrences.ShouldAllBe(x => x.Status == RoomReservationStatus.Cancelled);
			var ids = occurrences.Select(x => x.Id).ToArray();
			(await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
				x.TenantId == TenantId && ids.Contains(x.SourceId))).ShouldBeEmpty();
		});

		NotificationRecipientRefDto Teacher(int index) => new("AssignedTeacher", UserId: teachers[index].UserId, EmployeeId: teachers[index].Id);

		async Task UpdateAsync(Guid? id = null)
		{
			using var updated = await Client.PutAsJsonAsync($"{Route}/{id ?? reservation.Id}", input, JsonOptions);
			updated.IsSuccessStatusCode.ShouldBeTrue(await updated.Content.ReadAsStringAsync());
		}

		async Task<NotificationIntent?> CheckAsync(Guid reservationId, string eventKey, int expected = 1, NotificationRecipientRefDto[]? targets = null)
		{
			NotificationIntent? intent = null;
			await InSchoolAsync(async services =>
			{
				var rows = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
					x.TenantId == TenantId && x.SourceId == reservationId && x.EventKey == eventKey);
				rows.Count.ShouldBe(expected, eventKey);
				if (expected == 0) return;
				intent = rows.Single();
				var source = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
				source.CurrentRecipients.ToHashSet().SetEquals(targets ?? [new("Requester", UserId: RequesterId)]).ShouldBeTrue();
				source.Snapshot!.Variables.Values.ShouldNotContain(x => x.Value == "Private reason" || x.Value == input.Remarks && input.Remarks != null);
			});
			return intent;
		}
	}

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
