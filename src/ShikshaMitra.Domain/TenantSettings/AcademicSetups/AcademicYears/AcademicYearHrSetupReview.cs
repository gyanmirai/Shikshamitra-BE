using System;
using System.Collections.Generic;
using System.Text.Json;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.TenantSettings.AcademicYears;

/// <summary>Additive advisory evidence. Never initializes or mutates operational configuration.</summary>
public static class AcademicYearHrSetupReview
{
	public const string Property = "HrSetupReviewV1";
	public sealed record Entry(Guid ActorId, DateTimeOffset ReviewedAt, string Fingerprint, Guid? EmployeeId, DateTime? Date);
	public sealed class Document
	{
		public int Version { get; set; } = 1;
		public Dictionary<string, Entry> Sections { get; set; } = [];
	}

	public static Document Read(AcademicYear year)
	{
		var json = year.GetProperty<string?>(Property, null);
		if (json == null) return new();
		try
		{
			var document = JsonSerializer.Deserialize<Document>(json);
			if (document?.Version == 1 && document.Sections != null) return document;
		}
		catch (JsonException) { }
		// Unknown evidence is never claimed as reviewed or silently overwritten.
		throw new BusinessException(HrSetupReviewSections.Invalid);
	}

	public static void Record(AcademicYear year, string section, Entry entry)
	{
		if (entry.ActorId == Guid.Empty || string.IsNullOrWhiteSpace(entry.Fingerprint))
			throw new BusinessException(HrSetupReviewSections.Invalid);
		var document = Read(year);
		document.Sections[section] = entry;
		year.SetProperty(Property, JsonSerializer.Serialize(document));
	}
}
