using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
public class LeavePolicyLifecyclePostgres_Tests
{
	[PayrollPostgresFact]
	public async Task Concurrent_active_edits_cannot_reuse_the_same_current_version()
	{
		// Opt-in disposable loopback factory only. This test never starts a cluster.
		using var factory = new PayrollPostgresTestFactory();
		await new Scenario(factory).RunAsync();
	}

	private sealed class Scenario(PayrollPostgresTestFactory factory) : ShikshaMitraApiTestBase(factory)
	{
		public async Task RunAsync()
		{
			DateTime date;
			using (var scope = Factory.Services.CreateScope())
			using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
			using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
			{
				date = (await scope.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(Factory.SeededAcademicYearId)).StartDate.Date;
				await unit.CompleteAsync();
			}
			var create = new LeavePolicyCreateRequest { PolicyTitle = $"Concurrent leave {Guid.NewGuid():N}", Status = "Active",
				EffectiveFrom = date, Rules = [new() { PaidType = "Paid", LeaveCount = 0 }, new() { PaidType = "Unpaid", LeaveCount = 0 }] };
			var createdResponse = await Client.PostAsJsonAsync($"/api/leave-policies?academicYearId={Factory.SeededAcademicYearId}", create);
			createdResponse.EnsureSuccessStatusCode();
			var policy = (await createdResponse.Content.ReadFromJsonAsync<LeavePolicyResponse>(JsonOptions))!;
			using var first = Factory.CreateClient();
			using var second = Factory.CreateClient();
			LeavePolicyUpdateRequest Body(string description) => new() { PolicyTitle = policy.PolicyTitle, Status = "Active",
				IsDefault = policy.IsDefault, EffectiveFrom = date, Rules = create.Rules, Description = description, ExpectedVersion = 1,
				ImpactScope = new() { AsOfDate = date, ExpectedVersion = 1 } };
			var a = Body("First concurrent proposal"); var b = Body("Second concurrent proposal");
			foreach (var pair in new[] { (first, a), (second, b) })
			{
				var previewResponse = await pair.Item1.PostAsJsonAsync("/api/leave-policies/impact/preview", new LeavePolicyImpactRequest
					{ AcademicYearId = Factory.SeededAcademicYearId, PolicyId = policy.Id, Action = "update", ExpectedVersion = 1, AsOfDate = date, Proposal = pair.Item2 });
				previewResponse.EnsureSuccessStatusCode();
				var preview = (await previewResponse.Content.ReadFromJsonAsync<LeavePolicyImpactReviewDto>(JsonOptions))!;
				preview.ConfirmationToken.ShouldNotBeNullOrEmpty();
				pair.Item1.DefaultRequestHeaders.Add(HrPolicyImpactConfirmationService.HeaderName, preview.ConfirmationToken);
			}
			var responses = await Task.WhenAll(first.PutAsJsonAsync($"/api/leave-policies/{policy.Id}", a), second.PutAsJsonAsync($"/api/leave-policies/{policy.Id}", b));
			responses.Count(x => x.IsSuccessStatusCode).ShouldBe(1);
			var stale = responses.Single(x => !x.IsSuccessStatusCode);
			stale.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await stale.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.HrPolicyImpactConfirmationInvalid);
			var history = await Client.GetFromJsonAsync<PagedResultDto<HrPolicyVersionDto>>($"/api/hr/leave/policies/{policy.Id}/versions", JsonOptions);
			history!.Items.Select(x => x.VersionNumber).Order().ShouldBe([1, 2]);
			var final = await Client.GetFromJsonAsync<LeavePolicyDetailResponse>($"/api/leave-policies/{policy.Id}", JsonOptions);
			final!.Version.ShouldBe(2);
			foreach (var response in responses) response.Dispose();
		}
	}
}
