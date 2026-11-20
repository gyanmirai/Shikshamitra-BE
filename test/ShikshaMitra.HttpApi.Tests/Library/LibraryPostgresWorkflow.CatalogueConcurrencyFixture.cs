using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	private static async Task<Guid> SeedCatalogueOperatorAsync(LibraryPostgresApiTestFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId); var id = Guid.NewGuid();
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(id, $"catalogue-{id:N}", $"{id:N}@test.invalid", tenantId), true);
		await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new(Guid.NewGuid(), tenantId,
			userId: id, description: "Independent Library catalogue operator"), true);
		await unit.CompleteAsync();
		return id;
	}

	private static async Task<(LibraryBookUpdatedResponse Result, bool RightWon)> ContendCatalogueAsync(
		LibraryMigrationPostgresFixture database, HttpClient left, HttpClient right, string route, object first, object second)
	{
		await using var gate = database.CreateContext();
		await using var transaction = await gate.Database.BeginTransactionAsync();
		var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
		provider.GetDbContextAsync().Returns(Task.FromResult(gate));
		await new EfCoreLibraryInventoryLock(provider).AcquireAsync(Guid.Parse(TestAuthHandler.TestTenantId));
		var requests = new[] { left.PutAsJsonAsync(route, first, JsonOptions), right.PutAsJsonAsync(route, second, JsonOptions) };
		bool blocked;
		try { blocked = await ObserveTwoAdvisoryWaitersAsync(database); }
		finally { await transaction.RollbackAsync(); }
		var responses = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(30));
		try
		{
			blocked.ShouldBeTrue("Both operators must be waiting at the actual PostgreSQL transaction fence.");
			responses.Count(response => response.IsSuccessStatusCode).ShouldBe(1,
				string.Join("\n", await Task.WhenAll(responses.Select(response => response.Content.ReadAsStringAsync()))));
			var rejected = responses.Single(response => !response.IsSuccessStatusCode);
			rejected.StatusCode.ShouldBe(HttpStatusCode.Conflict, await rejected.Content.ReadAsStringAsync());
			using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
			problem.RootElement.GetProperty("errorCode").GetString().ShouldBe(LibraryErrorCodes.CatalogueRevisionChanged);
			var result = await responses.Single(response => response.IsSuccessStatusCode).Content.ReadFromJsonAsync<LibraryBookUpdatedResponse>(JsonOptions);
			result.ShouldNotBeNull(); result.MetadataRevision.ShouldNotBeNullOrWhiteSpace(); result.CopiesRevision.ShouldNotBeNullOrWhiteSpace();
			return (result, responses[1].IsSuccessStatusCode);
		}
		finally { foreach (var response in responses) response.Dispose(); }
	}
}
