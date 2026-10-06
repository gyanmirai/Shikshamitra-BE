using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	[Fact]
	public async Task Assignment_Policy_Defaults_Overrides_Token_And_Saved_Versions_Agree()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var candidate = (await service.GetCandidatesAsync(input.StudentId, input.AcademicYearId)).Single();
		candidate.Policies.ShouldContain(p => p.Family == "collection");
		var original = await service.PreviewAsync(input);
		var collectionId = await CreateCollectionPolicyAsync(input.AcademicYearId, original.Heads.Single().FeeHeadId, "Student collection exception");
		var collection = (await service.GetPolicyOptionsAsync(input.StudentId, input.AcademicYearId)).Single(p => p.PolicyId == collectionId);
		var concession = (await service.GetPolicyOptionsAsync(input.StudentId, input.AcademicYearId)).Single(p => p.Family == "concession");
		input.Policies = [new() { Family = "concession", VersionId = concession.VersionId }, new() { Family = "collection", VersionId = collection.VersionId }];
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe(AssignmentErrorCodes.InvalidInput);
		input.PolicyOverrideReason = "Student merit approval";
		input.PreviewToken = original.PreviewToken;
		(await Should.ThrowAsync<BusinessException>(() => service.AssignAsync(input))).Code.ShouldBe(AssignmentErrorCodes.StalePreview);
		var preview = await service.PreviewAsync(input);
		preview.Policies.Single(p => p.Family == "concession").Source.ShouldBe("StudentOverride");
		preview.ProratedTotal.ShouldBe(original.ProratedTotal, "Selecting concession rules must not grant a concession.");
		input.PreviewToken = preview.PreviewToken;
		var saved = await service.AssignAsync(input);
		var assignment = await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetAsync(saved.AssignmentId);
		assignment.ConcessionPolicyVersionId.ShouldBe(concession.VersionId);
		assignment.CollectionPolicyVersionId.ShouldBe(collection.VersionId);
		using (var resolution = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false))
		{
			var rules = await GetRequiredService<CollectionPolicyResolver>().ResolveEffectiveRulesForStudentAsync(input.StudentId, input.StandardFeeStructureId);
			rules.Policy!.Id.ShouldBe(collectionId);
			await resolution.CompleteAsync();
		}
		(await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId)).Assignment!.Policies.ShouldContain(p => p.VersionId == concession.VersionId);
		(await service.AssignAsync(input)).Outcome.ShouldBe("AlreadyAssigned");
	}

	[Fact]
	public async Task Assignment_Policy_Edit_Preserves_Evidence_Dues_Payments_And_Rejects_Stale_Save()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync(amount: 1000m);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var saved = await service.AssignAsync(input);
		var assignments = GetRequiredService<IRepository<StudentFeeStructure, Guid>>();
		StudentFeeStructure originalAssignment;
		using (var hydration = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			originalAssignment = await assignments.GetAsync(saved.AssignmentId);
			// Hydrate a historical assignment pin; the removed setup flow cannot create one.
			var structure = await GetRequiredService<IRepository<FeeStructure, Guid>>().GetAsync(input.StandardFeeStructureId);
			var historicPolicy = await GetRequiredService<IProrationPolicyAppService>().CreateAsync(new() {
				AcademicYearId = input.AcademicYearId, PolicyName = "Historical proration", Status = "Active",
				FeeHeadRules = [new() { FeeHeadId = structure.FeeStructureDetails.Single().FeeHeadId, RuleType = "Daily" }] });
			var historicVersion = (await GetRequiredService<IProrationPolicyAppService>()
				.GetVersionsAsync(historicPolicy.Id, new())).Items.Single().Id;
			typeof(StudentFeeStructure).GetProperty(nameof(StudentFeeStructure.ProrationPolicyId))!.SetValue(originalAssignment, historicPolicy.Id);
			typeof(StudentFeeStructure).GetProperty(nameof(StudentFeeStructure.ProrationPolicyVersionId))!.SetValue(originalAssignment, historicVersion);
			await assignments.UpdateAsync(originalAssignment, autoSave: true);
			await hydration.CompleteAsync();
		}
		var originalSnapshot = originalAssignment.AssignmentSnapshotJson;
		var originalProrationVersion = originalAssignment.ProrationPolicyVersionId;
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var before = await dues.GetListAsync(x => x.StudentFeeStructureId == saved.AssignmentId);
		before[0].ApplyPayment(100m);
		await dues.UpdateAsync(before[0], autoSave: true);
		var read = await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId);
		var concession = (await service.GetPolicyOptionsAsync(input.StudentId, input.AcademicYearId)).Single(p => p.Family == "concession");
		var update = new AssignmentPolicyUpdateDto { AcademicYearId = input.AcademicYearId, ConcurrencyStamp = read.ConcurrencyStamp, Reason = "Student policy exception",
			Policies = new[] { "concession", "refund", "collection" }.Select(family => new AssignmentPolicySelectionDto {
				Family = family, VersionId = family == "concession" ? concession.VersionId : read.Assignment!.Policies.FirstOrDefault(p => p.Family == family)?.VersionId }).ToList() };
		var changed = await service.UpdatePoliciesAsync(input.StudentId, update);
		changed.Assignment!.Policies.ShouldContain(p => p.VersionId == concession.VersionId);
		changed.ConcurrencyStamp.ShouldNotBe(read.ConcurrencyStamp);
		changed.Assignment.Lines.Sum(x => x.PaidAmount).ShouldBe(100m);
		changed.Assignment.Lines.Sum(x => x.OutstandingAmount).ShouldBe(900m);
		var aggregate = await assignments.GetAsync(saved.AssignmentId);
		aggregate.AssignmentSnapshotJson.ShouldBe(originalSnapshot);
		aggregate.ProrationPolicyVersionId.ShouldBe(originalProrationVersion);
		aggregate.ExtraProperties["AssignedPolicyHistory"].ToString().ShouldContain("Student policy exception");
		(await dues.GetListAsync(x => x.StudentFeeStructureId == saved.AssignmentId)).Select(x => x.Id).Order().ShouldBe(before.Select(x => x.Id).Order());
		(await Should.ThrowAsync<BusinessException>(() => service.UpdatePoliciesAsync(input.StudentId, update))).Code.ShouldBe(AssignmentErrorCodes.StalePreview);
	}

	[Fact]
	public async Task Independent_Proration_Still_Simulates_But_Legacy_Structures_Do_Not_Supply_Assignment_Policies()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync(withProration: true, amount: 1000m);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		(await service.GetCandidatesAsync(input.StudentId, input.AcademicYearId)).Single().Policies.ShouldNotContain(p => p.Family == "proration");
		var legacy = await GetRequiredService<IRepository<FeeStructure, Guid>>().GetAsync(input.StandardFeeStructureId);
		var policies = GetRequiredService<IProrationPolicyAppService>();
		var policy = await policies.GetAsync(legacy.ProrationPolicyId!.Value);
		var headId = policy.FeeHeadRules.Single().FeeHeadId;
		policy.FeeHeadRules.Single().ApplyOnAdmission.ShouldBeFalse();
		policy.FeeHeadRules.Single().EarlyWithdrawalCutoffDay.ShouldBe(15);
		var clone = await policies.CloneAsync(policy.Id, new());
		clone.FeeHeadRules.Single().ApplyOnAdmission.ShouldBeFalse();
		clone.FeeHeadRules.Single().EarlyWithdrawalBeforeCutoffAction.ShouldBe("HalfCharge");
		var result = await policies.SimulateAsync(policy.Id, new() { FeeHeadId = headId, ScheduledAmount = 3000m,
			CoverageStartDate = new(2026, 4, 1), CoverageEndDate = new(2026, 5, 31),
			ChargeStartDate = new(2026, 4, 1), ChargeEndDate = new(2026, 4, 15) });
		result.ProratedAmount.ShouldBe(750m);
		(await service.GetPolicyOptionsAsync(input.StudentId, input.AcademicYearId)).ShouldNotContain(p => p.Family == "proration");
		var preview = await service.PreviewAsync(input);
		preview.Policies.ShouldNotContain(p => p.Family == "proration");
		preview.ProratedTotal.ShouldBe(1000m);
		input.PreviewToken = preview.PreviewToken;
		var saved = await service.AssignAsync(input);
		var read = await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId);
		var concession = (await service.GetPolicyOptionsAsync(input.StudentId, input.AcademicYearId)).Single(p => p.Family == "concession");
		var update = new AssignmentPolicyUpdateDto { AcademicYearId = input.AcademicYearId, ConcurrencyStamp = read.ConcurrencyStamp, Reason = "Apply concession policy",
			Policies = new[] { "concession", "refund", "collection" }.Select(family => new AssignmentPolicySelectionDto { Family = family,
				VersionId = family == "concession" ? concession.VersionId : read.Assignment!.Policies.FirstOrDefault(p => p.Family == family)?.VersionId }).ToList() };
		var unchanged = await service.UpdatePoliciesAsync(input.StudentId, update);
		unchanged.Assignment!.Policies.ShouldContain(p => p.Family == "concession" && p.VersionId == concession.VersionId);
		unchanged.Assignment!.Policies.ShouldNotContain(p => p.Family == "proration");
		var aggregate = await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetAsync(saved.AssignmentId);
		aggregate.ProrationPolicyId.ShouldBeNull();
		aggregate.ProrationPolicyVersionId.ShouldBeNull();
		(await GetRequiredService<IRepository<FeeStructure, Guid>>().GetAsync(legacy.Id)).ProrationPolicyVersionId.ShouldBe(policy.CurrentVersionId);
		update.Policies.Add(new() { Family = "proration", VersionId = null });
		(await Should.ThrowAsync<BusinessException>(() => service.UpdatePoliciesAsync(input.StudentId, update))).Code.ShouldBe(AssignmentErrorCodes.InvalidInput);
	}

	[Theory]
	[InlineData("collection", true)]
	[InlineData("unknown", false)]
	[InlineData("proration", false)]
	[InlineData("proration", true)]
	[InlineData("refund", false)]
	public async Task Assignment_Policy_Rejects_Removed_Collection_Unknown_Family_And_Foreign_Version(string family, bool clear)
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync();
		input.PolicyOverrideReason = "Synthetic validation";
		input.Policies = [new() { Family = family, VersionId = clear ? null : Guid.NewGuid() }];
		await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentFeeStructureAppService>().PreviewAsync(input));
	}
}
