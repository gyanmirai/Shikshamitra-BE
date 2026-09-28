using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionInquiryIntakeRules
{
	public int Version { get; init; } = 1;
	public AdmissionInquiryDuplicateRule DuplicateRule { get; init; } = AdmissionInquiryDuplicateRule.RequireReason;
	public AdmissionInquiryApplicationStartRequirement ApplicationStartRequirement { get; init; } = AdmissionInquiryApplicationStartRequirement.Captured;
	public int? FirstContactDueInHours { get; init; }
	public int? QualificationDueInDays { get; init; }
	public int FollowUpDueInDays { get; init; } = 3;
	public int MaxOpenFollowUps { get; init; } = 3;
	public bool RequireGuardianEmail { get; init; }
	public bool RequireApplicantDateOfBirth { get; init; }
	public List<AdmissionInquirySource> AllowedSources { get; init; } = [];
	public List<AdmissionInquirySource> RequireSourceDetailFor { get; init; } = [];

	public void Validate()
	{
		if (Version != 1 || !Enum.IsDefined(DuplicateRule) || !Enum.IsDefined(ApplicationStartRequirement))
			throw new ArgumentException("Choose supported inquiry settings.");
		if (FirstContactDueInHours is < 1 or > 720 || QualificationDueInDays is < 1 or > 365 ||
			FollowUpDueInDays is < 1 or > 90 || MaxOpenFollowUps is < 1 or > 20)
			throw new ArgumentException("Choose valid inquiry follow-up deadlines.");
		if (AllowedSources == null || RequireSourceDetailFor == null ||
			AllowedSources.Count > 13 || RequireSourceDetailFor.Count > 13 ||
			AllowedSources.Any(source => !Enum.IsDefined(source)) ||
			RequireSourceDetailFor.Any(source => !Enum.IsDefined(source)) ||
			AllowedSources.Distinct().Count() != AllowedSources.Count ||
			RequireSourceDetailFor.Distinct().Count() != RequireSourceDetailFor.Count ||
			(AllowedSources.Count > 0 && RequireSourceDetailFor.Any(source => !AllowedSources.Contains(source))))
			throw new ArgumentException("Choose unique, supported inquiry sources.");
	}
}

public sealed record AdmissionCounsellorAssignmentRules
{
	public int Version { get; init; } = 1;
	public List<Guid> CounsellorUserIds { get; init; } = [];
	public int MaxActiveCasesPerCounsellor { get; init; } = 100;
	public Guid? FallbackManagerUserId { get; init; }

	public void Validate()
	{
		if (Version != 1 || CounsellorUserIds == null || CounsellorUserIds.Count is < 1 or > 50 ||
			CounsellorUserIds.Any(id => id == Guid.Empty) ||
			CounsellorUserIds.Distinct().Count() != CounsellorUserIds.Count ||
			MaxActiveCasesPerCounsellor is < 1 or > 10000 || FallbackManagerUserId == Guid.Empty)
			throw new ArgumentException("Choose up to 50 unique counsellors and a valid workload limit.");
	}
}
