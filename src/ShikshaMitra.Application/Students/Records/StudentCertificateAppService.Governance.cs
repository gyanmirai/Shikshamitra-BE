using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

public partial class StudentCertificateAppService
{
	private void ValidateCertificateType(StudentCertificateType type)
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue) throw new BusinessException("Students:Records:TenantRequired");
		if (!Enum.IsDefined(type)) throw new BusinessException("Students:Records:UnsupportedCertificateType");
	}

	private async Task<string?> ValidateIssuanceAsync(Student student, StudentCertificatePreviewRequestDto input)
	{
		ValidateCertificateType(input.CertificateType);
		var policy = (await recordsPolicies.ResolveAsync(student.AcademicYearId)).Policy;
		if (!policy.Documents.EnabledCertificateTypes.Contains(input.CertificateType.ToString())) throw new BusinessException("Students:Records:CertificateDisabled");
		if (input.CertificateFields == null || input.CertificateFields.Count > 100 || input.CertificateFields.Any(f => f.Key.Length > 100 || f.Value?.Length > 4000))
			throw new BusinessException("Students:Records:InvalidCertificateFields");
		if (input.CertificateType != StudentCertificateType.Leaving) return null;
		if (!input.TransitionId.HasValue) throw new BusinessException("Students:Records:LeavingExitRequired");
		var exit = await transitions.FindAsync(x => x.Id == input.TransitionId && x.TenantId == CurrentTenant.Id && x.StudentId == student.Id);
		if (exit == null || exit.Outcome is not ("Withdraw" or "Transfer" or "Graduate")) throw new BusinessException("Students:Records:LeavingExitInvalid");
		if (await transitions.AnyAsync(t => t.TenantId == CurrentTenant.Id && t.StudentId == student.Id && t.CorrectionOfId == exit.Id && t.Status == "Applied"))
			throw new BusinessException("Students:Records:LeavingExitInvalid");
		// Graduation certificates require the completed graduation, even when withdrawal certificates are allowed on approval.
		if (exit.Outcome == "Graduate" && (exit.Status != "Applied" || exit.EffectiveDate.Date > AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now)))
			throw new BusinessException("Students:Records:LeavingExitNotReady");
		var permitted = policy.Documents.LeavingCertificateStage switch
		{
			"ApprovedExit" => exit.Status is "Approved" or "Scheduled" or "Applied",
			"EffectiveExit" => exit.Status == "Applied" && exit.EffectiveDate.Date <= AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now),
			_ => false
		};
		if (!permitted) throw new BusinessException("Students:Records:LeavingExitNotReady");
		return JsonSerializer.Serialize(new { exit.Id, exit.StudentId, exit.Outcome, exit.Status, exit.EffectiveDate, exit.Reason, policy.Documents.LeavingCertificateStage }, JsonOptions);
	}

	private static void ValidateDocumentReason(StudentDocumentActionDto? input)
	{
		if (input == null || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length < 5 || input.Reason.Length > 2000)
			throw new BusinessException("Students:Records:DocumentReasonRequired");
	}

	private Task AddDocumentEventAsync(StudentCertificate certificate, string action, string? reason) => documentEvents.InsertAsync(
		new StudentRecordDocumentEvent(GuidGenerator.Create(), CurrentTenant.Id!.Value, certificate.StudentId, certificate.Id,
			CurrentUser.Id!.Value, "Certificate", action, reason, certificate.AcademicYearId), autoSave: true);

	[UnitOfWork(true)]
	public async Task<StudentCertificatePdfDto> ReprintAsync(Guid certificateId, StudentDocumentActionDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.Add);
		ValidateDocumentReason(input);
		var certificate = await LoadScopedCertificateAsync(certificateId, "reprint certificate");
		if (certificate.Status == StudentCertificateStatus.Cancelled) throw new BusinessException("Students:Records:DocumentVoided");
		var pdf = await GeneratePdfAsync(certificateId);
		certificate.MarkPrinted(); await certificateRepository.UpdateAsync(certificate, autoSave: true);
		await AddDocumentEventAsync(certificate, "Reprint", input.Reason);
		return pdf;
	}

	[UnitOfWork(true)]
	public async Task<StudentCertificateResponseDto> VoidAsync(Guid certificateId, StudentDocumentActionDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.Add);
		ValidateDocumentReason(input);
		var certificate = await LoadScopedCertificateAsync(certificateId, "void certificate");
		if (certificate.Status != StudentCertificateStatus.Cancelled)
		{
			certificate.Cancel(); await certificateRepository.UpdateAsync(certificate, autoSave: true);
			await AddDocumentEventAsync(certificate, "Void", input.Reason);
		}
		return BuildStoredCertificateResponse(certificate);
	}
}
