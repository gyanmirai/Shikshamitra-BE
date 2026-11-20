using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Notifications;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Notification_Recorded_Candidate_Manual_Send_Should_Use_The_Recorded_Borrower_Scope_After_Reassignment(bool staff, bool reservation)
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var fixture = await SeedRecordedCandidateAsync(context, staff, reservation);
		await MoveRecordedCandidateAsync(fixture);
		await SetRecordedCandidateScopeAsync(fixture, currentPlacement: false);

		var result = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationAppService>().SendAsync(RecordedCandidateRequest(fixture)));

		result.Queued.ShouldBe(1);
		result.Sent.ShouldBe(0);
		result.Items.Single().RelatedEntityId.ShouldBe(fixture.SourceId);
		result.Items.Single().RecipientMemberId.ShouldBe(fixture.MemberId);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Notification_Recorded_Candidate_Manual_Reminders_Should_Include_Recorded_Loans_And_Requests_After_Reassignment(bool staff, bool reservation)
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var fixture = await SeedRecordedCandidateAsync(context, staff, reservation);
		await MoveRecordedCandidateAsync(fixture);
		await SetRecordedCandidateScopeAsync(fixture, currentPlacement: false);
		var services = Services(new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc));

		var result = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationAppService>().RunRemindersAsync(new() { RunDate = new(2026, 11, 17) }));

		var eventType = reservation ? LibraryNotificationEventTypes.ReservationExpiring : LibraryNotificationEventTypes.DueDateReminder;
		var rows = result.Items.Where(row => row.EventType == eventType && row.RelatedEntityId == fixture.SourceId &&
			row.Channel == LibraryNotificationChannels.Email).ToList();
		rows.Count.ShouldBe(1);
		rows.Single().Status.ShouldBe(LibraryNotificationDeliveryStatuses.Queued);
		rows.Single().RecipientMemberId.ShouldBe(fixture.MemberId);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Notification_Recorded_Candidate_Current_Placement_Should_Not_Grant_Manual_Send_Or_Reminder_Access_To_The_Prior_Record(bool staff, bool reservation)
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var fixture = await SeedRecordedCandidateAsync(context, staff, reservation);
		await MoveRecordedCandidateAsync(fixture);
		await SetRecordedCandidateScopeAsync(fixture, currentPlacement: true);
		var services = Services(new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc));

		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			services.Get<LibraryNotificationAppService>().SendAsync(RecordedCandidateRequest(fixture))));
		var reminders = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationAppService>().RunRemindersAsync(new() { RunDate = new(2026, 11, 17) }));
		reminders.Items.ShouldNotContain(row => row.RelatedEntityId == fixture.SourceId &&
			(row.Status == LibraryNotificationDeliveryStatuses.Queued || row.Status == LibraryNotificationDeliveryStatuses.Sent));
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryNotificationHistory, Guid>>().CountAsync(row =>
			row.RelatedEntityId == fixture.SourceId && (row.Status == LibraryNotificationDeliveryStatuses.Queued || row.Status == LibraryNotificationDeliveryStatuses.Sent))))
			.ShouldBe(0);
	}
}
