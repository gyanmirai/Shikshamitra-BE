using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Custody;
using ShikshaMitra.Library.InventoryVerification;
using ShikshaMitra.Library.Receiving;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using CustodyActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Custody;
using StockActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.InventoryVerification;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Classroom_Custody_Should_Seal_Actual_Handoff_And_Partial_Return_Evidence()
	{
		const string previous = "PersistLibraryStockImports";
		const string migration = "PersistLibraryClassroomCustody";
		const string route = "/api/library/custody";
		const string stockRoute = "/api/library/stock-verification";
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(previous);
		await MigrateStockVerificationAsync(database, migration);
		await MigrateStockVerificationAsync(database, previous);
		await MigrateStockVerificationAsync(database, migration);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var view = string.Join(',', CustodyActions.View, ShikshaMitraSubmodulePermissions.Library.AllBooks.View);
		using var reader = OwningClient(factory, view);
		using var librarian = OwningClient(factory, string.Join(',', view, CustodyActions.Add, CustodyActions.Return,
			StockActions.View, StockActions.Add, StockActions.Update, StockActions.Approve, ShikshaMitraSubmodulePermissions.Library.Receiving.View,
			ShikshaMitraSubmodulePermissions.Library.Receiving.Add));
		var (copy, _) = await SeedLoanContextAsync(factory);
		var employee = await SeedCustodyEmployeeAsync(factory);
		var book = Guid.Parse((await database.ScalarAsync($"SELECT \"BookId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{copy:D}';"))!);
		var section = Guid.Parse((await database.ScalarAsync($"SELECT \"SectionId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{copy:D}';"))!);
		var shelf = Guid.Parse((await database.ScalarAsync($"SELECT \"ShelfId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{copy:D}';"))!);
		var year = factory.YearId;
		var day = new DateOnly(2026, 11, 17);
		var receiving = new LibraryReceivingInput { ReceivedOn = day, Source = LibraryStockSource.Donation,
			Lines = [new() { BookId = book, SectionId = section, ShelfId = shelf, Quantity = 1, Accessions = ["PG-CUSTODY-02"] }] };
		var planned = await PostAsync<LibraryReceivingPreviewDto>(librarian, "/api/library/receiving/preview", receiving);
		var stocked = await PostAsync<LibraryStockReceiptDto>(librarian, "/api/library/receiving", new LibraryReceiveCommand
			{ Receipt = receiving, Revision = planned.Revision!, IdempotencyKey = "custody-receiving" });
		var second = stocked.Lines.Single().Copies.Single().CopyId;
		var input = new LibraryCustodyHandoffInput(year, employee, null, null, "Classroom readers", day, day.AddDays(7), [copy, second]);
		using var denied = await reader.PostAsJsonAsync(route + "/preview", input, JsonOptions);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var options = await librarian.GetFromJsonAsync<LibraryCustodyOptionsDto>($"{route}/options?academicYearId={year:D}", JsonOptions);
		options.ShouldNotBeNull();
		options.Employees.ShouldContain(row => row.Id == employee);
		var preview = await PostAsync<LibraryCustodyPreviewDto>(librarian, route + "/preview", input);
		preview.Errors.ShouldBeEmpty();
		var command = new LibraryCustodyHandoffCommand(input, preview.Revision!, "native-custody-handoff");
		var accepted = await Task.WhenAll(PostAsync<LibraryCustodyHandoffDto>(librarian, route, command),
			PostAsync<LibraryCustodyHandoffDto>(librarian, route, command));
		accepted[0].ShouldBe(accepted[1]);
		accepted[0].CopyCount.ShouldBe(2);
		var handoff = accepted[0].Id;
		var countInput = new LibraryStockCountInput("Verify classroom set", section, shelf, day);
		var baseline = await PostAsync<LibraryStockCountPreviewDto>(librarian, stockRoute + "/preview", countInput);
		var count = await PostAsync<LibraryStockVerificationSessionDto>(librarian, stockRoute,
			new LibraryOpenStockCountCommand(countInput, baseline.Revision, "custody-count"));
		var countRoute = $"{stockRoute}/{count.Id:D}";
		await PostAsync<LibraryStockTransitionDto>(librarian, countRoute + "/review", new LibraryBeginStockReviewCommand(0, "custody-count-review"));
		var publication = await librarian.GetFromJsonAsync<LibraryStockApprovalPreviewDto>(countRoute + "/approval-preview", JsonOptions);
		publication.ShouldNotBeNull();
		publication.CanFinalize.ShouldBeTrue();
		await PostAsync<LibraryStockTransitionDto>(librarian, countRoute + "/finalize",
			new LibraryFinalizeStockCountCommand(publication.Revision, "custody-count-finalize"));

		var returning = new LibraryCustodyReturnInput(day.AddDays(1), [new(copy, BookCopyStatus.Damaged)], "One reader returned damaged");
		var returnPlan = await PostAsync<LibraryCustodyPreviewDto>(librarian, $"{route}/{handoff:D}/returns/preview", returning);
		returnPlan.Errors.ShouldBeEmpty();
		var receiptCommand = new LibraryCustodyReturnCommand(returning, returnPlan.Revision!, "native-custody-return");
		var receipt = await PostAsync<LibraryCustodyReceiptDto>(librarian, $"{route}/{handoff:D}/returns", receiptCommand);
		(await PostAsync<LibraryCustodyReceiptDto>(librarian, $"{route}/{handoff:D}/returns", receiptCommand)).Id.ShouldBe(receipt.Id);
		receipt.Copies.Single().ReturnedCondition.ShouldBe(BookCopyStatus.Damaged);
		var remaining = await reader.GetFromJsonAsync<LibraryCustodyDetailDto>($"{route}/{handoff:D}", JsonOptions);
		remaining.ShouldNotBeNull();
		remaining.PendingCount.ShouldBe(1);
		remaining.Copies.Single(row => row.ReturnReceiptId == null).CopyId.ShouldBe(second);
		(await database.ScalarAsync("SELECT count(*) FROM \"AppBookIssues\";")).ShouldBe("0");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockVerificationResults\" WHERE \"Finding\"=16;")).ShouldBe("2");
		var mismatchedCopy = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync($"UPDATE \"AppBookCopies\" SET \"Status\"=0 WHERE \"Id\"='{second:D}';"));
		mismatchedCopy.SqlState.ShouldBe("23514");
		var immutable = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync("UPDATE \"AppLibraryCustodyItems\" SET \"Title\"='Changed';"));
		immutable.SqlState.ShouldBe("23514");
		(await Should.ThrowAsync<PostgresException>(() => MigrateStockVerificationAsync(database, previous))).SqlState.ShouldBe("23514");
	}

	private static async Task<Guid> SeedCustodyEmployeeAsync(LibraryPostgresApiTestFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var suffix = Guid.NewGuid().ToString("N");
		var role = new IdentityRole(Guid.NewGuid(), $"Custody test {suffix}", tenantId);
		role.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee);
		role.SetProperty(EmployeeGroupAudience.ExtraPropertyName, EmployeeGroupAudience.Teaching);
		role.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, true);
		await services.GetRequiredService<IIdentityRoleRepository>().InsertAsync(role, true);
		var user = await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(
			new IdentityUser(Guid.NewGuid(), suffix, $"{suffix}@example.test", tenantId), true);
		var employee = await services.GetRequiredService<EmployeeManager>().CreateEmployeeAsync(user.Id, role.Id,
			"Teacher", "Reader", null, "T Reader", EmployeeStatusType.Active, null, email: user.Email,
			panNo: "ABCDE0000F", adharNo: "200000000001", designation: "Teacher", employeeGroup: EmployeeGroupType.Teaching);
		await services.GetRequiredService<IEmployeeRepository>().InsertAsync(employee, true);
		await unit.CompleteAsync();
		return employee.Id;
	}
}
