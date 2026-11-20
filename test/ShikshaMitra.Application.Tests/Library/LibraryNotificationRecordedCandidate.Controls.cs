using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Notification_Recorded_Candidate_A_Noncanonical_Source_Type_Should_Not_Bypass_The_Stored_Record_Scope(bool reservation)
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var fixture = await SeedRecordedCandidateAsync(context, staff: false, reservation);
		await MoveRecordedCandidateAsync(fixture);
		await SetRecordedCandidateScopeAsync(fixture, currentPlacement: true);
		var request = RecordedCandidateRequest(fixture);
		request.RelatedEntityType += " ";

		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			Services().Get<LibraryNotificationAppService>().SendAsync(request)));
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryNotificationHistory, Guid>>().CountAsync(row =>
			row.RelatedEntityId == fixture.SourceId && (row.Status == LibraryNotificationDeliveryStatuses.Queued || row.Status == LibraryNotificationDeliveryStatuses.Sent))))
			.ShouldBe(0);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Notification_Recorded_Candidate_No_Source_Message_Should_Still_Require_Current_Person_Access(bool staff)
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var fixture = await SeedRecordedCandidateAsync(context, staff, reservation: false);
		await MoveRecordedCandidateAsync(fixture);
		await SetRecordedCandidateScopeAsync(fixture, currentPlacement: false);

		(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			Services().Get<LibraryNotificationAppService>().SendAsync(Request(fixture.MemberId)))))
			.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryNotificationHistory, Guid>>().CountAsync(row =>
			row.RecipientMemberId == fixture.MemberId && (row.Status == LibraryNotificationDeliveryStatuses.Queued || row.Status == LibraryNotificationDeliveryStatuses.Sent))))
			.ShouldBe(0);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Notification_Recorded_Candidate_Missing_Send_Grant_Should_Block_Dispatch_Despite_Readable_Source(bool reservation)
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var fixture = await SeedRecordedCandidateAsync(context, staff: false, reservation);
		await WithUnitOfWorkAsync(async () =>
		{
			var scope = await GetRequiredService<IUserScopeService>().GetUserScopeAsync(context.ActorId);
			(reservation ? await GetRequiredService<LibraryStoredReservationScope>().CanReadAsync(fixture.SourceId, scope) :
				await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(fixture.SourceId, scope)).ShouldBeTrue();
			await GetRequiredService<IPermissionManager>().SetAsync(Send, "U", context.ActorId.ToString(), false);
		});
		(await Services().Get<IPermissionChecker>().IsGrantedAsync(Send)).ShouldBeFalse();

		var result = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationAppService>().SendAsync(RecordedCandidateRequest(fixture)));

		result.Queued.ShouldBe(0); result.Sent.ShouldBe(0);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Notification_Recorded_Candidate_Foreign_Tenant_Should_Not_Use_A_Known_Source_And_Person_Id(bool reservation)
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var fixture = await SeedRecordedCandidateAsync(context, staff: false, reservation);
		using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
		{
			var error = await Record.ExceptionAsync(() => WithUnitOfWorkAsync(() =>
				Services().Get<LibraryNotificationAppService>().SendAsync(RecordedCandidateRequest(fixture))));
			error.ShouldNotBeNull();
			(error is AbpAuthorizationException || error is BusinessException business &&
				(business.Code == UserScopeErrorCodes.AccessDenied || business.Code == LibraryErrorCodes.MemberNotFound))
				.ShouldBeTrue(error.ToString());
			if (error is BusinessException { Code: LibraryErrorCodes.MemberNotFound })
				error.Message.ShouldBe("Library member was not found.");
		}
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryNotificationHistory, Guid>>().CountAsync(row =>
			row.RelatedEntityId == fixture.SourceId && (row.Status == LibraryNotificationDeliveryStatuses.Queued || row.Status == LibraryNotificationDeliveryStatuses.Sent))))
			.ShouldBe(0);
	}
}
