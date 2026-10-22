using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionAssessmentPolicy
{
	public int Version { get; init; }
	public bool Enabled { get; init; }
	public bool SchedulingEnabled { get; init; }
	public bool AttendanceRequired { get; init; }
	public bool ScoringEnabled { get; init; }
	public decimal TotalMarks { get; init; }
	public decimal PassingMarks { get; init; }
	public bool ReviewRequired { get; init; }
	public int MaxAttempts { get; init; }
	public string ScoringMode { get; init; } = "total";
	public List<AdmissionAssessmentClassProfile> ClassProfiles { get; init; } = [];
	public AdmissionAssessmentClassProfile? EffectiveProfile { get; init; }

	public static AdmissionAssessmentPolicy? Read(string? configurationJson)
	{
		if (string.IsNullOrWhiteSpace(configurationJson)) return null;
		using var document = JsonDocument.Parse(configurationJson);
		if (!document.RootElement.TryGetProperty("assessment", out var assessment)) return null;
		if (assessment.ValueKind != JsonValueKind.Object)
			throw new ArgumentException("Assessment configuration must be an object.");
		var policy = assessment.Deserialize<AdmissionAssessmentPolicy>(new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		}) ?? throw new ArgumentException("Assessment configuration is required.");
		if (!policy.Enabled) return null;
		policy.Validate();
		return policy;
	}

	public void Validate()
	{
		if (Version is not (1 or 2)) throw new ArgumentException("Choose a supported assessment configuration version.");
		if (MaxAttempts is < 1 or > 10) throw new ArgumentException("Assessment attempts must be between 1 and 10.");
		if (ScoringMode is not ("total" or "subjects") || Version == 1 && ScoringMode != "total")
			throw new ArgumentException("Choose a supported assessment scoring mode.");
		if (ScoringMode == "subjects")
		{
			if (!ScoringEnabled) throw new ArgumentException("Subject marks require scored assessment results.");
			if (ClassProfiles == null || ClassProfiles.Count == 0) throw new ArgumentException("Add a class assessment profile.");
			var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var scopes = new HashSet<(Guid, Guid, Guid)>();
			foreach (var profile in ClassProfiles)
			{
				if (profile == null) throw new ArgumentException("Choose a valid class assessment profile.");
				profile.Validate();
				if (!ids.Add(profile.Id)) throw new ArgumentException("Class assessment profile identifiers must be unique.");
				foreach (var standardId in profile.StandardIds)
					if (!scopes.Add((profile.BoardId, profile.MediumId, standardId))) throw new ArgumentException("A class can belong to only one assessment profile.");
			}
			EffectiveProfile?.Validate();
			return;
		}
		if (ScoringEnabled && (TotalMarks <= 0 || TotalMarks > 10000 || PassingMarks < 0 || PassingMarks > TotalMarks))
			throw new ArgumentException("Passing marks must be between zero and the positive total marks.");
	}

	public AdmissionAssessmentPolicy Resolve(Guid boardId, Guid mediumId, Guid standardId)
	{
		if (ScoringMode != "subjects") return this;
		var matching = ClassProfiles.Where(profile => profile.BoardId == boardId && profile.MediumId == mediumId && profile.StandardIds.Contains(standardId)).ToList();
		if (matching.Count != 1) throw new ArgumentException("Configure exactly one assessment profile for this Offering's board, medium and class.");
		var profile = matching[0] with { StandardIds = [standardId], Subjects = matching[0].Subjects.Select(subject => subject with { }).ToList() };
		return this with { EffectiveProfile = profile, TotalMarks = profile.Subjects.Sum(subject => subject.TotalMarks), PassingMarks = profile.PassingMarks };
	}
}

public sealed record AdmissionAssessmentClassProfile
{
	public string Id { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public Guid BoardId { get; init; }
	public Guid MediumId { get; init; }
	public List<Guid> StandardIds { get; init; } = [];
	public string PassRule { get; init; } = "overall";
	public decimal PassingMarks { get; init; }
	public List<AdmissionAssessmentSubject> Subjects { get; init; } = [];

	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Id) || Id.Length > 64 || string.IsNullOrWhiteSpace(Name) || Name.Length > 128 || BoardId == Guid.Empty || MediumId == Guid.Empty ||
			StandardIds == null || StandardIds.Count == 0 || StandardIds.Any(id => id == Guid.Empty) || StandardIds.Distinct().Count() != StandardIds.Count)
			throw new ArgumentException("Choose a name, board, medium and distinct classes for each assessment profile.");
		if (PassRule is not ("overall" or "everySubject" or "both")) throw new ArgumentException("Choose an assessment passing rule.");
		if (Subjects == null || Subjects.Count == 0) throw new ArgumentException("Add an assessment subject.");
		var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var catalogIds = new HashSet<Guid>();
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var subject in Subjects)
		{
			if (subject == null || string.IsNullOrWhiteSpace(subject.Code) || subject.Code.Length > 64 || !codes.Add(subject.Code) ||
				string.IsNullOrWhiteSpace(subject.Name) || subject.Name.Length > 128 || !names.Add(subject.Name.Trim()) || subject.SubjectId == Guid.Empty ||
				subject.SubjectId.HasValue && !catalogIds.Add(subject.SubjectId.Value)) throw new ArgumentException("Each assessment subject needs a unique identifier and name.");
			if (subject.TotalMarks <= 0 || subject.TotalMarks > 10000 || subject.PassingMarks.HasValue && (subject.PassingMarks < 0 || subject.PassingMarks > subject.TotalMarks) ||
				(PassRule is "everySubject" or "both") && !subject.PassingMarks.HasValue) throw new ArgumentException("Set positive subject maximum marks and valid passing marks for the selected rule.");
		}
		var total = Subjects.Sum(subject => subject.TotalMarks);
		if (total > 10000 || PassingMarks < 0 || PassingMarks > total) throw new ArgumentException("Overall passing marks must be within the combined maximum, up to 10000.");
	}
}

public sealed record AdmissionAssessmentSubject
{
	public string Code { get; init; } = string.Empty;
	public Guid? SubjectId { get; init; }
	public string Name { get; init; } = string.Empty;
	public decimal TotalMarks { get; init; }
	public decimal? PassingMarks { get; init; }
}

public sealed record AdmissionAssessmentSubjectScore
{
	public string Code { get; init; } = string.Empty;
	public decimal? Marks { get; init; }
}

public static class AdmissionAssessmentErrorCodes
{
	public const string InvalidPolicy = "Admissions:Assessment:InvalidPolicy";
	public const string InvalidState = "Admissions:Assessment:InvalidState";
	public const string EvidenceRequired = "Admissions:Assessment:EvidenceRequired";
	public const string CapacityExceeded = "Admissions:Assessment:CapacityExceeded";
	public const string ConcurrencyConflict = "Admissions:Assessment:ConcurrencyConflict";
	public const string RouteRequired = "Admissions:Assessment:RouteRequired";
}
