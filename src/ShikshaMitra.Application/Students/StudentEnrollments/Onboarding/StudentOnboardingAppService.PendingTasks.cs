using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	internal async Task<IQueryable<StudentOnboardingCase>> FilterPendingTaskAsync(IQueryable<StudentOnboardingCase> query, string? task)
	{
		if (string.IsNullOrWhiteSpace(task)) return query;
		query = query.Where(c => c.Status != StudentOnboardingStatus.Cancelled);
		if (task == "division") return query.Where(c => !c.DivisionId.HasValue);
		if (task is "roll" or "roll-number") return query.Where(c => !c.RollNumber.HasValue);
		if (task is "gr" or "gr-number")
		{
			var students = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
			return from item in query join student in students on item.StudentId equals student.Id
				where !item.IdentifierAllocationId.HasValue && student.GrNumber == null && !student.GrNo.HasValue select item;
		}
		if (task is not ("fees" or "credentials")) throw OnboardingRuleConfiguration.Invalid("Choose a supported pending task.");
		var scoped = await AsyncExecuter.ToListAsync(query);
		var caseIds = scoped.Select(c => c.Id).ToArray();
		var pending = new HashSet<Guid>();
		if (task == "fees")
		{
			var applicationIds = scoped.Select(c => c.ApplicationId).ToArray();
			var applications = (await applicationRepository.GetListAsync(a => Enumerable.Contains(applicationIds, a.Id))).ToDictionary(a => a.Id);
			foreach (var item in scoped)
				if (!applications.TryGetValue(item.ApplicationId, out var application) ||
                    !await admissionFeeSelectionReader.IsConfirmedAsync(application)) pending.Add(item.Id);
		}
		else
		{
			var emails = await LazyServiceProvider.LazyGetRequiredService<IRepository<ShikshaMitra.Students.Portal.StudentCredentialEmailRequest, Guid>>()
				.GetListAsync(e => Enumerable.Contains(caseIds, e.CaseId));
			var parentFlows = await SiblingParents.ReadParentFlowsAsync(scoped);
			foreach (var item in scoped)
			{
				var manualParent = parentFlows.IsParentFlow(item);
				var reusedParent = StudentSiblingParentService.IsReusedCore(SiblingParents.WasReused(item), StudentSiblingParentService.HasOnboardingConfirmation(item), manualParent);
				var emailPending = emails.Any(email => email.CaseId == item.Id && email.Status != "Sent" &&
					(email.AccountType != "guardian" || !reusedParent) && email.RequiresSavedOwnerValidation());
				if (emailPending) pending.Add(item.Id);
			}
		}
		var ids = pending.ToArray(); return query.Where(c => Enumerable.Contains(ids, c.Id));
	}

	internal async Task<IQueryable<StudentOnboardingCase>> FilterDashboardQueueAsync(IQueryable<StudentOnboardingCase> query, string? queue, DateTime? asOf = null)
	{
		if (string.IsNullOrWhiteSpace(queue)) return query;
		query = query.Where(c => c.Status != StudentOnboardingStatus.Completed && c.Status != StudentOnboardingStatus.Cancelled);
		if (queue == "unfinished") return query;
		if (queue == "attention") { var now = asOf ?? Clock.Now; return query.Where(c => c.Status == StudentOnboardingStatus.NeedsAttention || c.DueAt < now); }
		if (queue == "ready") return query.Where(c => c.Status == StudentOnboardingStatus.ReadyForActivation && !c.StudentEnrollmentId.HasValue);
		if (queue == "documents")
		{
			var steps = await stepRepository.GetQueryableAsync();
			return query.Where(c => steps.Any(s => s.OnboardingCaseId == c.Id && s.Required && s.Key == StudentOnboardingStepKeys.Documents &&
				(s.Status == StudentOnboardingStepStatus.Pending || s.Status == StudentOnboardingStepStatus.Blocked || s.Status == StudentOnboardingStepStatus.InProgress)));
		}
		return await FilterPendingTaskAsync(query, queue);
	}

	private async Task<JsonElement?> ReadSavedRulesAsync(StudentOnboardingCase item,
		IReadOnlyDictionary<Guid, OnboardingRuleConfiguration?>? configurations = null)
	{
		if (item.ExtraProperties.TryGetValue("ReadinessRulesSnapshot", out var snapshot) && snapshot is string text)
		{
			using var document = JsonDocument.Parse(text); return StudentPortalAccountRules.ForStaffSelection(document.RootElement);
		}
		var configuration = configurations != null && configurations.TryGetValue(item.AcademicYearId, out var cached)
			? cached : await configurationManager.GetAsync(item.AcademicYearId);
		return AutomaticOnboardingReadinessRules.CanAdopt(item)
			? AutomaticOnboardingReadinessRules.Resolve(configuration)
			: StudentPortalAccountRules.ForStaffSelection(configuration?.Root.GetProperty("readinessAccess"));
	}

	private async Task<bool> CredentialsPendingAsync(StudentOnboardingCase item, bool requiredOnly = false)
	{
		if (item.Status == StudentOnboardingStatus.Cancelled) return false;
		var rules = await ReadSavedRulesAsync(item);
		return (await ReadCredentialReadinessAsync(item, rules, requiredOnly)).Pending;
	}

	private async Task<(bool Pending, bool? RequiredDeliveryConfirmed)> ReadCredentialReadinessAsync(
		StudentOnboardingCase item, JsonElement? rules, bool requiredOnly = false, OnboardingListReadContext? listReadContext = null)
	{
		if (item.Status == StudentOnboardingStatus.Cancelled || !rules.HasValue) return (false, null);
		if (requiredOnly) return (false, null); // Staff-requested login delivery never holds enrollment completion.
		if (listReadContext?.CredentialMetadata == null) listReadContext = null;
		var metadata = listReadContext != null ? await listReadContext.CredentialMetadata!.Value : null;
		var requests = metadata != null ? metadata.Requests[item.Id].ToList()
			: await LazyServiceProvider.LazyGetRequiredService<IRepository<ShikshaMitra.Students.Portal.StudentCredentialEmailRequest, Guid>>().GetListAsync(e => e.CaseId == item.Id);
		var manualParent = metadata != null ? metadata.ParentFlows.IsParentFlow(item) : await SiblingParents.IsParentFlowAsync(item);
		bool reusedParent;
		if (metadata != null)
		{
			var wasReused = SiblingParents.WasReused(item);
			reusedParent = StudentSiblingParentService.IsReusedCore(wasReused,
				wasReused && StudentSiblingParentService.HasOnboardingConfirmation(item), manualParent);
		}
		else reusedParent = await SiblingParents.IsReusedAsync(item.Id, "guardian");
		var applicableRequests = requests.Where(request => (request.AccountType != "guardian" || !reusedParent) &&
			request.RequiresSavedOwnerValidation()).ToList();
		return (applicableRequests.Any(request => request.Status != "Sent"), null);
	}
}
