using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Library;

[Collection(LibraryApiTestCollection.Name)]
public class LibraryCirculationMemberApi_Tests(LibraryApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Fact]
	public async Task Printed_Borrower_Id_Should_Resolve_With_Circulation_View_Without_Member_Management_View()
	{
		var (studentId, printedId) = await SeedStudentAsync();
		using var circulation = LimitedClient(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View);

		var response = await circulation.GetAsync($"/api/library/circulation/members/resolve?identifier={Uri.EscapeDataString(printedId)}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var person = await response.Content.ReadFromJsonAsync<LibraryCirculationMemberDto>(JsonOptions);
		person.ShouldNotBeNull();
		person.Id.ShouldBe(studentId);
		person.MemberIdentifier.ShouldBe(printedId);
		person.IsEligibleForIssue.ShouldBeTrue();
	}

	[Fact]
	public async Task Member_Management_View_Alone_Should_Not_Grant_Circulation_Search_Or_Exact_Resolution()
	{
		using var unrelated = LimitedClient(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View);
		foreach (var route in new[] { "/api/library/circulation/members?search=Library", "/api/library/circulation/members/resolve?identifier=anything" })
		{
			var response = await unrelated.GetAsync(route);
			response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
		}
	}

	private HttpClient LimitedClient(string permission)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
		return client;
	}

	private async Task<(Guid StudentId, string PrintedId)> SeedStudentAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenantId, "Library", null, "API borrower",
			Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId,
			Factory.SeededStandardId, Factory.SeededDivisionId, new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero),
			GenderEnum.Male, "9999999999");
		student.AdditionalDetail!.IsLibraryMember = true;
		await services.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
		var printedId = $"વાચક/01 #{Guid.NewGuid():N}"[..20];
		await services.GetRequiredService<IRepository<LibraryMember, Guid>>().InsertAsync(
			new LibraryMember(Guid.NewGuid(), tenantId, student.Id, printedId, true), true);
		await uow.CompleteAsync();
		return (student.Id, printedId);
	}
}
