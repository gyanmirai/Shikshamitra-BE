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
			var configurations = new Dictionary<Guid, OnboardingRuleConfiguration?>();
			foreach (var year in scoped.Where(AutomaticOnboardingReadinessRules.CanAdopt).Select(c => c.AcademicYearId).Distinct())
				configurations[year] = await configurationManager.GetAsync(year);
			var handoffs = await handoffRepository.GetListAsync(h => Enumerable.Contains(caseIds, h.OnboardingCaseId) && h.Required &&
				(h.ServiceKey == "student-account" || h.ServiceKey == "guardian-account") && h.Status != StudentOnboardingServiceHandoffStatus.Acknowledged);
			var emails = await LazyServiceProvider.LazyGetRequiredService<IRepository<ShikshaMitra.Students.Portal.StudentCredentialEmailRequest, Guid>>()
				.GetListAsync(e => Enumerable.Contains(caseIds, e.CaseId));
			foreach (var item in scoped)
			{
				var manualParent = await SiblingParents.IsParentFlowAsync(item);
				var reusedParent = manualParent && SiblingParents.WasReused(item);
				var rules = AutomaticOnboardingReadinessRules.CanAdopt(item)
					? AutomaticOnboardingReadinessRules.Resolve(configurations[item.AcademicYearId])
					: await ReadSavedRulesAsync(item);
				var accountPending = handoffs.Any(h => h.OnboardingCaseId == item.Id) || !item.StudentEnrollmentId.HasValue && rules.HasValue &&
					(OnboardingRuleConfiguration.Boolean(rules.Value, "studentAccount") || OnboardingRuleConfiguration.Boolean(rules.Value, "guardianAccount"));
				var emailPending = rules.HasValue && new[] { "student", "guardian" }.Any(type =>
					(type != "guardian" || !manualParent) &&
					StudentPortalAccountRules.IsRequired(rules.Value, type) &&
					OnboardingRuleConfiguration.Boolean(rules.Value, type == "student" ? "sendStudentCredentialEmail" : "sendGuardianCredentialEmail") &&
					!emails.Any(e => e.CaseId == item.Id && e.AccountType == type && e.Status == "Sent"));
				emailPending |= emails.Any(email => email.CaseId == item.Id && email.Status != "Sent" &&
					(email.AccountType != "guardian" || !reusedParent) &&
					(email.RequiresSavedOwnerValidation() || rules.HasValue && StudentPortalAccountRules.Access(rules.Value, email.AccountType) == StudentPortalAccountRules.Optional));
				if (accountPending || emailPending) pending.Add(item.Id);
			}
		}
		var ids = pending.ToArray(); return query.Where(c => Enumerable.Contains(ids, c.Id));
	}

	internal async Task<IQueryable<StudentOnboardingCase>> FilterDashboardQueueAsync(IQueryable<StudentOnboardingCase> query, string? queue)
	{
		if (string.IsNullOrWhiteSpace(queue)) return query;
		query = query.Where(c => c.Status != StudentOnboardingStatus.Completed && c.Status != StudentOnboardingStatus.Cancelled);
		if (queue == "unfinished") return query;
		if (queue == "attention") { var now = Clock.Now; return query.Where(c => c.Status == StudentOnboardingStatus.NeedsAttention || c.DueAt < now); }
		if (queue == "ready") return query.Where(c => c.Status == StudentOnboardingStatus.ReadyForActivation && !c.StudentEnrollmentId.HasValue);
		if (queue == "documents")
		{
			var steps = await stepRepository.GetQueryableAsync();
			return query.Where(c => steps.Any(s => s.OnboardingCaseId == c.Id && s.Required && s.Key == StudentOnboardingStepKeys.Documents &&
				(s.Status == StudentOnboardingStepStatus.Pending || s.Status == StudentOnboardingStepStatus.Blocked || s.Status == StudentOnboardingStepStatus.InProgress)));
		}
		return await FilterPendingTaskAsync(query, queue);
	}

	private async Task<JsonElement?> ReadSavedRulesAsync(StudentOnboardingCase item)
	{
		if (item.ExtraProperties.TryGetValue("ReadinessRulesSnapshot", out var snapshot) && snapshot is string text)
		{
			using var document = JsonDocument.Parse(text); return document.RootElement.Clone();
		}
		var configuration = await configurationManager.GetAsync(item.AcademicYearId);
		return AutomaticOnboardingReadinessRules.CanAdopt(item)
			? AutomaticOnboardingReadinessRules.Resolve(configuration)
			: configuration?.Root.GetProperty("readinessAccess");
	}

	private async Task<bool> CredentialsPendingAsync(StudentOnboardingCase item, bool requiredOnly = false)
	{
		if (item.Status == StudentOnboardingStatus.Cancelled) return false;
		var rules = await ReadSavedRulesAsync(item);
		if (!rules.HasValue) return false;
		var types = new[] { "student", "guardian" }.Where(type => !requiredOnly || StudentPortalAccountRules.IsRequired(rules.Value, type)).ToArray();
		if (!item.StudentEnrollmentId.HasValue && types.Any(type => OnboardingRuleConfiguration.Boolean(rules.Value, type == "student" ? "studentAccount" : "guardianAccount"))) return true;
		if (await handoffRepository.AnyAsync(h => h.OnboardingCaseId == item.Id && h.Required && (h.ServiceKey == "student-account" || h.ServiceKey == "guardian-account") && h.Status != StudentOnboardingServiceHandoffStatus.Acknowledged)) return true;
		var requests = await LazyServiceProvider.LazyGetRequiredService<IRepository<ShikshaMitra.Students.Portal.StudentCredentialEmailRequest, Guid>>().GetListAsync(e => e.CaseId == item.Id);
		var manualParent = await SiblingParents.IsParentFlowAsync(item);
		var reusedParent = manualParent && SiblingParents.WasReused(item);
		if (requests.Any(request => Enumerable.Contains(types, request.AccountType) && request.Status != "Sent" &&
			(request.AccountType != "guardian" || !reusedParent) &&
			(request.RequiresSavedOwnerValidation() || StudentPortalAccountRules.Access(rules.Value, request.AccountType) == StudentPortalAccountRules.Optional))) return true;
		return types.Any(type => (type != "guardian" || !manualParent) && StudentPortalAccountRules.IsRequired(rules.Value, type) && OnboardingRuleConfiguration.Boolean(rules.Value, type == "student" ? "sendStudentCredentialEmail" : "sendGuardianCredentialEmail") && !requests.Any(e => e.AccountType == type && e.Status == "Sent"));
	}
}
