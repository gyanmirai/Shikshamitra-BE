using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Portal;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students.Portal;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Portal;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task My_Library_Should_Use_Exact_Portal_Grants_And_Persisted_Guardian_Ownership()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync("PersistLibraryReservationPickup");
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var (_, student) = await SeedLoanContextAsync(factory);
		var (guardian, link) = await SeedLibraryGuardianAsync(factory, student);
		var route = $"/api/mobile/v1/students/{student:D}/library";
		using var viewer = OwningClient(factory, Actions.View);
		viewer.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, guardian.ToString());
		using var reserve = OwningClient(factory, string.Join(',', Actions.View, Actions.Reserve));
		reserve.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, guardian.ToString());
		using var unlinked = OwningClient(factory, string.Join(',', Actions.View, Actions.Reserve, Actions.Renew));
		var studentRows = await viewer.GetFromJsonAsync<LibraryPortalStudent[]>("/api/mobile/v1/library/students", JsonOptions);
		studentRows.ShouldNotBeNull();
		studentRows.Single().StudentId.ShouldBe(student);
		var catalogue = await viewer.GetFromJsonAsync<PaginatedResponse<LibraryPortalBook>>(route + "/catalogue", JsonOptions);
		catalogue.ShouldNotBeNull();
		var book = catalogue.Items.Single();
		book.AvailableCopies.ShouldBe(1);
		using var otherChild = await viewer.GetAsync($"/api/mobile/v1/students/{Guid.NewGuid():D}/library/loans");
		otherChild.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var otherGuardian = await unlinked.GetAsync(route + "/loans");
		otherGuardian.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var missingReceiptGrant = await viewer.GetAsync(route + "/receipt-years");
		missingReceiptGrant.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var staffRoute = await viewer.GetAsync($"/api/library/reservations?bookId={book.Id:D}&academicYearId={factory.YearId:D}");
		staffRoute.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var input = new LibraryPortalReserveInput(book.Id, null);
		using var missingAction = await viewer.PostAsJsonAsync(route + "/reservations/preview", input, JsonOptions);
		missingAction.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var preview = await PostAsync<LibraryPortalReservePreview>(reserve, route + "/reservations/preview", input);
		preview.CanRequest.ShouldBeFalse();
		preview.Reasons.ShouldContain(reason => reason.Contains("manually", StringComparison.OrdinalIgnoreCase));
		(await database.ScalarAsync("""SELECT count(*) FROM "AppBookReservations";""")).ShouldBe("0");
		await database.ExecuteAsync($"""UPDATE "AppStudentPortalAccountLinks" SET "IsDeleted"=true WHERE "Id"='{link:D}';""");
		using var revoked = await reserve.GetAsync(route + "/catalogue");
		revoked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	private static async Task<(Guid Guardian, Guid Link)> SeedLibraryGuardianAsync(LibraryPostgresApiTestFactory factory, Guid student)
	{
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenant = Guid.Parse(TestAuthHandler.TestTenantId);
		using var currentTenant = services.GetRequiredService<ICurrentTenant>().Change(tenant);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var guardian = new IdentityUser(Guid.NewGuid(), "library-guardian", "library-guardian@example.test", tenant);
		guardian.SetEmailConfirmed(true);
		await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(guardian, true);
		var link = new StudentPortalAccountLink(Guid.NewGuid(), tenant, student, guardian.Id, "guardian", "mother", guardian.Id);
		await services.GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(link);
		await unit.CompleteAsync();
		return (guardian.Id, link.Id);
	}
}
