using System;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Policies;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryRolloverPreview_Tests : ShikshaMitraApplicationTestBase<LibraryStockVerificationTestModule>
{
	[Fact]
	public async Task Rollover_Should_Show_Reviewed_Terms_And_Outstanding_Original_Loans_Within_Their_Owning_Authority()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var tenant = GetRequiredService<ICurrentTenant>().Id;
		var actor = GetRequiredService<ICurrentUser>().Id!.Value;
		var selectedDate = new DateOnly(2026, 4, 1);
		var priorDay = selectedDate.AddDays(-10);
		var loan = BookIssue.CreateCivil(Guid.NewGuid(), tenant, seed.CopyId, "ORIGINAL-LOAN", Guid.NewGuid(), 0, priorDay,
			selectedDate.AddDays(2), new(seed.OriginYearId, null, null, null, null, null, "{}", "{}", "{}", actor, DateTimeOffset.UtcNow));
		var global = new LibraryBorrowingPolicyVersion(Guid.NewGuid(), tenant, seed.OriginYearId, Guid.NewGuid(), 1,
			new("School-wide manual dates", 0, priorDay, null, null, null, null, null, 0,
				new(2, 1, 0, LibraryDayCountingMode.CalendarDays, 0, 0, 0, false, LibraryDueDateMode.Manual)));
		global.Publish(global.ConcurrencyStamp, DateTimeOffset.UtcNow);
		var draft = new LibraryBorrowingPolicyVersion(Guid.NewGuid(), tenant, seed.YearId, Guid.NewGuid(), 1,
			new("New school year automated dates", 1, selectedDate, null, null, null, null, null, 0,
				new(5, 2, 14, LibraryDayCountingMode.OpenDays, 1, 7, 2, false)));
		await WithUnitOfWorkAsync(async () =>
		{
			var versions = GetRequiredService<IRepository<LibraryBorrowingPolicyVersion, Guid>>();
			await versions.InsertManyAsync([global, draft], true);
			var loans = GetRequiredService<IRepository<BookIssue, Guid>>();
			await loans.InsertAsync(loan, true);
			var day = priorDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
			await loans.InsertAsync(new(Guid.NewGuid(), Guid.NewGuid(), "UNASSIGNED-LOAN", Guid.NewGuid(), 0, day, day.AddDays(7), tenant), true);
		});
		var permissions = GetRequiredService<IPermissionChecker>();
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.View).Returns(true);
		var app = GetRequiredService<ILibraryRolloverAppService>();
		var options = await app.GetOptionsAsync();
		options.Select(year => year.Id).ShouldContain(seed.OriginYearId);
		options.Select(year => year.Id).ShouldContain(seed.YearId);
		var request = new LibraryRolloverInput(seed.OriginYearId, seed.YearId, selectedDate);
		var restricted = await app.PreviewAsync(request);
		restricted.Loans.Scope.ShouldBe("Restricted");
		restricted.Loans.SourceYearOpenCount.ShouldBeNull();

		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View).Returns(true);
		var preview = await app.PreviewAsync(request);
		preview.SourceYear.Id.ShouldBe(seed.OriginYearId);
		preview.TargetYear.Id.ShouldBe(seed.YearId);
		preview.EffectivePolicies.ShouldHaveSingleItem().Name.ShouldBe(global.Name);
		preview.DraftPolicies.ShouldHaveSingleItem().Name.ShouldBe(draft.Name);
		preview.Calendar.OpenWeekdays.ShouldBe(5);
		preview.Calendar.CanPublishAutomatic.ShouldBeFalse();
		preview.Loans.SourceYearOpenCount.ShouldBe(1);
		preview.Loans.UnassignedYearOpenCount.ShouldBe(1);
		preview.Loans.Scope.ShouldBe("School");
		await Should.ThrowAsync<BusinessException>(() => app.PreviewAsync(request with { SchoolDate = selectedDate.AddDays(-1) }));
		await WithUnitOfWorkAsync(async () =>
		{
			var saved = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(loan.Id);
			saved.AcademicYearId.ShouldBe(seed.OriginYearId);
			saved.OriginalSchoolDueDate.ShouldBe(selectedDate.AddDays(2));
			saved.Status.ShouldBe(BookIssueStatus.Issued);
		});
	}
}
