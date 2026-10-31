using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public sealed record StudentRollRoster(string RuleJson, string PolicyJson, string Hash, IReadOnlyList<StudentRollBatchRow> Rows, string ScopeLabel, Guid? PolicyVersionId);

/// <summary>Reads the complete division for reservations and freshness; optional validated case IDs bound assign-missing targets.</summary>
[RemoteService(false)]
public class StudentRollRosterReader(IStudentRepository students, IRepository<StudentOnboardingCase, Guid> cases,
	IRepository<StudentEnrollment, Guid> enrollments, IRepository<StudentRecordRenewalCase, Guid> renewals,
	IRepository<StudentRecordTransition, Guid> transitions, IRepository<Division, Guid> divisions,
	IRepository<Standard, Guid> standards, OnboardingConfigurationManager configuration,
	StudentRollSourceReader sources, StudentOnboardingRollRuleReader rollRules,
	StudentRecordsPolicyResolver policies) : ApplicationService, ITransientDependency
{
	public async Task<StudentRollRoster> ReadAsync(PreviewStudentRollBatchInput input)
	{
		var members = await AsyncExecuter.ToListAsync((await enrollments.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == input.AcademicYearId && x.StandardId == input.StandardId && x.DivisionId == input.DivisionId && x.IsActive)
			.Select(x => new { x.Id, x.StudentId, x.RollNumber, x.ConcurrencyStamp }));
		var pending = await AsyncExecuter.ToListAsync((await cases.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == input.AcademicYearId && x.IntendedStandardId == input.StandardId && x.DivisionId == input.DivisionId && !x.StudentEnrollmentId.HasValue && x.Status != StudentOnboardingStatus.Cancelled && x.Status != StudentOnboardingStatus.Completed && x.Status != StudentOnboardingStatus.Provisioning)
			.Select(x => new { x.Id, x.StudentId, x.RollNumber, x.ConcurrencyStamp, x.ExtraProperties }));
		var targetIds = input.CaseIds?.OrderBy(x => x).ToArray();
		var targetCases = targetIds == null ? null : await AsyncExecuter.ToListAsync((await cases.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(targetIds, x.Id))
			.Select(x => new { x.Id, x.StudentId, x.AcademicYearId, x.IntendedStandardId, x.DivisionId, x.StudentEnrollmentId, x.ConcurrencyStamp }));
		if (targetCases != null && (targetCases.Count != targetIds!.Length || targetCases.Any(x =>
			x.AcademicYearId != input.AcademicYearId || (x.StudentEnrollmentId.HasValue
				? !members.Any(member => member.StudentId == x.StudentId && member.Id == x.StudentEnrollmentId)
				: x.IntendedStandardId != input.StandardId || x.DivisionId != input.DivisionId || !pending.Any(item => item.Id == x.Id)))))
			throw new BusinessException(StudentRollNumberErrorCodes.InvalidBatch);
		var targetStudents = targetCases?.Select(x => x.StudentId).ToHashSet();
		var memberIds = members.Select(x => x.StudentId).ToHashSet();
		var studentIds = memberIds.Concat(pending.Select(x => x.StudentId)).Distinct().OrderBy(x => x).ToArray();
		var people = await AsyncExecuter.ToListAsync((await students.GetQueryableWithoutAutoIncludesAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(studentIds, x.Id))
			.Select(x => new { x.Id, x.ConcurrencyStamp, x.ExtraProperties, x.AcademicYearId, x.DivisionId, x.RollNo }));
		var candidates = (await sources.ReadAsync(studentIds)).ToDictionary(x => x.StudentId);
		var linked = await AsyncExecuter.ToListAsync((await cases.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.StudentEnrollmentId.HasValue && Enumerable.Contains(studentIds, x.StudentId) && x.Status != StudentOnboardingStatus.Completed && x.Status != StudentOnboardingStatus.Cancelled)
			.Select(x => new { x.StudentId, x.StudentEnrollmentId, x.RollNumber, x.ConcurrencyStamp, x.ExtraProperties }));
		var division = await AsyncExecuter.SingleAsync((await divisions.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.Id == input.DivisionId).Select(x => new { x.Name, x.ConcurrencyStamp, x.ExtraProperties }));
		var path = await AsyncExecuter.SingleAsync((await standards.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.Id == input.StandardId)
			.Select(x => new { x.Name, StageId = x.EducationMediumStageId, MediumId = x.EducationMediumStage.EducationMediumId, BoardId = x.EducationMediumStage.EducationMedium.EducationBoardId }));
		var settings = await configuration.GetAsync(input.AcademicYearId);
		var rule = settings?.ResolveRule("rollNumber", path.BoardId, path.MediumId, path.StageId, input.StandardId, input.DivisionId, null,
			ShikshaMitra.Students.StudentEnrollments.Admissions.AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now));
		var frozen = rule.HasValue ? JsonNode.Parse(rule.Value.GetRawText())!.AsObject() : new JsonObject();
		frozen["startingNumber"] = 1; frozen["numberingScope"] = "class-division";
		frozen["assignmentMode"] = settings == null ? "manual" : OnboardingRuleConfiguration.Text(settings.Root.GetProperty("rollNumber"), "assignmentMode", "both");
		var json = frozen.ToJsonString(); var ordering = new StudentRollOrdering(StudentOnboardingRollRuleReader.Parse(json));
		var policySnapshot = await policies.ResolveAsync(input.AcademicYearId);
		var policy = policySnapshot.Policy.ToJson();
		var reserved = (await rollRules.GetReservedNumbersAsync(Guid.Empty, input.AcademicYearId, input.StandardId, input.DivisionId, json)).OrderBy(x => x).ToArray();
		var last = await rollRules.LastIssuedAsync(input.AcademicYearId, input.StandardId, input.DivisionId);
		var renewalsPending = await renewals.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == input.AcademicYearId && x.DivisionId == input.DivisionId && (x.Status == "PendingChecklist" || x.Status == "Provisioning"));
		var transitionsPending = await transitions.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.TargetAcademicYearId == input.AcademicYearId && x.TargetDivisionId == input.DivisionId && (x.Status == "Submitted" || x.Status == "Approved" || x.Status == "Scheduled" || x.Status == "AwaitingRenewal" || x.Status == "Exception"));
		string? global = !rule.HasValue ? "Publish a roll-number rule for this class before automatic batch assignment." :
			OnboardingRuleConfiguration.Text(ParseElement(json), "assignmentMode") == "manual" ? "The school requires manual roll assignment." : null;
		Guid?[] academicPath = [path.BoardId, path.MediumId, path.StageId, input.StandardId, input.DivisionId];
		if (settings?.Rules("rollNumber").Any(x => OnboardingRuleConfiguration.IsPublished(x) && OnboardingRuleConfiguration.Text(x, "campusMode", "all") == "selected" &&
			OnboardingRuleConfiguration.Levels.Select((level, i) => OnboardingRuleConfiguration.Text(x, level.Mode) == "all" || academicPath[i].HasValue && OnboardingRuleConfiguration.Ids(x, level.Ids).Contains(academicPath[i]!.Value)).All(x => x)) == true)
			global = "Campus-specific roll rules require review before a full-division batch can be assigned.";
		if (await cases.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == input.AcademicYearId && x.DivisionId == input.DivisionId && x.Status == StudentOnboardingStatus.Provisioning && !x.StudentEnrollmentId.HasValue))
			global = "Wait for the in-progress student activation to finish before numbering this division.";
		if (input.Operation == "renumber" && (renewalsPending || transitionsPending)) global = "Resolve pending promotion or return reservations before renumbering this division.";
		if (input.Operation == "renumber" && Locked(division.ExtraProperties, $"IdentifierGroupLocked:{input.AcademicYearId:N}:roll")) global = "Unlock this division's roll numbers before renumbering.";
		var rows = new List<StudentRollBatchRow>();
		foreach (var id in studentIds)
		{
			if (targetStudents != null && !targetStudents.Contains(id)) continue;
			var person = people.SingleOrDefault(x => x.Id == id);
			if (person == null || !candidates.TryGetValue(id, out var source)) throw new BusinessException(StudentRollNumberErrorCodes.BatchBlocked);
			var enrollment = members.SingleOrDefault(x => x.StudentId == id);
			var caseRows = pending.Where(x => x.StudentId == id).OrderBy(x => x.Id).ToList(); var item = caseRows.FirstOrDefault();
			var current = enrollment?.RollNumber ?? item?.RollNumber;
			if (input.Operation == "assign-missing" && current.HasValue) continue;
			var issue = global ?? ordering.Issue(source);
			var linkedCases = linked.Where(x => x.StudentId == id).ToList();
			if (enrollment != null && linkedCases.Any(x => x.StudentEnrollmentId != enrollment.Id || x.RollNumber != current)) issue ??= "Review the linked onboarding placement before numbering this student.";
			if (enrollment != null && (person.AcademicYearId != input.AcademicYearId || person.DivisionId != input.DivisionId || person.RollNo != enrollment.RollNumber)) issue ??= "Review the student's current placement and roll projection before numbering.";
			if (enrollment == null && caseRows.Count != 1) issue ??= "Resolve duplicate pending onboarding cases before numbering this student.";
			if (input.Operation == "renumber" && current.HasValue && (Locked(person.ExtraProperties, "IdentifierLocked:roll") || item != null && Locked(item.ExtraProperties, "IdentifierLocked:roll") || linkedCases.Any(x => Locked(x.ExtraProperties, "IdentifierLocked:roll")))) issue ??= "Unlock this student's roll number before renumbering.";
			rows.Add(new(id, enrollment == null ? item?.Id : null, enrollment?.Id,
				string.Join(" ", new[] { source.FirstName, source.MiddleName, source.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))), person.ConcurrencyStamp,
				enrollment?.ConcurrencyStamp ?? item!.ConcurrencyStamp, current, null, issue));
		}
		var sequence = input.Operation == "renumber" ? 0L : last;
		if (!rows.Any(x => x.Issue != null))
		{
			var lookup = rows.ToDictionary(x => x.StudentId);
			rows = ordering.Order(rows.Select(x => candidates[x.StudentId])).Select(x => {
				sequence++; return lookup[x.StudentId] with { ProposedRollNumber = sequence <= int.MaxValue ? (int)sequence : null,
					Issue = sequence > int.MaxValue ? "The available roll-number range is exhausted." : null }; }).ToList();
		}
		var basis = JsonSerializer.Serialize(new { json, policy, policySnapshot.VersionId, division.ConcurrencyStamp, last, reserved, targetIds, targetCases = targetCases?.OrderBy(x => x.Id),
			members = members.OrderBy(x => x.Id), pending = pending.OrderBy(x => x.Id), linked = linked.OrderBy(x => x.StudentId), people = people.OrderBy(x => x.Id), sources = candidates.Values.OrderBy(x => x.StudentId) });
		return new(json, policy, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(basis))), rows, path.Name + " / " + division.Name, policySnapshot.VersionId);
	}
	private static JsonElement ParseElement(string json) { using var doc = JsonDocument.Parse(json); return doc.RootElement.Clone(); }
	private static bool Locked(IDictionary<string, object?> values, string key) => values.TryGetValue(key, out var value) && bool.TryParse(value?.ToString(), out var locked) && locked;
}
