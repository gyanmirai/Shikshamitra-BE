using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Students;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Applies (or requests approval for) a waiver against a due line's computed late fee.
/// Preserves the accrued assessment on DueFee without changing its principal balance.
/// </summary>
[RemoteService(false)]
[Authorize]
public class LateFeeWaiverAppService(
	IDueFeeRepository dueFeeRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<LateFeeWaiverApplication, Guid> waiverRepository,
	CollectionPolicyResolver policyResolver,
	LateFeeBalanceService lateFeeBalanceService,
	WaiverCalculationService waiverCalculationService,
	FinanceAuditWriter financeAuditWriter,
	IReadOnlyRepository<Student, Guid> studentRepository,
	IReadOnlyRepository<FeeHead, Guid> feeHeadRepository,
	FinanceScopeAccessService financeScopeAccessService,
	IClock clock
) : ApplicationService, ILateFeeWaiverAppService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ViewWaivers)]
	public async Task<PagedResultDto<LateFeeWaiverApplicationDto>> GetListAsync(GetLateFeeWaiversInput input)
	{
		var query = await waiverRepository.GetQueryableAsync();
		var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
		query = query.Where(x => scopedStudentIds.Contains(x.StudentId));

		if (input.AcademicYearId.HasValue)
			query = query.Where(x => x.AcademicYearId == input.AcademicYearId.Value);
		if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<LateFeeWaiverStatus>(input.Status, true, out var status))
			query = query.Where(x => x.Status == status);
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim();
			var students = await studentRepository.GetQueryableAsync();
			var matchingStudentIds = students
				.Where(x => (x.FirstName != null && x.FirstName.Contains(search)) ||
					(x.MiddleName != null && x.MiddleName.Contains(search)) ||
					(x.LastName != null && x.LastName.Contains(search)) ||
					(x.GrNumber != null && x.GrNumber.Contains(search)))
				.Select(x => x.Id);
			query = query.Where(x => matchingStudentIds.Contains(x.StudentId));
		}

		var total = await AsyncExecuter.CountAsync(query);
		var maxResultCount = Math.Clamp(input.MaxResultCount, 1, 100);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.CreationTime)
			.ThenByDescending(x => x.Id)
			.Skip(Math.Max(0, input.SkipCount))
			.Take(maxResultCount));
		return new PagedResultDto<LateFeeWaiverApplicationDto>(total, await ToDtosAsync(rows));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ViewWaivers)]
	public async Task<List<LateFeeWaiverApplicationDto>> GetWaiversForDueFeeAsync(Guid dueFeeId)
	{
		var dueFee = await dueFeeRepository.FindAsync(dueFeeId);
		if (dueFee is null)
			throw new BusinessException(LateFeeWaiverErrorCodes.DueFeeNotFound).WithData("dueFeeId", dueFeeId);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(dueFee.StudentId);
		var query = await waiverRepository.GetQueryableAsync();
		var rows = query.Where(w => w.DueFeeId == dueFeeId).OrderByDescending(w => w.CreationTime).ToList();
		return await ToDtosAsync(rows);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.RequestWaiver)]
	public async Task<LateFeeWaiverApplicationDto> ApplyWaiverAsync(ApplyLateFeeWaiverInput input)
	{
		if (string.IsNullOrWhiteSpace(input.Reason))
		{
			throw new BusinessException(LateFeeWaiverErrorCodes.ReasonRequired);
		}

		var dueFee = await dueFeeRepository.FindAsync(input.DueFeeId);
		if (dueFee is null)
		{
			throw new BusinessException(LateFeeWaiverErrorCodes.DueFeeNotFound).WithData("dueFeeId", input.DueFeeId);
		}
		await financeScopeAccessService.EnsureStudentAccessibleAsync(dueFee.StudentId);

		var feeStructureId = await ResolveFeeStructureIdAsync(dueFee.StudentId);
		var effective = await policyResolver.ResolveEffectiveRulesForStudentAsync(dueFee.StudentId, feeStructureId);
		if (effective.Policy is null)
		{
			throw new BusinessException(LateFeeWaiverErrorCodes.NoCollectionPolicyResolved).WithData("studentId", dueFee.StudentId);
		}

		var policy = effective.Policy;
		var balances = await lateFeeBalanceService.GetBalancesAsync([dueFee], policy.AcademicYearId,
			clock.Now, preserveAssessment: true);
		var remainingLateFee = balances[dueFee.Id].PendingAmount;

		var requestedAmount = input.RequestedAmount
			?? (input.RequestedPercentage is { } pct ? remainingLateFee * pct / 100m : remainingLateFee);

		if (requestedAmount <= 0)
		{
			throw new BusinessException(LateFeeWaiverErrorCodes.RequestedAmountMustBePositive).WithData("requestedAmount", requestedAmount);
		}

		var waiverResult = waiverCalculationService.Calculate(policy, new WaiverCalculationInput(
			requestedAmount, remainingLateFee, clock.Now, effective.WaiverPreApproved, dueFee.FeeHeadId));

		var entity = new LateFeeWaiverApplication(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			dueFee.StudentId,
			dueFee.Id,
			policy.Id,
			policy.AcademicYearId,
			remainingLateFee,
			requestedAmount,
			waiverResult.EffectiveWaiverAmount,
			input.Reason,
			waiverResult.RequiresApproval,
			CurrentUser.Id ?? Guid.Empty);

		await waiverRepository.InsertAsync(entity, autoSave: false);
		await WriteAuditAsync(entity, waiverResult.Explanation);
		await CurrentUnitOfWork!.SaveChangesAsync();

		return (await ToDtosAsync(new[] { entity })).Single();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ApproveWaiver)]
	public async Task<LateFeeWaiverApplicationDto> ApproveAsync(Guid id)
	{
		var entity = await waiverRepository.GetAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(entity.StudentId);
		EnsureIndependentReviewer(entity);

		entity.Approve(CurrentUser.Id ?? Guid.Empty, clock.Now);
		await waiverRepository.UpdateAsync(entity, autoSave: false);
		await WriteAuditAsync(entity, "Waiver approved.", FinanceAuditActions.Approve);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return (await ToDtosAsync(new[] { entity })).Single();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.RejectWaiver)]
	public async Task<LateFeeWaiverApplicationDto> RejectAsync(Guid id, RejectLateFeeWaiverInput input)
	{
		if (string.IsNullOrWhiteSpace(input.Reason))
			throw new BusinessException(LateFeeWaiverErrorCodes.DecisionReasonRequired);
		var entity = await waiverRepository.GetAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(entity.StudentId);
		EnsureIndependentReviewer(entity);
		entity.Reject(CurrentUser.Id ?? Guid.Empty, clock.Now, input.Reason);
		await waiverRepository.UpdateAsync(entity, autoSave: false);
		await WriteAuditAsync(entity, "Waiver rejected.", FinanceAuditActions.Reject, input.Reason);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return (await ToDtosAsync(new[] { entity })).Single();
	}

	private void EnsureIndependentReviewer(LateFeeWaiverApplication entity)
	{
		if (CurrentUser.Id.HasValue && entity.AppliedByUserId == CurrentUser.Id.Value)
			throw new BusinessException(LateFeeWaiverErrorCodes.MakerCheckerRequired);
	}

	private async Task<Guid> ResolveFeeStructureIdAsync(Guid studentId)
	{
		var assignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x => x.StudentId == studentId);
		return assignment?.FeeStructureStandardId ?? Guid.Empty;
	}

	private async Task WriteAuditAsync(LateFeeWaiverApplication entity, string summary, string action = FinanceAuditActions.Waive, string? decisionReason = null)
	{
		var student = await studentRepository.FindAsync(entity.StudentId);
		var studentContext = student is null ? null : new FinanceAuditStudentContext
		{
			StudentId = entity.StudentId,
			StudentName = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
				.Where(p => !string.IsNullOrWhiteSpace(p))),
			GrNo = student.GrNo?.ToString(),
			RollNo = student.RollNo?.ToString(),
		};

		await financeAuditWriter.WriteAsync(
			entity.AcademicYearId,
			FinanceAuditEntityTypes.LateFeeWaiver,
			entity.Id,
			$"Late fee waiver on due line {entity.DueFeeId:N}",
			action,
			summary,
			studentContext,
			decisionReason ?? entity.Reason,
			null,
			new { entity.DueFeeId, entity.ComputedLateFeeAmount, entity.RequestedAmount, entity.EffectiveAmount, status = entity.Status.ToString() },
			$"lfw-{entity.Id:N}");
	}

	private async Task<List<LateFeeWaiverApplicationDto>> ToDtosAsync(IEnumerable<LateFeeWaiverApplication> entities)
	{
		var rows = entities.ToList();
		var studentIds = rows.Select(x => x.StudentId).Distinct().ToList();
		var dueFeeIds = rows.Select(x => x.DueFeeId).Distinct().ToList();
		var students = (await studentRepository.GetListAsync(x => studentIds.Contains(x.Id))).ToDictionary(x => x.Id);
		var dueFees = (await dueFeeRepository.GetListAsync(x => dueFeeIds.Contains(x.Id))).ToDictionary(x => x.Id);
		var feeHeadIds = dueFees.Values.Select(x => x.FeeHeadId).Distinct().ToList();
		var feeHeads = (await feeHeadRepository.GetListAsync(x => feeHeadIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.Name);
		return rows.Select(entity => ToDto(entity, students.GetValueOrDefault(entity.StudentId), dueFees.GetValueOrDefault(entity.DueFeeId), feeHeads)).ToList();
	}

	private static LateFeeWaiverApplicationDto ToDto(LateFeeWaiverApplication entity, Student? student, DueFee? dueFee, IReadOnlyDictionary<Guid, string> feeHeads) => new()
	{
		Id = entity.Id,
		StudentId = entity.StudentId,
		DueFeeId = entity.DueFeeId,
		CollectionPolicyId = entity.CollectionPolicyId,
		AcademicYearId = entity.AcademicYearId,
		StudentName = student is null ? entity.StudentId.ToString() : string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
		GrNumber = student?.GrNumber ?? student?.GrNo?.ToString(),
		RollNo = student?.RollNo,
		FeeHeadId = dueFee?.FeeHeadId ?? Guid.Empty,
		FeeHeadName = dueFee is null ? string.Empty : feeHeads.GetValueOrDefault(dueFee.FeeHeadId, dueFee.FeeHeadId.ToString()),
		DueDate = dueFee?.DueDate,
		ComputedLateFeeAmount = entity.ComputedLateFeeAmount,
		RequestedAmount = entity.RequestedAmount,
		EffectiveAmount = entity.EffectiveAmount,
		Reason = entity.Reason,
		Status = entity.Status.ToString(),
		AppliedByUserId = entity.AppliedByUserId,
		ApprovedByUserId = entity.ApprovedByUserId,
		ApprovedAt = entity.ApprovedAt,
		DecisionReason = entity.DecisionReason,
		CreationTime = entity.CreationTime
	};
}
