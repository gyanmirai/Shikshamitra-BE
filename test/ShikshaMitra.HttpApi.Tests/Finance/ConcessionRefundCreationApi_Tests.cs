using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.FeeRefunds;
using ShikshaMitra.Finance.FeeRefundTests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Concessions;

[Collection(FeeRefundApiTestCollection.Name)]
public class ConcessionRefundCreationApi_Tests : ShikshaMitraApiTestBase
{
	public ConcessionRefundCreationApi_Tests(FeeRefundApiTestFactory factory) : base(factory)
	{
		FeeRefundTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
		FeeRefundTestHelper.DefaultBoardId = factory.SeededBoardId;
		FeeRefundTestHelper.DefaultMediumId = factory.SeededMediumId;
		FeeRefundTestHelper.DefaultMediumStageId = factory.SeededMediumStageId;
		FeeRefundTestHelper.DefaultStandardId = factory.SeededStandardId;
		FeeRefundTestHelper.DefaultDivisionId = factory.SeededDivisionId;
		FeeCollectionTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
		FeeCollectionTestHelper.DefaultMediumId = factory.SeededMediumId;
		FeeCollectionTestHelper.DefaultStandardId = factory.SeededStandardId;
		FeeCollectionTestHelper.DefaultDivisionId = factory.SeededDivisionId;
		ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
		ConcessionRequestTestHelper.DefaultMediumId = factory.SeededMediumId;
		ConcessionRequestTestHelper.DefaultStandardId = factory.SeededStandardId;
		ConcessionRequestTestHelper.DefaultDivisionId = factory.SeededDivisionId;
	}

	[Fact]
	public async Task Manual_refunds_are_linked_to_paid_concession_and_retry_does_not_duplicate()
	{
		var request = await SeedApprovedAsync(false);
		var command = new CreateConcessionRefundRequestsDto {
			RequestConcurrencyStamp = request.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid().ToString("N") };
		var url = $"/api/concession-request/{request.Id}/refund-requests";
		var response = await Client.PostAsJsonAsync(url, command, JsonOptions);
		response.EnsureSuccessStatusCode();
		var detail = (await response.Content.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions))!.Data!;
		detail.RefundRequests.Count.ShouldBe(1);
		detail.RefundRequests.Sum(x => x.Amount).ShouldBe(200m);
		detail.CanCreateRefundRequests.ShouldBeFalse();
		var replay = await Client.PostAsJsonAsync(url, command, JsonOptions);
		replay.EnsureSuccessStatusCode();
		var replayDetail = (await replay.Content.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions))!.Data!;
		replayDetail.RefundRequests.Select(x => x.RefundRequestId).ShouldBe(detail.RefundRequests.Select(x => x.RefundRequestId));
		command.IdempotencyKey = Guid.NewGuid().ToString("N");
		command.RequestConcurrencyStamp = detail.ConcurrencyStamp;
		(await Client.PostAsJsonAsync(url, command, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Automatic_policy_rejects_manual_creation()
	{
		var request = await SeedApprovedAsync(true);
		var response = await Client.PostAsJsonAsync($"/api/concession-request/{request.Id}/refund-requests",
			new CreateConcessionRefundRequestsDto { RequestConcurrencyStamp = request.ConcurrencyStamp,
				IdempotencyKey = Guid.NewGuid().ToString("N") }, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	private async Task<ConcessionRequest> SeedApprovedAsync(bool automatic)
	{
		var (studentId, dueId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
		var dues = services.GetRequiredService<IDueFeeRepository>();
		var due = await dues.GetAsync(dueId);
		var policy = new ConcessionPolicy(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
			new ConcessionPolicyUpdateModel($"Refund {Guid.NewGuid():N}", $"R{Guid.NewGuid():N}"[..20], null,
				ConcessionPolicyStatus.Active, false, ConcessionValueSource.PolicyFixed,
				ConcessionCalculationMethod.Percentage, 20m, null, null, null, ConcessionApplyOn.AllFees,
				RoundingMethodType.RoundUp, AutoCreateRefund: automatic));
		await services.GetRequiredService<IRepository<ConcessionPolicy, Guid>>().InsertAsync(policy);
		var version = new FinancePolicyVersion(Guid.NewGuid(), tenantId, "concession", policy.Id,
			policy.AcademicYearId, policy.Version, policy.PolicyName, "Active", JsonSerializer.Serialize(policy.ToSnapshot()), "created");
		await services.GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(version);
		var request = new ConcessionRequest(Guid.NewGuid(), tenantId, studentId,
			due.StudentFeeStructureId ?? throw new InvalidOperationException("Paid fixture requires a fee assignment."),
			policy.Id, version.Id, typeId, policy.AcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId,
			Factory.SeededStandardId, Factory.SeededDivisionId, "Paid concession refund test");
		request.Approve(Guid.NewGuid(), null, [new ConcessionDueFeeAllocation(Guid.NewGuid(), tenantId,
			request.Id, dueId, due.FeeHeadId, 200m)], DateTime.UtcNow);
		request.RecordPaidConcessionAmounts(new Dictionary<Guid, decimal> { [dueId] = 200m }, automatic);
		due.ApplyConcession(200m);
		await dues.UpdateAsync(due);
		await services.GetRequiredService<IRepository<ConcessionRequest, Guid>>().InsertAsync(request);
		await uow.CompleteAsync();
		return request;
	}
}
