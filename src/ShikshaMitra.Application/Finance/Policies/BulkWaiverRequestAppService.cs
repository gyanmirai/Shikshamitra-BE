using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// Waives the outstanding late fee for every due line matching a scope in one action —
/// implementation plan §4d. Always requires approval (see BulkWaiverRequest's class doc for
/// why); applying reuses the exact same per-line waiver math and audit mechanism as an
/// individual waiver (LateFeeWaiverApplication, FinanceAuditWriter) rather than a parallel one.
/// </summary>
[RemoteService(false)]
[Authorize]
public class BulkWaiverRequestAppService(
	IRepository<BulkWaiverRequest, Guid> requestRepository,
	IReadOnlyRepository<Student, Guid> studentRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<DueFee, Guid> dueFeeRepository,
	IRepository<LateFeeWaiverApplication, Guid> waiverRepository,
	CollectionPolicyResolver policyResolver,
	LateFeeCalculationService lateFeeCalculationService,
	WaiverCalculationService waiverCalculationService,
	ExceptionWindowResolver exceptionWindowResolver,
	LateFeeWaiverLookupService waiverLookupService,
	FinanceAuditWriter financeAuditWriter,
	IClock clock
) : ApplicationService, IBulkWaiverRequestAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ManageBulkWaiver)]
	public async Task<BulkWaiverPreviewResponseDto> PreviewAsync(BulkWaiverPreviewRequestDto input)
	{
		var affected = await ResolveAffectedDueLinesAsync(input.AcademicYearId, ParseScope(input.Scope), input.ScopeId);

		return new BulkWaiverPreviewResponseDto
		{
			AffectedStudentCount = affected.Select(a => a.StudentId).Distinct().Count(),
			AffectedDueLineCount = affected.Count,
			AffectedAmount = affected.Sum(a => a.NetLateFee)
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ManageBulkWaiver)]
	public async Task<BulkWaiverRequestDto> CreateAsync(CreateBulkWaiverRequestDto input)
	{
		var scope = ParseScope(input.Scope);
		var affected = await ResolveAffectedDueLinesAsync(input.AcademicYearId, scope, input.ScopeId);

		var entity = new BulkWaiverRequest(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			input.AcademicYearId,
			scope,
			input.ScopeId,
			input.ReasonCode,
			input.Reason,
			CurrentUser.Id ?? Guid.Empty,
			affected.Select(a => a.StudentId).Distinct().Count(),
			affected.Count,
			affected.Sum(a => a.NetLateFee));

		await requestRepository.InsertAsync(entity, autoSave: true);
		return ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View)]
	public async Task<List<BulkWaiverRequestDto>> GetListAsync(Guid academicYearId)
	{
		var query = await requestRepository.GetQueryableAsync();
		var rows = query.Where(x => x.AcademicYearId == academicYearId)
			.OrderByDescending(x => x.CreationTime).ToList();
		return rows.Select(ToDto).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ManageBulkWaiver)]
	public async Task<BulkWaiverRequestDto> ApproveAsync(Guid id)
	{
		var entity = await requestRepository.GetAsync(id);

		// Re-resolve now, at approval time — the set of affected due lines is snapshotted here,
		// not re-computed at apply time, so who was waived stays stable even if the scope's
		// membership changes between approval and apply (implementation plan §4d data-model bullet).
		var affected = await ResolveAffectedDueLinesAsync(entity.AcademicYearId, entity.Scope, entity.ScopeId);
		entity.Approve(CurrentUser.Id ?? Guid.Empty, clock.Now, affected.Select(a => a.DueFee.Id).ToList());

		await requestRepository.UpdateAsync(entity, autoSave: true);
		return ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ManageBulkWaiver)]
	public async Task<BulkWaiverRequestDto> RejectAsync(Guid id)
	{
		var entity = await requestRepository.GetAsync(id);
		entity.Reject(CurrentUser.Id ?? Guid.Empty, clock.Now);
		await requestRepository.UpdateAsync(entity, autoSave: true);
		return ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ManageBulkWaiver)]
	public async Task<BulkWaiverApplyResultDto> ApplyAsync(Guid id)
	{
		var entity = await requestRepository.GetAsync(id);
		var dueFeeIds = entity.GetAffectedDueFeeIds();

		if (dueFeeIds.Count == 0)
		{
			throw new BusinessException(BulkWaiverErrorCodes.NoAffectedDueLines);
		}

		var dueFees = await dueFeeRepository.GetListAsync(d => dueFeeIds.Contains(d.Id));
		var waivedByDueFee = await waiverLookupService.GetAppliedWaivedAmountsAsync(dueFeeIds);
		var today = clock.Now.Date;

		var created = new List<LateFeeWaiverApplication>();
		foreach (var dueFee in dueFees)
		{
			if (dueFee.OutstandingAmount <= 0)
			{
				continue;
			}

			var assignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x => x.StudentId == dueFee.StudentId);
			if (assignment is null)
			{
				continue;
			}

			var effective = await policyResolver.ResolveEffectiveRulesForStudentAsync(dueFee.StudentId, assignment.FeeStructureStandardId);
			if (effective.Policy is null || !effective.Policy.EnableWaiver || effective.IsLateFeeExempt)
			{
				continue;
			}

			var activeWindow = await exceptionWindowResolver.ResolveActiveWindowAsync(
				effective.Policy.AcademicYearId, assignment.FeeStructureStandardId, today);
			var daysLate = (today - dueFee.DueDate.Date).Days;
			var lateFeeResult = lateFeeCalculationService.Calculate(effective.Policy, new LateFeeCalculationInput(
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

			var alreadyWaived = waivedByDueFee.GetValueOrDefault(dueFee.Id, 0m);
			var remainingLateFee = Math.Max(0, lateFeeResult.LateFee - alreadyWaived);
			if (remainingLateFee <= 0)
			{
				continue;
			}

			var waiverResult = waiverCalculationService.Calculate(
				effective.Policy, new WaiverCalculationInput(remainingLateFee, remainingLateFee, clock.Now,
					WaiverPreApproved: true, FeeHeadId: dueFee.FeeHeadId));

			var waiver = new LateFeeWaiverApplication(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				dueFee.StudentId,
				dueFee.Id,
				effective.Policy.Id,
				effective.Policy.AcademicYearId,
				remainingLateFee,
				remainingLateFee,
				waiverResult.EffectiveWaiverAmount,
				$"Bulk waiver: {entity.Reason}",
				requiresApproval: false,
				appliedByUserId: CurrentUser.Id ?? Guid.Empty,
				bulkWaiverRequestId: entity.Id);

			await waiverRepository.InsertAsync(waiver, autoSave: false);
			created.Add(waiver);

			var student = await studentRepository.FindAsync(dueFee.StudentId);
			var studentContext = student is null ? null : new FinanceAuditStudentContext
			{
				StudentId = dueFee.StudentId,
				StudentName = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
					.Where(p => !string.IsNullOrWhiteSpace(p))),
				GrNo = student.GrNo?.ToString(),
				RollNo = student.RollNo?.ToString(),
			};

			await financeAuditWriter.WriteAsync(
				effective.Policy.AcademicYearId,
				FinanceAuditEntityTypes.LateFeeWaiver,
				waiver.Id,
				$"Bulk waiver on due line {dueFee.Id:N}",
				FinanceAuditActions.Waive,
				$"Waived as part of bulk waiver request {entity.Id:N}.",
				studentContext,
				entity.Reason,
				null,
				new { BulkWaiverRequestId = entity.Id, DueFeeId = dueFee.Id, waiver.EffectiveAmount },
				$"bwr-{entity.Id:N}");
		}

		entity.MarkApplied(clock.Now);
		await requestRepository.UpdateAsync(entity, autoSave: false);
		await CurrentUnitOfWork!.SaveChangesAsync();

		return new BulkWaiverApplyResultDto
		{
			BulkWaiverRequestId = entity.Id,
			WaiversCreated = created.Count,
			TotalWaivedAmount = created.Sum(w => w.EffectiveAmount)
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View)]
	public async Task<List<BulkWaiverRollupItemDto>> GetRollupAsync(Guid id)
	{
		var query = await waiverRepository.GetQueryableAsync();
		var rows = query.Where(w => w.BulkWaiverRequestId == id).ToList();
		return rows.Select(w => new BulkWaiverRollupItemDto
		{
			LateFeeWaiverApplicationId = w.Id,
			StudentId = w.StudentId,
			DueFeeId = w.DueFeeId,
			EffectiveAmount = w.EffectiveAmount
		}).ToList();
	}

	private async Task<List<(DueFee DueFee, Guid StudentId, decimal NetLateFee)>> ResolveAffectedDueLinesAsync(
		Guid academicYearId, BulkWaiverScope scope, Guid? scopeId)
	{
		var studentQuery = await studentRepository.GetQueryableAsync();
		var students = studentQuery.Where(s => s.AcademicYearId == academicYearId);
		students = scope switch
		{
			BulkWaiverScope.Class => students.Where(s => s.StandardId == scopeId),
			BulkWaiverScope.Section => students.Where(s => s.DivisionId == scopeId),
			_ => students
		};
		var candidateStudentIds = students.Select(s => s.Id).ToList();

		var assignmentQuery = await studentFeeStructureRepository.GetQueryableAsync();
		var assignments = assignmentQuery.Where(a => candidateStudentIds.Contains(a.StudentId));
		if (scope == BulkWaiverScope.FeeStructure && scopeId is { } feeStructureId)
		{
			assignments = assignments.Where(a => a.FeeStructureStandardId == feeStructureId);
		}
		var assignmentMap = assignments.ToList().ToDictionary(a => a.StudentId, a => a.FeeStructureStandardId);
		if (assignmentMap.Count == 0)
		{
			return new List<(DueFee, Guid, decimal)>();
		}

		var dueFeeQuery = await dueFeeRepository.GetQueryableAsync();
		var dueFees = dueFeeQuery
			.Where(d => assignmentMap.Keys.Contains(d.StudentId) && d.OutstandingAmount > 0)
			.ToList();

		var waivedByDueFee = await waiverLookupService.GetAppliedWaivedAmountsAsync(dueFees.Select(d => d.Id).ToList());
		var today = clock.Now.Date;
		var result = new List<(DueFee, Guid, decimal)>();

		foreach (var dueFee in dueFees)
		{
			var structureId = assignmentMap[dueFee.StudentId];
			var effective = await policyResolver.ResolveEffectiveRulesForStudentAsync(dueFee.StudentId, structureId);
			if (effective.Policy is null || !effective.Policy.EnableWaiver || effective.IsLateFeeExempt)
			{
				continue;
			}

			var activeWindow = await exceptionWindowResolver.ResolveActiveWindowAsync(academicYearId, structureId, today);
			var daysLate = (today - dueFee.DueDate.Date).Days;
			var lateFeeResult = lateFeeCalculationService.Calculate(effective.Policy, new LateFeeCalculationInput(
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

			var net = Math.Max(0, lateFeeResult.LateFee - waivedByDueFee.GetValueOrDefault(dueFee.Id, 0m));
			if (net > 0)
			{
				result.Add((dueFee, dueFee.StudentId, net));
			}
		}

		return result;
	}

	private static BulkWaiverScope ParseScope(string value) => Enum.TryParse<BulkWaiverScope>(value, true, out var scope)
		? scope
		: BulkWaiverScope.WholeSchool;

	private static BulkWaiverRequestDto ToDto(BulkWaiverRequest entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		Scope = entity.Scope.ToString(),
		ScopeId = entity.ScopeId,
		ReasonCode = entity.ReasonCode,
		Reason = entity.Reason,
		RequestedByUserId = entity.RequestedByUserId,
		Status = entity.Status.ToString(),
		ApprovedByUserId = entity.ApprovedByUserId,
		ApprovedAt = entity.ApprovedAt,
		AppliedAt = entity.AppliedAt,
		PreviewAffectedStudentCount = entity.PreviewAffectedStudentCount,
		PreviewAffectedDueLineCount = entity.PreviewAffectedDueLineCount,
		PreviewAffectedAmount = entity.PreviewAffectedAmount,
		AffectedDueLineCount = entity.GetAffectedDueFeeIds().Count,
		CreationTime = entity.CreationTime
	};
}
