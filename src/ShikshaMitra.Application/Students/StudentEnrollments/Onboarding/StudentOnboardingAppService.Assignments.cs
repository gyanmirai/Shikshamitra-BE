using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Uow;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	public async Task<StudentOnboardingCaseDto> AssignGrNumberAsync(Guid id, AssignStudentOnboardingGrNumberDto input)
	{
		using var work = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var item = await GetCaseAsync(id);
		EnsureConcurrency(item, input.ConcurrencyStamp);
		var student = await studentRepository.GetAsync(item.StudentId);
		var existing = await allocationRepository.FindAsync(a => a.OnboardingCaseId == id && a.Status == StudentIdentifierAllocationStatus.Reserved);
		var current = student.GrNumber ?? student.GrNo?.ToString() ?? existing?.Value;
		var automaticByLock = current == null && (item.IsIdentifierLocked("gr") || await IsDivisionIdentifierGroupLockedAsync(item.DivisionId, item.AcademicYearId, "gr"));
		var manual = !automaticByLock && !string.IsNullOrWhiteSpace(input.ManualValue);
		if (current != null && (!manual || current == input.ManualValue!.Trim())) return await MapCaseAsync(item, true);
		if (current != null && (item.IsIdentifierLocked("gr") || student.IsIdentifierLocked("gr")))
			throw new BusinessException("Students:Onboarding:IdentifierLocked");
		if (current != null && string.IsNullOrWhiteSpace(input.Reason))
			throw new BusinessException(StudentOnboardingErrorCodes.WaiverReasonRequired);
		if (!string.IsNullOrWhiteSpace(student.GrNumber) || student.GrNo.HasValue)
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable).WithData("Reason", "Correct committed identifiers through Student Records.");
		var config = await configurationManager.GetAsync(item.AcademicYearId);
		var rule = config?.ResolveRule("grNumber", item, Clock.Now);
		var series = (config == null ? await seriesRepository.FindAsync(input.NumberSeriesId) : await GetConfiguredSeriesAsync(config, item, true))
			?? throw new BusinessException(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
		if (series.Id != input.NumberSeriesId || series.IdentifierType != StudentIdentifierType.GrNumber ||
			config == null && series.AcademicYearId != item.AcademicYearId || series.StandardId.HasValue && series.StandardId != item.IntendedStandardId)
			throw new BusinessException(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
		series.EnsureAvailable(Clock.Now);
		if (manual && config != null && (!rule.HasValue || !OnboardingRuleConfiguration.Boolean(rule.Value, "manualEntryAllowed") ||
			OnboardingRuleConfiguration.Text(config.Root.GetProperty("grNumber"), "assignmentMode") == "automatic"))
			throw OnboardingRuleConfiguration.Invalid("Manual GR entry is not allowed by the published rule.");
		long sequence; string value;
		if (manual)
		{
			sequence = input.ManualSequence ?? 0; value = input.ManualValue!.Trim();
			if (sequence == 0 && value.StartsWith(series.Prefix, StringComparison.Ordinal) && value.EndsWith(series.Suffix, StringComparison.Ordinal) && value.Length > series.Prefix.Length + series.Suffix.Length)
				long.TryParse(value.Substring(series.Prefix.Length, value.Length - series.Prefix.Length - series.Suffix.Length), out sequence);
			if (sequence > 0) { series.AdvancePastManualSequence(sequence); await seriesRepository.UpdateAsync(series, false); }
		}
		else
		{
			if (!automaticByLock && config != null && OnboardingRuleConfiguration.Text(config.Root.GetProperty("grNumber"), "assignmentMode") == "manual")
				throw OnboardingRuleConfiguration.Invalid("Enter a GR number under the manual assignment rule.");
			do { (sequence, value) = series.Allocate(Clock.Now); }
			while (await GrValueExistsAsync(value, student.Id, existing?.Id));
			await seriesRepository.UpdateAsync(series, false);
		}
		if (value.Length > 100 || await GrValueExistsAsync(value, student.Id, existing?.Id))
			throw new BusinessException(StudentOnboardingErrorCodes.IdentifierAlreadyAllocated);
		if (existing == null)
		{
			existing = new StudentIdentifierAllocation(GuidGenerator.Create(), CurrentTenant.Id, id, student.Id,
				series.Id, StudentIdentifierType.GrNumber, sequence, value, Clock.Now, manual, input.Reason);
			await allocationRepository.InsertAsync(existing, false);
		}
		else { existing.CorrectReservation(series.Id, sequence, value, input.Reason!, Clock.Now); await allocationRepository.UpdateAsync(existing, false); }
		item.RecordGrIdentifier(existing.Id, Clock.Now);
		await InvalidateApprovalAsync(id, "GR number prepared; review readiness again.");
		await AddEventAsync(id, current == null ? "GrNumberPrepared" : "GrNumberCorrected", $"GR number {value} prepared. {input.Reason}");
		await RefreshIdentifierStepAsync(item);
		await caseRepository.UpdateAsync(item, false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		var result = await MapCaseAsync(item, true); await work.CompleteAsync(); return result;
	}

	private async Task<bool> GrValueExistsAsync(string value, Guid studentId, Guid? allocationId)
	{
		var numeric = int.TryParse(value, out var number) ? (int?)number : null;
        if (await allocationRepository.AnyAsync(a => a.Id != allocationId && a.IdentifierType == StudentIdentifierType.GrNumber && a.Value == value) ||
            await studentRepository.AnyAsync(s => s.Id != studentId && (s.GrNumber == value || numeric.HasValue && s.GrNo == numeric))) return true;
        if (!numeric.HasValue) return false;
        var reservedValues = await AsyncExecuter.ToListAsync((await allocationRepository.GetQueryableAsync())
            .Where(a => a.Id != allocationId && a.IdentifierType == StudentIdentifierType.GrNumber).Select(a => a.Value));
        if (reservedValues.Any(v => int.TryParse(v, out var reservedNumber) && reservedNumber == numeric.Value)) return true;
        var canonicalValues = await AsyncExecuter.ToListAsync((await studentRepository.GetQueryableWithoutAutoIncludesAsync())
            .Where(s => s.Id != studentId && s.GrNumber != null).Select(s => s.GrNumber));
        return canonicalValues.Any(v => int.TryParse(v, out var assignedNumber) && assignedNumber == numeric.Value);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	public async Task<StudentOnboardingCaseDto> AssignRollNumberAsync(Guid id, AssignStudentOnboardingRollNumberDto input)
	{
		using var work = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var item = await GetCaseAsync(id); EnsureConcurrency(item, input.ConcurrencyStamp);
		if (!item.DivisionId.HasValue) throw new BusinessException(StudentOnboardingErrorCodes.PlacementInvalid);
		if (item.RollNumber.HasValue && item.RollNumber != input.RollNumber && item.IsIdentifierLocked("roll")) throw new BusinessException("Students:Onboarding:IdentifierLocked");
		var config = await configurationManager.GetAsync(item.AcademicYearId);
		var rule = config?.ResolveRule("rollNumber", item, Clock.Now);
		if (item.RollNumber.HasValue && item.RollNumber != input.RollNumber && rule.HasValue &&
			OnboardingRuleConfiguration.Boolean(rule.Value, "requireChangeReason") && string.IsNullOrWhiteSpace(input.Reason))
			throw new BusinessException(StudentOnboardingErrorCodes.WaiverReasonRequired);
		var frozen = await LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollRuleReader>().CaptureForCaseAsync(item);
		var proposal = await ProposeCaseRollAsync(item, input.RollNumber, input.RollAssignmentMode, input.Reason, frozen);
		if (proposal.RollNumber != input.RollNumber) throw new BusinessException("Students:Records:RollProposalChanged").WithData("ProposedRollNumber", proposal.RollNumber);
		item.RecordIdentifier(item.IdentifierAllocationId, proposal.RollNumber, Clock.Now);
		await LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollRuleReader>().RecordIssuedAsync(item.AcademicYearId, item.IntendedStandardId, item.DivisionId.Value, proposal.RollNumber);
		await InvalidateApprovalAsync(id, "Roll number prepared; review readiness again.");
		await RefreshIdentifierStepAsync(item);
		await caseRepository.UpdateAsync(item, false);
		await AddEventAsync(id, "RollNumberPrepared", $"Roll number {proposal.RollNumber} prepared. {input.Reason}");
		await CurrentUnitOfWork!.SaveChangesAsync(); var result = await MapCaseAsync(item, true); await work.CompleteAsync(); return result;
	}
}
