using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using System.Text.Json;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;

namespace ShikshaMitra.Finance.Fees.ConcessionRequests;

public class ConcessionRequestManager(
	IConcessionRequestRepository concessionRequestRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<ConcessionType, Guid> concessionTypeRepository,
	IRepository<DueFee, Guid> dueFeeRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<FinancePolicyVersion, Guid> financePolicyVersionRepository,
	ConcessionPolicyCalculationService concessionPolicyCalculationService,
	IGuidGenerator guidGenerator,
	ILogger<ConcessionRequestManager> logger) : DomainService
{
	public async Task<ConcessionRequest> CreatePendingAsync(
		Guid? tenantId,
		Guid academicYearId,
		Guid studentId,
		Guid concessionTypeId,
		string? remarks,
		IReadOnlyList<FeeHeadConcessionInput> feeHeadConcessions)
	{
		var student = await LoadStudentAsync(studentId);
		var concessionType = await LoadConcessionTypeAsync(concessionTypeId, academicYearId);
		var assignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x =>
			x.StudentId == studentId && x.AcademicYearId == academicYearId)
			?? await studentFeeStructureRepository.FirstOrDefaultAsync(x =>
				x.StudentId == studentId && x.AcademicYearId == Guid.Empty)
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyNotAttached);
		await EnsureAssignmentPolicySnapshotAsync(assignment, academicYearId);
		var policy = await LoadPolicySnapshotAsync(assignment);

		if (await concessionRequestRepository.HasPendingForStudentAndTypeAsync(
			studentId, concessionTypeId, academicYearId))
		{
			throw new BusinessException(FinanceConcessionErrorCodes.DuplicatePendingRequest);
		}

		var dueFees = await dueFeeRepository.GetListAsync(df => df.StudentId == studentId);

		var requestId = guidGenerator.Create();
		var request = new ConcessionRequest(
			requestId,
			tenantId,
			studentId,
			assignment.Id,
			assignment.ConcessionPolicyId,
			assignment.ConcessionPolicyVersionId,
			concessionTypeId,
			academicYearId,
			student.AcademicBoardId ?? Guid.Empty,
			student.AcademicMediumId,
			student.StandardId!.Value,
			student.DivisionId!.Value,
			remarks);

		foreach (var m in BuildFeeHeadMappings(
			tenantId, requestId, feeHeadConcessions, dueFees, policy,
			assignment.GetConcessionApplicableFeeHeadIds()))
		{
			request.ConcessionFeeHeadMappings.Add(m);
		}

		await concessionRequestRepository.InsertAsync(request, autoSave: false);
		logger.LogDebug("Concession request {RequestId} created for student {StudentId}", request.Id, studentId);
		return request;
	}

	public async Task<ConcessionRequestPreviewResult> PreviewAsync(
		Guid? tenantId,
		Guid academicYearId,
		Guid studentId,
		Guid concessionTypeId,
		IReadOnlyList<FeeHeadConcessionInput> feeHeadConcessions)
	{
		await LoadStudentAsync(studentId);
		await LoadConcessionTypeAsync(concessionTypeId, academicYearId);
		var assignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x =>
			x.StudentId == studentId && x.AcademicYearId == academicYearId)
			?? await studentFeeStructureRepository.FirstOrDefaultAsync(x =>
				x.StudentId == studentId && x.AcademicYearId == Guid.Empty)
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyNotAttached);
		await EnsureAssignmentPolicySnapshotAsync(assignment, academicYearId);
		var policy = await LoadPolicySnapshotAsync(assignment);
		var dueFees = await dueFeeRepository.GetListAsync(df => df.StudentId == studentId);
		var mappings = BuildFeeHeadMappings(
			tenantId,
			Guid.Empty,
			feeHeadConcessions,
			dueFees,
			policy,
			assignment.GetConcessionApplicableFeeHeadIds());
		return new ConcessionRequestPreviewResult(assignment, policy, mappings);
	}

	public async Task ReplaceFeeHeadMappingsAsync(
		ConcessionRequest request,
		IReadOnlyList<FeeHeadConcessionInput> feeHeadConcessions)
	{
		request.EnsurePending();
		var dueFees = await dueFeeRepository.GetListAsync(df => df.StudentId == request.StudentId);
		var assignment = await studentFeeStructureRepository.GetAsync(request.StudentFeeStructureId);
		var policy = await LoadPolicySnapshotAsync(assignment);
		var mappings = BuildFeeHeadMappings(
			request.TenantId,
			request.Id,
			feeHeadConcessions,
			dueFees,
			policy,
			assignment.GetConcessionApplicableFeeHeadIds());
		request.ReplaceFeeHeadMappings(mappings);
	}

	public List<ConcessionDueFeeAllocation> BuildDueFeeAllocations(
		Guid? tenantId,
		Guid concessionRequestId,
		IReadOnlyList<FeeHeadApprovalInput> feeHeadApprovals,
		List<DueFee> studentDueFees,
		IReadOnlyList<ConcessionFeeHeadMapping>? requestedMappings = null)
	{
		var allocations = new List<ConcessionDueFeeAllocation>();
		var seenDueFeeIds = new HashSet<Guid>();

		foreach (var group in feeHeadApprovals)
		{
			var requested = requestedMappings?.FirstOrDefault(x => x.FeeHeadId == group.FeeHeadId);
			if (requestedMappings is not null && requested is null)
				throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed)
					.WithData("FeeHeadId", group.FeeHeadId);
			if (group.ApprovedConcessionAmount <= 0m)
				throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule)
					.WithData("FeeHeadId", group.FeeHeadId);
			if (requested is not null && group.ApprovedConcessionAmount > requested.FeeHeadConcessionAmount)
				throw new BusinessException(ConcessionPolicyErrorCodes.LimitExceeded)
					.WithData("FeeHeadId", group.FeeHeadId);
			var allocatedForHead = 0m;
			foreach (var line in group.DueFeeAllocations)
			{
				if (!seenDueFeeIds.Add(line.DueFeeId))
					throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed)
						.WithData("DueFeeId", line.DueFeeId);
				var dueFee = studentDueFees.FirstOrDefault(df => df.Id == line.DueFeeId)
					?? throw new BusinessException(FinanceConcessionErrorCodes.DueFeeNotFound)
						.WithData("DueFeeId", line.DueFeeId);
				if (string.IsNullOrWhiteSpace(line.ConcurrencyStamp))
					throw new BusinessException(FinanceConcessionErrorCodes.ConcurrencyStampRequired)
						.WithData("DueFeeId", line.DueFeeId);
				if (!string.Equals(dueFee.ConcurrencyStamp, line.ConcurrencyStamp, StringComparison.Ordinal))
					throw new BusinessException(FinanceConcessionErrorCodes.ConcurrencyConflict)
						.WithData("DueFeeId", line.DueFeeId);

				if (dueFee.FeeHeadId != group.FeeHeadId)
				{
					throw new BusinessException(FinanceConcessionErrorCodes.DueFeeWrongFeeHead);
				}

				if (line.ConcessionAmount <= 0)
				{
					continue;
				}

				if (dueFee.OutstandingAmount <= 0)
				{
					throw new BusinessException(FinanceConcessionErrorCodes.ConcessionOnFullyPaidDueFee)
						.WithData("DueFeeId", line.DueFeeId);
				}
				if (line.ConcessionAmount > (decimal)dueFee.OutstandingAmount)
					throw new BusinessException(FinanceConcessionErrorCodes.DiscountExceedsBase)
						.WithData("DueFeeId", line.DueFeeId);
				allocatedForHead += line.ConcessionAmount;

				allocations.Add(new ConcessionDueFeeAllocation(
					guidGenerator.Create(),
					tenantId,
					concessionRequestId,
					line.DueFeeId,
					group.FeeHeadId,
					line.ConcessionAmount));
			}
			if (allocatedForHead != group.ApprovedConcessionAmount)
				throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule)
					.WithData("FeeHeadId", group.FeeHeadId);
		}

		return allocations;
	}

	public List<FeeHeadApprovalInput> BuildAutoApproveAllocations(
		ConcessionRequest request,
		List<DueFee> studentDueFees)
	{
		var result = new List<FeeHeadApprovalInput>();

		foreach (var mapping in request.ConcessionFeeHeadMappings)
		{
			var unpaid = studentDueFees
				.Where(df => df.FeeHeadId == mapping.FeeHeadId && df.OutstandingAmount > 0)
				.OrderBy(df => df.Year)
				.ThenBy(df => df.Month)
				.ToList();

			if (unpaid.Count == 0)
			{
				continue;
			}

			var total = Math.Min(mapping.FeeHeadConcessionAmount,
				unpaid.Sum(x => (decimal)x.OutstandingAmount));
			var remaining = total;
			var lines = new List<DueFeeAllocationInput>();

			foreach (var dueFee in unpaid)
			{
				if (remaining <= 0) break;
				var amount = Math.Min(remaining, (decimal)dueFee.OutstandingAmount);
				lines.Add(new DueFeeAllocationInput(dueFee.Id, amount, dueFee.ConcurrencyStamp));
				remaining -= amount;
			}

			result.Add(new FeeHeadApprovalInput(mapping.FeeHeadId, total, lines));
		}

		return result;
	}

	public decimal CalculateDiscount(decimal baseAmount, DiscountBasis basis, decimal value)
	{
		if (basis == DiscountBasis.Percentage)
		{
			if (value > 100)
			{
				throw new BusinessException(FinanceConcessionErrorCodes.PercentageOver100);
			}

			return Math.Round(baseAmount * value / 100m, 2, MidpointRounding.AwayFromZero);
		}

		return value;
	}

	private List<ConcessionFeeHeadMapping> BuildFeeHeadMappings(
		Guid? tenantId,
		Guid concessionRequestId,
		IReadOnlyList<FeeHeadConcessionInput> inputs,
		List<DueFee> dueFees,
		ConcessionPolicySnapshot policy,
		IReadOnlyCollection<Guid> applicableFeeHeadIds)
	{
		if (inputs.Count == 0)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.DiscountExceedsBase)
				.WithData("Message", "At least one fee head concession is required");
		}

		var duplicateHead = inputs.GroupBy(x => x.FeeHeadId).FirstOrDefault(x => x.Count() > 1);
		if (duplicateHead is not null)
			throw new BusinessException(FinanceConcessionErrorCodes.FeeHeadNotInStructure)
				.WithData("FeeHeadId", duplicateHead.Key);

		var permittedHeads = applicableFeeHeadIds.ToHashSet();
		var eligibleDueFees = GetEligibleDueFees(
			dueFees.Where(x => permittedHeads.Contains(x.FeeHeadId)).ToList(), policy.ApplyOn);
		var requestEnteredFixed = policy.ValueSource == ConcessionValueSource.RequestEnteredWithinLimits &&
			policy.CalculationMethod == ConcessionCalculationMethod.FixedAmount;
		var policyFixedTotal = policy.ValueSource == ConcessionValueSource.PolicyFixed &&
			policy.CalculationMethod == ConcessionCalculationMethod.FixedAmount;
		if (requestEnteredFixed && inputs.Any(x => x.BasedOn != DiscountBasis.FlatAmount || x.Amount <= 0m))
			throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);
		var candidates = new List<(FeeHeadConcessionInput Input, decimal Base, decimal Amount)>();
		foreach (var input in inputs)
		{
			if (!permittedHeads.Contains(input.FeeHeadId))
				throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed).WithData("FeeHeadId", input.FeeHeadId);
			var headDueFees = eligibleDueFees.Where(df => df.FeeHeadId == input.FeeHeadId).ToList();
			if (headDueFees.Count == 0)
			{
				throw new BusinessException(FinanceConcessionErrorCodes.FeeHeadNotInStructure)
					.WithData("FeeHeadId", input.FeeHeadId);
			}

			var baseAmount = headDueFees.Sum(df => (decimal)df.OutstandingAmount);
			var amount = policyFixedTotal || requestEnteredFixed
				? 0m
				: concessionPolicyCalculationService.Calculate(
					policy,
					baseAmount,
					policy.ValueSource == ConcessionValueSource.RequestEnteredWithinLimits ? input.BasedOn : null,
					policy.ValueSource == ConcessionValueSource.RequestEnteredWithinLimits ? input.Amount : null);
			candidates.Add((input, baseAmount, amount));
		}

		var totalBase = candidates.Sum(x => x.Base);
		if (policyFixedTotal || requestEnteredFixed)
		{
			var requestedTotal = requestEnteredFixed ? inputs.Sum(x => x.Amount) : (decimal?)null;
			var totalFixed = concessionPolicyCalculationService.Calculate(
				policy,
				totalBase,
				requestEnteredFixed ? DiscountBasis.FlatAmount : null,
				requestedTotal);
			candidates = AllocateTotal(candidates, totalFixed);
		}

		return candidates.Select(candidate =>
		{
			var displayedBasis = policy.ValueSource == ConcessionValueSource.RequestEnteredWithinLimits
				? candidate.Input.BasedOn
				: policy.CalculationMethod == ConcessionCalculationMethod.FixedAmount
					? DiscountBasis.FlatAmount : DiscountBasis.Percentage;
			var displayedValue = policy.ValueSource == ConcessionValueSource.RequestEnteredWithinLimits
				? requestEnteredFixed ? candidate.Amount : candidate.Input.Amount
				: policy.CalculationMethod switch
				{
					ConcessionCalculationMethod.Percentage => policy.PercentageValue ?? 0m,
					ConcessionCalculationMethod.FixedAmount => policy.FixedAmount ?? 0m,
					_ => 100m,
				};
			return new ConcessionFeeHeadMapping(
				guidGenerator.Create(),
				tenantId,
				concessionRequestId,
				candidate.Input.FeeHeadId,
				candidate.Base,
				candidate.Amount,
				candidate.Base - candidate.Amount,
				displayedBasis,
				displayedValue);
		}).ToList();
	}

	public static List<DueFee> GetEligibleDueFees(List<DueFee> dueFees, ConcessionApplyOn applyOn)
	{
		var outstanding = dueFees.Where(x => x.OutstandingAmount > 0).OrderBy(x => x.Year).ThenBy(x => x.Month).ToList();
		if (outstanding.Count == 0 || applyOn == ConcessionApplyOn.AllOutstanding) return outstanding;
		var first = outstanding[0];
		return applyOn == ConcessionApplyOn.CurrentInstallment
			? outstanding.Where(x => x.Year == first.Year && x.Month == first.Month).ToList()
			: outstanding.Where(x => x.Year > first.Year || (x.Year == first.Year && x.Month > first.Month)).ToList();
	}

	private static List<(FeeHeadConcessionInput Input, decimal Base, decimal Amount)> AllocateTotal(
		List<(FeeHeadConcessionInput Input, decimal Base, decimal Amount)> items,
		decimal total)
	{
		var baseTotal = items.Sum(x => x.Base);
		if (baseTotal <= 0) throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);
		var remaining = Math.Min(total, baseTotal);
		var result = new List<(FeeHeadConcessionInput, decimal, decimal)>();
		for (var i = 0; i < items.Count; i++)
		{
			var amount = i == items.Count - 1
				? remaining
				: Math.Min(items[i].Base, Math.Round(total * items[i].Base / baseTotal, 2, MidpointRounding.AwayFromZero));
			remaining -= amount;
			result.Add((items[i].Input, items[i].Base, amount));
		}
		return result;
	}

	private async Task<ConcessionPolicySnapshot> LoadPolicySnapshotAsync(StudentFeeStructure assignment)
	{
		var version = await financePolicyVersionRepository.FindAsync(assignment.ConcessionPolicyVersionId)
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
		if (version.PolicyType != "concession" || version.PolicyId != assignment.ConcessionPolicyId)
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
		return JsonSerializer.Deserialize<ConcessionPolicySnapshot>(version.SnapshotJson)
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
	}

	private async Task EnsureAssignmentPolicySnapshotAsync(StudentFeeStructure assignment, Guid academicYearId)
	{
		if (assignment.AcademicYearId != Guid.Empty &&
			assignment.ConcessionPolicyId != Guid.Empty &&
			assignment.ConcessionPolicyVersionId != Guid.Empty)
			return;

		var feeStructure = await feeStructureRepository.GetAsync(assignment.FeeStructureStandardId);
		if (!feeStructure.ConcessionPolicyId.HasValue || !feeStructure.ConcessionPolicyVersionId.HasValue)
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyNotAttached);
		var version = await financePolicyVersionRepository.FindAsync(feeStructure.ConcessionPolicyVersionId.Value)
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
		if (version.PolicyType != "concession" ||
			version.PolicyId != feeStructure.ConcessionPolicyId.Value ||
			version.AcademicYearId != academicYearId)
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);

		assignment.RefreshConcessionPolicySnapshot(
			academicYearId, version.PolicyId, version.Id,
			feeStructure.ConcessionFeeHeadScope, feeStructure.GetConcessionApplicableFeeHeadIds());
		await studentFeeStructureRepository.UpdateAsync(assignment, autoSave: true);
	}

	private async Task<Student> LoadStudentAsync(Guid studentId)
	{
		var student = await studentRepository.FindAsync(studentId)
			?? throw new BusinessException(FinanceConcessionErrorCodes.StudentNotFound)
				.WithData("StudentId", studentId);

		if (student.StandardId is null || student.DivisionId is null)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.ScopeMismatch)
				.WithData("StudentId", studentId);
		}

		return student;
	}

	private async Task<ConcessionType> LoadConcessionTypeAsync(Guid concessionTypeId, Guid academicYearId)
	{
		var type = await concessionTypeRepository.FindAsync(concessionTypeId)
			?? throw new BusinessException(FinanceConcessionErrorCodes.ConcessionTypeNotFound);

		if (type.AcademicYearId != academicYearId)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.ConcessionTypeNotFound);
		}

		if (!type.IsActive)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.Inactive)
				.WithData("ConcessionTypeId", concessionTypeId);
		}

		return type;
	}
}

public record FeeHeadConcessionInput(Guid FeeHeadId, DiscountBasis BasedOn, decimal Amount);

public record ConcessionRequestPreviewResult(
	StudentFeeStructure Assignment,
	ConcessionPolicySnapshot Policy,
	IReadOnlyList<ConcessionFeeHeadMapping> Mappings);

public record DueFeeAllocationInput(Guid DueFeeId, decimal ConcessionAmount, string ConcurrencyStamp);

public record FeeHeadApprovalInput(
	Guid FeeHeadId,
	decimal ApprovedConcessionAmount,
	IReadOnlyList<DueFeeAllocationInput> DueFeeAllocations);
