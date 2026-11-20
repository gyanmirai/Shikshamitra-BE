using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Portal;
using ShikshaMitra.Students.Portal;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCanonicalRenewal_Tests
{
	[Fact]
	public async Task A_Linked_Guardian_Should_Renew_The_Owned_Loan_Using_The_Saved_Terms_And_Lose_Access_On_Revocation()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		await GetRequiredService<ILibraryCalendarAppService>().UpdateSettingsAsync(new() { TimeZoneId = "Asia/Kolkata" });
		await PublishAsync(context.Catalog.YearId, 7, 1);
		var input = new LibraryCheckoutRequest
		{
			CopyId = context.Catalog.CopyId, MemberId = context.StudentId, IssueDate = new(2026, 11, 17), IdempotencyKey = "portal-issue"
		};
		input.Revision = (await GetRequiredService<ILibraryIssuePlanAppService>().PreviewAsync(new()
		{
			CopyId = input.CopyId, MemberId = input.MemberId, IssueDate = input.IssueDate
		})).Revision;
		var issued = await GetRequiredService<ILibraryLoanAppService>().CheckoutAsync(input);
		var tenant = GetRequiredService<ICurrentTenant>().Id!.Value;
		var guardianId = Guid.NewGuid();
		var linkId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var guardian = new IdentityUser(guardianId, $"guardian-{guardianId:N}", "guardian@example.test", tenant);
			guardian.SetEmailConfirmed(true);
			await GetRequiredService<IIdentityUserRepository>().InsertAsync(guardian, true);
			await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(
				new StudentPortalAccountLink(linkId, tenant, context.StudentId, guardianId, "guardian", "mother", guardianId), true);
		});
		using (GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, guardianId.ToString()),
			new Claim(AbpClaimTypes.TenantId, tenant.ToString())
		], "My Library Test"))))
		{
			var portal = GetRequiredService<ILibraryPortalAppService>();
			(await portal.GetStudentsAsync()).Single().StudentId.ShouldBe(context.StudentId);
			await Should.ThrowAsync<AbpAuthorizationException>(() => portal.GetLoansAsync(Guid.NewGuid()));
			await Should.ThrowAsync<AbpAuthorizationException>(() => portal.PreviewRenewAsync(context.StudentId, Guid.NewGuid()));
			(await portal.SearchAsync(context.StudentId, null)).Items.Single().AvailableCopies.ShouldBe(0);
			var loans = await portal.GetLoansAsync(context.StudentId);
			loans.Items.Single().DueOn.ShouldBe(new DateOnly(2026, 11, 24));
			var renewal = await portal.PreviewRenewAsync(context.StudentId, issued.Loan.Id);
			renewal.CanRenew.ShouldBeTrue();
			var command = new LibraryPortalRenewInput(renewal.SchoolDate!.Value, renewal.Revision!, "portal-renew");
			var recorded = await portal.RenewAsync(context.StudentId, issued.Loan.Id, command);
			recorded.DueOn.ShouldBe(new DateOnly(2026, 12, 1));
			recorded.Renewals.ShouldBe(1);
			(await portal.RenewAsync(context.StudentId, issued.Loan.Id, command)).DueOn.ShouldBe(recorded.DueOn);
			(await portal.PreviewRenewAsync(context.StudentId, issued.Loan.Id)).CanRenew.ShouldBeFalse();
			await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().DeleteAsync(linkId, true));
			await Should.ThrowAsync<AbpAuthorizationException>(() => portal.GetLoansAsync(context.StudentId));
			await Should.ThrowAsync<AbpAuthorizationException>(() => portal.RenewAsync(context.StudentId, issued.Loan.Id, command));
		}
	}
}
