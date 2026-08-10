using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmLeadDuplicateMatcher
{
	internal static string BuildMatchReason(
		AdmissionInquiryLead lead,
		string? normalizedPhone,
		string? normalizedEmail,
		AdmissionCrmDuplicateProfileProbe profile)
	{
		if (!string.IsNullOrWhiteSpace(normalizedPhone) &&
			lead.NormalizedPrimaryContactNo == normalizedPhone)
		{
			return "Contact number";
		}

		if (!string.IsNullOrWhiteSpace(normalizedEmail) &&
			lead.NormalizedEmail == normalizedEmail)
		{
			return "Email";
		}

		if (HasProfileMatch(lead, profile))
		{
			return HasDateOfBirthMatch(lead, profile) && !HasParentGuardianMatch(lead, profile)
				? "Applicant name, date of birth, and desired class"
				: "Applicant name, parent name, and desired class";
		}

		return "Likely duplicate";
	}

	internal static bool AreLikelyDuplicates(AdmissionInquiryLead first, AdmissionInquiryLead second)
	{
		return IsLikelyDuplicate(
			second,
			first.NormalizedPrimaryContactNo,
			first.NormalizedEmail,
			AdmissionCrmDuplicateProfileProbe.FromLead(first));
	}

	internal static bool IsLikelyDuplicate(
		AdmissionInquiryLead lead,
		string? normalizedPhone,
		string? normalizedEmail,
		AdmissionCrmDuplicateProfileProbe profile)
	{
		return
			(!string.IsNullOrWhiteSpace(normalizedPhone) &&
			 lead.NormalizedPrimaryContactNo == normalizedPhone) ||
			(!string.IsNullOrWhiteSpace(normalizedEmail) &&
			 lead.NormalizedEmail == normalizedEmail) ||
			HasProfileMatch(lead, profile);
	}

	internal static bool HasProfileMatch(AdmissionInquiryLead lead, AdmissionCrmDuplicateProfileProbe profile)
	{
		if (!profile.HasMinimumProfile())
		{
			return false;
		}

		if (NormalizeText(lead.FirstName) != profile.FirstName)
		{
			return false;
		}

		if (!AreLastNamesCompatible(lead.LastName, profile.LastName))
		{
			return false;
		}

		if (!HasAdmissionTargetMatch(lead, profile))
		{
			return false;
		}

		return HasParentGuardianMatch(lead, profile) || HasDateOfBirthMatch(lead, profile);
	}

	internal static bool HasAdmissionTargetMatch(AdmissionInquiryLead lead, AdmissionCrmDuplicateProfileProbe profile)
	{
		return
			(profile.TargetStandardId.HasValue && lead.TargetStandardId == profile.TargetStandardId.Value) ||
			(profile.DesiredClassName != null &&
			 NormalizeText(lead.DesiredClassName) == profile.DesiredClassName);
	}

	internal static bool HasParentGuardianMatch(AdmissionInquiryLead lead, AdmissionCrmDuplicateProfileProbe profile)
	{
		return profile.ParentGuardianName != null &&
			   NormalizeText(lead.ParentGuardianName) == profile.ParentGuardianName;
	}

	internal static bool HasDateOfBirthMatch(AdmissionInquiryLead lead, AdmissionCrmDuplicateProfileProbe profile)
	{
		return lead.DateOfBirth.HasValue &&
			   profile.DateOfBirth.HasValue &&
			   lead.DateOfBirth.Value.Date == profile.DateOfBirth.Value.Date;
	}

	internal static bool AreLastNamesCompatible(string? leadLastName, string? profileLastName)
	{
		var normalizedLeadLastName = NormalizeText(leadLastName);
		if (normalizedLeadLastName == null || profileLastName == null)
		{
			return true;
		}

		return normalizedLeadLastName == profileLastName;
	}

	internal static AdmissionCrmLeadDuplicateDto ToDuplicateDto(
		AdmissionInquiryLead lead,
		string? normalizedPhone,
		string? normalizedEmail,
		AdmissionCrmDuplicateProfileProbe profile)
	{
		var matchedFields = BuildMatchedFields(lead, normalizedPhone, normalizedEmail, profile);
		return new AdmissionCrmLeadDuplicateDto
		{
			LeadId = lead.Id,
			Stage = lead.Stage,
			FirstName = lead.FirstName,
			LastName = lead.LastName,
			PrimaryContactNo = lead.PrimaryContactNo,
			Email = lead.Email,
			MatchReason = BuildMatchReason(lead, normalizedPhone, normalizedEmail, profile),
			ConfidenceScore = BuildConfidenceScore(matchedFields),
			MatchedFields = matchedFields,
			LastActivityTime = lead.LastActivityTime
		};
	}

	internal static IReadOnlyList<string> BuildMatchedFields(
		AdmissionInquiryLead lead,
		string? normalizedPhone,
		string? normalizedEmail,
		AdmissionCrmDuplicateProfileProbe profile)
	{
		var fields = new List<string>();
		if (!string.IsNullOrWhiteSpace(normalizedPhone) &&
			lead.NormalizedPrimaryContactNo == normalizedPhone)
		{
			fields.Add("Contact number");
		}

		if (!string.IsNullOrWhiteSpace(normalizedEmail) &&
			lead.NormalizedEmail == normalizedEmail)
		{
			fields.Add("Email");
		}

		if (HasProfileMatch(lead, profile))
		{
			fields.Add("Applicant name");
			if (HasParentGuardianMatch(lead, profile))
			{
				fields.Add("Parent or guardian");
			}

			if (HasDateOfBirthMatch(lead, profile))
			{
				fields.Add("Date of birth");
			}

			fields.Add(profile.TargetStandardId.HasValue ? "Target standard" : "Desired class");
		}

		return fields;
	}

	internal static int BuildConfidenceScore(IReadOnlyCollection<string> matchedFields)
	{
		if (matchedFields.Contains("Contact number"))
		{
			return 100;
		}

		if (matchedFields.Contains("Email"))
		{
			return 95;
		}

		if (matchedFields.Contains("Parent or guardian") &&
			matchedFields.Contains("Date of birth"))
		{
			return 90;
		}

		if (matchedFields.Contains("Date of birth"))
		{
			return 85;
		}

		if (matchedFields.Contains("Parent or guardian"))
		{
			return 80;
		}

		return 50;
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

	internal static string? NormalizePhone(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		var digits = new string(value.Where(char.IsDigit).ToArray());
		return string.IsNullOrWhiteSpace(digits) ? null : digits;
	}
}