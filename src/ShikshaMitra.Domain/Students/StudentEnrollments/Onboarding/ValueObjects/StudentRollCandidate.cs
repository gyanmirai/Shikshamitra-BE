using System;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public sealed record StudentRollCandidate(Guid StudentId, string? FirstName, string? MiddleName,
	string? LastName, GenderEnum? Gender, string? GrNumber, string? AdmissionNumber, DateTime? AdmissionDate);
