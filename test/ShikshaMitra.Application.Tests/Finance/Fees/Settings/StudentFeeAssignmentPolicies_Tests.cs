using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
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
		var rules = await GetRequiredService<CollectionPolicyResolver>().ResolveEffectiveRulesForStudentAsync(input.StudentId, input.StandardFeeStructureId);
		rules.Policy!.Id.ShouldBe(collectionId);
		(await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId)).Assignment!.Policies.ShouldContain(p => p.VersionId == concession.VersionId);
		(await service.AssignAsync(input)).Outcome.ShouldBe("AlreadyAssigned");
	}

	[Fact]
	public async Task Assignment_Policy_Edit_Preserves_Evidence_Dues_Payments_And_Rejects_Stale_Save()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var saved = await service.AssignAsync(input);
		var assignments = GetRequiredService<IRepository<StudentFeeStructure, Guid>>();
		var originalSnapshot = (await assignments.GetAsync(saved.AssignmentId)).AssignmentSnapshotJson;
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var before = await dues.GetListAsync(x => x.StudentFeeStructureId == saved.AssignmentId);
		before[0].ApplyPayment(100m);
		await dues.UpdateAsync(before[0], autoSave: true);
		var read = await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId);
		var concession = (await service.GetPolicyOptionsAsync(input.StudentId, input.AcademicYearId)).Single(p => p.Family == "concession");
		var update = new AssignmentPolicyUpdateDto { AcademicYearId = input.AcademicYearId, ConcurrencyStamp = read.ConcurrencyStamp, Reason = "Student policy exception",
			Policies = new[] { "concession", "refund", "collection", "proration" }.Select(family => new AssignmentPolicySelectionDto {
				Family = family, VersionId = family == "concession" ? concession.VersionId : read.Assignment!.Policies.FirstOrDefault(p => p.Family == family)?.VersionId }).ToList() };
		var changed = await service.UpdatePoliciesAsync(input.StudentId, update);
		changed.Assignment!.Policies.ShouldContain(p => p.VersionId == concession.VersionId);
		changed.ConcurrencyStamp.ShouldNotBe(read.ConcurrencyStamp);
		changed.Assignment.Lines.Sum(x => x.PaidAmount).ShouldBe(100m);
		changed.Assignment.Lines.Sum(x => x.OutstandingAmount).ShouldBe(900.01m);
		var aggregate = await assignments.GetAsync(saved.AssignmentId);
		aggregate.AssignmentSnapshotJson.ShouldBe(originalSnapshot);
		aggregate.ExtraProperties["AssignedPolicyHistory"].ToString().ShouldContain("Student policy exception");
		(await dues.GetListAsync(x => x.StudentFeeStructureId == saved.AssignmentId)).Select(x => x.Id).Order().ShouldBe(before.Select(x => x.Id).Order());
		(await Should.ThrowAsync<BusinessException>(() => service.UpdatePoliciesAsync(input.StudentId, update))).Code.ShouldBe(AssignmentErrorCodes.StalePreview);
	}

	[Theory]
	[InlineData("collection", true)]
	[InlineData("unknown", false)]
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
