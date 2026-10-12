using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Admissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionFeeSelectionContext(AdmissionApplication Application,
	AdmissionApplicationStageInstance? Owner, AdmissionApplicationWorkflowInstance? Workflow,
	bool IsRequired, bool HasPaymentEvidence, string? Currency)
{
	public bool IsEditable => Owner != null && Workflow != null && !HasPaymentEvidence &&
		AdmissionStagePreparationRules.CanPrepare(Application, Workflow.Status, Owner.Status);
}

[RemoteService(false)]
public class AdmissionFeeSelectionAccessService(
	AdmissionApplicationReadService applications,
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<StudentOnboardingCase, Guid> cases,
	IUserScopeAccessService userScopes,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	IRepository<AdmissionWorkflowTransitionHistory, Guid> history,
	IRepository<AdmissionPaymentTransaction, Guid> payments) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionApplication> GetApplicationAsync(Guid id, bool requireOnboarding = false)
	{
		var application = requireOnboarding ? null : await applications.FindEntityAsync(id);
		if (application != null) return application;
		var item = await cases.FindAsync(c => c.ApplicationId == id);
		if (item != null && item.TenantId == CurrentTenant.Id && await CanAccessCaseAsync(item))
		{
			application = await applicationRepository.GetAsync(id);
			await LazyServiceProvider.LazyGetRequiredService<AdmissionApplicationScopeAccessService>().EnsureCanAccessAsync(application);
			return application;
		}
		throw new EntityNotFoundException(typeof(AdmissionApplication), id);
	}

	public async Task<StudentOnboardingCase> GetOnboardingCaseAsync(AdmissionApplication application)
	{
		return await FindOnboardingCaseAsync(application) ??
			throw new EntityNotFoundException(typeof(StudentOnboardingCase), application.OnboardingCaseId);
	}

	public async Task<StudentOnboardingCase?> FindOnboardingCaseAsync(AdmissionApplication application)
	{
		var item = application.OnboardingCaseId.HasValue ? await cases.FindAsync(application.OnboardingCaseId.Value) : null;
		if (item == null || item.ApplicationId != application.Id || item.TenantId != CurrentTenant.Id || !await CanAccessCaseAsync(item))
			return null;
		return item;
	}

	private async Task<bool> CanAccessCaseAsync(StudentOnboardingCase item)
	{
		var scope = await userScopes.GetCurrentUserScopeAsync();
		return scope.IsAdmin || scope.HasWildcardAcademicYear || scope.HasAnyAccess && scope.AcademicYearIds.Contains(item.AcademicYearId) &&
			(scope.WildcardBoardAcademicYearIds.Contains(item.AcademicYearId) || scope.WildcardMediumBoardIds.Contains(item.IntendedBoardId) ||
			 scope.WildcardMediumStageMediumIds.Contains(item.IntendedMediumId) || scope.WildcardStandardMediumStageIds.Contains(item.IntendedStageId) ||
			 scope.WildcardDivisionStandardIds.Contains(item.IntendedStandardId) || item.DivisionId.HasValue && scope.DivisionIds.Contains(item.DivisionId.Value));
	}

	public async Task<AdmissionFeeSelectionContext> GetContextAsync(AdmissionApplication application)
	{
		var workflow = await workflows.FindAsync(w => w.ApplicationId == application.Id);
		var workflowStages = workflow == null ? [] : await stages.GetListAsync(s => s.WorkflowInstanceId == workflow.Id);
		var entered = workflowStages.Where(s => s.EnteredAt.HasValue).ToList();
		var events = workflow == null ? [] : await history.GetListAsync(h => h.WorkflowInstanceId == workflow.Id);
		var snapshot = application.GetFeeSelection();
		var owner = snapshot == null ? ResolveOwner(entered, events) ?? workflowStages
			.Where(s => s.StageType == AdmissionWorkflowStageType.Payment && s.Status == AdmissionRuntimeStageStatus.Pending)
			.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).FirstOrDefault()
			: workflowStages.SingleOrDefault(s => s.Id == snapshot.OwnerStageId);
		var paymentDefinitions = await definitions.GetListAsync(d => d.WorkflowDefinitionId == application.WorkflowDefinitionId &&
			d.StageType == AdmissionWorkflowStageType.Payment);
		return new(application, owner, workflow, paymentDefinitions.Count > 0,
			await payments.AnyAsync(p => p.ApplicationId == application.Id && p.Amount > 0m),
			snapshot?.Currency ?? AdmissionFeeStructureReader.ReferenceCurrency);
	}

	// Causal transition links decide order even when timestamps tie or diagram order differs.
	public static AdmissionApplicationStageInstance? ResolveOwner(IReadOnlyList<AdmissionApplicationStageInstance> entered,
		IReadOnlyList<AdmissionWorkflowTransitionHistory> events)
	{
		var incoming = events.Where(h => h.ToStageInstanceId.HasValue).Select(h => h.ToStageInstanceId!.Value).ToHashSet();
		var next = entered.Where(s => !incoming.Contains(s.Id)).OrderBy(s => s.EnteredAt).ThenBy(s => s.Id).FirstOrDefault();
		var visited = new HashSet<Guid>();
		while (next != null && visited.Add(next.Id))
		{
			if (next.StageType == AdmissionWorkflowStageType.Payment) return next;
			var to = events.FirstOrDefault(h => h.FromStageInstanceId == next.Id)?.ToStageInstanceId;
			next = entered.FirstOrDefault(s => s.Id == to);
		}
		return entered.Where(s => s.StageType == AdmissionWorkflowStageType.Payment)
			.OrderBy(s => s.EnteredAt).ThenBy(s => s.CompletedAt ?? DateTime.MaxValue).ThenBy(s => s.Id).FirstOrDefault();
	}

	public async Task EnsureSelectionAsync(AdmissionApplication application)
	{
		var context = await GetContextAsync(application);
		if (context.IsRequired && (context.Owner == null || application.GetFeeSelection() is not { } selection || selection.OwnerStageId != context.Owner.Id))
			throw new BusinessException(AdmissionFeeSelectionErrorCodes.Required,
				"Confirm the Finance fee structure before completing this stage or finalizing admission.");
	}
}
