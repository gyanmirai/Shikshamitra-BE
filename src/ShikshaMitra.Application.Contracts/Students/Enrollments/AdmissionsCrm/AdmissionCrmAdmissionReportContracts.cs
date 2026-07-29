using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class GetAdmissionCrmAdmissionReportInput
{
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public Guid? TargetStandardId { get; set; }
}

public class AdmissionCrmAdmissionReportDto
{
	public Guid AcademicYearId { get; set; }
	public DateTime GeneratedAt { get; set; }
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public Guid? TargetStandardId { get; set; }
	public bool IsScopeRestricted { get; set; }
	public int TotalApplicantCount { get; set; }
	public int OpenApplicantCount { get; set; }
	public int ActivatedApplicantCount { get; set; }
	public int RejectedApplicantCount { get; set; }
	public int WithdrawnApplicantCount { get; set; }
	public int UnassignedStandardApplicantCount { get; set; }
	public IReadOnlyList<AdmissionCrmAdmissionReportLifecycleDto> Lifecycle { get; set; } = [];
	public IReadOnlyList<AdmissionCrmAdmissionReportSourceDto> Sources { get; set; } = [];
	public IReadOnlyList<AdmissionCrmAdmissionReportStandardDto> Standards { get; set; } = [];
	public IReadOnlyList<AdmissionCrmAdmissionReportOutcomeDto> Outcomes { get; set; } = [];
}

public class AdmissionCrmAdmissionReportLifecycleDto
{
	public AdmissionCrmPipelineColumnKey Column { get; set; }
	public string ColumnName { get; set; } = string.Empty;
	public int ApplicantCount { get; set; }
	public decimal ShareOfApplicants { get; set; }
}

public class AdmissionCrmAdmissionReportSourceDto
{
	public string SourceName { get; set; } = string.Empty;
	public int InquiryCount { get; set; }
	public int ExamScheduledCount { get; set; }
	public int ActivatedCount { get; set; }
	public decimal ActivationRate { get; set; }
}

public class AdmissionCrmAdmissionReportStandardDto
{
	public Guid? TargetStandardId { get; set; }
	public string StandardName { get; set; } = string.Empty;
	public int InquiryCount { get; set; }
	public int ExamScheduledCount { get; set; }
	public int ExamCompletedCount { get; set; }
	public int EligibleCount { get; set; }
	public int RegistrationPendingCount { get; set; }
	public int RegistrationVerifiedCount { get; set; }
	public int FinancePendingCount { get; set; }
	public int ActivationPendingCount { get; set; }
	public int ActivatedCount { get; set; }
	public int RejectedCount { get; set; }
	public int WithdrawnCount { get; set; }
}

public class AdmissionCrmAdmissionReportOutcomeDto
{
	public AdmissionCrmClosedOutcome Outcome { get; set; }
	public string OutcomeName { get; set; } = string.Empty;
	public int ApplicantCount { get; set; }
	public decimal ShareOfApplicants { get; set; }
}

public interface IAdmissionCrmReportAppService : IApplicationService
{
	Task<AdmissionCrmAdmissionReportDto> GetAdmissionReportAsync(
		Guid academicYearId,
		GetAdmissionCrmAdmissionReportInput input);
}
