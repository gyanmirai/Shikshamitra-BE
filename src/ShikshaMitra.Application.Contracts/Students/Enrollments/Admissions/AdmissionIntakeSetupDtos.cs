using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryIntakeRulesDto
{
	public AdmissionInquiryDuplicateRule DuplicateRule { get; set; } = AdmissionInquiryDuplicateRule.RequireReason;
	public AdmissionInquiryApplicationStartRequirement ApplicationStartRequirement { get; set; } =
		AdmissionInquiryApplicationStartRequirement.Captured;
	public int? FirstContactDueInHours { get; set; } = 24;
	public int? QualificationDueInDays { get; set; } = 7;
	public int FollowUpDueInDays { get; set; } = 3;
	public int MaxOpenFollowUps { get; set; } = 3;
	public bool RequireGuardianEmail { get; set; }
	public bool RequireApplicantDateOfBirth { get; set; }
	public List<AdmissionInquirySource> AllowedSources { get; set; } = [];
	public List<AdmissionInquirySource> RequireSourceDetailFor { get; set; } = [];
}

public class AdmissionCounsellorAssignmentRulesDto
{
	public List<Guid> CounsellorUserIds { get; set; } = [];
	public int MaxActiveCasesPerCounsellor { get; set; } = 100;
	public Guid FallbackManagerUserId { get; set; }
}

public class CreateAdmissionIntakeSetupDto
{
	public string Name { get; set; } = string.Empty;
	public AdmissionInquiryIntakeRulesDto InquiryRules { get; set; } = new();
	public AdmissionCounsellorAssignmentRulesDto CounsellorAssignmentRules { get; set; } = new();
}

public class UpdateAdmissionIntakeSetupDto : CreateAdmissionIntakeSetupDto
{
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class ArchiveAdmissionIntakeSetupDto
{
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class RestoreAdmissionIntakeSetupDto
{
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class AdmissionIntakeSetupDto : EntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
	public Guid InquiryIntakeProfileId { get; set; }
	public Guid CounsellorAssignmentProfileId { get; set; }
	public AdmissionInquiryIntakeRulesDto InquiryRules { get; set; } = new();
	public AdmissionCounsellorAssignmentRulesDto CounsellorAssignmentRules { get; set; } = new();
	public bool IsArchived { get; set; }
	public DateTime? ArchivedTime { get; set; }
	public int ActiveOfferingCount { get; set; }
	public int ActivePublicInquiryLinkCount { get; set; }
	public bool IsConfigurationValid { get; set; } = true;
	public string? ConfigurationIssue { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class AdmissionIntakeSetupOfferingUsageDto : EntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public Guid AdmissionCycleId { get; set; }
	public AdmissionEntryMode EntryMode { get; set; }
	public bool IsActive { get; set; }
}

public class AdmissionIntakeSetupPublicLinkUsageDto : EntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
	public Guid AdmissionCycleId { get; set; }
	public DateTime? ExpiresAtUtc { get; set; }
	public bool IsActive { get; set; }
	public bool IsOperational { get; set; }
}

public class AdmissionIntakeSetupUsageDto
{
	public Guid SetupId { get; set; }
	public List<AdmissionIntakeSetupOfferingUsageDto> Offerings { get; set; } = [];
	public List<AdmissionIntakeSetupPublicLinkUsageDto> PublicInquiryLinks { get; set; } = [];
	public int ActiveOfferingCount => Offerings.Count(item => item.IsActive);
	public int ActivePublicInquiryLinkCount => PublicInquiryLinks.Count(item => item.IsOperational);
	public bool HasActiveUsage => ActiveOfferingCount > 0 || ActivePublicInquiryLinkCount > 0;
}
