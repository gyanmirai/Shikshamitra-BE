using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicWithdrawalWorkflow(
	AdmissionCrmPublicAccess accessWorkflow,
	AdmissionCrmPublicStatusWorkflow statusWorkflow,
	AdmissionCrmPublicRegistrationWorkflow registrationWorkflow,
	AdmissionCrmPublicSeatWithdrawalWorkflow seatWorkflow,
	AdmissionCrmPublicStatusCoreReader coreReader,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository) : ApplicationService, ITransientDependency
{
	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionApplicantStatusDto> WithdrawAsync(
		Guid tenantId, string token, WithdrawPublicAdmissionApplicationDto input)
	{
		using (CurrentTenant.Change(tenantId))
		using (var uow = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true))
		{
			var (access, lead) = await accessWorkflow.GetAsync(token);
			await accessWorkflow.EnsureAccessSessionAsync(access, lead, input.AccessSessionToken);
			var activation = await coreReader.GetActivationAsync(lead.Id);
			if (lead.StudentId != null || lead.Stage is AdmissionInquiryStage.Archived or AdmissionInquiryStage.Rejected ||
				activation is { Status: not AdmissionCrmActivationStatus.Cancelled })
			{
				throw new BusinessException("AdmissionCrm:ApplicationCannotBeWithdrawn");
			}

			var reason = input.Reason.Trim();
			var now = Clock.Now;
			await registrationWorkflow.WithdrawIfActiveAsync(lead, reason);
			await seatWorkflow.ReleaseAsync(lead, reason, now);
			lead.Archive($"Withdrawn by family: {reason}", now);
			await leadRepository.UpdateAsync(lead, autoSave: true);
			await timelineRepository.InsertAsync(
				new AdmissionCrmTimelineEvent(
					GuidGenerator.Create(), tenantId, lead.Id, lead.AcademicYearId,
					AdmissionCrmTimelineEventType.FamilyApplicationWithdrawn,
					"Application withdrawn by family",
					"The family withdrew this application in the secure applicant portal.",
					null, null, now),
				autoSave: true);
			var status = await statusWorkflow.BuildVerifiedAsync(
				lead, access, input.AccessSessionToken, lead.PreferredCommunicationLanguage);
			await uow.CompleteAsync();
			return status;
		}
	}
}
