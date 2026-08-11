using System;
using System.Collections.Generic;
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
		var blockers = await EvaluateAsync(lead, path, direct);
		if (blockers.Count == 0) return;
		throw new BusinessException(blockers[0].Code)
			.WithData("LeadId", lead.Id)
			.WithData("Stage", lead.Stage)
			.WithData("RegistrationPath", path);
	}

	public async Task<IReadOnlyList<AdmissionCrmRegistrationReadinessBlockerDto>> EvaluateAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmRegistrationPath path,
		bool direct)
	{
		var blockers = new List<AdmissionCrmRegistrationReadinessBlockerDto>();
		void Add(string code, string message, string? actionCode = null) => blockers.Add(new()
		{
			Code = code,
			Message = message,
			ActionCode = actionCode
		});

		if (direct)
		{
			if (path != AdmissionCrmRegistrationPath.DirectRegistration)
				Add("AdmissionCrm:DirectRegistrationPlaybookRequired", "Select a direct-registration playbook before issuing the form.", "select-playbook");
			return blockers;
		}
		if (path == AdmissionCrmRegistrationPath.DirectRegistration)
		{
			Add("AdmissionCrm:DirectRegistrationPathRequiresDirectEndpoint", "Use the direct-registration workflow for this admission path.", "open-direct-registration");
			return blockers;
		}
		if (!lead.OwnerUserId.HasValue)
			Add("AdmissionCrm:RegistrationOwnerRequired", "Assign an inquiry owner before issuing registration.", "assign-owner");
		if (lead.Stage is AdmissionInquiryStage.Rejected or AdmissionInquiryStage.Stale or AdmissionInquiryStage.Archived)
			Add("AdmissionCrm:RegistrationNotAllowedForTerminalLead", "Reopen this applicant before issuing registration.", "reopen-applicant");
		if (lead.Stage == AdmissionInquiryStage.NewInquiry)
			Add("AdmissionCrm:RegistrationFollowUpRequired", "Complete an inquiry follow-up before issuing registration.", "schedule-follow-up");

		if (path == AdmissionCrmRegistrationPath.RegistrationBeforeExam &&
			lead.Stage is not (AdmissionInquiryStage.FollowUp or AdmissionInquiryStage.ExamReady))
			Add("AdmissionCrm:RegistrationMustBeIssuedBeforeExam", "Move the applicant to follow-up or exam-ready before issuing registration.", "review-inquiry");
		else if (path == AdmissionCrmRegistrationPath.RegistrationAfterExam)
		{
			var queryable = await examRepository.GetQueryableAsync();
			var eligible = await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
				x.AcademicYearId == lead.AcademicYearId && x.LeadId == lead.Id &&
				(x.ResultRecordedAt.HasValue || x.EligibilityOverrideUsed) &&
				x.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible));
			if (!eligible || lead.Stage != AdmissionInquiryStage.Eligible)
				Add("AdmissionCrm:RegistrationRequiresEligibleExamResult", "Record an eligible exam result or use an authorized exam override before registration.", "complete-exam");
		}
		else if (path == AdmissionCrmRegistrationPath.ExamWaived &&
			lead.Stage is not (AdmissionInquiryStage.FollowUp or AdmissionInquiryStage.ExamReady or AdmissionInquiryStage.Eligible))
			Add("AdmissionCrm:RegistrationStageNotEligible", "Complete inquiry follow-up before issuing registration.", "review-inquiry");
		else if (!Enum.IsDefined(path))
			Add("AdmissionCrm:InvalidRegistrationPath", "The registration path is not valid.", "review-admission-year");

		return blockers;
	}
}
