using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;

namespace ShikshaMitra.Library;

public partial class LibraryLoanMaintenanceRead_Tests
	: ShikshaMitraApplicationTestBase<LibraryCanonicalLookupCalendarTestModule>
{
	private const string View = ShikshaMitraSubmodulePermissions.Library.IssueReturn.View;
	private const string Renew = ShikshaMitraSubmodulePermissions.Library.IssueReturn.Renew;
	private const string Review = ShikshaMitraSubmodulePermissions.Library.IssueReturn.ReviewDates;
	private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);
	private ILibraryLoanAppService Loans => GetRequiredService<ILibraryLoanAppService>();
	private IPermissionChecker Permissions => GetRequiredService<IPermissionChecker>();

	private Task<LibraryCopyLifecycleTestContext> SeedLegacyLoanAsync() =>
		WithUnitOfWorkAsync<LibraryCopyLifecycleTestContext>(() =>
			LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider, onLoan: true));

	private async Task OwnOnlyAsync(params string[] actions)
	{
		var allowed = new HashSet<string>(actions, StringComparer.Ordinal);
		Permissions.IsGrantedAsync(Arg.Any<string>()).Returns(call => allowed.Contains(call.Arg<string>()));
		foreach (var action in actions) (await Permissions.IsGrantedAsync(action)).ShouldBeTrue();
		foreach (var unrelated in new[]
		{
			ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return,
			ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue,
			ShikshaMitraSubmodulePermissions.Library.AllBooks.View,
			ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View,
			ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.View,
			ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add,
			ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View
		}) (await Permissions.IsGrantedAsync(unrelated)).ShouldBeFalse();
	}

	private Task SetZoneAsync(string name, string value) => WithUnitOfWorkAsync(() =>
		GetRequiredService<ISettingManager>().SetForTenantAsync(
			GetRequiredService<ICurrentTenant>().Id!.Value, name, value, forceToSet: true));

	private static LibraryCirculationSchoolContextDto ContextOf(LibraryLoanDto loan)
	{
		var wire = JsonSerializer.SerializeToElement(loan, WireJson);
		wire.TryGetProperty("schoolContext", out var context).ShouldBeTrue(
			"The exact-loan read must provide its owning school context; callers must not need Return or Calendar access.");
		context.ValueKind.ShouldBe(JsonValueKind.Object);
		return context.Deserialize<LibraryCirculationSchoolContextDto>(WireJson)!;
	}

	private static bool CanReviewDatesOf(LibraryLoanDto loan)
	{
		var wire = JsonSerializer.SerializeToElement(loan, WireJson);
		wire.TryGetProperty("canReviewDates", out var capability).ShouldBeTrue(
			"The exact-loan read must explicitly report the current actor's date-review capability.");
		(capability.ValueKind == JsonValueKind.True || capability.ValueKind == JsonValueKind.False).ShouldBeTrue();
		return capability.GetBoolean();
	}

	private Task AssertLegacyDatesUntouchedAsync(Guid loanId) => WithUnitOfWorkAsync(async () =>
	{
		var stored = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(loanId);
		stored.SchoolIssueDate.ShouldBeNull(); stored.SchoolDueDate.ShouldBeNull();
		((int)stored.IssueDateProvenance).ShouldBe(0); ((int)stored.DueDateProvenance).ShouldBe(0);
		stored.IssueDate.ShouldBe(new DateTime(2026, 11, 1));
		stored.DueDate.ShouldBe(new DateTime(2026, 11, 8));
	});
}
