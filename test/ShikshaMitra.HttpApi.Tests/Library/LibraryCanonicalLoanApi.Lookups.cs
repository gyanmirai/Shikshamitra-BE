using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCanonicalLoanApi_Tests
{
	[Fact]
	public async Task Canonical_Copy_Lookup_Should_Preserve_Encoded_Accession_And_Stable_Token_Identity_With_Only_Issue_Grants()
	{
		var (copyId, _) = await SeedLoanContextAsync();
		var accession = $"પુસ્તક/26 #A+B-{Guid.NewGuid().ToString("N")[..8]}";
		await SetLookupAccessionAsync(copyId, accession);
		using var client = LimitedClient($"{ShikshaMitraSubmodulePermissions.Library.IssueReturn.View},{ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue}");

		var response = await client.GetAsync($"/api/library/circulation/lookups/copy?scan={Uri.EscapeDataString(accession)}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var found = await response.Content.ReadFromJsonAsync<LibraryCopyLookupDto>(JsonOptions);
		found.ShouldNotBeNull();
		found.CopyId.ShouldBe(copyId);
		found.AccessionNo.ShouldBe(accession);
		response = await client.GetAsync($"/api/library/circulation/lookups/copy?scan={Uri.EscapeDataString($"SM-LIB-COPY:{copyId:N}")}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadFromJsonAsync<LibraryCopyLookupDto>(JsonOptions))!.CopyId.ShouldBe(copyId);
	}

	[Fact]
	public async Task Canonical_Return_Lookup_Should_Expose_Exact_Loan_Id_And_Current_Revision_With_Only_Return_Grants()
	{
		var input = await CheckoutRequestAsync();
		var issued = await PostAndReadAsync<LibraryLoanCommandResultDto>(LoansRoute, input);
		var current = await GetAndReadAsync<LibraryLoanDto>($"{LoansRoute}/{issued.Loan.Id}");
		var accession = await ReadLookupAccessionAsync(input.CopyId);
		using var client = LimitedClient($"{ShikshaMitraSubmodulePermissions.Library.IssueReturn.View},{ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return}");

		var response = await client.GetAsync($"/api/library/circulation/lookups/return?scan={Uri.EscapeDataString(accession)}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var found = await response.Content.ReadFromJsonAsync<LibraryReturnLookupDto>(JsonOptions);
		found.ShouldNotBeNull();
		found.Copy.CopyId.ShouldBe(input.CopyId);
		found.Loan.Id.ShouldBe(issued.Loan.Id);
		found.Loan.Revision.ShouldBe(current.Revision);
		found.Loan.CanReturn.ShouldBeTrue();
		found.Borrower.DisplaySource.ShouldBe("LoanSnapshot");
	}

	[Theory]
	[InlineData("copy", ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
	[InlineData("copy", ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue)]
	[InlineData("return", ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
	[InlineData("return", ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return)]
	public async Task Canonical_Lookup_Http_Should_Require_Both_View_And_Its_Exact_Action(string lookup, string onlyGrant)
	{
		using var client = LimitedClient(onlyGrant);
		var response = await client.GetAsync($"/api/library/circulation/lookups/{lookup}?scan=unknown");
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	private async Task SetLookupAccessionAsync(Guid copyId, string accession)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var repository = services.GetRequiredService<IRepository<BookCopy, Guid>>();
		var copy = await repository.GetAsync(copyId);
		copy.AccessionNumber = accession;
		await repository.UpdateAsync(copy, true);
		await unit.CompleteAsync();
	}

	private async Task<string> ReadLookupAccessionAsync(Guid copyId)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
		var accession = (await services.GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(copyId)).AccessionNumber;
		await unit.CompleteAsync();
		return accession;
	}
}
