using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionDocumentPolicy
{
	public int Version { get; init; }
	public bool Enabled { get; init; }
	public bool AllowWaivers { get; init; }
	public bool RequireIndependentReview { get; init; }
	public string? CollectionFormStageCode { get; init; }
	public List<AdmissionDocumentRequirement> Requirements { get; init; } = [];
	public static AdmissionDocumentPolicy? Read(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return null;
		using var document = JsonDocument.Parse(json);
		if (!document.RootElement.TryGetProperty("documentVerification", out var value)) return null;
		var policy = value.Deserialize<AdmissionDocumentPolicy>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
			?? throw new ArgumentException("Document verification settings are required.");
		if (!policy.Enabled) return null;
		policy.Validate();
		return policy;
	}
	public void Validate()
	{
		if (Version != 1) throw new ArgumentException("Document verification version must be 1.");
		if (CollectionFormStageCode != null && !Regex.IsMatch(CollectionFormStageCode, "^[A-Z][A-Z0-9_]{0,63}$"))
			throw new ArgumentException("Choose a valid Form stage for early document collection.");
		if (Requirements == null || Requirements.Count is < 1 or > 30)
			throw new ArgumentException("Configure between 1 and 30 document requirements.");
		var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var requirement in Requirements)
		{
			if (requirement == null) throw new ArgumentException("Document requirements must be objects.");
			requirement.Validate();
			if (!codes.Add(requirement.Code)) throw new ArgumentException("Document requirement codes must be unique.");
		}
	}
}

public sealed record AdmissionDocumentRequirement
{
	public string Code { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string Requirement { get; init; } = "required";
	public string? ConditionJson { get; init; }
	public string CollectionMode { get; init; } = "upload";
	public List<string> AllowedExtensions { get; init; } = ["pdf", "jpg", "jpeg", "png"];
	public int MaxFileSizeMb { get; init; } = 10;
	public Guid? DocumentTypeId { get; init; }
	public List<Guid>? StandardIds { get; init; }
	public void Validate()
	{
		if (Code == null || !Regex.IsMatch(Code, "^[A-Z][A-Z0-9_]{0,63}$")) throw new ArgumentException("Use a unique uppercase document code beginning with a letter, up to 64 characters.");
		if (string.IsNullOrWhiteSpace(Name) || Name.Length > 128 || Description?.Length > 1024) throw new ArgumentException("Document names are required (128 characters maximum); descriptions allow 1024 characters.");
		if (Requirement is not ("required" or "optional" or "conditional")) throw new ArgumentException("Choose required, optional, or conditional.");
		if (CollectionMode is not ("upload" or "original" or "either" or "both")) throw new ArgumentException("Choose a supported document collection mode.");
		if (MaxFileSizeMb is < 1 or > 20) throw new ArgumentException("Document size must be between 1 and 20 MB.");
		if (AllowedExtensions == null || AllowedExtensions.Count is < 1 or > 4 || AllowedExtensions.Any(value => value is not ("pdf" or "jpg" or "jpeg" or "png")) || AllowedExtensions.Distinct().Count() != AllowedExtensions.Count)
			throw new ArgumentException("Choose unique supported document formats: pdf, jpg, jpeg, png.");
		if (DocumentTypeId == Guid.Empty) throw new ArgumentException("Choose a valid document type.");
		if (StandardIds != null && (StandardIds.Count is < 1 or > 100 || StandardIds.Contains(Guid.Empty) || StandardIds.Distinct().Count() != StandardIds.Count))
			throw new ArgumentException("Select between 1 and 100 different classes, or choose all classes.");
		if (Requirement == "conditional") AdmissionDocumentCondition.Validate(ConditionJson);
		else if (!string.IsNullOrWhiteSpace(ConditionJson)) throw new ArgumentException("Conditions belong to conditional requirements only.");
	}
}

public static class AdmissionDocumentErrorCodes
{
	public const string InvalidPolicy = "Admissions:Documents:InvalidPolicy";
	public const string InvalidState = "Admissions:Documents:InvalidState";
	public const string ConcurrencyConflict = "Admissions:Documents:ConcurrencyConflict";
	public const string EvidenceRequired = "Admissions:Documents:EvidenceRequired";
	public const string InvalidEvidence = "Admissions:Documents:InvalidEvidence";
	public const string ReviewNotAllowed = "Admissions:Documents:ReviewNotAllowed";
}
