using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentComplianceReportQueryService(StudentDirectoryQueryService directory,
	IRepository<StudentIdentityCompliance, Guid> compliance, StudentRecordsPolicyResolver policies,
	StudentRecordsAccessService access, StudentRecordReportAuthority authority) : ApplicationService, ITransientDependency
{
	public async Task<StudentRecordReportPreviewDto> GenerateAsync(StudentRecordReportInput input)
	{
		await authority.EnsureAsync(input.ReportType);
		if (!input.AcademicYearId.HasValue || input.AcademicYearId == Guid.Empty ||
			input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100 || input.Status != null ||
			new[] { input.BoardId, input.MediumId, input.StandardId, input.DivisionId, input.StudentId }.Any(id => id == Guid.Empty))
			throw new BusinessException("Students:Records:InvalidReport");
		var policy = (await policies.ResolveAsync(input.AcademicYearId)).Policy;
		var roles = await access.GetDatabaseRoleNamesAsync();
		var identityHidden = StudentRecordVisibility.IsMasked(policy, "general", roles);
		var trackingHidden = StudentRecordVisibility.IsMasked(policy, "additional", roles);
		if (identityHidden && input.ReportType == "IdentityExceptions")
			throw new BusinessException("Students:Records:RestrictedFields");
		var students = await directory.GetPopulationAsync(new()
		{
			AcademicYearId = input.AcademicYearId, BoardId = input.BoardId, MediumId = input.MediumId,
			StandardId = input.StandardId, DivisionId = input.DivisionId, StudentId = input.StudentId,
			Population = "Active"
		});
		students = students.Where(s => s.AcademicYearId == input.AcademicYearId &&
			(!input.BoardId.HasValue || s.AcademicBoardId == input.BoardId) && (!input.MediumId.HasValue || s.AcademicMediumId == input.MediumId) &&
			(!input.StandardId.HasValue || s.StandardId == input.StandardId) && (!input.DivisionId.HasValue || s.DivisionId == input.DivisionId));
		if (input.ReportType == "GujaratCompliance")
			students = students.Where(s => s.AcademicBoard != null && s.AcademicBoard.Type == EducationBoardType.GSEB);
		var records = (await compliance.GetQueryableAsync()).Where(c => c.TenantId == CurrentTenant.Id);
		var query = from s in students
			join c in records on s.Id equals c.StudentId into joined
			from c in joined.DefaultIfEmpty()
			select new { Student = s, Compliance = c };
		// Exception membership uses persisted statuses, never a client-computed eligibility decision.
		if (input.ReportType == "IdentityExceptions")
			query = query.Where(x => x.Compliance == null ||
				x.Compliance.AadhaarVerificationStatus != IdentityVerificationStatus.Verified ||
				x.Compliance.ApaarVerificationStatus != IdentityVerificationStatus.Verified ||
				x.Compliance.ApaarConsentStatus != ConsentStatus.Granted);
		var total = await AsyncExecuter.CountAsync(query);
		var page = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Student.Id).Skip(input.SkipCount).Take(input.MaxResultCount)
			.Select(x => new StudentComplianceReportRow
			{
				Name = (x.Student.FirstName ?? "") + " " + (x.Student.LastName ?? ""),
				GrNumber = x.Student.GrNumber, GrNo = x.Student.GrNo,
				Board = x.Student.AcademicBoard == null ? null : (EducationBoardType?)x.Student.AcademicBoard.Type,
				Medium = x.Student.AcademicMedium == null ? null : x.Student.AcademicMedium.Name,
				Class = x.Student.Standard == null ? null : x.Student.Standard.Name,
				Division = x.Student.Division == null ? null : x.Student.Division.Name,
				HasCompliance = x.Compliance != null,
				AadhaarLast4 = x.Compliance == null ? null : x.Compliance.AadhaarLast4,
				AadhaarStatus = x.Compliance == null ? IdentityVerificationStatus.NotProvided : x.Compliance.AadhaarVerificationStatus,
				ApaarId = x.Compliance == null ? null : x.Compliance.ApaarId,
				ApaarStatus = x.Compliance == null ? IdentityVerificationStatus.NotProvided : x.Compliance.ApaarVerificationStatus,
				Consent = x.Compliance == null ? ConsentStatus.NotAsked : x.Compliance.ApaarConsentStatus,
				Pen = x.Compliance == null ? null : x.Compliance.PenNumber,
				UdiseCode = x.Compliance == null ? null : x.Compliance.UdiseStudentCode,
				UdiseStatus = x.Compliance == null ? UdiseSyncStatus.NotReady : x.Compliance.UdiseSyncStatus,
				UdiseExported = x.Compliance == null ? null : x.Compliance.UdiseLastExportedAt,
				Rte = x.Compliance != null && x.Compliance.RteStatus,
				Scholarship = x.Compliance == null ? ScholarshipStatus.NotApplicable : x.Compliance.ScholarshipStatus,
				Scheme = x.Compliance == null ? null : x.Compliance.ScholarshipSchemeName,
				Application = x.Compliance == null ? null : x.Compliance.ScholarshipApplicationNo,
				CasteCertificate = x.Compliance == null ? null : x.Compliance.CasteCertificateNo,
				CasteStatus = x.Compliance == null ? IdentityVerificationStatus.NotProvided : x.Compliance.CasteCertificateVerifiedStatus
			}));
		return new()
		{
			AsOf = new DateTimeOffset(Clock.Now.ToUniversalTime()), TotalCount = total,
			Columns = StudentComplianceReportProjection.Columns(input.ReportType),
			Rows = page.Select(row => StudentComplianceReportProjection.Project(row, input.ReportType, identityHidden,
				input.ReportType == "EligibilityTracking" ? trackingHidden : identityHidden)).ToList()
		};
	}
}
