using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Communication;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private const string Send = ShikshaMitraSubmodulePermissions.Library.Notification.Add;

	[Fact]
	public async Task Legacy_Membership_Id_Should_Resolve_To_The_Current_Student_And_Communication_Contact()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var recipient = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationRecipientResolver>().ResolveMemberAsync(context.MemberId));
		recipient.MemberId.ShouldBe(context.StudentId);
		recipient.IsActiveLibraryMember.ShouldBeTrue();
		recipient.Email.ShouldBe("guardian@example.test");
	}

	[Fact]
	public async Task Selected_Guardian_Should_Receive_Email_Instead_Of_The_Childs_Contact_Address()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().SendManualAsync(Request(context.StudentId)));
		rows.Single().RecipientEmail.ShouldBe("guardian@example.test");
		rows.Single().RecipientMemberId.ShouldBe(context.StudentId);
	}

	[Fact]
	public async Task An_Exact_Recipient_Destination_Opt_Out_Should_Block_An_Institutional_Email()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<ConsentRecord, Guid>>().InsertAsync(new ConsentRecord(Guid.NewGuid(), context.TenantId)
		{
			RecipientType = "ParentGuardian", RecipientId = context.StudentId, Channel = CommunicationChannelType.Email,
			Destination = "guardian@example.test", Status = CommunicationConsentStatus.OptedOut, RevokedAt = DateTimeOffset.UtcNow
		}, true));
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().SendManualAsync(Request(context.StudentId)));
		rows.ShouldNotContain(row => row.Status == LibraryNotificationDeliveryStatuses.Queued);
		rows.ShouldContain(row => row.Status == LibraryNotificationDeliveryStatuses.Skipped);
	}

	[Fact]
	public async Task Verified_Guardian_Should_Receive_A_Real_Recipient_Bound_In_App_Item()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var request = Request(context.StudentId);
		request.Channel = LibraryNotificationChannels.InApp;
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().SendManualAsync(request));
		var row = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryNotificationHistory, Guid>>().GetAsync(rows.Single().Id));
		row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Sent);
		row.SentAt.ShouldNotBeNull();
		row.RecipientUserId.ShouldBe(context.GuardianId);
	}

	[Fact]
	public async Task A_Shared_Email_Address_Should_Never_Create_A_Portal_Inbox_Association()
	{
		var context = await SeedAsync(linkGuardian: false);
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var request = Request(context.StudentId);
		request.Channel = LibraryNotificationChannels.InApp;
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().SendManualAsync(request));
		rows.ShouldNotContain(row => row.Status == LibraryNotificationDeliveryStatuses.Sent);
	}

	[Fact]
	public async Task Automatic_Reminder_Window_Should_Use_The_Confirmed_School_Date_At_A_Utc_Midnight_Boundary()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<ISettingManager>().SetForTenantAsync(context.TenantId, LibraryPolicySettingNames.CalendarTimeZone, "Pacific/Honolulu");
			await GetRequiredService<ISettingManager>().SetForTenantAsync(context.TenantId, LibraryNotificationSettingNames.AutomaticReminderTimeLocal, "00:00");
		});
		await SeedIssueAsync(context, context.StudentId, new DateOnly(2026, 11, 17));
		var services = Services(new DateTime(2026, 11, 16, 0, 30, 0, DateTimeKind.Utc));
		var rows = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync());
		rows.ShouldContain(row => row.EventType == LibraryNotificationEventTypes.DueDateReminder &&
			row.RecipientMemberId == context.StudentId && row.ReminderDate == new DateTime(2026, 11, 15));
	}

	[Fact]
	public async Task Due_Reminder_Should_Honor_Each_Channels_Own_Offset()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var services = Services();
		await WithUnitOfWorkAsync(async () =>
		{
			var settings = await services.Get<LibraryNotificationDeliveryService>().EnsureSettingsAsync();
			foreach (var setting in settings.Where(setting => setting.EventType == LibraryNotificationEventTypes.DueDateReminder))
			{
				setting.ReminderOffsetDays = setting.Channel == LibraryNotificationChannels.Email ? 3 : 2;
				await GetRequiredService<IRepository<LibraryNotificationSetting, Guid>>().UpdateAsync(setting, true);
			}
		});
		await SeedIssueAsync(context, context.StudentId, new DateOnly(2026, 11, 18));
		var rows = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 16)));
		rows.ShouldContain(row => row.EventType == LibraryNotificationEventTypes.DueDateReminder && row.Channel == LibraryNotificationChannels.InApp);
		rows.ShouldNotContain(row => row.EventType == LibraryNotificationEventTypes.DueDateReminder && row.Channel == LibraryNotificationChannels.Email);
	}

	[Fact]
	public async Task Guardian_Should_Read_Their_Own_Inbox_Without_A_Staff_Notification_Grant_And_Mark_Read_Idempotently()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var itemId = await CreateInboxItemAsync(context);
		using var guardian = Principal(context.GuardianId);
		(await Services().Get<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Notification.View)).ShouldBeFalse();
		var inbox = GetRequiredService<ILibraryInboxAppService>();
		var before = await inbox.GetAsync(new());
		before.Items.Single().Id.ShouldBe(itemId);
		before.UnreadCount.ShouldBe(1);
		var read = await inbox.MarkReadAsync(itemId);
		read.ReadAt.ShouldNotBeNull();
		(await inbox.MarkReadAsync(itemId)).ReadAt.ShouldBe(read.ReadAt);
		(await inbox.GetAsync(new() { UnreadOnly = true })).TotalCount.ShouldBe(0);
		(await inbox.GetAsync(new())).UnreadCount.ShouldBe(0);
	}

	[Fact]
	public async Task Even_The_Sender_Should_Not_Read_Or_Mark_Another_Users_Private_Inbox_Item()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var itemId = await CreateInboxItemAsync(context);
		using var sender = Principal(context.ActorId);
		var inbox = GetRequiredService<ILibraryInboxAppService>();
		(await inbox.GetAsync(new())).TotalCount.ShouldBe(0);
		await Should.ThrowAsync<AbpAuthorizationException>(() => inbox.MarkReadAsync(itemId));
	}

	[Fact]
	public async Task Removing_A_Verified_Guardian_Link_Should_Hide_Previously_Delivered_Private_Items()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var itemId = await CreateInboxItemAsync(context);
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().DeleteAsync(
			link => link.StudentId == context.StudentId && link.UserId == context.GuardianId, true));
		using var guardian = Principal(context.GuardianId);
		var inbox = GetRequiredService<ILibraryInboxAppService>();
		(await inbox.GetAsync(new())).TotalCount.ShouldBe(0);
		await Should.ThrowAsync<AbpAuthorizationException>(() => inbox.MarkReadAsync(itemId));
	}

	[Fact]
	public async Task An_Inactive_Database_Identity_Should_Not_Read_An_Inbox_Using_An_Old_Session()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var itemId = await CreateInboxItemAsync(context);
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IIdentityUserRepository>();
			var user = await users.GetAsync(context.GuardianId);
			user.SetIsActive(false);
			await users.UpdateAsync(user, true);
		});
		using var guardian = Principal(context.GuardianId);
		var inbox = GetRequiredService<ILibraryInboxAppService>();
		await Should.ThrowAsync<AbpAuthorizationException>(() => inbox.GetAsync(new()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => inbox.MarkReadAsync(itemId));
	}

	[Fact]
	public async Task An_Inbox_Item_Should_Not_Be_Read_From_A_Different_Tenant()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var itemId = await CreateInboxItemAsync(context);
		using var guardian = Principal(context.GuardianId);
		using var otherSchool = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var inbox = GetRequiredService<ILibraryInboxAppService>();
		await Should.ThrowAsync<AbpAuthorizationException>(() => inbox.GetAsync(new()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => inbox.MarkReadAsync(itemId));
	}

	[Fact]
	public async Task A_Legacy_Unbound_In_App_Sent_Row_Should_Remain_Staff_Audit_Only()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationDeliveryService>().EnsureSettingsAsync());
		var row = new LibraryNotificationHistory(Guid.NewGuid(), context.TenantId, LibraryNotificationEventTypes.FineDueReminder,
			LibraryNotificationEventGroups.MemberManagement, "Legacy reminder", LibraryNotificationChannels.InApp,
			LibraryNotificationRecipientRoles.Member, context.StudentId, "Legacy member", null, "Legacy", "Historical audit record",
			LibraryNotificationDeliveryStatuses.Sent, null, null, null, null, null, DateTime.UtcNow);
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryNotificationHistory, Guid>>().InsertAsync(row, true));
		using var guardian = Principal(context.GuardianId);
		var inbox = GetRequiredService<ILibraryInboxAppService>();
		(await inbox.GetAsync(new())).TotalCount.ShouldBe(0);
		await Should.ThrowAsync<AbpAuthorizationException>(() => inbox.MarkReadAsync(row.Id));
	}

	[Fact]
	public async Task Departed_Nonmember_Should_Still_Receive_An_Existing_Loan_Recovery_Email_Without_Reviving_Portal_Links()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var issueId = await SeedIssueAsync(context, context.StudentId, new DateOnly(2026, 11, 15));
		await WithUnitOfWorkAsync(async () =>
		{
			var students = GetRequiredService<IRepository<Student, Guid>>();
			var student = await students.GetAsync(context.StudentId);
			student.ChangeStatus(StudentLifeCycleStatusEnum.Left);
			student.AdditionalDetail!.IsLibraryMember = false;
			await students.UpdateAsync(student, true);
			var members = GetRequiredService<IRepository<LibraryMember, Guid>>();
			var member = await members.GetAsync(context.MemberId);
			member.ChangeMemberStatus(false);
			await members.UpdateAsync(member, true);
			await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().DeleteAsync(link => link.StudentId == context.StudentId, true);
		});
		var services = Services();
		var rows = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 16)));
		var email = rows.Single(row => row.RelatedEntityId == issueId && row.EventType == LibraryNotificationEventTypes.OverdueReminder && row.Channel == LibraryNotificationChannels.Email);
		email.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Queued);
		email.RecipientEmail.ShouldBe("guardian@example.test");
		rows.ShouldNotContain(row => row.RelatedEntityId == issueId && row.Channel == LibraryNotificationChannels.InApp && row.Status == LibraryNotificationDeliveryStatuses.Sent);
		await services.Get<LibraryNotificationDeliveryJob>().ExecuteAsync(new() { TenantId = context.TenantId, HistoryId = email.Id });
		(await StoredNotificationAsync(email.Id)).Status.ShouldBe(LibraryNotificationDeliveryStatuses.Sent);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<Student, Guid>>().GetAsync(context.StudentId)).Status.ShouldBe(StudentLifeCycleStatusEnum.Left);
			(await GetRequiredService<IRepository<LibraryMember, Guid>>().GetAsync(context.MemberId)).IsActive.ShouldBeFalse();
			(await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(issueId)).Status.ShouldBe(BookIssueStatus.Issued);
			(await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().GetListAsync(link => link.StudentId == context.StudentId)).ShouldBeEmpty();
		});
	}

	[Fact]
	public async Task A_Queued_Reminder_For_A_Legacy_Member_Alias_Should_Still_Dispatch_To_The_Same_Current_Person()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var issueId = await SeedIssueAsync(context, context.MemberId, new DateOnly(2026, 11, 18));
		var services = Services();
		var rows = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 16)));
		var email = rows.Single(row => row.RelatedEntityId == issueId && row.EventType == LibraryNotificationEventTypes.DueDateReminder && row.Channel == LibraryNotificationChannels.Email);
		email.RecipientMemberId.ShouldBe(context.StudentId);
		email.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Queued);
		await services.Get<LibraryNotificationDeliveryJob>().ExecuteAsync(new() { TenantId = context.TenantId, HistoryId = email.Id });
		(await StoredNotificationAsync(email.Id)).Status.ShouldBe(LibraryNotificationDeliveryStatuses.Sent);
	}

	[Fact]
	public async Task Renewal_Changing_The_Due_Date_Should_Stop_A_Queued_Reminder_With_The_Old_Date()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var issueId = await SeedIssueAsync(context, context.StudentId, new DateOnly(2026, 11, 18));
		var services = Services();
		var rows = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 16)));
		var email = rows.Single(row => row.RelatedEntityId == issueId && row.EventType == LibraryNotificationEventTypes.DueDateReminder && row.Channel == LibraryNotificationChannels.Email);
		await WithUnitOfWorkAsync(async () =>
		{
			var issues = GetRequiredService<IRepository<BookIssue, Guid>>();
			var issue = await issues.GetAsync(issueId);
			issue.ApplyRenewedDueDate(new DateOnly(2026, 12, 5));
			await issues.UpdateAsync(issue, true);
		});
		await services.Get<LibraryNotificationDeliveryJob>().ExecuteAsync(new() { TenantId = context.TenantId, HistoryId = email.Id });
		(await StoredNotificationAsync(email.Id)).Status.ShouldBe(LibraryNotificationDeliveryStatuses.Skipped);
		await services.Sender.DidNotReceive().SendAsync(to: Arg.Any<string>(), subject: Arg.Any<string>(), body: Arg.Any<string>(), isBodyHtml: false);
	}

	[Fact]
	public async Task An_Issue_Confirmation_Should_Use_The_Issue_Grant_Without_An_Unrelated_Notification_Send_Grant()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var issueId = await SeedIssueAsync(context, context.StudentId, new DateOnly(2026, 11, 18));
		await WithUnitOfWorkAsync(async () =>
		{
			var permissions = GetRequiredService<IPermissionManager>();
			await permissions.SetAsync(Send, "U", context.ActorId.ToString(), false);
			await permissions.SetAsync(ShikshaMitraSubmodulePermissions.Library.Notification.View, "U", context.ActorId.ToString(), false);
			await permissions.SetAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue, "U", context.ActorId.ToString(), true);
		});
		var services = Services();
		var rows = await WithUnitOfWorkAsync(async () =>
		{
			var issue = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(issueId);
			var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.CopyId);
			return await services.Get<LibraryNotificationReminderService>().SendIssueConfirmationAsync(issue, await GetRequiredService<IRepository<Book, Guid>>().GetAsync(copy.BookId), copy);
		});
		var email = rows.Single(row => row.Channel == LibraryNotificationChannels.Email);
		email.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Queued);
		(await services.Get<IPermissionChecker>().IsGrantedAsync(Send)).ShouldBeFalse();
		await services.Get<LibraryNotificationDeliveryJob>().ExecuteAsync(new() { TenantId = context.TenantId, HistoryId = email.Id });
		(await StoredNotificationAsync(email.Id)).Status.ShouldBe(LibraryNotificationDeliveryStatuses.Sent);
	}

	[Fact]
	public async Task An_Opt_Out_Saved_After_Queueing_Should_Stop_Dispatch()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var services = Services();
		var rows = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().SendManualAsync(Request(context.StudentId)));
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<ConsentRecord, Guid>>().InsertAsync(new ConsentRecord(Guid.NewGuid(), context.TenantId)
		{
			RecipientType = "ParentGuardian", RecipientId = context.StudentId, Channel = CommunicationChannelType.Email,
			Destination = "guardian@example.test", Status = CommunicationConsentStatus.Revoked, RevokedAt = DateTimeOffset.UtcNow
		}, true));
		await services.Get<LibraryNotificationDeliveryJob>().ExecuteAsync(new() { TenantId = context.TenantId, HistoryId = rows.Single().Id });
		(await StoredNotificationAsync(rows.Single().Id)).Status.ShouldBe(LibraryNotificationDeliveryStatuses.Skipped);
		await services.Sender.DidNotReceive().SendAsync(to: Arg.Any<string>(), subject: Arg.Any<string>(), body: Arg.Any<string>(), isBodyHtml: false);
	}

	[Fact]
	public async Task Missing_Automatic_Setup_Should_Record_One_Skipped_Operational_Audit_Without_Inventing_A_School_Date()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var services = Services();
		var first = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync());
		var row = first.Single();
		row.EventType.ShouldBe("automaticReminderRun");
		row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Skipped);
		row.RecipientMemberId.ShouldBeNull();
		row.RecipientUserId.ShouldBeNull();
		row.ReminderDate.ShouldBeNull();
		row.SentAt.ShouldBeNull();
		row.ErrorMessage.ShouldNotBeNull().ShouldContain("timezone");
		(await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync())).ShouldBeEmpty();
		using var actor = Principal(context.ActorId);
		var request = Request(context.StudentId);
		request.EventType = "automaticReminderRun";
		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().SendManualAsync(request)));
	}

	[Theory]
	[InlineData("email")]
	[InlineData("inapp")]
	public async Task Supported_Channel_Names_Should_Keep_Their_Existing_Case_Insensitive_Contract(string channel)
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var services = Services();
		var request = Request(context.StudentId);
		request.Channel = channel;
		var rows = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().SendManualAsync(request));
		var row = rows.Single();
		if (channel == "email")
		{
			row.Channel.ShouldBe(LibraryNotificationChannels.Email);
			row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Queued);
			await services.Get<LibraryNotificationDeliveryJob>().ExecuteAsync(new() { TenantId = context.TenantId, HistoryId = row.Id });
			(await StoredNotificationAsync(row.Id)).Status.ShouldBe(LibraryNotificationDeliveryStatuses.Sent);
		}
		else
		{
			row.Channel.ShouldBe(LibraryNotificationChannels.InApp);
			row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Sent);
			row.RecipientUserId.ShouldBe(context.GuardianId);
		}
	}

	[Theory]
	[InlineData(301, 10)]
	[InlineData(10, 2001)]
	public async Task Send_Api_Should_Reject_An_Oversized_Subject_Or_Message_Before_Persistence(int subjectLength, int messageLength)
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var request = Request(context.StudentId);
		request.Subject = new string('S', subjectLength);
		request.Message = new string('M', messageLength);
		await Should.ThrowAsync<AbpValidationException>(() => GetRequiredService<ILibraryNotificationAppService>().SendAsync(request));
	}

	[Fact]
	public async Task Send_Api_Should_Reject_An_Unknown_Channel_Instead_Of_Returning_An_Empty_Result()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var request = Request(context.StudentId);
		request.Channel = "Pager";
		await Should.ThrowAsync<AbpValidationException>(() => GetRequiredService<ILibraryNotificationAppService>().SendAsync(request));
	}

	[Theory]
	[InlineData("grant")]
	[InlineData("scope")]
	[InlineData("identity")]
	public async Task Queued_Manual_Email_Should_Recheck_The_Initiators_Current_Database_Authority(string revoked)
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var services = Services();
		var rows = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().SendManualAsync(Request(context.StudentId)));
		await WithUnitOfWorkAsync(async () =>
		{
			if (revoked == "grant") await GetRequiredService<IPermissionManager>().SetAsync(Send, "U", context.ActorId.ToString(), false);
			if (revoked == "scope") await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().DeleteAsync(x => x.UserId == context.ActorId, true);
			if (revoked == "identity")
			{
				var users = GetRequiredService<IIdentityUserRepository>();
				var user = await users.GetAsync(context.ActorId);
				user.SetIsActive(false);
				await users.UpdateAsync(user, true);
			}
		});
		await services.Get<LibraryNotificationDeliveryJob>().ExecuteAsync(new() { TenantId = context.TenantId, HistoryId = rows.Single().Id });
		var stored = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryNotificationHistory, Guid>>().GetAsync(rows.Single().Id));
		stored.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Skipped);
		stored.SentAt.ShouldBeNull();
		await services.Sender.DidNotReceive().SendAsync(to: Arg.Any<string>(), subject: Arg.Any<string>(), body: Arg.Any<string>(), isBodyHtml: false);
	}

	[Fact]
	public async Task Repeat_Interval_Should_Use_The_Last_Intents_School_Reminder_Date()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var issueId = await SeedIssueAsync(context, context.StudentId, new DateOnly(2026, 11, 15));
		var services = Services();
		await WithUnitOfWorkAsync(async () =>
		{
			foreach (var setting in (await services.Get<LibraryNotificationDeliveryService>().EnsureSettingsAsync())
				.Where(setting => setting.EventType == LibraryNotificationEventTypes.OverdueReminder))
			{
				setting.RepeatIntervalDays = 3;
				await GetRequiredService<IRepository<LibraryNotificationSetting, Guid>>().UpdateAsync(setting, true);
			}
		});
		var first = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 16)));
		first.ShouldContain(row => row.EventType == LibraryNotificationEventTypes.OverdueReminder && row.RelatedEntityId == issueId && row.Channel == LibraryNotificationChannels.Email);
		var nextDay = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 17)));
		nextDay.ShouldNotContain(row => row.EventType == LibraryNotificationEventTypes.OverdueReminder && row.RelatedEntityId == issueId);
		var eligibleDay = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 19)));
		eligibleDay.ShouldContain(row => row.EventType == LibraryNotificationEventTypes.OverdueReminder && row.RelatedEntityId == issueId && row.Channel == LibraryNotificationChannels.Email);
	}

	[Fact]
	public async Task Operational_Recipient_Save_Dispatch_And_Private_Read_Should_Use_Current_Database_View_Authority()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var services = Services();
		await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationAppService>().UpdateSettingsAsync(new() { OperationalRecipientUserIds = [context.ActorId] }));
		var rows = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 16)));
		var inboxRow = rows.Single(row => row.EventType == LibraryNotificationEventTypes.LowStockAlert && row.Channel == LibraryNotificationChannels.InApp);
		inboxRow.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Sent);
		inboxRow.RecipientUserId.ShouldBe(context.ActorId);
		var email = rows.Single(row => row.EventType == LibraryNotificationEventTypes.LowStockAlert && row.Channel == LibraryNotificationChannels.Email);
		email.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Queued);
		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(ShikshaMitraSubmodulePermissions.Library.Notification.View,
			"U", context.ActorId.ToString(), false));
		await services.Get<LibraryNotificationDeliveryJob>().ExecuteAsync(new() { TenantId = context.TenantId, HistoryId = email.Id });
		(await StoredNotificationAsync(email.Id)).Status.ShouldBe(LibraryNotificationDeliveryStatuses.Skipped);
		(await services.Get<LibraryInboxAppService>().GetAsync(new())).TotalCount.ShouldBe(0);
		await services.Sender.DidNotReceive().SendAsync(to: Arg.Any<string>(), subject: Arg.Any<string>(), body: Arg.Any<string>(), isBodyHtml: false);
	}

	[Fact]
	public async Task School_Reminder_Time_And_Operational_Recipients_Should_Not_Inherit_Host_Global_Values()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		await WithUnitOfWorkAsync(async () =>
		{
			var settings = GetRequiredService<ISettingManager>();
			await settings.SetGlobalAsync(LibraryNotificationSettingNames.AutomaticReminderTimeLocal, "09:00");
			await settings.SetGlobalAsync(LibraryNotificationSettingNames.OperationalRecipientUserIds, "[\"" + context.ActorId + "\"]");
			await settings.SetForTenantAsync(context.TenantId, LibraryPolicySettingNames.CalendarTimeZone, "Pacific/Honolulu", forceToSet: true);
		});
		var services = Services();
		var setup = await services.Get<LibraryNotificationPreferences>().GetSetupAsync();
		setup.AutomaticReminderTimeLocal.ShouldBeNull();
		setup.OperationalRecipientUserIds.ShouldBeEmpty();
		setup.NeedsConfiguration.ShouldBeTrue();
		var rows = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 16)));
		rows.ShouldNotContain(row => row.EventType == LibraryNotificationEventTypes.LowStockAlert && row.Status == LibraryNotificationDeliveryStatuses.Sent);
		rows.ShouldContain(row => row.EventType == LibraryNotificationEventTypes.LowStockAlert && row.Status == LibraryNotificationDeliveryStatuses.Skipped);
	}

	[Fact]
	public async Task Inactive_Membership_Should_Keep_Private_Existing_Loan_Notices_Without_Opening_Unrelated_Historical_Items()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var unrelatedId = await CreateInboxItemAsync(context);
		var issueId = await SeedIssueAsync(context, context.StudentId, new DateOnly(2026, 11, 15));
		await WithUnitOfWorkAsync(async () =>
		{
			var students = GetRequiredService<IRepository<Student, Guid>>();
			var student = await students.GetAsync(context.StudentId);
			student.AdditionalDetail!.IsLibraryMember = false;
			await students.UpdateAsync(student, true);
			var members = GetRequiredService<IRepository<LibraryMember, Guid>>();
			var member = await members.GetAsync(context.MemberId);
			member.ChangeMemberStatus(false);
			await members.UpdateAsync(member, true);
		});
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 16)));
		var recovery = rows.Single(row => row.RelatedEntityId == issueId && row.EventType == LibraryNotificationEventTypes.OverdueReminder && row.Channel == LibraryNotificationChannels.InApp);
		recovery.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Sent);
		using var guardian = Principal(context.GuardianId);
		var inbox = await GetRequiredService<ILibraryInboxAppService>().GetAsync(new());
		inbox.Items.ShouldContain(row => row.Id == recovery.Id);
		inbox.Items.ShouldNotContain(row => row.Id == unrelatedId);
		inbox.TotalCount.ShouldBe(1);
		(await GetRequiredService<ILibraryInboxAppService>().MarkReadAsync(recovery.Id)).ReadAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task Operational_Recipient_Options_Should_Require_Owning_Actions_And_Only_List_Current_Eligible_Users()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		await WithUnitOfWorkAsync(() => GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(Guid.NewGuid(),
			"librarian-ineligible-" + Guid.NewGuid().ToString("N"), "ineligible@example.test", context.TenantId), true));
		using var actor = Principal(context.ActorId);
		var services = Services();
		var options = await services.Get<LibraryNotificationAppService>().GetOperationalRecipientsAsync("librarian");
		options.Select(option => option.UserId).ShouldBe([context.ActorId]);
		using var guardian = Principal(context.GuardianId);
		await Should.ThrowAsync<AbpAuthorizationException>(() => services.Get<LibraryNotificationAppService>().GetOperationalRecipientsAsync("librarian"));
	}

	[Theory]
	[InlineData("subject")]
	[InlineData("message")]
	public async Task An_Expanded_Template_Exceeding_Storage_Length_Should_Record_A_Skipped_Intent_Without_Breaking_Circulation(string field)
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var issueId = await SeedIssueAsync(context, context.StudentId, new DateOnly(2026, 11, 18));
		var services = Services();
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IPermissionManager>().SetAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue, "U", context.ActorId.ToString(), true);
			var setting = (await services.Get<LibraryNotificationDeliveryService>().EnsureSettingsAsync()).Single(setting =>
				setting.EventType == LibraryNotificationEventTypes.BookIssueConfirmation && setting.Channel == LibraryNotificationChannels.Email);
			if (field == "subject") { setting.SubjectTemplate = new string('S', 285) + "{{bookTitle}}"; setting.SubjectTemplate.Length.ShouldBeLessThanOrEqualTo(300); }
			else { setting.MessageTemplate = new string('M', 1985) + "{{bookTitle}}"; setting.MessageTemplate.Length.ShouldBeLessThanOrEqualTo(2000); }
			await GetRequiredService<IRepository<LibraryNotificationSetting, Guid>>().UpdateAsync(setting, true);
		});
		var rows = await WithUnitOfWorkAsync(async () =>
		{
			var issue = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(issueId);
			var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.CopyId);
			return await services.Get<LibraryNotificationReminderService>().SendIssueConfirmationAsync(issue,
				await GetRequiredService<IRepository<Book, Guid>>().GetAsync(copy.BookId), copy);
		});
		var email = rows.Single(row => row.Channel == LibraryNotificationChannels.Email);
		email.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Skipped);
		email.ErrorMessage.ShouldNotBeNull().ShouldContain("length");
		email.Subject.Length.ShouldBeLessThanOrEqualTo(300);
		email.Message.Length.ShouldBeLessThanOrEqualTo(2000);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(issueId))).Status.ShouldBe(BookIssueStatus.Issued);
	}

	[Fact]
	public async Task A_Delayed_Due_Soon_Reminder_Should_Not_Dispatch_After_Its_School_Due_Date_Has_Passed()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		await WithUnitOfWorkAsync(() => GetRequiredService<ISettingManager>().SetForTenantAsync(context.TenantId,
			LibraryPolicySettingNames.CalendarTimeZone, "UTC", forceToSet: true));
		var issueId = await SeedIssueAsync(context, context.StudentId, new DateOnly(2026, 11, 18));
		var services = Services(new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc));
		var rows = await WithUnitOfWorkAsync(() => services.Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 16)));
		var email = rows.Single(row => row.RelatedEntityId == issueId && row.EventType == LibraryNotificationEventTypes.DueDateReminder && row.Channel == LibraryNotificationChannels.Email);
		email.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Queued);
		services.Get<IClock>().Now.Returns(new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc));
		await services.Get<LibraryNotificationDeliveryJob>().ExecuteAsync(new() { TenantId = context.TenantId, HistoryId = email.Id });
		(await StoredNotificationAsync(email.Id)).Status.ShouldBe(LibraryNotificationDeliveryStatuses.Skipped);
		await services.Sender.DidNotReceive().SendAsync(to: Arg.Any<string>(), subject: Arg.Any<string>(), body: Arg.Any<string>(), isBodyHtml: false);
	}

	private Task<Context> SeedAsync(bool linkGuardian = true) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid();
		using var school = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var actorId = Guid.NewGuid();
		var guardianId = Guid.NewGuid();
		var users = GetRequiredService<IIdentityUserRepository>();
		await users.InsertAsync(new IdentityUser(actorId, "librarian-" + actorId.ToString("N"), "librarian@example.test", tenantId), true);
		var guardian = new IdentityUser(guardianId, "guardian-" + guardianId.ToString("N"), "guardian@example.test", tenantId);
		guardian.SetEmailConfirmed(true);
		await users.InsertAsync(guardian, true);
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: actorId), true);
		await GetRequiredService<IPermissionManager>().SetAsync(Send, "U", actorId.ToString(), true);
		await GetRequiredService<IPermissionManager>().SetAsync(ShikshaMitraSubmodulePermissions.Library.Notification.View, "U", actorId.ToString(), true);
		var year = new AcademicYear(Guid.NewGuid(), tenantId, "Library communication", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31));
		await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
		var board = new EducationBoard(Guid.NewGuid(), tenantId, EducationBoardType.CBSE, year.Id);
		await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
		var medium = new EducationMedium(Guid.NewGuid(), tenantId, "English", board.Id);
		await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
		var stage = new EducationMediumStage(Guid.NewGuid(), tenantId, "Primary", null, medium.Id);
		await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenantId, "Library", null, "Child", year.Id, board.Id, medium.Id, stage.Id,
			standardId: null, divisionId: null, new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero), GenderEnum.Female,
			primaryContactNo: "9999999999", email: "child@example.test");
		student.AdditionalDetail!.IsLibraryMember = true;
		student.UpdateFamilyInfo(null, new ParentGuardianDetails("Selected guardian", null, null, "guardian@example.test", "9999999998",
			null, null, null, null, null, null, null, null), null, null);
		StudentFamilyContactSelection.Select(student, "mother");
		await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
		var member = new LibraryMember(Guid.NewGuid(), tenantId, student.Id, "LIB-" + Guid.NewGuid().ToString("N")[..12], true);
		await GetRequiredService<IRepository<LibraryMember, Guid>>().InsertAsync(member, true);
		if (linkGuardian) await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(
			new StudentPortalAccountLink(Guid.NewGuid(), tenantId, student.Id, guardianId, "guardian", "mother", actorId), true);
		var category = new BookCategory("Communication books", string.Empty, year.Id, tenantId);
		await GetRequiredService<IRepository<BookCategory, Guid>>().InsertAsync(category, true);
		var section = new LibrarySection(Guid.NewGuid(), tenantId, year.Id, "Main Library", 1);
		await GetRequiredService<IRepository<LibrarySection, Guid>>().InsertAsync(section, true);
		var shelf = new LibraryShelf(Guid.NewGuid(), section.Id, "A1");
		await GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(shelf, true);
		var book = new Book("Communication book", string.Empty, "Author", "School press", "1", "Reading", category.Id, Language.English,
			"10+", 100m, 100, 1, year.Id, tenantId);
		await GetRequiredService<IRepository<Book, Guid>>().InsertAsync(book, true);
		var copy = new BookCopy(Guid.NewGuid(), book.Id, 1, "NOTIFY-COPY", section.Id, shelf.Id, tenantId);
		await GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(copy, true);
		return new Context(tenantId, actorId, guardianId, student.Id, member.Id, copy.Id);
	});

	private IDisposable Principal(Guid userId) => GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
		[new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.TenantId, GetRequiredService<ICurrentTenant>().Id!.Value.ToString())], "LibraryNotificationTest")));

	private static SendLibraryNotificationRequest Request(Guid studentId) => new()
	{
		EventType = LibraryNotificationEventTypes.FineDueReminder, Channel = LibraryNotificationChannels.Email,
		Subject = "Library reminder", Message = "Please review your Library account.", Recipients = [new() { MemberId = studentId }]
	};

	private async Task<Guid> CreateInboxItemAsync(Context context)
	{
		using var sender = Principal(context.ActorId);
		var request = Request(context.StudentId);
		request.Channel = LibraryNotificationChannels.InApp;
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().SendManualAsync(request));
		rows.Single().Status.ShouldBe(LibraryNotificationDeliveryStatuses.Sent);
		return rows.Single().Id;
	}

	private Task<LibraryNotificationHistory> StoredNotificationAsync(Guid id) =>
		WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryNotificationHistory, Guid>>().GetAsync(id));

	private Task<Guid> SeedIssueAsync(Context context, Guid memberId, DateOnly dueDate) => WithUnitOfWorkAsync(async () =>
	{
		var student = await GetRequiredService<IRepository<Student, Guid>>().GetAsync(context.StudentId);
		var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
		var copy = await copies.GetAsync(context.CopyId);
		var issue = BookIssue.CreateCivil(Guid.NewGuid(), context.TenantId, copy.Id, copy.AccessionNumber, memberId, 0,
			new DateOnly(2026, 11, 10), dueDate, new LibraryLoanIssueContext(student.AcademicYearId!.Value,
				student.AcademicBoardId, student.AcademicMediumId, student.AcademicMediumStageId, student.StandardId, student.DivisionId,
				"{}", "{}", "{}", context.ActorId, new DateTimeOffset(2026, 11, 10, 3, 0, 0, TimeSpan.Zero)));
		await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(issue, true);
		copy.Status = BookCopyStatus.Issued;
		await copies.UpdateAsync(copy, true);
		return issue.Id;
	});

	private NotificationServices Services(DateTime? now = null) => new(ServiceProvider, now);
	private sealed record Context(Guid TenantId, Guid ActorId, Guid GuardianId, Guid StudentId, Guid MemberId, Guid CopyId);

	private sealed class NotificationServices : IServiceProvider
	{
		private readonly IServiceProvider _inner;
		private readonly IPermissionChecker _checker;
		private readonly IClock? _clock;
		private readonly Dictionary<Type, object> _services = new();
		public IEmailSender Sender { get; } = Substitute.For<IEmailSender>();
		public NotificationServices(IServiceProvider inner, DateTime? now)
		{
			_inner = inner;
			_checker = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionChecker>(inner);
			if (now.HasValue)
			{
				_clock = Substitute.For<IClock>();
				_clock.Now.Returns(now.Value);
			}
		}
		public T Get<T>() where T : class => (T)GetService(typeof(T))!;
		public object? GetService(Type type)
		{
			if (type == typeof(IPermissionChecker)) return _checker;
			if (type == typeof(IEmailSender)) return Sender;
			if (type == typeof(ILibraryNotificationAuthorityService)) return GetService(typeof(LibraryNotificationAuthorityService));
			if (type == typeof(ILibraryNotificationRecipientResolver)) return GetService(typeof(LibraryNotificationRecipientResolver));
			if (type == typeof(ILibraryNotificationReminderEligibility)) return GetService(typeof(LibraryNotificationReminderEligibility));
			if (type == typeof(IClock) && _clock != null) return _clock;
			if (type == typeof(LibraryCalendarService) && _clock != null)
			{
				if (!_services.TryGetValue(type, out var calendar)) _services[type] = calendar = ActivatorUtilities.CreateInstance<LibraryCalendarService>(this);
				return calendar;
			}
			if (type.IsClass && type.Namespace == typeof(LibraryNotificationAppService).Namespace && type.Assembly == typeof(LibraryNotificationAppService).Assembly)
			{
				if (_services.TryGetValue(type, out var existing)) return existing;
				var service = ActivatorUtilities.CreateInstance(this, type);
				if (service is ApplicationService application) application.LazyServiceProvider = new AbpLazyServiceProvider(this);
				_services[type] = service;
				return service;
			}
			return _inner.GetService(type);
		}
	}
}
