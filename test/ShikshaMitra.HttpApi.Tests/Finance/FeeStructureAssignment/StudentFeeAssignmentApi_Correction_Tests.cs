using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.FeeStructureAssignment;

public partial class StudentFeeAssignmentApi_Tests
{
	private new async Task<T> PostAndReadAsync<T>(string url, object body)
	{
		using var response = await Client.PostAsJsonAsync(url, body, JsonOptions);
		response.IsSuccessStatusCode.ShouldBeTrue($"POST {url}: {await response.Content.ReadAsStringAsync()}");
		return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))
			?? throw new InvalidOperationException($"Empty JSON response from POST {url}");
	}

	private async Task<AssignStudentFeeStructureDto> BeginCorrectionAsync(AssignStudentFeeStructureDto input, string reason)
	{
		var read = await GetAndReadAsync<StudentFeeStructureResponseDto>($"{BaseUrl}/student/{input.StudentId}?academicYearId={input.AcademicYearId}");
		input.EditAssignmentId = read.StudentFeeStructureId;
		input.AssignmentConcurrencyStamp = read.ConcurrencyStamp;
		input.EditReason = reason;
		input.PreviewToken = null;
		return input;
	}

	private async Task<AssignStudentFeeStructureDto> CloneAssignmentStudentAsync(AssignStudentFeeStructureDto source, string firstName)
	{
		using var services = Factory.Services.CreateScope();
		using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var students = services.ServiceProvider.GetRequiredService<IRepository<Student, Guid>>();
		var original = await services.ServiceProvider.GetRequiredService<IStudentRepository>().GetForFeeAssignmentAsync(source.StudentId)
			?? throw new InvalidOperationException("The source assignment student was not found.");
		var student = new Student(Guid.NewGuid(), original.TenantId, firstName, null, "Assignment", source.AcademicYearId,
			original.AcademicBoardId!.Value, original.AcademicMediumId, original.AcademicMediumStageId!.Value,
			original.StandardId, original.DivisionId, status: StudentLifeCycleStatusEnum.Enrolled);
		await students.InsertAsync(student, true);
		await uow.CompleteAsync();
		return new() { StudentId = student.Id, AcademicYearId = source.AcademicYearId, StandardFeeStructureId = source.StandardFeeStructureId,
			SelectedOptionalFeeHeadIds = source.SelectedOptionalFeeHeadIds.ToList(), VariableFeeHeads = source.VariableFeeHeads
				.Select(h => new VariableFeeHeadAmountDto { FeeHeadId = h.FeeHeadId, Amount = h.Amount, ZeroAmountReason = h.ZeroAmountReason }).ToList() };
	}

	private async Task<FeeStructureCreateResponse> PublishCorrectionRevisionAsync(Guid structureId, Guid mandatoryHead, Guid? optionalHead = null)
	{
		string stamp;
		using (var services = Factory.Services.CreateScope())
		using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
			stamp = (await services.ServiceProvider.GetRequiredService<IRepository<FeeStructure, Guid>>().GetAsync(structureId)).ConcurrencyStamp;
		var draft = await PostAndReadAsync<FeeStructureCreateResponse>($"/api/fee-structures/{structureId}/revisions", new { concurrencyStamp = stamp });
		var updates = new List<object> { new { lineId = draft.FeeHeads.Single(h => h.FeeHeadId == mandatoryHead).Id, amount = 14000m } };
		if (optionalHead.HasValue) updates.Add(new { lineId = draft.FeeHeads.Single(h => h.FeeHeadId == optionalHead).Id, amount = 7500m });
		var patch = await Client.PatchAsJsonAsync($"/api/fee-structures/{draft.Id}", new FeeStructurePatchRequest {
			RequestId = Guid.NewGuid(), ConcurrencyStamp = draft.ConcurrencyStamp,
			Changes = JsonSerializer.SerializeToElement(new { feeHeads = new { update = updates } }, JsonOptions) }, JsonOptions);
		patch.StatusCode.ShouldBe(HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
		var changed = (await patch.Content.ReadFromJsonAsync<FeeStructureCreateResponse>(JsonOptions)).ShouldNotBeNull();
		return await PostAndReadAsync<FeeStructureCreateResponse>($"/api/fee-structures/{draft.Id}/publish", new { concurrencyStamp = changed.ConcurrencyStamp });
	}

	private static async Task AssertAssignmentErrorAsync(HttpResponseMessage response, string code)
	{
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("errorCode").GetString().ShouldBe(code);
	}

	private async Task<CollectFeeOutput> CollectAssignedFeeAsync(AssignStudentFeeStructureDto input, AssignmentCommitResultDto assignment,
		double amount, bool cheque = false)
	{
		var line = assignment.Assignment.Lines.First();
		var payment = FeeCollectionTestHelper.BuildCollectFeeInput(input.StudentId, input.AcademicYearId,
			line.FeeHeadId, line.DueFeeId!.Value, amount);
		if (cheque)
		{
			var bank = await PostAndReadAsync<JsonElement>($"/api/bank-accounts?academicYearId={input.AcademicYearId}", new {
				bankName = "Synthetic correction bank", branchName = "Test branch", accountHolderName = "Synthetic school",
				accountType = 0, accountNumber = Random.Shared.NextInt64(100000000000, 999999999999).ToString(), ifscCode = "SBIN0123456" });
			payment.Payments[0].PaymentMethod = PaymentMethodEnum.Cheque;
			payment.Payments[0].BankAccountId = bank.GetProperty("id").GetGuid();
			payment.Payments[0].Details = new() { ["chequeNumber"] = "123456", ["bankName"] = "Synthetic bank",
				["branchName"] = "Test branch", ["chequeDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd") };
		}
		var response = await Client.PostAsJsonAsync("/api/fee-collections", payment, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions)).ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Correction_HTTP_Unpaid_Revision_Optional_Selection_And_Retry_Preserve_Evidence()
	{
		var (input, heads) = await CreateFixtureAsync(true, true, true, true);
		input.SelectedOptionalFeeHeadIds = [heads[1], heads[2]];
		input.VariableFeeHeads = [new() { FeeHeadId = heads[2], Amount = 1800m }];
		var original = await PreviewAndAssignAsync(input);
		original.Assignment.ProratedTotal.ShouldBe(19800m);
		var replacement = await PublishCorrectionRevisionAsync(input.StandardFeeStructureId, heads[0], heads[1]);
		await BeginCorrectionAsync(input, "Correct the selected structure and applicable fees");
		input.StandardFeeStructureId = replacement.Id;
		input.SelectedOptionalFeeHeadIds = [heads[1]];
		input.VariableFeeHeads = [];
		var contexts = await PostAndReadAsync<List<AssignmentContextDto>>($"{BaseUrl}/contexts", new { input.AcademicYearId, studentIds = new[] { input.StudentId } });
		contexts.Single().CanEditStructure.ShouldBeTrue();
		contexts.Single().AssignedAssignment.ShouldNotBeNull();
		var preview = await PostAndReadAsync<StudentFeeAssignmentPreviewDto>($"{BaseUrl}/preview", input);
		preview.ProratedTotal.ShouldBe(21500m);
		input.PreviewToken = preview.PreviewToken;
		var saved = await PostAndReadAsync<AssignmentCommitResultDto>($"{BaseUrl}/assign", input);
		saved.AssignmentId.ShouldBe(original.AssignmentId);
		saved.Assignment.StructureId.ShouldBe(replacement.Id);
		saved.Assignment.Lines.Select(l => l.DueFeeId).Intersect(original.Assignment.Lines.Select(l => l.DueFeeId)).ShouldBeEmpty();
		(await PostAndReadAsync<AssignmentCommitResultDto>($"{BaseUrl}/assign", input)).Outcome.ShouldBe("AlreadyAssigned");
		var form = await GetAndReadAsync<FeeCollectFormDto>($"/api/fee-collections/{input.StudentId}/collect-form?academicYearId={input.AcademicYearId}");
		form.Summary.TotalFee.ShouldBe(21500d);
		using var services = Factory.Services.CreateScope();
		using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var entity = await services.ServiceProvider.GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetAsync(saved.AssignmentId);
		entity.ExtraProperties["AssignmentCorrectionHistory"].ShouldNotBeNull();
		using (services.ServiceProvider.GetRequiredService<IDataFilter>().Disable<ISoftDelete>())
			(await services.ServiceProvider.GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(d => d.StudentFeeStructureId == saved.AssignmentId))
				.Count.ShouldBe(original.Assignment.Lines.Count + saved.Assignment.Lines.Count);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Correction_HTTP_Bulk_Common_Original_Succeeds_And_Mixed_Originals_Are_Rejected()
	{
		var (first, heads) = await CreateFixtureAsync(true, true);
		first.SelectedOptionalFeeHeadIds = [heads[1], heads[2]];
		var second = await CloneAssignmentStudentAsync(first, "Synthetic bulk two");
		await PreviewAndAssignAsync(first);
		await PreviewAndAssignAsync(second);
		foreach (var item in new[] { first, second })
		{
			await BeginCorrectionAsync(item, "Correct common applicable fees");
			item.SelectedOptionalFeeHeadIds = [];
		}
		var batch = new BulkAssignStudentFeeStructureDto { Items = [first, second] };
		var previews = await PostAndReadAsync<AssignmentBulkPreviewDto>($"{BaseUrl}/preview-bulk", batch);
		foreach (var item in batch.Items) item.PreviewToken = previews.Results.Single(p => p.StudentId == item.StudentId).Preview.ShouldNotBeNull().PreviewToken;
		var result = await PostAndReadAsync<BulkAssignStudentFeeStructureResultDto>($"{BaseUrl}/assign-bulk", batch);
		result.SuccessCount.ShouldBe(2, JsonSerializer.Serialize(result, JsonOptions));
		result.Results.ShouldAllBe(r => r.Result!.Assignment.ProratedTotal == 12000m);
		(await PostAndReadAsync<BulkAssignStudentFeeStructureResultDto>($"{BaseUrl}/assign-bulk", batch)).AlreadyAssignedCount.ShouldBe(2);
		var (third, _) = await CreateFixtureAsync(useCurrentAcademicContext: true);
		await PreviewAndAssignAsync(third);
		await BeginCorrectionAsync(first, "New batch correction");
		await BeginCorrectionAsync(third, "New batch correction");
		await AssertAssignmentErrorAsync(await Client.PostAsJsonAsync($"{BaseUrl}/preview-bulk",
			new BulkAssignStudentFeeStructureDto { Items = [first, third] }, JsonOptions), AssignmentErrorCodes.MixedOriginalStructures);
	}

	[Theory]
	[InlineData("stamp")]
	[InlineData("reason")]
	[InlineData("token")]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Correction_HTTP_Stale_Request_Does_Not_Replace_Dues(string changed)
	{
		var (input, _) = await CreateFixtureAsync(useCurrentAcademicContext: true);
		var original = await PreviewAndAssignAsync(input);
		await BeginCorrectionAsync(input, "Correct the unused assignment");
		input.PreviewToken = (await PostAndReadAsync<StudentFeeAssignmentPreviewDto>($"{BaseUrl}/preview", input)).PreviewToken;
		if (changed == "stamp") input.AssignmentConcurrencyStamp = "stale";
		if (changed == "reason") input.EditReason = "Different correction reason";
		if (changed == "token") input.PreviewToken = "tampered";
		await AssertAssignmentErrorAsync(await Client.PostAsJsonAsync($"{BaseUrl}/assign", input, JsonOptions), AssignmentErrorCodes.StalePreview);
		var after = await GetAndReadAsync<StudentFeeStructureResponseDto>($"{BaseUrl}/student/{input.StudentId}?academicYearId={input.AcademicYearId}");
		after.Assignment!.Lines.Select(l => l.DueFeeId).ShouldBe(original.Assignment.Lines.Select(l => l.DueFeeId));
	}

	[Theory]
	[InlineData(1000d, false)]
	[InlineData(12000d, false)]
	[InlineData(1000d, true)]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Correction_HTTP_Payment_Blocks_Structure_And_Policies_Remain_Editable(double amount, bool reversed)
	{
		var (input, _) = await CreateFixtureAsync(useCurrentAcademicContext: true, withRefundPolicy: true);
		var original = await PreviewAndAssignAsync(input);
		await BeginCorrectionAsync(input, "Preview before payment arrives");
		input.PreviewToken = (await PostAndReadAsync<StudentFeeAssignmentPreviewDto>($"{BaseUrl}/preview", input)).PreviewToken;
		var payment = await CollectAssignedFeeAsync(input, original, amount, cheque: reversed);
		if (reversed)
		{
			payment.Status.ShouldBe(FeeCollectionStatus.InReconciliation);
			var rejection = await Client.PatchAsJsonAsync($"/api/v1/finance/reconciliations/{payment.FeeReceiptId!.Value}/reject",
				new { remarks = "Synthetic cheque was returned" }, JsonOptions);
			rejection.StatusCode.ShouldBe(HttpStatusCode.OK, await rejection.Content.ReadAsStringAsync());
		}
		var read = await GetAndReadAsync<StudentFeeStructureResponseDto>($"{BaseUrl}/student/{input.StudentId}?academicYearId={input.AcademicYearId}");
		// Real collection can rotate the assignment identity as well as due balances.
		var oldCommandError = read.ConcurrencyStamp == input.AssignmentConcurrencyStamp
			? AssignmentErrorCodes.StructureEditBlocked : AssignmentErrorCodes.StalePreview;
		await AssertAssignmentErrorAsync(await Client.PostAsJsonAsync($"{BaseUrl}/assign", input, JsonOptions), oldCommandError);
		read.CanEditStructure.ShouldBeFalse();
		read.StructureEditBlocker.ShouldNotBeNull().ShouldContain("payment", Case.Insensitive);
		input.AssignmentConcurrencyStamp = read.ConcurrencyStamp;
		input.PreviewToken = null;
		await AssertAssignmentErrorAsync(await Client.PostAsJsonAsync($"{BaseUrl}/preview", input, JsonOptions), AssignmentErrorCodes.StructureEditBlocked);
		var update = new AssignmentPolicyUpdateDto { AcademicYearId = input.AcademicYearId, ConcurrencyStamp = read.ConcurrencyStamp,
			Reason = "Change only subsequent refund policy selection", Policies = [new() { Family = "concession" }, new() { Family = "refund" },
				new() { Family = "collection", VersionId = read.Assignment!.Policies.Single(p => p.Family == "collection").VersionId }] };
		var policyResponse = await Client.PutAsJsonAsync($"{BaseUrl}/student/{input.StudentId}/policies", update, JsonOptions);
		policyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await policyResponse.Content.ReadAsStringAsync());
		var after = (await policyResponse.Content.ReadFromJsonAsync<StudentFeeStructureResponseDto>(JsonOptions)).ShouldNotBeNull();
		after.RefundPolicyVersionId.ShouldBeNull();
		after.Assignment!.Lines.Select(l => l.DueFeeId).ShouldBe(original.Assignment.Lines.Select(l => l.DueFeeId));
		after.Assignment.Lines.Sum(l => l.PaidAmount).ShouldBe(reversed ? 0m : (decimal)amount);
		after.CanEditStructure.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Correction_HTTP_Unchanged_Policy_Pins_Require_Add_And_Not_Override_Authority()
	{
		var (input, _) = await CreateFixtureAsync(useCurrentAcademicContext: true, withRefundPolicy: true);
		await PreviewAndAssignAsync(input);
		await BeginCorrectionAsync(input, "Correct without changing policy pins");
		var current = await GetAndReadAsync<StudentFeeStructureResponseDto>($"{BaseUrl}/student/{input.StudentId}?academicYearId={input.AcademicYearId}");
		input.Policies = current.Assignment!.Policies.Where(p => p.Family != "proration")
			.Select(p => new AssignmentPolicySelectionDto { Family = p.Family, VersionId = p.VersionId }).ToList();
		using var allowed = Factory.CreateClient();
		allowed.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "accountant");
		allowed.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "Finance.Fees.StructureAssign.Add,Finance.Fees.StructureAssign.View");
		var previewResponse = await allowed.PostAsJsonAsync($"{BaseUrl}/preview", input, JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		input.PreviewToken = (await previewResponse.Content.ReadFromJsonAsync<StudentFeeAssignmentPreviewDto>(JsonOptions)).ShouldNotBeNull().PreviewToken;
		var saved = await allowed.PostAsJsonAsync($"{BaseUrl}/assign", input, JsonOptions);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		using var denied = Factory.CreateClient();
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "Finance.Fees.StructureAssign.View");
		(await denied.PostAsJsonAsync($"{BaseUrl}/preview", input, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
}
