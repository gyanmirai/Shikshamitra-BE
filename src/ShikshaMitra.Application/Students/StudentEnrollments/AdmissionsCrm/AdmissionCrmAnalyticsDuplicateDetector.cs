using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Students.StudentEnrollments.Settings;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmAnalyticsDuplicateDetector
{
internal static int CountLikelyDuplicateLeads(IReadOnlyCollection<AdmissionInquiryLead> leads)
	{
		var duplicateLeadIds = new HashSet<Guid>();
		foreach (var group in leads
			         .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedPrimaryContactNo))
			         .GroupBy(x => x.NormalizedPrimaryContactNo)
			         .Where(x => x.Count() > 1))
		{
			foreach (var lead in group)
			{
				duplicateLeadIds.Add(lead.Id);
			}
		}

		foreach (var group in leads
			         .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedEmail))
			         .GroupBy(x => x.NormalizedEmail)
			         .Where(x => x.Count() > 1))
		{
			foreach (var lead in group)
			{
				duplicateLeadIds.Add(lead.Id);
			}
		}

		foreach (var group in leads
			         .Where(HasDuplicateProfile)
			         .GroupBy(x => new
			         {
				         FirstName = NormalizeText(x.FirstName),
				         AdmissionTarget = GetDuplicateTargetKey(x)
			         })
			         .Where(x => x.Count() > 1))
		{
			var groupedLeads = group.ToList();
			foreach (var lead in groupedLeads)
			{
				if (groupedLeads.Any(candidate =>
					    candidate.Id != lead.Id &&
					    AreLastNamesCompatible(candidate.LastName, lead.LastName) &&
					    HasDuplicateIdentityMatch(candidate, lead)))
				{
					duplicateLeadIds.Add(lead.Id);
				}
			}
		}

		return duplicateLeadIds.Count;
	}

internal static bool HasDuplicateProfile(AdmissionInquiryLead lead)
	{
		return
			NormalizeText(lead.FirstName) != null &&
			(NormalizeText(lead.ParentGuardianName) != null || lead.DateOfBirth.HasValue) &&
			GetDuplicateTargetKey(lead) != null;
	}

internal static string? GetDuplicateTargetKey(AdmissionInquiryLead lead)
	{
		if (lead.TargetStandardId.HasValue)
		{
			return "standard:" + lead.TargetStandardId.Value.ToString("D");
		}

		var desiredClassName = NormalizeText(lead.DesiredClassName);
		return desiredClassName == null ? null : "class:" + desiredClassName;
	}

internal static bool AreLastNamesCompatible(string? firstLastName, string? secondLastName)
	{
		var normalizedFirstLastName = NormalizeText(firstLastName);
		var normalizedSecondLastName = NormalizeText(secondLastName);
		return
			normalizedFirstLastName == null ||
			normalizedSecondLastName == null ||
			normalizedFirstLastName == normalizedSecondLastName;
	}

internal static bool HasDuplicateIdentityMatch(AdmissionInquiryLead first, AdmissionInquiryLead second)
	{
		var firstParent = NormalizeText(first.ParentGuardianName);
		var secondParent = NormalizeText(second.ParentGuardianName);
		if (firstParent != null && firstParent == secondParent)
		{
			return true;
		}

		return first.DateOfBirth.HasValue &&
		       second.DateOfBirth.HasValue &&
		       first.DateOfBirth.Value.Date == second.DateOfBirth.Value.Date;
	}

internal static string? NormalizeText(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		return string.Join(
			" ",
			value
				.Trim()
				.ToLowerInvariant()
				.Split(' ', StringSplitOptions.RemoveEmptyEntries));
	}
}

