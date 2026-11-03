using System;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.FeeRefunds;
using ShikshaMitra.Finance.FeeRefundTests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Concessions;

[Collection(ConcessionAutomaticRefundApiTestCollection.Name)]
public class ConcessionAutomaticRefundApprovalApi_Tests : ConcessionRequestApiTestsBase
{
	public ConcessionAutomaticRefundApprovalApi_Tests(ConcessionAutomaticRefundApiTestFactory factory) : base(factory)
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
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Approval_of_paid_concession_obeys_automatic_refund_policy_and_replays_once(bool automatic)
	{
		using var maker = await CreatePersistedCheckerClientAsync();
		var draft = await CreatePaidDraftAsync(automatic, maker);
		var previewResponse = await maker.PostAsJsonAsync(
			$"{BaseUrl}/preview?academicYearId={Factory.SeededAcademicYearId}",
			new ConcessionRequestPreviewInputDto
			{
				StudentId = draft.StudentId, StudentFeeStructureId = draft.StudentFeeStructureId,
				ConcessionTypeId = draft.ConcessionTypeId, ConcessionPolicyId = draft.ConcessionPolicyId,
				ConcessionPolicyVersionId = draft.ConcessionPolicyVersionId,
				FeeHeadConcessions = draft.FeeHeadConcessions.Select(x => new FeeHeadConcessionDto { FeeHeadId = x.FeeHeadId }).ToList(),
			}, JsonOptions);
		await EnsureHttpSuccessAsync(previewResponse);
		var preview = (await previewResponse.Content.ReadFromJsonAsync<ConcessionRequestPreviewDto>(JsonOptions))!;
		var submission = await maker.PostAsJsonAsync($"{BaseUrl}/{draft.ConcessionRequestId}/submit",
			new SubmitConcessionRequestDto { ConcurrencyStamp = draft.ConcurrencyStamp,
				PreviewFingerprint = preview.PreviewFingerprint, IdempotencyKey = Guid.NewGuid().ToString("N") }, JsonOptions);
		await EnsureHttpSuccessAsync(submission);
		(await submission.Content.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions))!
			.Data!.Status.ShouldBe(ConcessionRequestStatus.Pending);
		using var checker = await CreatePersistedCheckerClientAsync();
		var context = (await checker.GetFromJsonAsync<ConcessionApprovalContextDto>(
			$"{BaseUrl}/{draft.ConcessionRequestId}/approval-context", JsonOptions))!;
		var command = new ApproveConcessionRequestDto
		{
			RequestConcurrencyStamp = context.RequestConcurrencyStamp,
			PreviewFingerprint = context.PreviewFingerprint,
			IdempotencyKey = Guid.NewGuid().ToString("N"),
		};
		var url = $"{ConcessionRequestsBaseUrl}/{draft.ConcessionRequestId}/approve";
		var approval = await checker.PostAsJsonAsync(url, command, JsonOptions);
		await EnsureHttpSuccessAsync(approval);
		var result = (await approval.Content.ReadFromJsonAsync<ConcessionApprovalResponseDto>(JsonOptions))!;
		result.TotalApprovedAmount.ShouldBe(1538m);
		result.RefundRequests.Count.ShouldBe(automatic ? 1 : 0);
		var replay = await checker.PostAsJsonAsync(url, command, JsonOptions);
		await EnsureHttpSuccessAsync(replay);
		var replayResult = (await replay.Content.ReadFromJsonAsync<ConcessionApprovalResponseDto>(JsonOptions))!;
		replayResult.RefundRequests.Select(x => x.RefundRequestId).ShouldBe(result.RefundRequests.Select(x => x.RefundRequestId));
		var detail = (await checker.GetFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(
			$"{BaseUrl}/{draft.ConcessionRequestId}", JsonOptions))!.Data!;
		detail.Status.ShouldBe(ConcessionRequestStatus.Approved);
		detail.PaidConcessionAmount.ShouldBe(1538m);
		detail.RefundRequests.Count.ShouldBe(automatic ? 1 : 0);
		var refunds = (await checker.GetFromJsonAsync<PagedResultDto<FeeRefundRequestDto>>(
			$"/api/fee-refunds?studentId={draft.StudentId}&academicYearId={Factory.SeededAcademicYearId}", JsonOptions))!;
		refunds.TotalCount.ShouldBe(automatic ? 1 : 0);
		if (automatic)
		{
			var refund = refunds.Items.Single();
			refund.ConcessionRequestId.ShouldBe(draft.ConcessionRequestId);
			refund.CalculatedAmount.ShouldBe(1538m);
			refund.OriginalReceiptId.ShouldNotBeNull();
		}
	}

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, false)]
	[InlineData(true, true)]
	[InlineData(false, true)]
	public async Task Administrator_submission_creates_paid_refund_only_when_enabled_and_replays_once(bool automatic, bool createAndSubmit)
	{
		using var administrator = await CreatePersistedCheckerClientAsync();
		var administratorId = Guid.Parse(administrator.DefaultRequestHeaders.GetValues("X-Test-UserId").Single());
		await FeeRefundTestHelper.SetAdministratorAsync(Factory.Services, administratorId, true);
		var draft = await CreatePaidDraftAsync(automatic, administrator);
		var previewResponse = await administrator.PostAsJsonAsync(
			$"{BaseUrl}/preview?academicYearId={Factory.SeededAcademicYearId}",
			new ConcessionRequestPreviewInputDto
			{
				StudentId = draft.StudentId, StudentFeeStructureId = draft.StudentFeeStructureId,
				ConcessionTypeId = draft.ConcessionTypeId, ConcessionPolicyId = draft.ConcessionPolicyId,
				ConcessionPolicyVersionId = draft.ConcessionPolicyVersionId,
				FeeHeadConcessions = draft.FeeHeadConcessions.Select(x => new FeeHeadConcessionDto { FeeHeadId = x.FeeHeadId }).ToList(),
			}, JsonOptions);
		await EnsureHttpSuccessAsync(previewResponse);
		var preview = (await previewResponse.Content.ReadFromJsonAsync<ConcessionRequestPreviewDto>(JsonOptions))!;
		object command = createAndSubmit
			? new CreateConcessionRequestDto
			{
				StudentId = draft.StudentId, StudentFeeStructureId = draft.StudentFeeStructureId,
				ConcessionTypeId = draft.ConcessionTypeId, ConcessionPolicyId = draft.ConcessionPolicyId,
				ConcessionPolicyVersionId = draft.ConcessionPolicyVersionId,
				FeeHeadConcessions = draft.FeeHeadConcessions.Select(x => new FeeHeadConcessionDto { FeeHeadId = x.FeeHeadId }).ToList(),
				PreviewFingerprint = preview.PreviewFingerprint, IdempotencyKey = Guid.NewGuid().ToString("N"),
			}
			: new SubmitConcessionRequestDto
			{
				ConcurrencyStamp = draft.ConcurrencyStamp, PreviewFingerprint = preview.PreviewFingerprint,
				IdempotencyKey = Guid.NewGuid().ToString("N"),
			};
		var url = createAndSubmit
			? $"{BaseUrl}?academicYearId={Factory.SeededAcademicYearId}"
			: $"{BaseUrl}/{draft.ConcessionRequestId}/submit";
		var submission = await administrator.PostAsJsonAsync(url, command, JsonOptions);
		await EnsureHttpSuccessAsync(submission);
		var detail = (await submission.Content.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions))!.Data!;
		detail.Status.ShouldBe(ConcessionRequestStatus.Approved);
		detail.PaidConcessionAmount.ShouldBe(1538m);
		detail.RefundRequests.Count.ShouldBe(automatic ? 1 : 0);
		var replay = await administrator.PostAsJsonAsync(url, command, JsonOptions);
		await EnsureHttpSuccessAsync(replay);
		var refunds = (await administrator.GetFromJsonAsync<PagedResultDto<FeeRefundRequestDto>>(
			$"/api/fee-refunds?studentId={draft.StudentId}&academicYearId={Factory.SeededAcademicYearId}", JsonOptions))!;
		refunds.TotalCount.ShouldBe(automatic ? 1 : 0);
		if (automatic) refunds.Items.Single().CalculatedAmount.ShouldBe(1538m);
	}

	private async Task<System.Net.Http.HttpClient> CreatePersistedCheckerClientAsync()
	{
		var id = Guid.NewGuid();
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var name = $"concession-refund-checker-{id:N}";
		await services.GetRequiredService<IRepository<Volo.Abp.Identity.IdentityUser, Guid>>().InsertAsync(
			new Volo.Abp.Identity.IdentityUser(id, name, $"{name}@example.test", tenantId));
		await services.GetRequiredService<IRepository<ShikshaMitra.Security.UserScope.UserScopeAssignment, Guid>>().InsertAsync(
			new ShikshaMitra.Security.UserScope.UserScopeAssignment(Guid.NewGuid(), tenantId, id,
				description: "Concession automatic refund regression scope"));
		await uow.CompleteAsync();
		return CreateClientAs(id);
	}

	private async Task EnsureHttpSuccessAsync(System.Net.Http.HttpResponseMessage response)
	{
		response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync() +
			Environment.NewLine + string.Join(Environment.NewLine, ((ConcessionAutomaticRefundApiTestFactory)Factory).ServerErrors));
	}

	private async Task<ConcessionRequestDetailDto> CreatePaidDraftAsync(bool automatic, System.Net.Http.HttpClient? author = null)
	{
		var (studentId, dueId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, 10000d);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		CreateConcessionDraftDto input;
		using (var scope = Factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
			using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
			using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var due = await services.GetRequiredService<IDueFeeRepository>().GetAsync(dueId);
			var assignment = await services.GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetAsync(due.StudentFeeStructureId!.Value);
			var metadata = new FeeStructureMetadata(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
				Factory.SeededBoardId, Factory.SeededMediumId, null, $"Automatic concession {Guid.NewGuid():N}");
			await services.GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(metadata);
			await services.GetRequiredService<IRepository<FeeStructure, Guid>>().InsertAsync(new FeeStructure(
				assignment.FeeStructureStandardId, tenantId, metadata.Id, Factory.SeededStandardId, metadata.Name!));
			var head = await services.GetRequiredService<IRepository<FeeHead, Guid>>().GetAsync(due.FeeHeadId);
			head.Update(head.FeeCategoryId, head.Name, head.Module, true, head.Description, head.IncomeCategoryId);
			var policy = new ConcessionPolicy(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
				new ConcessionPolicyUpdateModel($"Automatic refund {Guid.NewGuid():N}", $"AR{Guid.NewGuid():N}"[..20], null,
					ConcessionPolicyStatus.Active, false, ConcessionValueSource.PolicyFixed,
					ConcessionCalculationMethod.FixedAmount, null, 1538m, null, null, ConcessionApplyOn.AllFees,
					RoundingMethodType.RoundUp, AutoCreateRefund: automatic));
			await services.GetRequiredService<IRepository<ConcessionPolicy, Guid>>().InsertAsync(policy);
			var version = new FinancePolicyVersion(Guid.NewGuid(), tenantId, "concession", policy.Id,
				policy.AcademicYearId, policy.Version, policy.PolicyName, "Active", JsonSerializer.Serialize(policy.ToSnapshot()), "created");
			await services.GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(version);
			input = new CreateConcessionDraftDto
			{
				StudentId = studentId, StudentFeeStructureId = assignment.Id, ConcessionTypeId = typeId,
				ConcessionPolicyId = policy.Id, ConcessionPolicyVersionId = version.Id,
				FeeHeadConcessions = [new() { FeeHeadId = head.Id }], IdempotencyKey = Guid.NewGuid().ToString("N"),
			};
			await uow.CompleteAsync();
		}
		var client = author ?? Client;
		var contextResponse = await client.GetAsync($"{BaseUrl}/context?academicYearId={Factory.SeededAcademicYearId}" +
			$"&studentId={studentId}&studentFeeStructureId={input.StudentFeeStructureId}" +
			$"&concessionPolicyId={input.ConcessionPolicyId}&concessionPolicyVersionId={input.ConcessionPolicyVersionId}");
		await EnsureHttpSuccessAsync(contextResponse);
		var context = (await contextResponse.Content.ReadFromJsonAsync<ConcessionRequestContextDto>(JsonOptions))!;
		var paid = context.EligibleDueFees.Single(x => x.DueFeeId == dueId);
		paid.PaidAmount.ShouldBe(10000m);
		paid.OutstandingAmount.ShouldBe(0m);
		paid.EligibleConcessionAmount.ShouldBe(10000m);
		context.FeeHeads.Single(x => x.FeeHeadId == paid.FeeHeadId).EligibleOutstanding.ShouldBe(10000m);
		var draftResponse = await client.PostAsJsonAsync($"{BaseUrl}/drafts?academicYearId={Factory.SeededAcademicYearId}", input, JsonOptions);
		await EnsureHttpSuccessAsync(draftResponse);
		return (await draftResponse.Content.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions))!.Data!;
	}
}
