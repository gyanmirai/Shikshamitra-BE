using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Charges.Editor;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCanonicalLoanApi_Tests
{
	private const string ChargeEditorRoute = "/api/library/charges/editor";

	[Theory]
	[InlineData("categories")]
	[InlineData("members")]
	[InlineData("member")]
	[InlineData("loans")]
	[InlineData("update")]
	public async Task Charge_Editor_Http_Should_Require_The_Exact_Action_Beside_View(string operation)
	{
		using var reader = LimitedClient(ShikshaMitraSubmodulePermissions.Library.Charges.View);
		using var response = await reader.GetAsync(ChargeEditorHttpPath(operation));
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData("categories", false)]
	[InlineData("members", false)]
	[InlineData("member", false)]
	[InlineData("loans", false)]
	[InlineData("update", true)]
	public async Task Charge_Editor_Http_Should_Require_View_Beside_Its_Owning_Action(string operation, bool update)
	{
		using var writer = LimitedClient(update ? ShikshaMitraSubmodulePermissions.Library.Charges.Update
			: ShikshaMitraSubmodulePermissions.Library.Charges.Add);
		using var response = await writer.GetAsync(ChargeEditorHttpPath(operation));
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Charge_Editor_Http_Should_Load_Create_Lookups_Without_Settings_Member_Or_Circulation_Read_Grants()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var input = await CheckoutRequestAsync();
		var issued = await PostAndReadAsync<LibraryLoanCommandResultDto>(LoansRoute, input);
		var category = await SeedHttpChargeEditorAssessmentAsync(issued.Loan, createAssessment: false);
		using var creator = LimitedClient(string.Join(',', ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Library.Charges.Add));
		var year = Factory.SeededAcademicYearId;
		var page = await ReadHttpChargeEditorAsync<PaginatedResponse<LibraryChargeEditorCategoryDto>>(creator,
			$"{ChargeEditorRoute}/categories?academicYearId={year:D}&search={Uri.EscapeDataString(category.Name)}&page=1&pageSize=1");
		page.TotalCount.ShouldBe(1);
		page.Items.Count.ShouldBe(1);
		page.Items[0].Id.ShouldBe(category.CategoryId);
		page.Items[0].Amount.ShouldBe(100.75m);
		var members = await ReadHttpChargeEditorAsync<PaginatedResponse<LibraryChargeEditorMemberDto>>(creator,
			$"{ChargeEditorRoute}/members?academicYearId={year:D}&search={input.MemberId:D}&page=1&pageSize=1");
		members.TotalCount.ShouldBe(1);
		members.Items.Count.ShouldBe(1);
		members.Items[0].Id.ShouldBe(input.MemberId);
		var member = await ReadHttpChargeEditorAsync<LibraryChargeEditorMemberDto>(creator,
			$"{ChargeEditorRoute}/member?academicYearId={year:D}&identifier={input.MemberId:D}");
		member.Id.ShouldBe(input.MemberId);
		var loans = await ReadHttpChargeEditorAsync<PaginatedResponse<LibraryChargeEditorLoanSourceDto>>(creator,
			$"{ChargeEditorRoute}/loans?academicYearId={year:D}&memberId={input.MemberId:D}&page=1&pageSize=1");
		loans.TotalCount.ShouldBe(1);
		loans.Items.Count.ShouldBe(1);
		loans.Items[0].LoanId.ShouldBe(issued.Loan.Id);
		loans.Items[0].CopyId.ShouldBe(input.CopyId);
		loans.Items[0].CanonicalMemberId.ShouldBe(input.MemberId);
	}

	[Fact]
	public async Task Charge_Editor_Http_Should_Load_An_Existing_Source_With_Update_Without_Add_Or_Unrelated_Lookups()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var input = await CheckoutRequestAsync();
		var issued = await PostAndReadAsync<LibraryLoanCommandResultDto>(LoansRoute, input);
		var fixture = await SeedHttpChargeEditorAssessmentAsync(issued.Loan, createAssessment: true);
		var assessmentId = fixture.AssessmentId ?? throw new InvalidOperationException("The editor fixture requires an actual assessment.");
		using var editor = LimitedClient(string.Join(',', ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Library.Charges.Update));

		var context = await ReadHttpChargeEditorAsync<LibraryChargeEditorContextDto>(editor,
			$"/api/library/charges/{assessmentId:D}/editor");

		context.Assessment.Id.ShouldBe(assessmentId);
		context.Assessment.MemberId.ShouldBe(input.MemberId);
		context.Assessment.BookIssueId.ShouldBe(issued.Loan.Id);
		context.Assessment.AcademicYearId.ShouldBe(Factory.SeededAcademicYearId);
		context.Assessment.SourceVersion.ShouldBe(fixture.Version);
		context.CanAmend.ShouldBeTrue();
		context.RequiresContextReview.ShouldBeFalse();
	}

	private static string ChargeEditorHttpPath(string operation)
	{
		var id = Guid.NewGuid();
		return operation switch
		{
			"categories" or "members" => $"{ChargeEditorRoute}/{operation}?academicYearId={id:D}",
			"member" => $"{ChargeEditorRoute}/member?academicYearId={id:D}&identifier={id:D}",
			"loans" => $"{ChargeEditorRoute}/loans?academicYearId={id:D}&memberId={id:D}",
			"update" => $"/api/library/charges/{id:D}/editor",
			_ => throw new ArgumentOutOfRangeException(nameof(operation))
		};
	}

	private static async Task<T> ReadHttpChargeEditorAsync<T>(System.Net.Http.HttpClient client, string route)
	{
		using var response = await client.GetAsync(route);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<T>(JsonOptions)
			?? throw new InvalidOperationException("The charge editor returned an empty response.");
	}

	private async Task<(Guid CategoryId, string Name, Guid? AssessmentId, string? Version)> SeedHttpChargeEditorAssessmentAsync(
		LibraryLoanDto loan, bool createAssessment)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var name = $"Editor {Guid.NewGuid():N}"[..25];
		var category = new LibraryChargeCategory(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
			name, 100.75m, false, "Exact charge editor lookup fixture.");
		await services.GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(category, true);
		LibraryCharge? charge = null;
		if (createAssessment)
		{
			var evidence = JsonSerializer.Serialize(new { SourceLoanId = loan.Id,
				AcademicYearId = Factory.SeededAcademicYearId, ActorId = TestAuthHandler.TestUserId,
				Reason = "Reviewed the actual loan and explicit assessment context." });
			charge = LibraryCharge.CreateConfirmed(Guid.NewGuid(), loan.MemberId, loan.Role, category.Id, loan.Id,
				100.75m, new(2026, 11, 17), new(2026, 11, 30), Factory.SeededAcademicYearId,
				LibraryAcademicYearProvenance.ExplicitAssessmentContext, evidence, evidence,
				"Exact recorded source for the existing assessment editor.", tenantId);
			await services.GetRequiredService<IRepository<LibraryCharge, Guid>>().InsertAsync(charge, true);
		}
		await unit.CompleteAsync();
		return (category.Id, name, charge?.Id, charge?.GetAssessmentVersion());
	}
}
