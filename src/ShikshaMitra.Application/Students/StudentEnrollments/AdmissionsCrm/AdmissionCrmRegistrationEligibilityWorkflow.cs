using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationEligibilityWorkflow(
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
	IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmRegistrationPath> ResolvePathAsync(AdmissionInquiryLead lead, Guid? playbookId)
	{
		if (playbookId.HasValue)
		{
			var playbook = await playbookRepository.FindAsync(playbookId.Value);
			if (playbook == null || playbook.TenantId != CurrentTenant.Id)
				throw new BusinessException("AdmissionCrm:PlaybookNotFound").WithData("PlaybookId", playbookId.Value);
			if (playbook.AcademicYearId != lead.AcademicYearId) throw new BusinessException("AdmissionCrm:PlaybookAcademicYearMismatch")
				.WithData("PlaybookId", playbook.Id).WithData("LeadAcademicYearId", lead.AcademicYearId)
				.WithData("PlaybookAcademicYearId", playbook.AcademicYearId);
			if (playbook.Status != AdmissionCrmPlaybookStatus.Published)
				throw new BusinessException("AdmissionCrm:PublishedPlaybookRequired").WithData("PlaybookId", playbook.Id);
			return playbook.RegistrationPath;
		}
		var queryable = await cycleRepository.GetQueryableAsync();
		var configuration = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == lead.AcademicYearId));
		if (configuration?.EntranceExamRequired == false) return AdmissionCrmRegistrationPath.ExamWaived;
		return configuration?.RegistrationFormRequiredBeforeExam == true
			? AdmissionCrmRegistrationPath.RegistrationBeforeExam : AdmissionCrmRegistrationPath.RegistrationAfterExam;
	}

	public async Task EnsureAsync(AdmissionInquiryLead lead, AdmissionCrmRegistrationPath path, bool direct)
	{
		if (direct)
		{
			if (path != AdmissionCrmRegistrationPath.DirectRegistration)
				throw new BusinessException("AdmissionCrm:DirectRegistrationPlaybookRequired").WithData("LeadId", lead.Id).WithData("RegistrationPath", path);
			return;
		}
		if (path == AdmissionCrmRegistrationPath.DirectRegistration)
			throw new BusinessException("AdmissionCrm:DirectRegistrationPathRequiresDirectEndpoint").WithData("LeadId", lead.Id);
		if (!lead.OwnerUserId.HasValue) throw new BusinessException("AdmissionCrm:RegistrationOwnerRequired").WithData("LeadId", lead.Id);
		if (lead.Stage is AdmissionInquiryStage.Rejected or AdmissionInquiryStage.Stale or AdmissionInquiryStage.Archived)
			throw new BusinessException("AdmissionCrm:RegistrationNotAllowedForTerminalLead").WithData("LeadId", lead.Id).WithData("Stage", lead.Stage);
		if (lead.Stage == AdmissionInquiryStage.NewInquiry)
			throw new BusinessException("AdmissionCrm:RegistrationFollowUpRequired").WithData("LeadId", lead.Id);
		if (path == AdmissionCrmRegistrationPath.RegistrationBeforeExam)
		{
			if (lead.Stage is not (AdmissionInquiryStage.FollowUp or AdmissionInquiryStage.ExamReady))
				throw new BusinessException("AdmissionCrm:RegistrationMustBeIssuedBeforeExam").WithData("LeadId", lead.Id).WithData("Stage", lead.Stage);
			return;
		}
		if (path == AdmissionCrmRegistrationPath.RegistrationAfterExam)
		{
			var queryable = await examRepository.GetQueryableAsync();
			var eligible = await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
				x.AcademicYearId == lead.AcademicYearId && x.LeadId == lead.Id && x.ResultRecordedAt.HasValue &&
				x.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible));
			if (!eligible || lead.Stage != AdmissionInquiryStage.Eligible)
				throw new BusinessException("AdmissionCrm:RegistrationRequiresEligibleExamResult").WithData("LeadId", lead.Id).WithData("Stage", lead.Stage);
			return;
		}
		if (path == AdmissionCrmRegistrationPath.ExamWaived)
		{
			if (lead.Stage is not (AdmissionInquiryStage.FollowUp or AdmissionInquiryStage.ExamReady or AdmissionInquiryStage.Eligible))
				throw new BusinessException("AdmissionCrm:RegistrationStageNotEligible").WithData("LeadId", lead.Id).WithData("Stage", lead.Stage);
			return;
		}
		throw new BusinessException("AdmissionCrm:InvalidRegistrationPath").WithData("RegistrationPath", path);
	}
}
