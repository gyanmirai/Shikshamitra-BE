using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCanonicalLoanApi_Tests
{
	[Fact]
	public async Task Rollover_Http_Should_Require_Policy_View_And_Protect_Counts_And_Year_Identity()
	{
		const string route = "/api/library/policies/rollover";
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var targetId = Factory.SeededAcademicYearId;
		Guid sourceId;
		DateOnly day;
		using (var scope = Factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			var tenant = Guid.Parse(TestAuthHandler.TestTenantId);
			using var change = services.GetRequiredService<ICurrentTenant>().Change(tenant);
			using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var repository = services.GetRequiredService<IRepository<AcademicYear, Guid>>();
			var target = await repository.GetAsync(targetId);
			day = DateOnly.FromDateTime(target.StartDate);
			var source = await services.GetRequiredService<AcademicYearManager>().CreateAsync($"Prior {Guid.NewGuid():N}"[..30],
				target.StartDate.AddYears(-1), target.StartDate.AddDays(-1));
			sourceId = (await repository.InsertAsync(source, true)).Id;
			await unit.CompleteAsync();
		}
		var request = new LibraryRolloverInput(sourceId, targetId, null);
		using var circulationOnly = LimitedClient(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View);
		using var denied = await circulationOnly.GetAsync(route + "/options");
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		using var policyOnly = LimitedClient(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.View);
		using var response = await policyOnly.PostAsJsonAsync(route + "/preview", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var preview = await response.Content.ReadFromJsonAsync<LibraryRolloverPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.SchoolDate.ShouldBe(day);
		preview.Loans.Scope.ShouldBe("Restricted");
		preview.Loans.SourceYearOpenCount.ShouldBeNull();
		using var invalid = await policyOnly.PostAsJsonAsync(route + "/preview", request with { TargetAcademicYearId = Guid.NewGuid() }, JsonOptions);
		invalid.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
}
