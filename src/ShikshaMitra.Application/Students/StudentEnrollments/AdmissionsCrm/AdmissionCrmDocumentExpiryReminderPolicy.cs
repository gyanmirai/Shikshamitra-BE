using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmDocumentExpiryReminderPolicy
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static HashSet<Guid> GetExpiringLeadIds(
		IEnumerable<AdmissionCrmDocumentChecklistAssignment> checklists,
		IEnumerable<AdmissionCrmApplicantDocument> documents,
		DateTime now)
	{
		var reminderWindows = BuildReminderWindows(checklists);
		return CurrentDocuments(documents)
			.Where(document => IsReminderDue(document, reminderWindows, now))
			.Select(document => document.LeadId)
			.ToHashSet();
	}

	private static Dictionary<string, int> BuildReminderWindows(
		IEnumerable<AdmissionCrmDocumentChecklistAssignment> checklists)
	{
		var windows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (var checklist in checklists)
		{
			try
			{
				var template = JsonSerializer.Deserialize<AdmissionCrmDocumentChecklistTemplateDto>(
					checklist.TemplateSnapshotJson,
					JsonOptions);
				if (template == null)
				{
					continue;
				}

				foreach (var requirement in template.Requirements.Where(x =>
					         !string.IsNullOrWhiteSpace(x.Key) &&
					         x.ValidityDateRequired &&
					         x.ExpiryReminderDaysBefore is >= 1 and <= 365))
				{
					windows[ReminderKey(checklist.Id, requirement.Key)] =
						requirement.ExpiryReminderDaysBefore!.Value;
				}
			}
			catch (JsonException)
			{
				// A malformed historical snapshot must not stop automation for the whole tenant.
			}
		}

		return windows;
	}

	private static bool IsReminderDue(
		AdmissionCrmApplicantDocument document,
		IReadOnlyDictionary<string, int> reminderWindows,
		DateTime now)
	{
		if (!document.ValidUntil.HasValue ||
		    document.Status is AdmissionCrmApplicantDocumentStatus.Rejected or AdmissionCrmApplicantDocumentStatus.Waived ||
		    !reminderWindows.TryGetValue(
			    ReminderKey(document.ChecklistAssignmentId, document.RequirementKey),
			    out var daysBefore))
		{
			return false;
		}

		var expiryDate = document.ValidUntil.Value.Date;
		var today = now.Date;
		return expiryDate >= today && expiryDate <= today.AddDays(daysBefore);
	}

	private static IEnumerable<AdmissionCrmApplicantDocument> CurrentDocuments(
		IEnumerable<AdmissionCrmApplicantDocument> documents) =>
		documents
			.Where(x => x.SupersededAt == null)
			.GroupBy(x => new { x.ChecklistAssignmentId, x.RequirementKey })
			.Select(x => x.OrderByDescending(document => document.Version).First());

	private static string ReminderKey(Guid checklistAssignmentId, string requirementKey) =>
		$"{checklistAssignmentId:N}:{requirementKey.Trim()}";
}
