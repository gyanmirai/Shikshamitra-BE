using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
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
/// Does not mutate DueFee — see LateFeeWaiverApplication's class doc for why.
/// </summary>
[RemoteService(false)]
[Authorize]
public class LateFeeWaiverAppService(
	IDueFeeRepository dueFeeRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<LateFeeWaiverApplication, Guid> waiverRepository,
	IReadOnlyRepository<CollectionPolicy, Guid> collectionPolicyRepository,
	CollectionPolicyResolver policyResolver,
	LateFeeCalculationService lateFeeCalculationService,
	ExceptionWindowResolver exceptionWindowResolver,
	WaiverCalculationService waiverCalculationService,
	LateFeeWaiverLookupService waiverLookupService,
	FinanceAuditWriter financeAuditWriter,
	IReadOnlyRepository<Student, Guid> studentRepository,
	IClock clock
) : ApplicationService, ILateFeeWaiverAppService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View)]
	public async Task<List<LateFeeWaiverApplicationDto>> GetWaiversForDueFeeAsync(Guid dueFeeId)
	{
		var query = await waiverRepository.GetQueryableAsync();
		var rows = query.Where(w => w.DueFeeId == dueFeeId).OrderByDescending(w => w.CreationTime).ToList();
		return rows.Select(ToDto).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ApproveWaiver)]
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

		var feeStructureId = await ResolveFeeStructureIdAsync(dueFee.StudentId);
		var effective = await policyResolver.ResolveEffectiveRulesForStudentAsync(dueFee.StudentId, feeStructureId);
		if (effective.Policy is null)
		{
			throw new BusinessException(LateFeeWaiverErrorCodes.NoCollectionPolicyResolved).WithData("studentId", dueFee.StudentId);
		}

		var policy = effective.Policy;
		var daysLate = (clock.Now.Date - dueFee.DueDate.Date).Days;
		var activeWindow = await exceptionWindowResolver.ResolveActiveWindowAsync(
			policy.AcademicYearId, feeStructureId, clock.Now.Date);
		var lateFeeResult = lateFeeCalculationService.Calculate(policy, new LateFeeCalculationInput(
			Math.Max(0, daysLate),
			(decimal)dueFee.OutstandingAmount,
			(decimal)dueFee.OriginalAmount,
			(decimal)dueFee.OutstandingAmount,
			dueFee.FeeHeadId,
			effective.IsLateFeeExempt,
			effective.CustomGracePeriodDays,
			activeWindow?.Effect,
			activeWindow?.ExtensionDays,
			activeWindow?.Label));

		var alreadyWaived = await waiverLookupService.GetAppliedWaivedAmountAsync(dueFee.Id);
		var remainingLateFee = Math.Max(0, lateFeeResult.LateFee - alreadyWaived);

		var requestedAmount = input.RequestedAmount
			?? (input.RequestedPercentage is { } pct ? remainingLateFee * pct / 100m : remainingLateFee);

		if (requestedAmount <= 0)
		{
			throw new BusinessException(LateFeeWaiverErrorCodes.RequestedAmountMustBePositive).WithData("requestedAmount", requestedAmount);
		}

		var waiverResult = waiverCalculationService.Calculate(policy, new WaiverCalculationInput(
			requestedAmount, remainingLateFee, clock.Now, effective.WaiverPreApproved));

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

		return ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ApproveWaiver)]
	public async Task<LateFeeWaiverApplicationDto> ApproveAsync(Guid id)
	{
		var entity = await waiverRepository.GetAsync(id);

		// Phase 4e: ApproveWaiver alone only covers waivers up to the resolved policy's own
		// approvalThresholdAmount (Phase 1 field) — approving above it needs the distinct
		// ApproveWaiverAboveThreshold permission, so schools can run a two-tier approver
		// structure (e.g. Accountant for small waivers, Principal for large ones). A null
		// threshold means the policy never requires the higher permission.
		var policy = await collectionPolicyRepository.FindAsync(entity.CollectionPolicyId);
		if (policy?.ApprovalThresholdAmount is { } threshold && entity.RequestedAmount > threshold)
		{
			await AuthorizationService.CheckAsync(
				ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ApproveWaiverAboveThreshold);
		}

		entity.Approve(CurrentUser.Id ?? Guid.Empty, clock.Now);
		await waiverRepository.UpdateAsync(entity, autoSave: false);
		await WriteAuditAsync(entity, "Waiver approved.", FinanceAuditActions.Approve);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ApproveWaiver)]
	public async Task<LateFeeWaiverApplicationDto> RejectAsync(Guid id)
	{
		var entity = await waiverRepository.GetAsync(id);
		entity.Reject(CurrentUser.Id ?? Guid.Empty, clock.Now);
		await waiverRepository.UpdateAsync(entity, autoSave: false);
		await WriteAuditAsync(entity, "Waiver rejected.", FinanceAuditActions.Reject);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return ToDto(entity);
	}

	private async Task<Guid> ResolveFeeStructureIdAsync(Guid studentId)
	{
		var assignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x => x.StudentId == studentId);
		return assignment?.FeeStructureStandardId ?? Guid.Empty;
	}

	private async Task WriteAuditAsync(LateFeeWaiverApplication entity, string summary, string action = FinanceAuditActions.Waive)
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
			entity.Reason,
			null,
			new { entity.DueFeeId, entity.ComputedLateFeeAmount, entity.RequestedAmount, entity.EffectiveAmount, status = entity.Status.ToString() },
			$"lfw-{entity.Id:N}");
	}

	private static LateFeeWaiverApplicationDto ToDto(LateFeeWaiverApplication entity) => new()
	{
		Id = entity.Id,
		StudentId = entity.StudentId,
		DueFeeId = entity.DueFeeId,
		CollectionPolicyId = entity.CollectionPolicyId,
		AcademicYearId = entity.AcademicYearId,
		ComputedLateFeeAmount = entity.ComputedLateFeeAmount,
		RequestedAmount = entity.RequestedAmount,
		EffectiveAmount = entity.EffectiveAmount,
		Reason = entity.Reason,
		Status = entity.Status.ToString(),
		AppliedByUserId = entity.AppliedByUserId,
		ApprovedByUserId = entity.ApprovedByUserId,
		ApprovedAt = entity.ApprovedAt,
		CreationTime = entity.CreationTime
	};
}
