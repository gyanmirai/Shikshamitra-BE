using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

[Collection(ApiTestCollection.Name)]
public sealed class AcademicReportRecoveryApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Fact]
	public async Task Expired_report_link_exposes_recovery_to_its_requester_and_checks_export_permission_on_post()
	{
		var jobId = Guid.NewGuid();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var requester = Guid.Parse(TestAuthHandler.TestUserId);
		var path = $"/api/app/academic-report/exports/{jobId:D}";
		var request = new AcademicReportRequestDto { ReportType = AcademicReportTypes.ChapterDescriptionSummary };
		using (var scope = Factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			using var school = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
			using (var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
			{
				await services.GetRequiredService<IRepository<HrReportExportJob, Guid>>().InsertAsync(new(
					jobId, tenantId, requester, request.ReportType, request.Format, "{}", JsonSerializer.Serialize(request),
					DateTime.UtcNow.AddMinutes(-1)), autoSave: true);
				await unit.CompleteAsync();
			}
			var state = services.GetRequiredService<AcademicReportExportStateService>();
			var run = (await state.TryStartAsync(jobId)).ShouldNotBeNull();
			(await state.TryCompleteAsync(jobId, run, "expired-test.xlsx", 1, 10)).ShouldBeTrue();
		}

		using var expired = await Client.GetAsync(path + "/download");
		expired.IsSuccessStatusCode.ShouldBeFalse();
		expired.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
		var problem = await expired.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		problem.GetProperty("errorCode").GetString().ShouldBe("Academic:Reports:050002");

		using var anonymous = CreateAnonymousClient();
		(await anonymous.GetAsync(path + "/recovery")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		using var another = CreateClientAs(Guid.NewGuid());
		(await another.GetAsync(path + "/recovery")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await another.PostAsync(path + "/regenerate", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_Reports.View);
		var view = await GetAndReadAsync<AcademicReportRecoveryDto>(path + "/recovery");
		view.Status.ShouldBe("Expired");
		view.CanRegenerate.ShouldBeFalse();
		(await Client.PostAsync(path + "/regenerate", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		Client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_Reports.Add);
		(await GetAndReadAsync<AcademicReportRecoveryDto>(path + "/recovery")).CanRegenerate.ShouldBeTrue();
		var result = await PostAndReadAsync<AcademicReportExportCreateResponseDto>(path + "/regenerate", new { });
		result.JobId.ShouldBe(jobId);
		result.Status.ShouldBe(HrReportExportStatus.Queued);
		(await GetAndReadAsync<AcademicReportRecoveryDto>(path + "/recovery")).Status.ShouldBe(HrReportExportStatus.Queued);
	}
}
