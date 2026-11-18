using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Finance.Policies;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	[Authorize(ViewPermission)]
	public async Task<List<AssignmentContextDto>> GetContextsAsync(AssignmentContextsInput input)
	{
		if (input == null || input.AcademicYearId == Guid.Empty || input.StudentIds == null ||
			input.StudentIds.Count is < 1 or > 100 || input.StudentIds.Any(id => id == Guid.Empty) ||
			input.StudentIds.Distinct().Count() != input.StudentIds.Count)
			throw new BusinessException(AssignmentErrorCodes.InvalidInput);
		var reads = await ReadContextsAsync(input);
		var result = new List<AssignmentContextDto>();
		foreach (var studentId in input.StudentIds)
			result.Add(await AssignmentContextAsync(studentId, input.AcademicYearId, reads, input.IncludeEditDetails));
		return result;
	}

	private async Task<AssignmentContextDto> AssignmentContextAsync(Guid studentId, Guid yearId, AssignmentContextReads reads, bool includeEditDetails)
	{
		// Scope failures are not converted to a context that could disclose another student's admission.
		var enrollment = reads.Enrollments[studentId];
		var result = new AssignmentContextDto { StudentId = studentId, AcademicYearId = yearId };
		var existing = reads.Assignments.GetValueOrDefault(studentId);
		if (existing != null)
		{
			result.Status = "Assigned";
			result.AssignmentId = existing.Id;
			result.StructureId = existing.FeeStructureStandardId;
			result.FinanceFeeAccountId = existing.FinanceFeeAccountId;
			result.Source = existing.FinanceFeeAccountId.HasValue ? "Admission" : "Finance";
			result.AssignmentConcurrencyStamp = existing.ConcurrencyStamp;
			if (!includeEditDetails) return result;
			result.StructureEditBlocker = reads.Blockers[existing.Id] ?? (!enrollment.IsActive ? InactiveEditBlocker : null);
			result.CanEditStructure = result.StructureEditBlocker == null;
			var snapshot = SavedAssignment(existing);
			result.AssignedAssignment = snapshot;
			result.StructureRevision = snapshot?.RevisionNumber;
			if (snapshot != null)
			{
				result.JoiningDate = snapshot.RegistrationJoiningDate;
				result.ChargeStartDate = snapshot.ChargeStartDate;
				result.ChargeEndDate = snapshot.ChargeEndDate;
				result.ApplicationId = snapshot.ApplicationId;
				result.SelectedOptionalFeeHeadIds = snapshot.Heads.Where(h => h.Included && h.IsOptional).Select(h => h.FeeHeadId).ToList();
				result.VariableFeeHeads = SavedVariableAmounts(snapshot);
				var ids = new[] { existing.CollectionPolicyVersionId, existing.ConcessionPolicyVersionId, existing.RefundPolicyVersionId, existing.ProrationPolicyVersionId }
					.Where(id => id.HasValue).Select(id => id!.Value).ToArray();
				var policies = reads.Policies.Where(p => Enumerable.Contains(ids, p.Id)).ToList();
				snapshot.Policies = policies.Select(p => snapshot.Policies.FirstOrDefault(s => s.VersionId == p.Id) ?? ToAssignmentPolicy(p)).ToList();
				result.ProrationPolicy = snapshot.Policies.FirstOrDefault(p => p.Family == "proration");
				if (existing.ExtraProperties.TryGetValue("AssignedPolicyReason", out var reason)) snapshot.PolicyOverrideReason = reason?.ToString();
			}
			return result;
		}
		try
		{
			var source = reads.Admissions.GetValueOrDefault(studentId);
			if (source?.Error != null) throw source.Error;
			var facts = source?.Facts;
			if (facts == null)
			{
				await PopulateDefaultProrationAsync(result, reads);
				return result;
			}
			result.Source = "Admission";
			result.ApplicationId = facts.Application.Id;
			result.FinanceFeeAccountId = facts.Account?.Id;
			result.JoiningDate = facts.JoiningDate;
			result.StructureId = facts.Selection?.StructureId;
			result.StructureRevision = facts.Selection?.RevisionNumber;
			result.SelectedOptionalFeeHeadIds = facts.Selection?.Heads.Where(h => h.IsOptional).Select(h => h.FeeHeadId).ToList() ?? [];
			var year = reads.Year!;
			result.ChargeStartDate = facts.JoiningDate.HasValue && facts.JoiningDate.Value.Date > year.StartDate.Date ? facts.JoiningDate.Value.Date : year.StartDate.Date;
			result.ChargeEndDate = year.EndDate.Date;
			if (facts.Account?.AssignmentSnapshotJson != null)
			{
				var saved = await ReadPreparedAdmissionAsync(facts, studentId, enrollment.Id, reads.PreparedDues, reads.Structures);
				result.Status = "Prepared";
				result.PreparedAssignment = saved.Preview;
				result.ProrationPolicy = saved.Preview.Policies.FirstOrDefault(p => p.Family == "proration");
				result.VariableFeeHeads = SavedVariableAmounts(saved.Preview);
				return result;
			}
			result.Status = facts.Selection == null ? "Unassigned" : "Selected";
			await PopulateDefaultProrationAsync(result, reads);
			if (!facts.JoiningDate.HasValue && facts.Selection?.Heads.Any(h => !h.IsOneTime && result.AdmissionFeeHeadIds.Contains(h.FeeHeadId)) == true)
				throw new BusinessException(ProrationPolicyErrorCodes.JoiningDateRequired);
			if (facts.JoiningDate?.Date > year.EndDate.Date) throw new BusinessException(ProrationPolicyErrorCodes.JoiningDateInvalid);
		}
		catch (BusinessException error)
		{
			result.Status = "NeedsAttention";
			result.ErrorCode = error.Code;
			result.Blocker = error.Code == ProrationPolicyErrorCodes.JoiningDateRequired
				? "Confirm the actual joining date in Admission before preparing these fees."
				: error.Code == ProrationPolicyErrorCodes.JoiningDateInvalid
					? "Review the admission joining date for this academic year."
					: "The Finance policy configuration or prepared assignment records need review before assignment.";
		}
		return result;
	}

	private async Task PopulateDefaultProrationAsync(AssignmentContextDto result, AssignmentContextReads? reads = null)
	{
		var resolver = LazyServiceProvider.LazyGetRequiredService<AdmissionProrationPolicyResolver>();
		var policy = reads == null ? await resolver.ResolveAsync(result.AcademicYearId)
			: await (reads.DefaultProration ??= resolver.ResolveAsync(result.AcademicYearId));
		if (policy == null) return;
		ProrationPolicySnapshot rules;
		try
		{
			rules = JsonSerializer.Deserialize<ProrationPolicySnapshot>(policy.SnapshotJson)
				?? throw new JsonException();
			if (rules.Rules == null || rules.Rules.Any(rule => rule == null) ||
				rules.PolicyId != policy.PolicyId || rules.AcademicYearId != result.AcademicYearId) throw new JsonException();
		}
		catch (JsonException) { throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid); }
		result.ProrationPolicy = ToAssignmentPolicy(policy);
		result.ProrationPolicy.Source = "FinanceDefault";
		result.AdmissionFeeHeadIds = rules.Rules.Where(r => r.ApplyOnAdmission && r.RuleType != ProrationRuleType.FullCharge)
			.Select(r => r.FeeHeadId).ToList();
	}

	private sealed record AssignmentAdmissionFacts(AdmissionApplication Application, AdmissionOffering Offering,
		AdmissionFeeSelectionSnapshot? Selection, FinanceFeeAccount? Account, DateTime? JoiningDate, string Revision);

	private async Task<AssignmentAdmissionFacts?> ReadAssignmentAdmissionAsync(Guid studentId, Guid yearId, Guid? standardId)
	{
		var source = (await ReadAssignmentAdmissionsAsync(new Dictionary<Guid, Guid?> { [studentId] = standardId }, yearId))[studentId];
		if (source.Error != null) throw source.Error;
		return source.Facts;
	}

	private static List<VariableFeeHeadAmountDto> SavedVariableAmounts(StudentFeeAssignmentPreviewDto saved) =>
		saved.Heads.Where(h => h.Included && h.IsVariable).Select(h => new VariableFeeHeadAmountDto
		{ FeeHeadId = h.FeeHeadId, Amount = h.EffectiveAmount, ZeroAmountReason = h.ZeroAmountReason }).ToList();
}
