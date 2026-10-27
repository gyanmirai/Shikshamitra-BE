using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class LeavePolicyLifecycle(
	IRepository<LeavePolicy, Guid> repository,
	IRepository<AcademicYear, Guid> years,
	ILeavePolicyRepository policies,
	IPayrollInputRevisionStore fence,
	ILeavePolicyReferenceLock referenceLock,
	HrPolicyVersionCaptureService versions) : ApplicationService, ITransientDependency
{
	public async Task<LeavePolicyLifecycleContext> BeginAsync(Guid? policyId, Guid? academicYearId = null, bool changesDefault = false)
	{
		// Existing payroll SaveChanges and assignment paths use this same tenant fence.
		// Acquire it first; row locks are limited to the target and defaults being replaced.
		await fence.LockAsync();
		if (policyId.HasValue) await referenceLock.AcquireAsync(policyId.Value, CurrentTenant.Id);
		var policy = policyId.HasValue ? await policies.GetWithRulesAsync(policyId.Value) : null;
		if (policyId.HasValue && policy == null) throw new EntityNotFoundException(typeof(LeavePolicy), policyId);
		var year = await years.GetAsync(policy?.AcademicYearId ?? academicYearId!.Value);
		if (year.TenantId != CurrentTenant.Id) throw new EntityNotFoundException(typeof(AcademicYear), year.Id);
		var tracked = new List<LeavePolicy>();
		if (changesDefault)
		{
			var defaults = await policies.GetActiveDefaultsAsync(policy?.AcademicYearId ?? academicYearId!.Value, policyId);
			foreach (var item in defaults.OrderBy(x => x.Id))
			{
				await referenceLock.AcquireAsync(item.Id, CurrentTenant.Id);
				tracked.Add((await policies.GetWithRulesAsync(item.Id))!);
			}
		}
		if (policy != null) tracked.Add(policy);
		return new(policy, tracked.ToDictionary(x => x.Id, LeavePolicySnapshots.Capture), tracked, GuidGenerator.Create().ToString("N"));
	}

	public async Task CompleteAsync(LeavePolicyLifecycleContext context, LeavePolicy target, string action, string? reason = null)
	{
		foreach (var policy in context.Policies.Append(target).DistinctBy(x => x.Id))
		{
			if (!context.Before.TryGetValue(policy.Id, out var before))
			{
				await versions.CaptureAsync("leave", LeavePolicySnapshots.Capture(policy), action, reason, context.CorrelationId);
				continue;
			}
			if (LeavePolicySnapshots.Configuration(before) == LeavePolicySnapshots.Configuration(LeavePolicySnapshots.Capture(policy))) continue;
			await versions.CaptureAsync("leave", before, "baseline", "Current configuration enrolled before its first recorded change; earlier history is unavailable.", context.CorrelationId);
			policy.AdvanceVersion();
			await repository.UpdateAsync(policy, autoSave: false);
			await versions.CaptureAsync("leave", LeavePolicySnapshots.Capture(policy),
				policy.Id == target.Id ? action : "set-default-cleared", reason, context.CorrelationId);
		}
	}
}

public sealed record LeavePolicyLifecycleContext(LeavePolicy? Policy,
	Dictionary<Guid, LeavePolicySnapshotDto> Before, List<LeavePolicy> Policies, string CorrelationId);
