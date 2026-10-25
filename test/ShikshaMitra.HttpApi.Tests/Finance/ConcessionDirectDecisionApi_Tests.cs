using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.ConcessionRequests;

[Collection(FinanceReceiptApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ConcessionDirectDecisionApi_Tests : ConcessionRequestApiTestsBase
{
public ConcessionDirectDecisionApi_Tests(FinanceReceiptApiTestFactory factory) : base(factory) { }

	[Theory]
	[InlineData("approve")]
	[InlineData("reject")]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Checker_Can_Decide_Saved_Request_Without_Separate_Submission(string decision)
	{
		var (draft, boardId, dueId) = await CreateSavedDecisionFixtureAsync();
		draft.Status.ShouldBe(ConcessionRequestStatus.Draft);
		draft.TotalRequestedConcessionAmount.ShouldBe(100m);
		draft.AvailableActions.ShouldNotContain("approve");
		draft.AvailableActions.ShouldNotContain("reject");
		// Capacity rises after saving: approval must still apply the saved and reviewed 100, not recalculate it to 200.
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var dues = scope.ServiceProvider.GetRequiredService<IRepository<DueFee, Guid>>();
			var due = await dues.GetAsync(dueId);
			due.ReversePayment(1000m);
			await dues.UpdateAsync(due);
			await uow.CompleteAsync();
		}

		using var checker = await CreateScopedCheckerClientAsync();
		checker.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.View);
		var readOnlyApplications = await checker.GetFromJsonAsync<ConcessionApplicationsDto>(
			$"{BaseUrl}/concession-applications?boardId={boardId}&educationMediumId={draft.EducationMediumId}" +
			$"&academicYearId={ConcessionRequestTestHelper.DefaultAcademicYearId}&tab=pending&page=1&pageSize=100", JsonOptions);
		readOnlyApplications!.Items.ShouldNotContain(x => x.Id == draft.ConcessionRequestId);
		checker.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		var detail = (await checker.GetFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(
			$"{BaseUrl}/{draft.ConcessionRequestId}", JsonOptions))!.Data!;
		detail.AvailableActions.ShouldContain(decision);
		var applications = await checker.GetFromJsonAsync<ConcessionApplicationsDto>(
			$"{BaseUrl}/concession-applications?boardId={boardId}&educationMediumId={draft.EducationMediumId}" +
			$"&academicYearId={ConcessionRequestTestHelper.DefaultAcademicYearId}&tab=pending&page=1&pageSize=100", JsonOptions);
		applications!.Items.ShouldContain(x => x.Id == draft.ConcessionRequestId);

		object command;
		if (decision == "approve")
		{
			var context = await checker.GetFromJsonAsync<ConcessionApprovalContextDto>(
				$"{BaseUrl}/{draft.ConcessionRequestId}/approval-context", JsonOptions);
			var previewResponse = await checker.PostAsJsonAsync(
				$"{BaseUrl}/{draft.ConcessionRequestId}/approval-preview",
				new PreviewConcessionApprovalDto
				{
					RequestConcurrencyStamp = context!.RequestConcurrencyStamp,
					PreviewFingerprint = context.PreviewFingerprint,
				}, JsonOptions);
			previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
			var preview = (await previewResponse.Content.ReadFromJsonAsync<ConcessionApprovalPreviewDto>(JsonOptions))!;
			preview.TotalApprovedAmount.ShouldBe(100m);
			command = new ApproveConcessionRequestDto
			{
				RequestConcurrencyStamp = preview.RequestConcurrencyStamp,
				PreviewFingerprint = preview.PreviewFingerprint,
				IdempotencyKey = Guid.NewGuid().ToString("N"),
			};
		}
		else
		{
			command = new RejectConcessionRequestDto
			{
				Message = "Rejected after independent review.",
				RequestConcurrencyStamp = detail.ConcurrencyStamp,
				IdempotencyKey = Guid.NewGuid().ToString("N"),
			};
		}
		var response = await checker.PostAsJsonAsync(
			$"{ConcessionRequestsBaseUrl}/{draft.ConcessionRequestId}/{decision}", command, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var decided = (await checker.GetFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(
			$"{BaseUrl}/{draft.ConcessionRequestId}", JsonOptions))!.Data!;
		decided.Status.ShouldBe(decision == "approve" ? ConcessionRequestStatus.Approved : ConcessionRequestStatus.Rejected);
		if (decision == "approve") decided.TotalApprovedConcessionAmount.ShouldBe(100m);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Checker_Can_Reject_Incomplete_Saved_Request_Without_Submission()
	{
		var (draft, _, _) = await CreateSavedDecisionFixtureAsync(incomplete: true);
		using var checker = await CreateScopedCheckerClientAsync();
		var response = await checker.PostAsJsonAsync(
			$"{ConcessionRequestsBaseUrl}/{draft.ConcessionRequestId}/reject",
			new RejectConcessionRequestDto
			{
				Message = "Incomplete request is no longer required.",
				RequestConcurrencyStamp = draft.ConcurrencyStamp,
				IdempotencyKey = Guid.NewGuid().ToString("N"),
			}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var detail = (await checker.GetFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(
			$"{BaseUrl}/{draft.ConcessionRequestId}", JsonOptions))!.Data!;
		detail.Status.ShouldBe(ConcessionRequestStatus.Rejected);
		detail.Timeline.ShouldNotContain(x => x.Action == "submitted");
		detail.Timeline.First(x => x.Action == "created").Status.ShouldBe(ConcessionRequestStatus.Draft);
	}

	private async Task<(ConcessionRequestDetailDto Draft, Guid BoardId, Guid DueId)> CreateSavedDecisionFixtureAsync(bool incomplete = false)
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Factory.Services);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var categoryId = await ConcessionRequestTestHelper.CreateFeeCategoryAsync(Client);
		var headId = await ConcessionRequestTestHelper.CreateFeeHeadAsync(Client, categoryId);
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var assignmentId = Guid.NewGuid();
		var dueId = Guid.NewGuid();
		var policyId = Guid.NewGuid();
		var versionId = Guid.NewGuid();
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var services = scope.ServiceProvider;
			var heads = services.GetRequiredService<IRepository<FeeHead, Guid>>();
			var head = await heads.GetAsync(headId);
			head.Update(head.FeeCategoryId, head.Name, head.Module, true, head.Description, head.IncomeCategoryId);
			await heads.UpdateAsync(head);
			var policy = new ConcessionPolicy(policyId, tenantId, Factory.SeededAcademicYearId,
				new ConcessionPolicyUpdateModel($"Direct decision {policyId:N}", $"D{policyId:N}"[..20], null,
					ConcessionPolicyStatus.Active, false, ConcessionValueSource.RequestEnteredWithinLimits,
					ConcessionCalculationMethod.Percentage, null, null, 100m, null,
					ConcessionApplyOn.AllOutstanding, RoundingMethodType.RoundUp));
			var version = new FinancePolicyVersion(versionId, tenantId, "concession", policyId,
				policy.AcademicYearId, policy.Version, policy.PolicyName, "Active", JsonSerializer.Serialize(policy.ToSnapshot()), "created");
			var assignment = new StudentFeeStructure(assignmentId, tenantId, studentId, Guid.NewGuid(),
				Factory.SeededAcademicYearId, policyId, versionId, ConcessionFeeHeadScope.EntireFeeStructure, []);
			var due = new DueFee(dueId, tenantId, studentId, headId, MonthEnum.June, 2026, false, 2000m, new DateTime(2026, 6, 10));
			due.BindAssignment(assignmentId);
			due.ApplyPayment(1000m);
			await services.GetRequiredService<IRepository<ConcessionPolicy, Guid>>().InsertAsync(policy);
			await services.GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(version);
			await services.GetRequiredService<IRepository<StudentFeeStructure, Guid>>().InsertAsync(assignment);
			await services.GetRequiredService<IRepository<DueFee, Guid>>().InsertAsync(due);
			await uow.CompleteAsync();
		}
		var draft = await ConcessionRequestTestHelper.CreateDraftAsync(Client, new CreateConcessionDraftDto
		{
			StudentId = studentId, StudentFeeStructureId = assignmentId, ConcessionTypeId = typeId,
			ConcessionPolicyId = policyId, ConcessionPolicyVersionId = versionId,
			DueFeeConcessions = incomplete ? [] : [new() { DueFeeId = dueId, FeeHeadId = headId, Amount = 10m }],
		});
		return (draft, boardId, dueId);
	}

}
