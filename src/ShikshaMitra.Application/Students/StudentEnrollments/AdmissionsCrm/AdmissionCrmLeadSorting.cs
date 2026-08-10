using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmLeadSorting
{
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

	internal static IOrderedQueryable<AdmissionInquiryLead> ApplyLeadSorting(
		IQueryable<AdmissionInquiryLead> query,
		string? sorting)
	{
		var descending = sorting?.Contains("desc", StringComparison.OrdinalIgnoreCase) == true;
		var field = sorting?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
		return field switch
		{
			"applicantnumber" => descending
				? query.OrderByDescending(x => x.ApplicantNumber)
				: query.OrderBy(x => x.ApplicantNumber),
			"applicantname" => descending
				? query.OrderByDescending(x => x.FirstName).ThenByDescending(x => x.LastName)
				: query.OrderBy(x => x.FirstName).ThenBy(x => x.LastName),
			"stage" => descending ? query.OrderByDescending(x => x.Stage) : query.OrderBy(x => x.Stage),
			"registrationformnumber" => descending
				? query.OrderByDescending(x => x.RegistrationFormNumber)
				: query.OrderBy(x => x.RegistrationFormNumber),
			"channel" => descending ? query.OrderByDescending(x => x.Channel) : query.OrderBy(x => x.Channel),
			"desiredclassname" => descending
				? query.OrderByDescending(x => x.DesiredClassName)
				: query.OrderBy(x => x.DesiredClassName),
			"intendedroutetype" => descending
				? query.OrderByDescending(x => x.IntendedRouteType)
				: query.OrderBy(x => x.IntendedRouteType),
			"owneruserid" => descending
				? query.OrderByDescending(x => x.OwnerUserId)
				: query.OrderBy(x => x.OwnerUserId),
			"priority" => descending ? query.OrderByDescending(x => x.Priority) : query.OrderBy(x => x.Priority),
			"leadscore" => descending ? query.OrderByDescending(x => x.LeadScore) : query.OrderBy(x => x.LeadScore),
			"lastactivitytime" => descending
				? query.OrderByDescending(x => x.LastActivityTime)
				: query.OrderBy(x => x.LastActivityTime),
			_ => query.OrderByDescending(x => x.LastActivityTime).ThenBy(x => x.Id)
		};
	}

	internal static bool UsesLastActivitySorting(string? sorting)
	{
		var field = sorting?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
		return string.IsNullOrWhiteSpace(field) || field == "lastactivitytime";
	}

	internal static bool IsDateTimeOffsetOrderingNotSupported(NotSupportedException ex) =>
		ex.Message.Contains("DateTimeOffset", StringComparison.OrdinalIgnoreCase) &&
		ex.Message.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase);

	internal static IOrderedEnumerable<AdmissionInquiryLead> ApplyLeadSortingInMemory(
		IEnumerable<AdmissionInquiryLead> leads,
		string? sorting)
	{
		var descending = sorting?.Contains("desc", StringComparison.OrdinalIgnoreCase) == true;
		var field = sorting?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
		return field switch
		{
			"lastactivitytime" => descending
				? leads.OrderByDescending(x => x.LastActivityTime).ThenBy(x => x.Id)
				: leads.OrderBy(x => x.LastActivityTime).ThenBy(x => x.Id),
			_ => leads.OrderByDescending(x => x.LastActivityTime).ThenBy(x => x.Id)
		};
	}

	internal static int? ParseApplicantNumber(string? value)
	{
		var normalized = value?.Trim().TrimStart('#');
		return int.TryParse(normalized, out var applicantNumber) && applicantNumber > 0
			? applicantNumber
			: null;
	}
}