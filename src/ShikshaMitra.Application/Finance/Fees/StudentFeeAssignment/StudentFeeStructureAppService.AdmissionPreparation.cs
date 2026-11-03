using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Finance.Policies;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	private async Task<PreparedAssignment> PrepareAsync(AssignStudentFeeStructureDto input)
	{
		ValidateInput(input);
		var enrollment = await RequireEnrollmentAsync(input.StudentId, input.AcademicYearId, true);
		var facts = await ReadAssignmentAdmissionAsync(input.StudentId, input.AcademicYearId, enrollment.StandardId);
		if (facts == null) return await PrepareForContextAsync(input, null);
		if (facts.Selection != null && facts.Selection.StructureId != input.StandardFeeStructureId)
			throw new BusinessException(AssignmentErrorCodes.StructureMismatch);
		if (facts.Selection != null && !input.SelectedOptionalFeeHeadIds.Order().SequenceEqual(
			facts.Selection.Heads.Where(h => h.IsOptional).Select(h => h.FeeHeadId).Order()))
			throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
		if (facts.Account?.AssignmentSnapshotJson != null)
		{
			var saved = await ReadPreparedAdmissionAsync(facts, input.StudentId, enrollment.Id);
			ValidatePreparedRequest(input, saved.Preview);
			return saved;
		}
		var policy = await LazyServiceProvider.LazyGetRequiredService<AdmissionProrationPolicyResolver>().ResolveAsync(input.AcademicYearId);
		var student = await studentRepository.GetAsync(input.StudentId);
		// The account's existing unique application/year key serializes Admission and
		// Finance creation, including when both commands initially see no preparation.
		var account = facts.Account ?? new FinanceFeeAccount(GuidGenerator.Create(), CurrentTenant.Id, facts.Application.Id,
			facts.Application.ApplicantId, input.AcademicYearId, facts.Offering.BoardId, facts.Offering.MediumId,
			facts.Offering.StageId, enrollment.StandardId!.Value,
			string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(name => !string.IsNullOrWhiteSpace(name))),
			facts.Selection?.Currency ?? "INR", studentId: input.StudentId);
		account.SetDivision(enrollment.DivisionId);
		var prepared = await PrepareForContextAsync(input, new(account.Id, input.StudentId, enrollment.StandardId!.Value,
			facts.Offering.BoardId, facts.Offering.MediumId, facts.Selection?.RevisionNumber, facts.Revision, facts.JoiningDate,
			policy, facts.Application.Id, IsFinanceAssignment: true));
		prepared.Preview.EnrollmentId = enrollment.Id;
		var source = new AdmissionFinanceContext(facts.Application.Id, facts.Application.ApplicantId, input.AcademicYearId,
			facts.Offering.BoardId, facts.Offering.MediumId, facts.Offering.StageId, enrollment.StandardId.Value,
			enrollment.DivisionId, input.StudentId, account.ApplicantName, facts.Application.ApplicationNumber, "", facts.Revision,
			input.StandardFeeStructureId, prepared.Structure.RevisionNumber, input.SelectedOptionalFeeHeadIds, true, null, enrollment.Id, facts.JoiningDate);
		return prepared with { Account = account, AdmissionSource = source, NewAccount = facts.Account == null };
	}

	private async Task<PreparedAssignment> ReadPreparedAdmissionAsync(AssignmentAdmissionFacts facts, Guid studentId, Guid enrollmentId)
	{
		var account = facts.Account!;
		StudentFeeAssignmentPreviewDto saved;
		try { saved = JsonSerializer.Deserialize<StudentFeeAssignmentPreviewDto>(account.AssignmentSnapshotJson!)!; }
		catch (JsonException) { throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid); }
		if (saved == null || saved.AcademicYearId != account.AcademicYearId || saved.StructureId != account.StructureId ||
			saved.RevisionNumber != account.StructureRevision || account.StudentFeeStructureId.HasValue ||
			facts.Selection == null || saved.StructureId != facts.Selection.StructureId || saved.RevisionNumber != facts.Selection.RevisionNumber ||
			(saved.RegistrationJoiningDate.HasValue || saved.ProrationPolicyVersionId.HasValue) && saved.RegistrationJoiningDate?.Date != facts.JoiningDate?.Date ||
			!saved.Heads.Where(h => h.IsOptional && h.Included).Select(h => h.FeeHeadId).Order().SequenceEqual(facts.Selection.Heads.Where(h => h.IsOptional).Select(h => h.FeeHeadId).Order()))
			throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
		var rows = (await dueFeeRepository.GetListAsync(d => d.FinanceFeeAccountId == account.Id)).ToList();
		if (rows.Count == 0 || rows.Count != saved.Lines.Count || saved.Lines.Any(l => !l.DueFeeId.HasValue) ||
			saved.Lines.Select(l => l.DueFeeId).Distinct().Count() != rows.Count ||
			rows.Any(d => d.StudentFeeStructureId.HasValue || d.StudentId.HasValue && d.StudentId != studentId))
			throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
		var ordered = saved.Lines.Select(line => rows.SingleOrDefault(d => d.Id == line.DueFeeId)
			?? throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid)).ToList();
		for (var i = 0; i < ordered.Count; i++)
		{
			var due = ordered[i];
			var line = saved.Lines[i];
			if (due.FeeHeadId != line.FeeHeadId || due.OriginalAmount != line.ProratedAmount || due.ScheduledAmount != line.ScheduledAmount)
				throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
			line.PaidAmount = due.PaidAmount;
			line.OutstandingAmount = due.OutstandingAmount;
		}
		saved.StudentId = studentId;
		saved.EnrollmentId = enrollmentId;
		saved.AssignmentSource = "Admission";
		saved.ApplicationId = facts.Application.Id;
		var structure = await feeStructureRepository.GetAsync(saved.StructureId);
		var source = new AdmissionFinanceContext(facts.Application.Id, facts.Application.ApplicantId, account.AcademicYearId,
			account.BoardId, account.EducationMediumId, facts.Offering.StageId, account.StandardId, account.DivisionId, studentId,
			account.ApplicantName, facts.Application.ApplicationNumber, "", facts.Revision, saved.StructureId, saved.RevisionNumber,
			saved.Heads.Where(h => h.Included && h.IsOptional).Select(h => h.FeeHeadId).ToList(), true, null, enrollmentId, facts.JoiningDate);
		return new(structure, saved, ordered, Hash(new { Preview = saved, account.ConcurrencyStamp, facts.Revision,
			Dues = ordered.Select(d => new { d.Id, d.ConcurrencyStamp }) }), account, source, ReuseDues: true);
	}

	private static void ValidatePreparedRequest(AssignStudentFeeStructureDto input, StudentFeeAssignmentPreviewDto saved)
	{
		if (input.StandardFeeStructureId != saved.StructureId ||
			!input.SelectedOptionalFeeHeadIds.Order().SequenceEqual(saved.Heads.Where(h => h.Included && h.IsOptional).Select(h => h.FeeHeadId).Order()) ||
			input.Policies.Any(p => p.VersionId != saved.Policies.FirstOrDefault(s => s.Family == p.Family)?.VersionId) ||
			input.VariableFeeHeads.Count != SavedVariableAmounts(saved).Count || input.VariableFeeHeads.Any(v => !saved.Heads.Any(h =>
				h.Included && h.IsVariable && h.FeeHeadId == v.FeeHeadId && h.EffectiveAmount == v.Amount && h.ZeroAmountReason == v.ZeroAmountReason?.Trim())) ||
			input.FeeHeadDetails.Any(v => !saved.Heads.Any(h => h.Included && !h.IsVariable && h.FeeHeadId == v.FeeHeadId && h.EffectiveAmount == v.Amount)) ||
			input.ServicePeriods.Any(p => !saved.Heads.Any(h => h.Included && h.FeeHeadId == p.FeeHeadId &&
				h.ServicePeriod?.Source == p.Source && h.ServicePeriod.StartDate?.Date == p.StartDate?.Date &&
				h.ServicePeriod.EndDate?.Date == p.EndDate?.Date && h.ServicePeriod.SourceReferenceId == p.SourceReferenceId)) ||
			input.ChargePeriodSource != "AcademicYear" || input.ChargeStartDate.HasValue || input.ChargeEndDate.HasValue ||
			input.ChargePeriodSourceReferenceId.HasValue || !string.IsNullOrWhiteSpace(input.ChargePeriodOverrideReason) ||
			!string.IsNullOrWhiteSpace(input.OverrideReason) || !string.IsNullOrWhiteSpace(input.PolicyOverrideReason))
			throw new BusinessException(AssignmentErrorCodes.SourceUnavailable,
				"Fees have already been prepared in Admission. Complete their handoff or use a financial correction workflow.");
	}
}
