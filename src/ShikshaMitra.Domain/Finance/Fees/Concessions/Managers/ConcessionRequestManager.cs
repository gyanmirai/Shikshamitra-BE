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
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<FinancePolicyVersion, Guid> financePolicyVersionRepository,
	ConcessionPolicyCalculationService concessionPolicyCalculationService,
	IGuidGenerator guidGenerator,
	ILogger<ConcessionRequestManager> logger) : DomainService
{
	public async Task<ConcessionRequest> CreatePendingAsync(
		Guid? tenantId,
		Guid academicYearId,
		Guid studentId,
		Guid concessionPolicyId,
		Guid concessionPolicyVersionId,
		Guid concessionTypeId,
		string? remarks,
		IReadOnlyList<FeeHeadConcessionInput> feeHeadConcessions,
		Guid? studentFeeStructureId = null,
		Guid? sourceRequestId = null,
		Guid? actorId = null,
		string? idempotencyKey = null,
		string? payloadHash = null,
		bool allowIncomplete = false,
		bool enforcePendingUniqueness = true,
		IReadOnlyList<DueFeeConcessionInput>? dueFeeConcessions = null)
	{
		var student = await LoadStudentAsync(studentId);
		var concessionType = await LoadConcessionTypeAsync(concessionTypeId, academicYearId);
		var assignment = await ResolveAssignmentAsync(studentId, academicYearId, studentFeeStructureId);
		var policy = await LoadPolicySnapshotAsync(
			assignment, concessionPolicyId, concessionPolicyVersionId, requireActive: true);
		if (dueFeeConcessions is { Count: > 0 } && feeHeadConcessions.Count > 0)
			throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);

		if (enforcePendingUniqueness && await concessionRequestRepository.HasPendingForStudentAndTypeAsync(
			studentId, concessionTypeId, academicYearId))
		{
			throw new BusinessException(FinanceConcessionErrorCodes.DuplicatePendingRequest);
		}
		if (sourceRequestId.HasValue)
		{
			var source = await concessionRequestRepository.FindAsync(sourceRequestId.Value)
				?? throw new BusinessException(FinanceConcessionErrorCodes.ScopeMismatch);
			if (source.StudentId != studentId || source.AcademicYearId != academicYearId ||
				source.ConcessionTypeId != concessionTypeId ||
				!actorId.HasValue || source.CreatorId != actorId ||
				source.Status is not (ConcessionRequestStatus.Rejected or ConcessionRequestStatus.Cancelled))
				throw new BusinessException(FinanceConcessionErrorCodes.InvalidTransition);
		}

		var dueFees = await dueFeeRepository.GetListAsync(df =>
			df.StudentId == studentId && df.StudentFeeStructureId == assignment.Id);

		var requestId = guidGenerator.Create();
		var request = new ConcessionRequest(
			requestId,
			tenantId,
			studentId,
			assignment.Id,
			concessionPolicyId,
			concessionPolicyVersionId,
			concessionTypeId,
			academicYearId,
			student.AcademicBoardId ?? Guid.Empty,
			student.AcademicMediumId,
			student.StandardId!.Value,
			student.DivisionId!.Value,
			remarks,
			ConcessionRequestStatus.Draft,
			sourceRequestId,
			idempotencyKey,
			payloadHash);

		if (feeHeadConcessions.Count == 0 && (dueFeeConcessions?.Count ?? 0) == 0 && !allowIncomplete)
			throw new BusinessException(FinanceConcessionErrorCodes.DiscountExceedsBase)
				.WithData("Message", "At least one fee head concession is required");
		foreach (var m in dueFeeConcessions is { Count: > 0 }
			? BuildDueFeeMappings(tenantId, requestId, dueFeeConcessions, dueFees, policy,
				await GetPermittedFeeHeadIdsAsync(assignment, dueFees, policy))
			: feeHeadConcessions.Count == 0 ? [] : BuildFeeHeadMappings(
				tenantId, requestId, feeHeadConcessions, dueFees, policy,
				await GetPermittedFeeHeadIdsAsync(assignment, dueFees, policy), allowIncomplete))
		{
			request.ConcessionFeeHeadMappings.Add(m);
		}
		if (request.ConcessionFeeHeadMappings.Count > 0)
			PrepareSubmissionEvidence(request, concessionType, dueFees, policy, dueFeeConcessions);

		await concessionRequestRepository.InsertAsync(request, autoSave: false);
		logger.LogDebug("Concession request {RequestId} created for student {StudentId}", request.Id, studentId);
		return request;
	}

	public async Task<ConcessionRequestPreviewResult> PreviewAsync(
		Guid? tenantId,
		Guid academicYearId,
		Guid studentId,
		Guid concessionPolicyId,
		Guid concessionPolicyVersionId,
		Guid concessionTypeId,
		IReadOnlyList<FeeHeadConcessionInput> feeHeadConcessions,
		Guid? studentFeeStructureId = null,
		IReadOnlyList<DueFeeConcessionInput>? dueFeeConcessions = null)
	{
		await LoadStudentAsync(studentId);
		await LoadConcessionTypeAsync(concessionTypeId, academicYearId);
		var assignment = await ResolveAssignmentAsync(studentId, academicYearId, studentFeeStructureId);
		var policy = await LoadPolicySnapshotAsync(
			assignment, concessionPolicyId, concessionPolicyVersionId, requireActive: true);
		if (dueFeeConcessions is { Count: > 0 } && feeHeadConcessions.Count > 0)
			throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);
		var dueFees = await dueFeeRepository.GetListAsync(df =>
			df.StudentId == studentId && df.StudentFeeStructureId == assignment.Id);
		var mappings = dueFeeConcessions is { Count: > 0 }
			? BuildDueFeeMappings(tenantId, Guid.Empty, dueFeeConcessions, dueFees, policy,
				await GetPermittedFeeHeadIdsAsync(assignment, dueFees, policy))
			: BuildFeeHeadMappings(
			tenantId,
			Guid.Empty,
			feeHeadConcessions,
			dueFees,
			policy,
			await GetPermittedFeeHeadIdsAsync(assignment, dueFees, policy));
		return new ConcessionRequestPreviewResult(
			assignment,
			policy,
			mappings,
			dueFeeConcessions is { Count: > 0 }
				? dueFees.Where(x => dueFeeConcessions.Any(input => input.DueFeeId == x.Id)).ToList()
				: GetSelectedEligibleDueFees(dueFees, policy, mappings.Select(x => x.FeeHeadId)).ToList());
	}

	public async Task ReplaceFeeHeadMappingsAsync(
		ConcessionRequest request,
		Guid concessionPolicyId,
		Guid concessionPolicyVersionId,
		IReadOnlyList<FeeHeadConcessionInput> feeHeadConcessions,
		bool allowIncomplete = false,
		IReadOnlyList<DueFeeConcessionInput>? dueFeeConcessions = null)
	{
		request.EnsureDraft();
		var assignment = await studentFeeStructureRepository.GetAsync(request.StudentFeeStructureId);
		var policy = await LoadPolicySnapshotAsync(
			assignment, concessionPolicyId, concessionPolicyVersionId, requireActive: true);
		if (dueFeeConcessions is { Count: > 0 } && feeHeadConcessions.Count > 0)
			throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);
		request.ChangePolicySnapshot(concessionPolicyId, concessionPolicyVersionId);
		if (feeHeadConcessions.Count == 0 && (dueFeeConcessions?.Count ?? 0) == 0)
		{
			request.ReplaceFeeHeadMappings([]);
			request.ClearSubmissionEvidence();
			return;
		}
		var dueFees = await dueFeeRepository.GetListAsync(df =>
			df.StudentId == request.StudentId && df.StudentFeeStructureId == assignment.Id);
		var mappings = dueFeeConcessions is { Count: > 0 }
			? BuildDueFeeMappings(request.TenantId, request.Id, dueFeeConcessions, dueFees, policy,
				await GetPermittedFeeHeadIdsAsync(assignment, dueFees, policy))
			: BuildFeeHeadMappings(
			request.TenantId,
			request.Id,
			feeHeadConcessions,
			dueFees,
			policy,
			await GetPermittedFeeHeadIdsAsync(assignment, dueFees, policy), allowIncomplete);
		request.ReplaceFeeHeadMappings(mappings);
		var type = await LoadConcessionTypeAsync(request.ConcessionTypeId, request.AcademicYearId);
		PrepareSubmissionEvidence(request, type, dueFees, policy, dueFeeConcessions);
	}

	public Task RefreshDraftAsync(ConcessionRequest request)
	{
		request.EnsureDraft();
		var dueInputs = ReadDueFeeEvidence(request).Select(x => new DueFeeConcessionInput(
			x.DueFeeId, x.FeeHeadId, x.Amount)).ToList();
		var inputs = request.ConcessionFeeHeadMappings.Select(x => new FeeHeadConcessionInput(
			x.FeeHeadId,
			x.UserSpecifiedConcessionAmountType,
			x.UserSpecifiedConcessionAmountValue)).ToList();
		if (dueInputs.Count > 0) inputs.Clear();
		return ReplaceFeeHeadMappingsAsync(
			request, request.ConcessionPolicyId, request.ConcessionPolicyVersionId, inputs,
			allowIncomplete: false, dueFeeConcessions: dueInputs);
	}

	public async Task<ConcessionRequestContextResult> GetContextAsync(
		Guid academicYearId,
		Guid studentId,
		Guid? studentFeeStructureId = null)
	{
		var student = await LoadStudentAsync(studentId);
		var assignment = await ResolveAssignmentAsync(studentId, academicYearId, studentFeeStructureId);
		var feeStructure = await feeStructureRepository.GetAsync(assignment.FeeStructureStandardId);
		var includePaid = false;
		if (assignment.ConcessionPolicyId.HasValue && assignment.ConcessionPolicyVersionId.HasValue)
		{
			var policy = await LoadPolicySnapshotAsync(
				assignment, assignment.ConcessionPolicyId.Value, assignment.ConcessionPolicyVersionId.Value);
			includePaid = policy.ApplyOn == ConcessionApplyOn.AllFees;
		}
		var dueFees = await dueFeeRepository.GetListAsync(df =>
			df.StudentId == studentId &&
			df.StudentFeeStructureId == assignment.Id &&
			(includePaid ? df.OriginalAmount > df.NetAmount : df.OutstandingAmount > 0m));
		return new ConcessionRequestContextResult(student, assignment, feeStructure, dueFees);
	}

	public async Task<ConcessionRequestLiveEvaluation> EvaluateSubmittedAsync(ConcessionRequest request)
	{
		var assignment = await studentFeeStructureRepository.GetAsync(request.StudentFeeStructureId);
		if (assignment.StudentId != request.StudentId || assignment.AcademicYearId != request.AcademicYearId)
			throw new BusinessException(FinanceConcessionErrorCodes.ScopeMismatch);
		var policy = await LoadPolicySnapshotAsync(
			assignment, request.ConcessionPolicyId, request.ConcessionPolicyVersionId);
		var submittedDueIds = request.GetSelectedDueFeeIds().ToHashSet();
		if (submittedDueIds.Count == 0)
			throw new BusinessException(FinanceConcessionErrorCodes.ConfigurationSnapshotInvalid);
		var dueFees = await dueFeeRepository.GetListAsync(df =>
			df.StudentId == request.StudentId &&
			df.StudentFeeStructureId == assignment.Id &&
			submittedDueIds.Contains(df.Id));
		if (dueFees.Count != submittedDueIds.Count)
			throw new BusinessException(FinanceConcessionErrorCodes.PreviewChanged);

		var dueEvidence = ReadDueFeeEvidence(request);
		var inputs = request.ConcessionFeeHeadMappings
			.Where(mapping => dueFees.Any(dueFee =>
				dueFee.FeeHeadId == mapping.FeeHeadId && GetConcessionCapacity(dueFee, policy) > 0m))
			.Select(x => new FeeHeadConcessionInput(
				x.FeeHeadId,
				x.UserSpecifiedConcessionAmountType,
				x.UserSpecifiedConcessionAmountValue))
			.ToList();
		var mappings = dueEvidence.Count > 0
			? BuildDueFeeMappings(request.TenantId, request.Id,
				dueEvidence.Select(x => new DueFeeConcessionInput(x.DueFeeId, x.FeeHeadId, x.Amount)).ToList(),
				dueFees, policy, dueFees.Select(x => x.FeeHeadId).Distinct().ToList(), enforceApplyOn: false)
			: inputs.Count == 0
			? []
			: BuildFeeHeadMappings(
				request.TenantId,
				request.Id,
				inputs,
				dueFees,
				policy,
				dueFees.Select(x => x.FeeHeadId).Distinct().ToList());
		var evaluatedHeadIds = mappings.Select(x => x.FeeHeadId).ToHashSet();
		mappings.AddRange(request.ConcessionFeeHeadMappings
			.Where(x => !evaluatedHeadIds.Contains(x.FeeHeadId))
			.Select(x => new ConcessionFeeHeadMapping(
				guidGenerator.Create(),
				request.TenantId,
				request.Id,
				x.FeeHeadId,
				0m,
				0m,
				0m,
				x.UserSpecifiedConcessionAmountType,
				x.UserSpecifiedConcessionAmountValue)));
		return new ConcessionRequestLiveEvaluation(assignment, policy, mappings, dueFees);
	}

	public List<ConcessionDueFeeAllocation> BuildDueFeeAllocations(
		Guid? tenantId,
		Guid concessionRequestId,
		ConcessionPolicySnapshot policy,
		IReadOnlyList<FeeHeadApprovalInput> feeHeadApprovals,
		List<DueFee> studentDueFees,
		IReadOnlyList<ConcessionFeeHeadMapping>? requestedMappings = null,
		IReadOnlyList<ConcessionFeeHeadMapping>? liveMaximumMappings = null)
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
			var liveMaximum = liveMaximumMappings?.FirstOrDefault(x => x.FeeHeadId == group.FeeHeadId);
			if (liveMaximumMappings is not null && liveMaximum is null)
				throw new BusinessException(FinanceConcessionErrorCodes.PreviewChanged)
					.WithData("FeeHeadId", group.FeeHeadId);
			if (liveMaximum is not null && group.ApprovedConcessionAmount > liveMaximum.FeeHeadConcessionAmount)
				throw new BusinessException(FinanceConcessionErrorCodes.PreviewChanged)
					.WithData("FeeHeadId", group.FeeHeadId)
					.WithData("LiveMaximum", liveMaximum.FeeHeadConcessionAmount);
			var selectedDues = new List<DueFee>();
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

				if (GetConcessionCapacity(dueFee, policy) <= 0)
				{
					throw new BusinessException(FinanceConcessionErrorCodes.ConcessionOnFullyPaidDueFee)
						.WithData("DueFeeId", line.DueFeeId);
				}
				selectedDues.Add(dueFee);
			}

			if (selectedDues.Count == 0 ||
				group.ApprovedConcessionAmount > selectedDues.Sum(x => GetConcessionCapacity(x, policy)))
				throw new BusinessException(FinanceConcessionErrorCodes.DiscountExceedsBase)
					.WithData("FeeHeadId", group.FeeHeadId);

			var shares = ConcessionAmountAllocator.AllocateProportionally(
				selectedDues.Select(x => new ConcessionAmountCapacity(x.Id, GetConcessionCapacity(x, policy))).ToList(),
				group.ApprovedConcessionAmount);
			foreach (var share in shares.Where(x => x.Amount > 0m))
			{
				allocations.Add(new ConcessionDueFeeAllocation(
					guidGenerator.Create(),
					tenantId,
					concessionRequestId,
					share.Key,
					group.FeeHeadId,
					share.Amount));
			}
			if (shares.Sum(x => x.Amount) != group.ApprovedConcessionAmount)
				throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule)
					.WithData("FeeHeadId", group.FeeHeadId);
		}

		return allocations;
	}

	public List<FeeHeadApprovalInput> BuildAutoApproveAllocations(
		ConcessionRequest request,
		List<DueFee> studentDueFees,
		ConcessionPolicySnapshot policy)
	{
		var result = new List<FeeHeadApprovalInput>();

		foreach (var mapping in request.ConcessionFeeHeadMappings)
		{
			var unpaid = studentDueFees
				.Where(df => df.FeeHeadId == mapping.FeeHeadId && GetConcessionCapacity(df, policy) > 0)
				.OrderBy(df => df.Year)
				.ThenBy(df => df.Month)
				.ToList();

			if (unpaid.Count == 0)
			{
				continue;
			}

			var total = Math.Min(mapping.FeeHeadConcessionAmount,
				unpaid.Sum(x => GetConcessionCapacity(x, policy)));
			var lines = unpaid.Select(dueFee => new DueFeeAllocationInput(
				dueFee.Id, 0m, dueFee.ConcurrencyStamp)).ToList();

			result.Add(new FeeHeadApprovalInput(mapping.FeeHeadId, total, lines));
		}

		return result;
	}

	public List<ConcessionDueFeeAllocation> BuildSubmittedDueFeeAllocations(
		ConcessionRequest request,
		List<DueFee> liveDueFees,
		ConcessionPolicySnapshot policy)
	{
		var evidence = ReadDueFeeEvidence(request);
		if (evidence.Count == 0 || evidence.Select(x => x.DueFeeId).Distinct().Count() != evidence.Count)
			throw new BusinessException(FinanceConcessionErrorCodes.ConfigurationSnapshotInvalid);
		var dues = liveDueFees.ToDictionary(x => x.Id);
		var allocations = new List<ConcessionDueFeeAllocation>();
		foreach (var line in evidence)
		{
			if (!dues.TryGetValue(line.DueFeeId, out var due) || due.FeeHeadId != line.FeeHeadId ||
				line.RequestedAmount <= 0m || GetConcessionCapacity(due, policy) < line.RequestedAmount)
				throw new BusinessException(FinanceConcessionErrorCodes.PreviewChanged)
					.WithData("DueFeeId", line.DueFeeId);
			allocations.Add(new ConcessionDueFeeAllocation(guidGenerator.Create(), request.TenantId,
				request.Id, line.DueFeeId, line.FeeHeadId, line.RequestedAmount));
		}
		foreach (var mapping in request.ConcessionFeeHeadMappings)
		{
			if (allocations.Where(x => x.FeeHeadId == mapping.FeeHeadId).Sum(x => x.ConcessionAmount) !=
				mapping.FeeHeadConcessionAmount)
				throw new BusinessException(FinanceConcessionErrorCodes.ConfigurationSnapshotInvalid);
		}
		return allocations;
	}

	public decimal CalculateDiscount(decimal baseAmount, DiscountBasis basis, decimal value)
	{
		if (basis == DiscountBasis.Percentage)
		{
			if (value > 100)
			{
				throw new BusinessException(FinanceConcessionErrorCodes.PercentageOver100);
			}

			return ShikshaMitra.Finance.WholeRupeeAmounts.RoundUpWithin(baseAmount * value / 100m, baseAmount);
		}

		return value;
	}

	private List<ConcessionFeeHeadMapping> BuildDueFeeMappings(
		Guid? tenantId,
		Guid requestId,
		IReadOnlyList<DueFeeConcessionInput> inputs,
		List<DueFee> dueFees,
		ConcessionPolicySnapshot policy,
		IReadOnlyCollection<Guid> permittedFeeHeadIds,
		bool enforceApplyOn = true)
	{
		if (policy.ValueSource != ConcessionValueSource.RequestEnteredWithinLimits || inputs.Count == 0)
			throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);
		if (inputs.Any(x => x.DueFeeId == Guid.Empty || x.FeeHeadId == Guid.Empty || x.Amount <= 0m) ||
			inputs.Select(x => x.DueFeeId).Distinct().Count() != inputs.Count)
			throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);
		var eligible = (enforceApplyOn ? GetEligibleDueFees(dueFees, policy.ApplyOn) : dueFees)
			.Where(x => permittedFeeHeadIds.Contains(x.FeeHeadId) && GetConcessionCapacity(x, policy) > 0m)
			.ToDictionary(x => x.Id);
		var basis = policy.CalculationMethod == ConcessionCalculationMethod.Percentage
			? DiscountBasis.Percentage : DiscountBasis.FlatAmount;
		if (policy.CalculationMethod == ConcessionCalculationMethod.FullWaiver)
			throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);
		if (basis == DiscountBasis.FlatAmount && policy.MaximumFixedAmount.HasValue &&
			inputs.Sum(x => x.Amount) > policy.MaximumFixedAmount.Value)
			throw new BusinessException(ConcessionPolicyErrorCodes.LimitExceeded);
		var evaluated = inputs.Select(input =>
		{
			if (!eligible.TryGetValue(input.DueFeeId, out var dueFee) || dueFee.FeeHeadId != input.FeeHeadId)
				throw new BusinessException(FinanceConcessionErrorCodes.DueFeeNotFound)
					.WithData("DueFeeId", input.DueFeeId);
			if (basis == DiscountBasis.FlatAmount && decimal.Truncate(input.Amount) != input.Amount)
				throw new BusinessException(FinanceConcessionErrorCodes.WholeRupeeAmountRequired)
					.WithData("DueFeeId", input.DueFeeId)
					.WithData("FeeHeadId", input.FeeHeadId);
			if (basis == DiscountBasis.FlatAmount && input.Amount > GetConcessionCapacity(dueFee, policy))
				throw new BusinessException(FinanceConcessionErrorCodes.DiscountExceedsBase)
					.WithData("DueFeeId", input.DueFeeId);
			var discount = concessionPolicyCalculationService.Calculate(
				policy, GetConcessionCapacity(dueFee, policy), basis, input.Amount);
			return (Input: input, DueFee: dueFee, Discount: discount);
		}).ToList();
		if (evaluated.Sum(x => x.Discount) <= 0m)
			throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);
		if (basis == DiscountBasis.FlatAmount && policy.MaximumFixedAmount.HasValue &&
			evaluated.Sum(x => x.Discount) > policy.MaximumFixedAmount.Value)
			throw new BusinessException(ConcessionPolicyErrorCodes.LimitExceeded);
		return evaluated.GroupBy(x => x.Input.FeeHeadId).Select(group =>
		{
			var baseAmount = group.Sum(x => GetConcessionCapacity(x.DueFee, policy));
			var discount = group.Sum(x => x.Discount);
			return new ConcessionFeeHeadMapping(
				guidGenerator.Create(), tenantId, requestId, group.Key, baseAmount, discount,
				baseAmount - discount, basis, 0m);
		}).ToList();
	}

	public static List<DueFeeConcessionEvidence> ReadDueFeeEvidence(ConcessionRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.RequestInputSnapshotJson)) return [];
		try
		{
			return (JsonSerializer.Deserialize<List<DueFeeConcessionEvidence>>(
				request.RequestInputSnapshotJson) ?? []).Where(x => x.DueFeeId != Guid.Empty).ToList();
		}
		catch (JsonException)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.ConfigurationSnapshotInvalid);
		}
	}

	private List<ConcessionFeeHeadMapping> BuildFeeHeadMappings(
		Guid? tenantId,
		Guid concessionRequestId,
		IReadOnlyList<FeeHeadConcessionInput> inputs,
		List<DueFee> dueFees,
		ConcessionPolicySnapshot policy,
		IReadOnlyCollection<Guid> applicableFeeHeadIds,
		bool allowIncomplete = false)
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
		if (requestEnteredFixed && inputs.Any(x =>
			x.BasedOn != DiscountBasis.FlatAmount || (!allowIncomplete && x.Amount <= 0m)))
			throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);
		var fractionalHead = requestEnteredFixed
			? inputs.FirstOrDefault(x => decimal.Truncate(x.Amount) != x.Amount) : null;
		if (fractionalHead is not null)
			throw new BusinessException(FinanceConcessionErrorCodes.WholeRupeeAmountRequired)
				.WithData("FeeHeadId", fractionalHead.FeeHeadId);
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

			var baseAmount = headDueFees.Sum(df => GetConcessionCapacity(df, policy));
			var amount = policyFixedTotal || requestEnteredFixed
				? 0m
				: allowIncomplete &&
				  policy.ValueSource == ConcessionValueSource.RequestEnteredWithinLimits &&
				  input.Amount <= 0m
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
			var totalFixed = allowIncomplete && requestEnteredFixed && requestedTotal <= 0m
				? 0m
				: concessionPolicyCalculationService.Calculate(
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

	private async Task<List<Guid>> GetPermittedFeeHeadIdsAsync(
		StudentFeeStructure assignment, List<DueFee> dues, ConcessionPolicySnapshot policy)
	{
		var dueHeadIds = dues.Select(x => x.FeeHeadId).Distinct().ToList();
		var heads = await feeHeadRepository.GetListAsync(x => dueHeadIds.Contains(x.Id));
		var permitted = heads.Where(x => x.AcademicYearId == assignment.AcademicYearId &&
			x.IsActive && x.IsConcessionApplied).Select(x => x.Id).ToHashSet();
		if (assignment.ConcessionFeeHeadScope == ConcessionFeeHeadScope.SelectedFeeHeads)
			permitted.IntersectWith(assignment.GetConcessionApplicableFeeHeadIds());
		return permitted.ToList();
	}

	public static List<DueFee> GetEligibleDueFees(List<DueFee> dueFees, ConcessionApplyOn applyOn)
	{
		var outstanding = dueFees
			.Where(x => GetConcessionCapacity(x, applyOn) > 0m)
			.OrderBy(x => x.InstallmentSequence.HasValue ? 0 : 1)
			.ThenBy(x => x.InstallmentSequence ?? int.MaxValue)
			.ThenBy(x => x.DemandDate ?? x.CoverageStartDate ?? x.ScheduleDueDate ?? x.DueDate)
			.ThenBy(x => x.Year)
			.ThenBy(x => x.Month)
			.ThenBy(x => x.Id)
			.ToList();
		if (outstanding.Count == 0 || applyOn is ConcessionApplyOn.AllOutstanding or ConcessionApplyOn.AllFees) return outstanding;
		var first = outstanding[0];
		return applyOn == ConcessionApplyOn.CurrentInstallment
			? outstanding.Where(x => IsSameInstallmentCohort(x, first)).ToList()
			: outstanding.Where(x => !IsSameInstallmentCohort(x, first)).ToList();
	}

	public static decimal GetConcessionCapacity(DueFee dueFee, ConcessionPolicySnapshot policy) =>
		GetConcessionCapacity(dueFee, policy.ApplyOn);

	private static decimal GetConcessionCapacity(DueFee dueFee, ConcessionApplyOn applyOn) =>
		applyOn == ConcessionApplyOn.AllFees
			? Math.Max(0m, dueFee.OriginalAmount - dueFee.NetAmount)
			: dueFee.OutstandingAmount;

	private static bool IsSameInstallmentCohort(DueFee candidate, DueFee first)
	{
		if (first.PaymentTermInstallmentId.HasValue)
			return candidate.PaymentTermInstallmentId == first.PaymentTermInstallmentId;
		if (first.PaymentTermVersionId.HasValue && first.InstallmentSequence.HasValue)
			return candidate.PaymentTermVersionId == first.PaymentTermVersionId &&
				candidate.InstallmentSequence == first.InstallmentSequence;
		return candidate.Year == first.Year && candidate.Month == first.Month;
	}

	private static List<(FeeHeadConcessionInput Input, decimal Base, decimal Amount)> AllocateTotal(
		List<(FeeHeadConcessionInput Input, decimal Base, decimal Amount)> items,
		decimal total)
	{
		var shares = ConcessionAmountAllocator.AllocateProportionally(
			items.Select(x => new ConcessionAmountCapacity(x.Input.FeeHeadId, x.Base)).ToList(), total)
			.ToDictionary(x => x.Key, x => x.Amount);
		return items
			.OrderBy(x => x.Input.FeeHeadId)
			.Select(x => (x.Input, x.Base, shares[x.Input.FeeHeadId]))
			.ToList();
	}

	private async Task<ConcessionPolicySnapshot> LoadPolicySnapshotAsync(
		StudentFeeStructure assignment,
		Guid expectedPolicyId,
		Guid expectedPolicyVersionId,
		bool requireActive = false)
	{
		var version = await financePolicyVersionRepository.FindAsync(expectedPolicyVersionId)
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
		if (version.PolicyType != "concession" || version.PolicyId != expectedPolicyId ||
			version.AcademicYearId != assignment.AcademicYearId)
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
		var snapshot = JsonSerializer.Deserialize<ConcessionPolicySnapshot>(version.SnapshotJson)
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
		if (requireActive)
		{
			if (snapshot.Status != ConcessionPolicyStatus.Active)
				throw new BusinessException(ConcessionPolicyErrorCodes.InvalidStatus);
			if (await financePolicyVersionRepository.AnyAsync(x =>
				x.PolicyType == "concession" && x.PolicyId == expectedPolicyId &&
				x.VersionNumber > version.VersionNumber))
				throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
		}
		return snapshot;
	}

	private async Task<StudentFeeStructure> ResolveAssignmentAsync(
		Guid studentId,
		Guid academicYearId,
		Guid? requestedAssignmentId)
	{
		var assignments = await studentFeeStructureRepository.GetListAsync(x =>
			x.StudentId == studentId && x.AcademicYearId == academicYearId);
		StudentFeeStructure assignment;
		if (requestedAssignmentId.HasValue)
		{
			assignment = assignments.SingleOrDefault(x => x.Id == requestedAssignmentId.Value)
				?? throw new BusinessException(FinanceConcessionErrorCodes.ScopeMismatch);
		}
		else
		{
			if (assignments.Count == 0)
			{
				var hasLegacyAssignment = await studentFeeStructureRepository.AnyAsync(x =>
					x.StudentId == studentId && x.AcademicYearId == Guid.Empty);
				throw new BusinessException(hasLegacyAssignment
					? FinanceConcessionErrorCodes.LegacyRepairRequired
					: FinanceConcessionErrorCodes.FeeStructureAssignmentRequired,
					message: hasLegacyAssignment
						? "The student's existing fee structure assignment requires repair before a concession can be requested."
						: "No fee structure is assigned to this student for the selected academic year. Assign a fee structure before creating a concession request.");
			}
			if (assignments.Count > 1)
				throw new BusinessException(FinanceConcessionErrorCodes.AmbiguousAssignment);
			assignment = assignments[0];
		}

		return assignment;
	}

	private static IEnumerable<DueFee> GetSelectedEligibleDueFees(
		IEnumerable<DueFee> dueFees,
		ConcessionPolicySnapshot policy,
		IEnumerable<Guid> selectedFeeHeadIds)
	{
		var selected = selectedFeeHeadIds.ToHashSet();
		return GetEligibleDueFees(dueFees.Where(x => selected.Contains(x.FeeHeadId)).ToList(), policy.ApplyOn);
	}

	private void PrepareSubmissionEvidence(
		ConcessionRequest request,
		ConcessionType concessionType,
		IEnumerable<DueFee> dueFees,
		ConcessionPolicySnapshot policy,
		IReadOnlyList<DueFeeConcessionInput>? dueFeeConcessions = null)
	{
		var selectedDues = dueFeeConcessions is { Count: > 0 }
			? dueFeeConcessions.Select(x => x.DueFeeId)
			: GetSelectedEligibleDueFees(
			dueFees,
			policy,
			request.ConcessionFeeHeadMappings.Select(x => x.FeeHeadId)).Select(x => x.Id);
		var inputSnapshot = dueFeeConcessions is { Count: > 0 }
			? JsonSerializer.Serialize(dueFeeConcessions.OrderBy(x => x.DueFeeId).Select(x =>
			{
				var due = dueFees.First(df => df.Id == x.DueFeeId);
				var basis = policy.CalculationMethod == ConcessionCalculationMethod.Percentage
					? DiscountBasis.Percentage : DiscountBasis.FlatAmount;
				return new DueFeeConcessionEvidence(x.DueFeeId, x.FeeHeadId, x.Amount,
					concessionPolicyCalculationService.Calculate(policy, GetConcessionCapacity(due, policy), basis, x.Amount));
			}))
			: JsonSerializer.Serialize(request.ConcessionFeeHeadMappings
			.OrderBy(x => x.FeeHeadId)
			.Select(x => new
			{
				x.FeeHeadId,
				BasedOn = x.UserSpecifiedConcessionAmountType,
				Value = x.UserSpecifiedConcessionAmountValue,
				RequestedAmount = x.FeeHeadConcessionAmount
			}));
		var requirementsSnapshot = JsonSerializer.Serialize(concessionType.ConcessionTypeDocuments
			.OrderBy(x => x.Id)
			.Select(x => new ConcessionTypeRequirementSnapshot(
				x.Id,
				x.DocumentName,
				x.IsRequired,
				x.MaxFileSizeBytes,
				x.AcceptedFormats.Select(y => y.DocumentType).OrderBy(y => y).ToList())));
		request.PrepareSubmissionEvidence(selectedDues, inputSnapshot, requirementsSnapshot);
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
public record DueFeeConcessionInput(Guid DueFeeId, Guid FeeHeadId, decimal Amount);
public record DueFeeConcessionEvidence(Guid DueFeeId, Guid FeeHeadId, decimal Amount, decimal RequestedAmount);

public record ConcessionRequestPreviewResult(
	StudentFeeStructure Assignment,
	ConcessionPolicySnapshot Policy,
	IReadOnlyList<ConcessionFeeHeadMapping> Mappings,
	IReadOnlyList<DueFee> EligibleDueFees);

public record ConcessionRequestLiveEvaluation(
	StudentFeeStructure Assignment,
	ConcessionPolicySnapshot Policy,
	IReadOnlyList<ConcessionFeeHeadMapping> Mappings,
	IReadOnlyList<DueFee> DueFees);

public record ConcessionRequestContextResult(
	Student Student,
	StudentFeeStructure Assignment,
	FeeStructure FeeStructure,
	IReadOnlyList<DueFee> EligibleDueFees);

public sealed record ConcessionTypeRequirementSnapshot(
	Guid DocumentId,
	string DocumentName,
	bool IsRequired,
	long MaxFileSizeBytes,
	IReadOnlyList<ShikshaMitra.Common.DocumentType> AcceptedFormats);

public record DueFeeAllocationInput(Guid DueFeeId, decimal ConcessionAmount, string ConcurrencyStamp);

public record FeeHeadApprovalInput(
	Guid FeeHeadId,
	decimal ApprovedConcessionAmount,
	IReadOnlyList<DueFeeAllocationInput> DueFeeAllocations);
