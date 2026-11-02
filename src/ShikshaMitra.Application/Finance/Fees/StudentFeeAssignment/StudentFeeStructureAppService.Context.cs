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
		var result = new List<AssignmentContextDto>();
		foreach (var studentId in input.StudentIds)
			result.Add(await AssignmentContextAsync(studentId, input.AcademicYearId));
		return result;
	}

	private async Task<AssignmentContextDto> AssignmentContextAsync(Guid studentId, Guid yearId)
	{
		// Scope failures are not converted to a context that could disclose another student's admission.
		var enrollment = await RequireEnrollmentAsync(studentId, yearId, false);
		var result = new AssignmentContextDto { StudentId = studentId, AcademicYearId = yearId };
		var existing = await studentFeeStructureRepository.FindAsync(a => a.StudentId == studentId && a.AcademicYearId == yearId);
		if (existing != null)
		{
			result.Status = "Assigned";
			result.AssignmentId = existing.Id;
			result.StructureId = existing.FeeStructureStandardId;
			result.FinanceFeeAccountId = existing.FinanceFeeAccountId;
			result.Source = existing.FinanceFeeAccountId.HasValue ? "Admission" : "Finance";
			return result;
		}
		try
		{
			var facts = await ReadAssignmentAdmissionAsync(studentId, yearId, enrollment.StandardId);
			if (facts == null) return result;
			result.Source = "Admission";
			result.ApplicationId = facts.Application.Id;
			result.FinanceFeeAccountId = facts.Account?.Id;
			result.JoiningDate = facts.JoiningDate;
			result.StructureId = facts.Selection?.StructureId;
			result.StructureRevision = facts.Selection?.RevisionNumber;
			result.SelectedOptionalFeeHeadIds = facts.Selection?.Heads.Where(h => h.IsOptional).Select(h => h.FeeHeadId).ToList() ?? [];
			var year = await academicYearRepository.GetAsync(yearId);
			result.ChargeStartDate = facts.JoiningDate.HasValue && facts.JoiningDate.Value.Date > year.StartDate.Date ? facts.JoiningDate.Value.Date : year.StartDate.Date;
			result.ChargeEndDate = year.EndDate.Date;
			if (facts.Account?.AssignmentSnapshotJson != null)
			{
				var saved = await ReadPreparedAdmissionAsync(facts, studentId, enrollment.Id);
				result.Status = "Prepared";
				result.PreparedAssignment = saved.Preview;
				result.ProrationPolicy = saved.Preview.Policies.FirstOrDefault(p => p.Family == "proration");
				result.VariableFeeHeads = SavedVariableAmounts(saved.Preview);
				return result;
			}
			result.Status = facts.Selection == null ? "Unassigned" : "Selected";
			var policy = await LazyServiceProvider.LazyGetRequiredService<AdmissionProrationPolicyResolver>().ResolveAsync(yearId);
			if (policy != null)
			{
				result.ProrationPolicy = ToAssignmentPolicy(policy);
				result.ProrationPolicy.Source = "FinanceDefault";
				var rules = JsonSerializer.Deserialize<ProrationPolicySnapshot>(policy.SnapshotJson)
					?? throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
				result.AdmissionFeeHeadIds = rules.Rules.Where(r => r.ApplyOnAdmission && r.RuleType != ProrationRuleType.FullCharge).Select(r => r.FeeHeadId).ToList();
				if (!facts.JoiningDate.HasValue && facts.Selection?.Heads.Any(h => !h.IsOneTime && result.AdmissionFeeHeadIds.Contains(h.FeeHeadId)) == true)
					throw new BusinessException(ProrationPolicyErrorCodes.JoiningDateRequired);
			}
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
					: "The admission selection or prepared financial records need review before assignment.";
		}
		return result;
	}

	private sealed record AssignmentAdmissionFacts(AdmissionApplication Application, AdmissionOffering Offering,
		AdmissionFeeSelectionSnapshot? Selection, FinanceFeeAccount? Account, DateTime? JoiningDate, string Revision);

	private async Task<AssignmentAdmissionFacts?> ReadAssignmentAdmissionAsync(Guid studentId, Guid yearId, Guid? standardId)
	{
		var applications = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionApplication, Guid>>().GetQueryableAsync();
		var offerings = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionOffering, Guid>>().GetQueryableAsync();
		var cycles = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionCycle, Guid>>().GetQueryableAsync();
		var matches = await AsyncExecuter.ToListAsync(from application in applications
			join offering in offerings on application.AdmissionOfferingId equals offering.Id
			join cycle in cycles on offering.AdmissionCycleId equals cycle.Id
			where application.TenantId == CurrentTenant.Id && offering.TenantId == CurrentTenant.Id && cycle.TenantId == CurrentTenant.Id &&
				(application.StudentId == studentId || application.EnrolledStudentId == studentId) && cycle.AcademicYearId == yearId
			select new { Application = application, Offering = offering });
		if (matches.Count == 0)
		{
			if (await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>()
				.AnyAsync(a => a.StudentId == studentId && a.AcademicYearId == yearId && a.AssignmentSnapshotJson != null))
				throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
			return null;
		}
		if (matches.Count != 1) throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
		var match = matches[0];
		if (match.Offering.StandardId != standardId || match.Application.Status is AdmissionApplicationStatus.Withdrawn or AdmissionApplicationStatus.Rejected or AdmissionApplicationStatus.OfferDeclined)
			throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
		var selection = match.Application.GetFeeSelection();
		if (selection != null && (selection.AcademicYearId != yearId || selection.StandardId != standardId))
			throw new BusinessException(AssignmentErrorCodes.StructureMismatch);
		var account = await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>()
			.FindAsync(a => a.ApplicationId == match.Application.Id && a.AcademicYearId == yearId);
		if (await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>().AnyAsync(a =>
			a.StudentId == studentId && a.AcademicYearId == yearId && a.ApplicationId != match.Application.Id &&
			(a.AssignmentSnapshotJson != null || a.StudentFeeStructureId.HasValue)))
			throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
		if (account != null && (account.TenantId != CurrentTenant.Id || account.StudentId.HasValue && account.StudentId != studentId ||
			account.ApplicantId != match.Application.ApplicantId || account.StandardId != standardId ||
			account.BoardId != match.Offering.BoardId || account.EducationMediumId != match.Offering.MediumId))
			throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
		var joiningDate = await LazyServiceProvider.LazyGetRequiredService<AdmissionJoiningDateReader>().ReadAsync(match.Application);
		return new(match.Application, match.Offering, selection, account, joiningDate, Hash(new
		{ ApplicationRevision = match.Application.ConcurrencyStamp, OfferingRevision = match.Offering.ConcurrencyStamp, JoiningDate = joiningDate, Selection = selection }));
	}

	private static List<VariableFeeHeadAmountDto> SavedVariableAmounts(StudentFeeAssignmentPreviewDto saved) =>
		saved.Heads.Where(h => h.Included && h.IsVariable).Select(h => new VariableFeeHeadAmountDto
		{ FeeHeadId = h.FeeHeadId, Amount = h.EffectiveAmount, ZeroAmountReason = h.ZeroAmountReason }).ToList();
}
