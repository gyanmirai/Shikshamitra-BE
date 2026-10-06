using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	private const string PolicyPermission = ShikshaMitraSubmodulePermissions.Finance.Fees_StructureAssign.OverridePolicies;
	private static readonly string[] SelectablePolicyFamilies = ["concession", "refund", "collection"];
	private static readonly string[] PolicyFamilies = ["concession", "refund", "collection", "proration"];

	private static void ValidatePolicies(List<AssignmentPolicySelectionDto> selections)
	{
		if (selections == null || selections.Count > 3 || selections.Any(x => x == null || !SelectablePolicyFamilies.Contains(x.Family) || x.VersionId == Guid.Empty) ||
			selections.Select(x => x.Family).Distinct().Count() != selections.Count)
			throw new BusinessException(AssignmentErrorCodes.InvalidInput);
	}

	[Authorize(ViewPermission)]
	public async Task<List<AssignmentPolicyDto>> GetPolicyOptionsAsync(Guid studentId, Guid academicYearId)
	{
		await AuthorizationService.CheckAsync(PolicyPermission);
		await RequireEnrollmentAsync(studentId, academicYearId, false);
		var versions = await financePolicyVersionRepository.GetListAsync(x => x.AcademicYearId == academicYearId);
		return versions.Where(x => SelectablePolicyFamilies.Contains(x.PolicyType)).GroupBy(x => new { x.PolicyType, x.PolicyId })
			.Select(g => g.OrderByDescending(x => x.VersionNumber).ThenByDescending(x => x.CreationTime).First())
			.Where(x => x.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) && IsEffectiveVersion(x))
			.OrderBy(x => x.PolicyName).Select(ToAssignmentPolicy).ToList();
	}

	private bool IsEffectiveVersion(FinancePolicyVersion version)
	{
		try
		{
			using var json = JsonDocument.Parse(version.SnapshotJson);
			if (json.RootElement.ValueKind != JsonValueKind.Object) return false;
			foreach (var field in new[] { "EffectiveFrom", "EffectiveTo" })
				if (json.RootElement.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTime(out var date) &&
					(field == "EffectiveFrom" ? date.Date > Clock.Now.Date : date.Date < Clock.Now.Date)) return false;
			return true;
		}
		catch (JsonException) { return false; }
	}

	private static AssignmentPolicyDto ToAssignmentPolicy(FinancePolicyVersion version) => new() {
		Family = version.PolicyType, PolicyId = version.PolicyId, VersionId = version.Id, Version = version.VersionNumber, Name = version.PolicyName };

	// Proration is evaluated through independent events; templates never supply it to new assignments.
	private static Dictionary<string, Guid?> StructurePolicyVersions(FeeStructure structure) => new() {
		["concession"] = structure.ConcessionPolicyVersionId, ["collection"] = structure.CollectionPolicyVersionId,
		["refund"] = structure.RefundPolicyVersionId, ["proration"] = null };

	private async Task<Dictionary<string, FinancePolicyVersion?>> ResolveAssignedPoliciesAsync(
		FeeStructure structure, Guid year, List<AssignmentPolicySelectionDto> selections, string? reason)
	{
		ValidatePolicies(selections);
		var defaults = StructurePolicyVersions(structure);
		var defaultIds = new Dictionary<string, Guid?> { ["concession"] = structure.ConcessionPolicyId, ["collection"] = structure.CollectionPolicyId,
			["refund"] = structure.RefundPolicyId, ["proration"] = null };
		var changes = selections.Where(x => x.VersionId != defaults[x.Family] || (!x.VersionId.HasValue && defaultIds[x.Family].HasValue)).ToList();
		if (changes.Count > 0)
		{
			await AuthorizationService.CheckAsync(PolicyPermission);
			if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) throw new BusinessException(AssignmentErrorCodes.InvalidInput);
		}
		var options = changes.Any(x => x.VersionId.HasValue) ? await GetPolicyOptionsAsyncForYear(year) : [];
		var resolved = new Dictionary<string, FinancePolicyVersion?>();
		foreach (var family in PolicyFamilies)
		{
			var versionId = selections.FirstOrDefault(x => x.Family == family)?.VersionId;
			if (!selections.Any(x => x.Family == family)) versionId = defaults[family];
			if (!versionId.HasValue) {
				if (family == "collection" || (!selections.Any(x => x.Family == family) && defaultIds[family].HasValue)) throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
				resolved[family] = null; continue;
			}
			var version = await financePolicyVersionRepository.FindAsync(versionId.Value);
			if (version == null || version.PolicyType != family || version.AcademicYearId != year ||
				(versionId == defaults[family] && version.PolicyId != defaultIds[family]) ||
				(changes.Any(x => x.Family == family) && !options.Contains(version.Id))) throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
			resolved[family] = version;
		}
		return resolved;
	}

	private async Task<List<Guid>> GetPolicyOptionsAsyncForYear(Guid year)
	{
		var versions = await financePolicyVersionRepository.GetListAsync(x => x.AcademicYearId == year);
		return versions.GroupBy(x => new { x.PolicyType, x.PolicyId }).Select(g => g.OrderByDescending(x => x.VersionNumber).ThenByDescending(x => x.CreationTime).First())
			.Where(x => x.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) && IsEffectiveVersion(x)).Select(x => x.Id).ToList();
	}

	[Authorize(PolicyPermission)]
	public async Task<StudentFeeStructureResponseDto> UpdatePoliciesAsync(Guid studentId, AssignmentPolicyUpdateDto input)
	{
		await AuthorizationService.CheckAsync(ViewPermission);
		ValidatePolicies(input.Policies);
		if (input.Policies.Count != 3 || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
			throw new BusinessException(AssignmentErrorCodes.InvalidInput);
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		await RequireEnrollmentAsync(studentId, input.AcademicYearId, false);
		var assignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x => x.StudentId == studentId && x.AcademicYearId == input.AcademicYearId)
			?? throw new BusinessException(AssignmentErrorCodes.NotAssigned);
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || input.ConcurrencyStamp != assignment.ConcurrencyStamp)
			throw new BusinessException(AssignmentErrorCodes.StalePreview);
		var current = new Dictionary<string, Guid?> { ["concession"] = assignment.ConcessionPolicyVersionId, ["collection"] = assignment.CollectionPolicyVersionId,
			["refund"] = assignment.RefundPolicyVersionId, ["proration"] = assignment.ProrationPolicyVersionId };
		var allowed = await GetPolicyOptionsAsyncForYear(input.AcademicYearId);
		var policies = new Dictionary<string, FinancePolicyVersion?>();
		foreach (var selection in input.Policies)
		{
			var version = selection.VersionId.HasValue ? await financePolicyVersionRepository.FindAsync(selection.VersionId.Value) : null;
			if ((selection.Family == "collection" && version == null) || (selection.VersionId.HasValue && (version == null || version.PolicyType != selection.Family ||
				version.AcademicYearId != input.AcademicYearId || (current[selection.Family] != version.Id && !allowed.Contains(version.Id)))))
				throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
			policies[selection.Family] = version;
		}
		if (input.Policies.Any(x => x.VersionId != current[x.Family]))
		{
			var concession = policies["concession"] == null ? null : JsonSerializer.Deserialize<ConcessionPolicySnapshot>(policies["concession"]!.SnapshotJson);
			assignment.ChangeAssignedPolicies(policies["concession"]?.PolicyId, policies["concession"]?.Id,
				concession?.FeeHeadScope ?? ConcessionFeeHeadScope.EntireFeeStructure, concession?.ApplicableFeeHeadIds ?? [],
				policies["collection"]!.PolicyId, policies["collection"]!.Id, policies["refund"]?.PolicyId, policies["refund"]?.Id,
				assignment.ProrationPolicyId, assignment.ProrationPolicyVersionId, input.Reason, CurrentUser.Id!.Value, Clock.Now);
			await studentFeeStructureRepository.UpdateAsync(assignment, autoSave: true);
		}
		await uow.CompleteAsync();
		return await GetByStudentIdAsync(studentId, input.AcademicYearId);
	}
}
