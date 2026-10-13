using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	private async Task<IQueryable<StudentOnboardingCase>> FilterPendingTaskAsync(IQueryable<StudentOnboardingCase> query, string? task)
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
				if (applications.GetValueOrDefault(item.ApplicationId)?.GetFeeSelection()?.OnboardingConfirmedAt == null) pending.Add(item.Id);
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
				var rules = AutomaticOnboardingReadinessRules.CanAdopt(item)
					? AutomaticOnboardingReadinessRules.Resolve(configurations[item.AcademicYearId])
					: await ReadSavedRulesAsync(item);
				var accountPending = handoffs.Any(h => h.OnboardingCaseId == item.Id) || !item.StudentEnrollmentId.HasValue && rules.HasValue &&
					(OnboardingRuleConfiguration.Boolean(rules.Value, "studentAccount") || OnboardingRuleConfiguration.Boolean(rules.Value, "guardianAccount"));
				var emailPending = rules.HasValue && new[] { "student", "guardian" }.Any(type =>
					OnboardingRuleConfiguration.Boolean(rules.Value, type == "student" ? "sendStudentCredentialEmail" : "sendGuardianCredentialEmail") &&
					!emails.Any(e => e.CaseId == item.Id && e.AccountType == type && e.Status == "Sent"));
				emailPending |= emails.Any(email => email.CaseId == item.Id && email.RequiresSavedOwnerValidation() && email.Status != "Sent");
				if (accountPending || emailPending) pending.Add(item.Id);
			}
		}
		var ids = pending.ToArray(); return query.Where(c => Enumerable.Contains(ids, c.Id));
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

	private async Task<bool> CredentialsPendingAsync(StudentOnboardingCase item)
	{
		if (item.Status == StudentOnboardingStatus.Cancelled) return false;
		var rules = await ReadSavedRulesAsync(item);
		if (!rules.HasValue) return false;
		var types = new[] { "student", "guardian" };
		if (!item.StudentEnrollmentId.HasValue && types.Any(type => OnboardingRuleConfiguration.Boolean(rules.Value, type == "student" ? "studentAccount" : "guardianAccount"))) return true;
		if (await handoffRepository.AnyAsync(h => h.OnboardingCaseId == item.Id && h.Required && (h.ServiceKey == "student-account" || h.ServiceKey == "guardian-account") && h.Status != StudentOnboardingServiceHandoffStatus.Acknowledged)) return true;
		var requests = await LazyServiceProvider.LazyGetRequiredService<IRepository<ShikshaMitra.Students.Portal.StudentCredentialEmailRequest, Guid>>().GetListAsync(e => e.CaseId == item.Id);
		if (requests.Any(request => request.RequiresSavedOwnerValidation() && request.Status != "Sent")) return true;
		return types.Any(type => OnboardingRuleConfiguration.Boolean(rules.Value, type == "student" ? "sendStudentCredentialEmail" : "sendGuardianCredentialEmail") && !requests.Any(e => e.AccountType == type && e.Status == "Sent"));
	}
}
