using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public sealed record StudentOnboardingRollProposal(string AssignmentMode, int RollNumber);

/// <summary>One owner allocator serves actual initial onboarding and annual/return reservation commands.</summary>
[RemoteService(false)]
public class StudentOnboardingRollNumberAllocator(StudentOnboardingRollRuleReader rules) : ApplicationService, ITransientDependency
{
	public static string ResolveMode(string ruleJson, string? requestedMode)
	{
		var rule = StudentOnboardingRollRuleReader.Parse(ruleJson);
		var schoolMode = rule.HasValue ? OnboardingRuleConfiguration.Text(rule.Value, "assignmentMode", "manual") : "manual";
		if (schoolMode is not ("manual" or "automatic" or "both") || requestedMode is not (null or "manual" or "automatic")) throw OnboardingRuleConfiguration.Invalid("Choose a supported manual or automatic roll assignment mode.");
		if (schoolMode == "both" && requestedMode == null) throw OnboardingRuleConfiguration.Invalid("Choose manual or automatic roll assignment before continuing.");
		var mode = requestedMode ?? (schoolMode == "automatic" ? "automatic" : "manual");
		if (schoolMode != "both" && schoolMode != mode) throw OnboardingRuleConfiguration.Invalid("This roll assignment mode is not allowed by the school.");
		return mode;
	}
	public async Task<StudentOnboardingRollProposal> ProposeAsync(Guid studentId, Guid yearId, Guid standardId, Guid divisionId,
		int? enteredRoll, string? requestedMode, string ruleJson, string reason, IEnumerable<int>? cohortReservations = null)
	{
		var mode = ResolveMode(ruleJson, requestedMode); var rule = StudentOnboardingRollRuleReader.Parse(ruleJson);
		if (mode == "manual")
		{
			await rules.ValidateAsync(studentId, yearId, standardId, divisionId, enteredRoll, reason, ruleJson);
			return new(mode, enteredRoll!.Value);
		}
		if (rule.HasValue && (OnboardingRuleConfiguration.Text(rule.Value, "midYearHandling", "next-available") is "insert-and-recalculate" or "manual" ||
			!OnboardingRuleConfiguration.Boolean(rule.Value, "preserveExisting", true)))
			throw OnboardingRuleConfiguration.Invalid("This automatic rule requires a governed roster recalculation; use a rule that preserves existing roll numbers.");
		if (rule.HasValue && OnboardingRuleConfiguration.Text(rule.Value, "assignmentTiming", "after-division") is "first-academic-day" or "manual-bulk")
			throw OnboardingRuleConfiguration.Invalid("This command reserves the roll before enrollment activation. Choose after-division or during-activation timing; first-day and manual-bulk numbering require their owning workflow.");
		var used = await rules.GetReservedNumbersAsync(studentId, yearId, standardId, divisionId, ruleJson);
		used.AddRange(cohortReservations ?? []);
		used.Add(await rules.LastIssuedAsync(yearId, standardId, divisionId));
		var start = rule.HasValue ? OnboardingRuleConfiguration.Number(rule.Value, "startingNumber", 1) : 1;
		if (used.Any(x => x == int.MaxValue)) throw OnboardingRuleConfiguration.Invalid("The available roll number range is exhausted.");
		var proposedValue = Math.Max(start, used.Count == 0 ? start : (long)used.Max() + 1);
		if (proposedValue > int.MaxValue || proposedValue < 1) throw OnboardingRuleConfiguration.Invalid("The available roll number range is exhausted.");
		var proposed = checked((int)proposedValue);
		await rules.ValidateAsync(studentId, yearId, standardId, divisionId, proposed, reason, ruleJson);
		return new(mode, proposed);
	}
}
