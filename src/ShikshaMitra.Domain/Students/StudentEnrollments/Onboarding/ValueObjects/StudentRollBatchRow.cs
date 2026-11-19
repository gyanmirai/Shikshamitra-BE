using System;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public sealed record StudentRollBatchRow(Guid StudentId, Guid? CaseId, Guid? EnrollmentId,
	string StudentName, string StudentStamp, string SourceStamp, int? CurrentRollNumber,
	int? ProposedRollNumber, string? Issue);

public sealed record StudentRollBatchDecision(Guid ActorId, int Stage, bool Approved, string Reason, DateTimeOffset At);
