using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Reports;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class InstituteReportApi_Tests
{
	[Fact]
	public async Task Document_reports_count_active_tenant_entries_and_files_without_loading_blobs()
	{
		var (client, tenant, _) = await ClientAsync(); using var http = client;
		await SeedReportDocumentsAsync(tenant);
		var (_, foreignTenant, _) = await ClientAsync(); await SeedReportDocumentsAsync(foreignTenant);
		var counts = await ModulePreviewAsync(client, new() { ReportType = "school-document-counts" });
		counts.Rows.First()["documentCount"].ShouldBe("2"); counts.Rows.First()["attachmentCount"].ShouldBe("3");
		counts.Rows.Single(x => x["category"] == "Other")["documentCount"].ShouldBe("0");
		var filtered = await ModulePreviewAsync(client, new() { ReportType = "school-document-counts", Filters = new() { DocumentCategory = "GovernmentApproval" } });
		filtered.Rows.First()["documentCount"].ShouldBe("1"); filtered.Rows.First()["attachmentCount"].ShouldBe("2");
		var register = await ModulePreviewAsync(client, new() { ReportType = "school-document-register" });
		register.TotalCount.ShouldBe(2); register.Rows.ShouldNotContain(x => x["name"] == "Deleted");
		register.Rows.ShouldAllBe(x => !x.ContainsKey("blobName") && !x.ContainsKey("content"));
		factory.AiResult = new() { Plan = new() { ReportType = "school-document-counts", Format = "csv" } };
		var planned = await client.PostAsJsonAsync(Url + "/ai/plan", new InstituteAiPlanInput { Prompt = "How many school documents are available by category?" });
		planned.StatusCode.ShouldBe(HttpStatusCode.OK, await planned.Content.ReadAsStringAsync());
		var plan = (await planned.Content.ReadFromJsonAsync<InstituteAiPlanResult>())!;
		var queued = await client.PostAsJsonAsync(Url + "/ai/exports", new InstituteAiApprovedRequest { Plan = plan.Plan!, ApprovalToken = plan.ApprovalToken! });
		var job = (await queued.Content.ReadFromJsonAsync<InstituteReportJobDto>())!;
		using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<InstituteReportExportJob>().ExecuteAsync(new() { JobId = job.Id, TenantId = tenant });
		var csv = await client.GetStringAsync($"{Url}/exports/{job.Id}/download"); csv.ShouldContain("All categories,2,3,30");
		Grant(client, InstituteReportPermissions.View, InstituteReportPermissions.Add);
		(await client.GetAsync($"{Url}/exports/{job.Id}/download")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact]
	public async Task New_report_catalogue_and_endpoints_require_exact_source_grants_and_separate_signature_access()
	{
		var (client, _, _) = await ClientAsync(); using var http = client;
		Grant(client, InstituteReportPermissions.View, InstituteReportPermissions.Add, SchoolDocumentPermissions.View);
		var options = (await client.GetFromJsonAsync<InstituteReportOptions>(Url + "/options"))!;
		options.ReportTypes.Select(x => x.Id).ShouldBe(["school-document-register", "school-document-counts"]);
		options.ReportTypes.ShouldAllBe(x => x.RequiredPermissions.SequenceEqual(new[] { SchoolDocumentPermissions.View }));
		(await client.PostAsJsonAsync(Url + "/preview", new InstituteReportRequest { ReportType = "school-identity-register" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		Grant(client, InstituteReportPermissions.View, InstituteReportPermissions.Add, SchoolIdentityPermissions.View);
		options = (await client.GetFromJsonAsync<InstituteReportOptions>(Url + "/options"))!;
		options.ReportTypes.ShouldContain(x => x.Id == "school-identity-register"); options.ReportTypes.ShouldNotContain(x => x.Id == "school-identity-signatures");
		(await client.PostAsJsonAsync(Url + "/preview", new InstituteReportRequest { ReportType = "school-identity-signatures" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.PostAsJsonAsync(Url + "/preview", new InstituteReportRequest { ReportType = "school-document-counts" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		factory.AiResult = new() { Plan = new() { ReportType = "school-identity-signatures" } };
		(await client.PostAsJsonAsync(Url + "/ai/plan", new InstituteAiPlanInput { Prompt = "Show the principal signature configuration" })).IsSuccessStatusCode.ShouldBeFalse();
		Grant(client, InstituteReportPermissions.View, SchoolDocumentPermissions.View);
		(await client.PostAsJsonAsync(Url + "/exports", new InstituteReportRequest { ReportType = "school-document-counts" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact]
	public async Task Identity_reports_reuse_canonical_scope_and_guard_signatures_and_saved_downloads()
	{
		var (client, tenant, user) = await ClientAsync(); using var http = client;
		var (year, board, medium, organization) = await SeedReportIdentityAsync(tenant);
		await SeedReportIdentityAsync(tenant);
		factory.Scopes[user] = new() { UserId = user, TenantId = tenant, AcademicYearIds = [year], BoardIds = [board], MediumIds = [medium], MediumStageIds = [organization] };
		Grant(client, InstituteReportPermissions.View, InstituteReportPermissions.Add, SchoolIdentityPermissions.View);
		var register = await ModulePreviewAsync(client, new() { ReportType = "school-identity-register" });
		register.Rows.Count.ShouldBe(4); register.Rows.ShouldAllBe(x => !x.ContainsKey("principalName") && !x.ContainsKey("signatureMode"));
		var counts = await ModulePreviewAsync(client, new() { ReportType = "school-identity-counts" });
		counts.Rows.First()["profileCount"].ShouldBe("4");
		var onlySchool = await ModulePreviewAsync(client, new() { ReportType = "school-identity-counts", Filters = new() { IdentityScope = "school" } });
		onlySchool.Rows.First()["profileCount"].ShouldBe("1");
		Grant(client, InstituteReportPermissions.View, InstituteReportPermissions.Add, SchoolIdentityPermissions.View, SchoolIdentityPermissions.AccessSignature);
		var signature = await ModulePreviewAsync(client, new() { ReportType = "school-identity-signatures" });
		signature.Rows.ShouldContain(x => x["principalName"] == "Synthetic principal");
		var queued = await client.PostAsJsonAsync(Url + "/exports", new InstituteReportRequest { ReportType = "school-identity-signatures", Format = "csv" });
		var job = (await queued.Content.ReadFromJsonAsync<InstituteReportJobDto>())!;
		using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<InstituteReportExportJob>().ExecuteAsync(new() { JobId = job.Id, TenantId = tenant });
		(await client.GetAsync($"{Url}/exports/{job.Id}/download")).StatusCode.ShouldBe(HttpStatusCode.OK);
		Grant(client, InstituteReportPermissions.View, InstituteReportPermissions.Add, SchoolIdentityPermissions.View);
		(await client.GetAsync($"{Url}/exports/{job.Id}/download")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		Grant(client, InstituteReportPermissions.View, InstituteReportPermissions.Add, SchoolIdentityPermissions.View, SchoolIdentityPermissions.AccessSignature);
		factory.Scopes[user] = new() { UserId = user, TenantId = tenant };
		(await client.GetAsync($"{Url}/exports/{job.Id}/download")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await ModulePreviewAsync(client, new() { ReportType = "school-identity-register" })).Rows.Single()["scope"].ShouldBe("School");
	}
	private static void Grant(HttpClient client, params string[] names) { client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader); client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(',', names)); }
	private static async Task<AcademicReportPreviewDto> ModulePreviewAsync(HttpClient client, InstituteReportRequest request)
	{
		var response = await client.PostAsJsonAsync(Url + "/preview", request);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AcademicReportPreviewDto>())!;
	}
	private async Task SeedReportDocumentsAsync(Guid tenant)
	{
		using var scope = factory.Services.CreateScope(); using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var documents = scope.ServiceProvider.GetRequiredService<IRepository<SchoolDocument, Guid>>(); var files = scope.ServiceProvider.GetRequiredService<IRepository<SchoolDocumentFile, Guid>>();
		foreach (var (name, category, count) in new[] { ("Approval", SchoolDocumentCategory.GovernmentApproval, 2), ("NOC", SchoolDocumentCategory.PermissionNoc, 1), ("Deleted", SchoolDocumentCategory.Other, 1) }) {
			var document = new SchoolDocument(Guid.NewGuid(), tenant, name, category, null, "Test uploader"); await documents.InsertAsync(document, autoSave: true);
			for (var i = 0; i < count; i++) await files.InsertAsync(new(Guid.NewGuid(), tenant, document.Id, $"file-{i}.pdf", "application/pdf", 10));
			if (name == "Deleted") await documents.DeleteAsync(document);
		}
		await unit.CompleteAsync();
	}
	private async Task<(Guid Year, Guid Board, Guid Medium, Guid Organization)> SeedReportIdentityAsync(Guid tenant)
	{
		var (year, own, _) = await SeedHierarchyAsync(tenant);
		using var scope = factory.Services.CreateScope(); using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var division = await scope.ServiceProvider.GetRequiredService<IRepository<Division, Guid>>().GetAsync(own);
		var standard = await scope.ServiceProvider.GetRequiredService<IRepository<Standard, Guid>>().GetAsync(division.StandardId);
		var organization = await scope.ServiceProvider.GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(standard.EducationMediumStageId);
		var medium = await scope.ServiceProvider.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(organization.EducationMediumId);
		var profiles = scope.ServiceProvider.GetRequiredService<IRepository<SchoolIdentityProfile, Guid>>();
		if (!await profiles.AnyAsync(x => x.TenantId == tenant && x.ScopeKey == "school")) {
			var root = new SchoolIdentityProfile(Guid.NewGuid(), tenant); root.Replace(new() { Signature = new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = Guid.NewGuid(), PrincipalName = "Synthetic principal", Designation = "Principal" } }, root.ConcurrencyStamp);
			await profiles.InsertAsync(root);
		}
		await profiles.InsertAsync(new(Guid.NewGuid(), tenant, year, medium.EducationBoardId));
		await profiles.InsertAsync(new(Guid.NewGuid(), tenant, year, medium.EducationBoardId, medium.Id));
		await profiles.InsertAsync(new(Guid.NewGuid(), tenant, year, medium.EducationBoardId, medium.Id, organization.Id));
		await unit.CompleteAsync(); return (year, medium.EducationBoardId, medium.Id, organization.Id);
	}
}
