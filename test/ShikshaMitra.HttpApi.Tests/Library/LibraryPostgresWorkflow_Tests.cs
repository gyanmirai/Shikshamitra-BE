using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Library;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LibraryPostgresWorkflowCollection
{
	public const string Name = "Library PostgreSQL API workflows";
}

[Collection(LibraryPostgresWorkflowCollection.Name)]
public sealed partial class LibraryPostgresWorkflow_Tests
{
	private const string LoansRoute = "/api/library/circulation/loans";
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Http_Checkout_Return_And_Replay_Should_Commit_One_Exact_Civil_Loan_And_Its_Evidence()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.CataloguePrice);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		using var client = OwningClient(factory);
		(await database.ScalarAsync("SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\""))
			.ShouldBe(LibraryMigrationPostgresFixture.CataloguePrice);
		using (var scope = factory.Services.CreateScope())
		{
			scope.ServiceProvider.GetRequiredService<IClock>().Now.Kind.ShouldBe(DateTimeKind.Utc);
			scope.ServiceProvider.GetRequiredService<ILibraryInventoryLock>().ShouldBeAssignableTo<EfCoreLibraryInventoryLock>();
			var receiptRepository = scope.ServiceProvider.GetRequiredService<IReceiptNumberSequenceRepository>();
			var receiptTarget = receiptRepository is IProxyTargetAccessor proxy ? proxy.DynProxyGetTarget() : receiptRepository;
			receiptTarget.ShouldBeAssignableTo<EfCoreReceiptNumberSequenceRepository>();
		}
		var (copyId, memberId) = await SeedLoanContextAsync(factory);
		factory.Transactions.BeginObserving();
		var checkout = new LibraryCheckoutRequest
		{
			CopyId = copyId, MemberId = memberId, IssueDate = new(2026, 11, 17), RequestedDueDate = new(2026, 11, 30),
			IdempotencyKey = Guid.NewGuid().ToString("N")
		};
		var plan = await PostAsync<LibraryIssuePlanDto>(client, "/api/library/circulation/issues/preview", new LibraryIssuePlanRequest
		{
			CopyId = copyId, MemberId = memberId, IssueDate = checkout.IssueDate, RequestedDueDate = checkout.RequestedDueDate
		});
		plan.CanIssue.ShouldBeTrue(string.Join(" ", plan.BlockedReasons));
		checkout.Revision = plan.Revision;
		using (var viewOnly = OwningClient(factory, ShikshaMitraSubmodulePermissions.Library.IssueReturn.View))
		{
			using var denied = await viewOnly.PostAsJsonAsync(LoansRoute, checkout, JsonOptions);
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await denied.Content.ReadAsStringAsync());
		}
		var issued = await PostAsync<LibraryLoanCommandResultDto>(client, LoansRoute, checkout);
		issued.WasReplay.ShouldBeFalse();
		issued.Loan.Id.ShouldNotBe(Guid.Empty);
		issued.Loan.CopyId.ShouldBe(copyId);
		issued.Loan.MemberId.ShouldBe(memberId);
		issued.Loan.AcademicYearId.ShouldBe(factory.YearId);
		issued.Loan.SchoolIssueDate.ShouldBe(checkout.IssueDate);
		issued.Loan.SchoolDueDate.ShouldBe(checkout.RequestedDueDate);
		issued.Loan.OriginalSchoolDueDate.ShouldBe(checkout.RequestedDueDate);
		issued.Loan.RawIssueDate.ShouldBeNull();
		issued.Loan.RawDueDate.ShouldBeNull();
		var replay = await PostAsync<LibraryLoanCommandResultDto>(client, LoansRoute, checkout);
		replay.WasReplay.ShouldBeTrue();
		replay.Loan.Id.ShouldBe(issued.Loan.Id);
		replay.Loan.Revision.ShouldBe(issued.Loan.Revision);
		var current = await client.GetFromJsonAsync<LibraryLoanDto>($"{LoansRoute}/{issued.Loan.Id}", JsonOptions);
		current.ShouldNotBeNull();
		var receipt = new LibraryLoanReturnRequest
		{
			CopyId = copyId, Revision = current.Revision, SchoolReturnDate = new(2026, 11, 18),
			IdempotencyKey = Guid.NewGuid().ToString("N"), CopyStatus = (int)BookCopyStatus.Available
		};
		var returned = await PostAsync<LibraryLoanCommandResultDto>(client, $"{LoansRoute}/{current.Id}/return", receipt);
		returned.WasReplay.ShouldBeFalse();
		returned.Loan.Id.ShouldBe(issued.Loan.Id);
		returned.Loan.CopyId.ShouldBe(copyId);
		returned.Loan.SchoolReturnDate.ShouldBe(receipt.SchoolReturnDate);
		returned.Loan.RawReturnDate.ShouldBeNull();
		returned.Loan.CanReturn.ShouldBeFalse();
		var returnReplay = await PostAsync<LibraryLoanCommandResultDto>(client, $"{LoansRoute}/{current.Id}/return", receipt);
		returnReplay.WasReplay.ShouldBeTrue();
		returnReplay.Loan.Id.ShouldBe(current.Id);
		returnReplay.Loan.Revision.ShouldBe(returned.Loan.Revision);
		await AssertPersistedEvidenceAsync(factory, issued.Loan.Id, copyId, memberId, checkout, receipt);
		var observed = factory.Transactions.Observations;
		observed.Count(row => row.Kind == "InventoryLock").ShouldBeGreaterThanOrEqualTo(4);
		foreach (var kind in new[] { "LoanInsert", "LoanUpdate", "CopyEventInsert", "LoanEventInsert", "CommandInsert" })
			observed.ShouldContain(row => row.Kind == kind);
		observed.ShouldAllBe(row => row.HasTransaction && row.Isolation.HasValue && row.BackendId > 0);
	}

	private static HttpClient OwningClient(LibraryPostgresApiTestFactory factory, string? permissions = null)
	{
		var client = factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Librarian");
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permissions ?? string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.IssueReturn.View, ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue,
			ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return));
		return client;
	}

	private static async Task<T> PostAsync<T>(HttpClient client, string path, object input)
	{
		using var response = await client.PostAsJsonAsync(path, input, JsonOptions);
		response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<T>(JsonOptions)
			?? throw new InvalidOperationException($"The response from {path} was empty.");
	}

	private static async Task<(Guid CopyId, Guid MemberId)> SeedLoanContextAsync(LibraryPostgresApiTestFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var category = await services.GetRequiredService<IRepository<BookCategory, Guid>>().InsertAsync(
			new BookCategory("PostgreSQL workflow", "Actual HTTP transaction proof", factory.YearId, tenantId), true);
		var section = await services.GetRequiredService<IRepository<LibrarySection, Guid>>().InsertAsync(
			new LibrarySection(Guid.NewGuid(), tenantId, factory.YearId, "Main library", 1), true);
		var shelf = await services.GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(new LibraryShelf(Guid.NewGuid(), section.Id, "A1"), true);
		var book = await services.GetRequiredService<IRepository<Book, Guid>>().InsertAsync(
			new Book("PostgreSQL workflow title", "", "Library author", "", "", "", category.Id, Language.English, "", 0m, 0, 1,
				factory.YearId, tenantId), true);
		var copy = await services.GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(
			new BookCopy(Guid.NewGuid(), book.Id, 1, $"PG-{Guid.NewGuid():N}", section.Id, shelf.Id, tenantId), true);
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenantId, "PostgreSQL", null, "Borrower",
			factory.YearId, factory.BoardId, factory.MediumId, factory.StageId, factory.StandardId, null,
			new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero), GenderEnum.Male, "9999999999");
		student.AdditionalDetail!.IsLibraryMember = true;
		await services.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
		await services.GetRequiredService<IRepository<LibraryMember, Guid>>().InsertAsync(
			new LibraryMember(Guid.NewGuid(), tenantId, student.Id, $"PG-BOR-{Guid.NewGuid():N}", true), true);
		await unit.CompleteAsync();
		return (copy.Id, student.Id);
	}

	private static async Task AssertPersistedEvidenceAsync(LibraryPostgresApiTestFactory factory, Guid loanId, Guid copyId, Guid memberId,
		LibraryCheckoutRequest checkout, LibraryLoanReturnRequest receipt)
	{
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var actorId = Guid.Parse(TestAuthHandler.TestUserId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		db.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
		var loan = await db.Set<BookIssue>().AsNoTracking().SingleAsync(row => row.Id == loanId);
		loan.TenantId.ShouldBe(tenantId);
		loan.BookCopyId.ShouldBe(copyId);
		loan.MemberId.ShouldBe(memberId);
		loan.AcademicYearId.ShouldBe(factory.YearId);
		loan.Status.ShouldBe(BookIssueStatus.Returned);
		loan.SchoolIssueDate.ShouldBe(checkout.IssueDate);
		loan.SchoolDueDate.ShouldBe(checkout.RequestedDueDate);
		loan.SchoolReturnDate.ShouldBe(receipt.SchoolReturnDate);
		loan.IssueDate.ShouldBeNull();
		loan.DueDate.ShouldBeNull();
		loan.ReturnDate.ShouldBeNull();
		loan.FineDueDate.ShouldBeNull();
		loan.IssueDateProvenance.ShouldBe(LibraryCivilDateProvenance.ExplicitSchoolDate);
		loan.DueDateProvenance.ShouldBe(LibraryCivilDateProvenance.ExplicitSchoolDate);
		loan.ReturnDateProvenance.ShouldBe(LibraryCivilDateProvenance.ExplicitSchoolDate);
		(await db.Set<BookIssue>().CountAsync(row => row.BookCopyId == copyId)).ShouldBe(1);
		(await db.Set<BookCopy>().AsNoTracking().SingleAsync(row => row.Id == copyId)).Status.ShouldBe(BookCopyStatus.Available);
		var copyEvents = await db.Set<LibraryCopyEvent>().AsNoTracking().Where(row => row.CopyId == copyId).OrderBy(row => row.Sequence).ToListAsync();
		copyEvents.Select(row => row.EventType).ShouldBe(new[] { LibraryCopyEventType.Issued, LibraryCopyEventType.Returned });
		copyEvents.Select(row => row.Sequence).ShouldBe(new long[] { 1, 2 });
		copyEvents.ShouldAllBe(row => row.TenantId == tenantId && row.RelatedLoanId == loanId && row.ActorId == actorId && row.ActorKind == LibraryActorKind.User);
		copyEvents[0].PreviousStatus.ShouldBe(BookCopyStatus.Available);
		copyEvents[0].NextStatus.ShouldBe(BookCopyStatus.Issued);
		copyEvents[1].PreviousStatus.ShouldBe(BookCopyStatus.Issued);
		copyEvents[1].NextStatus.ShouldBe(BookCopyStatus.Available);
		var loanEvents = await db.Set<LibraryLoanEvent>().AsNoTracking().Where(row => row.LoanId == loanId).OrderBy(row => row.Sequence).ToListAsync();
		loanEvents.Select(row => row.EventKind).ShouldBe(new[] { LibraryLoanEventKind.Issued, LibraryLoanEventKind.Returned });
		loanEvents.ShouldAllBe(row => row.TenantId == tenantId && row.ActorId == actorId);
		var commands = await db.Set<LibraryLoanCommand>().AsNoTracking().Where(row => row.LoanId == loanId).ToListAsync();
		commands.Count.ShouldBe(2);
		commands.ShouldAllBe(row => row.TenantId == tenantId && row.ActorId == actorId);
		commands.Single(row => row.Operation == LibraryLoanOperation.Checkout).IdempotencyKey.ShouldBe(checkout.IdempotencyKey);
		commands.Single(row => row.Operation == LibraryLoanOperation.Return).IdempotencyKey.ShouldBe(receipt.IdempotencyKey);
		await unit.CompleteAsync();
	}
}
