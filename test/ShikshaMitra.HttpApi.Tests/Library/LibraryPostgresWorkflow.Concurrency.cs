using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresTheory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Contending_Postgres_Checkouts_Should_Recheck_Copy_And_Borrower_Quota_After_The_Real_Lock(bool quotaAcrossCopies)
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.CataloguePrice);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		using var client = OwningClient(factory);
		var first = await SeedLoanContextAsync(factory);
		var second = await SeedCompetingContextAsync(factory, first.CopyId, first.MemberId, quotaAcrossCopies);
		LibraryCheckoutRequest[] inputs = [new() { CopyId = first.CopyId, MemberId = first.MemberId },
			new() { CopyId = second.CopyId, MemberId = second.MemberId }];
		foreach (var input in inputs)
		{
			input.IssueDate = new(2026, 11, 17);
			input.RequestedDueDate = new(2026, 11, 30);
			input.IdempotencyKey = Guid.NewGuid().ToString("N");
			var plan = await PostAsync<LibraryIssuePlanDto>(client, "/api/library/circulation/issues/preview", new LibraryIssuePlanRequest
			{
				CopyId = input.CopyId, MemberId = input.MemberId, IssueDate = input.IssueDate, RequestedDueDate = input.RequestedDueDate
			});
			plan.CanIssue.ShouldBeTrue(string.Join(" ", plan.BlockedReasons));
			plan.ActiveLoanCount.ShouldBe(0);
			if (quotaAcrossCopies) plan.Policy.Terms.MaxBooksAtTime.ShouldBe(1);
			input.Revision = plan.Revision;
		}

		await using var gate = database.CreateContext();
		await using var transaction = await gate.Database.BeginTransactionAsync();
		var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
		provider.GetDbContextAsync().Returns(Task.FromResult(gate));
		await new EfCoreLibraryInventoryLock(provider).AcquireAsync(Guid.Parse(TestAuthHandler.TestTenantId));
		var requests = inputs.Select(input => client.PostAsJsonAsync(LoansRoute, input, JsonOptions)).ToArray();
		var bothBlocked = false;
		try { bothBlocked = await ObserveTwoAdvisoryWaitersAsync(database); }
		finally { await transaction.RollbackAsync(); }
		var responses = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(30));
		try
		{
			bothBlocked.ShouldBeTrue("Both HTTP requests must actually be waiting in PostgreSQL before the transaction fence is released.");
			responses.Count(response => response.IsSuccessStatusCode).ShouldBe(1,
				string.Join("\n", await Task.WhenAll(responses.Select(response => response.Content.ReadAsStringAsync()))));
			var rejected = responses.Single(response => !response.IsSuccessStatusCode);
			rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await rejected.Content.ReadAsStringAsync());
			using var error = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
			error.RootElement.GetProperty("errorCode").GetString().ShouldBe(LibraryCirculationErrorCodes.PlanChanged);
			var accepted = await responses.Single(response => response.IsSuccessStatusCode).Content.ReadFromJsonAsync<LibraryLoanCommandResultDto>(JsonOptions);
			accepted.ShouldNotBeNull();
			inputs.ShouldContain(input => input.CopyId == accepted.Loan.CopyId && input.MemberId == accepted.Loan.MemberId);
			(await database.ScalarAsync("SELECT count(*) FROM \"AppBookIssues\";")).ShouldBe("1");
			(await database.ScalarAsync("SELECT count(*) FROM \"AppBookCopies\" WHERE \"Status\"=1;")).ShouldBe("1");
			(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryCopyEvents\";")).ShouldBe("1");
			(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryLoanEvents\";")).ShouldBe("1");
			(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryLoanCommands\";")).ShouldBe("1");
		}
		finally { foreach (var response in responses) response.Dispose(); }
	}
}
