using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAdmissionHandoffWorkflow(
	IRepository<AdmissionCrmRegistrationApplication, Guid> registrationRepository,
	IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> checklistRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
	IRepository<AdmissionCrmApplicantDocument, Guid> documentRepository,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ConfirmAdmissionReadiness)]
	public async Task<AdmissionCrmAdmissionHandoffPackageDto> GetAdmissionHandoffPackageAsync(
		Guid registrationApplicationId)
	{
		var registrationQueryable = await registrationRepository.GetQueryableAsync();
		var registration = await AsyncExecuter.FirstOrDefaultAsync(registrationQueryable.Where(x =>
			x.Id == registrationApplicationId && x.TenantId == CurrentTenant.Id));
		if (registration == null)
		{
			throw new BusinessException("AdmissionCrm:RegistrationApplicationNotFound");
		}

		await scopeAccess.EnsureCanAccessAcademicYearAsync(
			registration.AcademicYearId,
			nameof(GetAdmissionHandoffPackageAsync));
		if (registration.Status != AdmissionCrmRegistrationStatus.AdmissionReady || !registration.VerifiedAt.HasValue)
		{
			throw new BusinessException("AdmissionCrm:RegistrationNotAdmissionReady");
		}

		var checklistQueryable = await checklistRepository.GetQueryableAsync();
		var checklist = await AsyncExecuter.FirstOrDefaultAsync(checklistQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.RegistrationApplicationId == registration.Id));
		if (checklist?.ReadinessConfirmedAt == null)
		{
			throw new BusinessException("AdmissionCrm:ReadinessNotConfirmed");
		}

		var leadQueryable = await leadRepository.GetQueryableAsync();
		var lead = await AsyncExecuter.FirstOrDefaultAsync(leadQueryable.Where(x =>
			x.Id == registration.LeadId && x.TenantId == CurrentTenant.Id));
		if (lead == null)
		{
			throw new BusinessException("AdmissionCrm:LeadNotFound");
		}
		await scopeAccess.EnsureCanAccessAdmissionLeadAsync(
			lead,
			nameof(GetAdmissionHandoffPackageAsync));

		var examQueryable = await examRepository.GetQueryableAsync();
		var exam = await AsyncExecuter.FirstOrDefaultAsync(examQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		var documentQueryable = await documentRepository.GetQueryableAsync();
		var documents = await AsyncExecuter.ToListAsync(documentQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.ChecklistAssignmentId == checklist.Id));
		var currentDocuments = documents
			.Where(x => x.SupersededAt == null)
			.OrderBy(x => x.RequirementKey)
			.ThenByDescending(x => x.Version)
			.ToList();

		return new AdmissionCrmAdmissionHandoffPackageDto
		{
			AcademicYearId = registration.AcademicYearId,
			LeadId = lead.Id,
			RegistrationApplicationId = registration.Id,
			ChecklistAssignmentId = checklist.Id,
			ApplicantName = AdmissionCrmOperationsFormat.GetApplicantName(lead),
			FormNumber = registration.FormNumber,
			LeadStage = lead.Stage,
			RegistrationStatus = registration.Status,
			RegistrationVerifiedAt = registration.VerifiedAt.Value,
			ReadinessConfirmedAt = checklist.ReadinessConfirmedAt.Value,
			ReadinessOverridden = checklist.ReadinessOverridden,
			ReadinessReason = checklist.ReadinessReason,
			EligibilityDecision = exam?.EligibilityDecision,
			MeritRank = exam?.MeritRank,
			PaymentReference = registration.PaymentReference,
			FeeStatus = registration.FeeStatus,
			ApplicantProfile = new Dictionary<string, string?>
			{
				["firstName"] = lead.FirstName,
				["middleName"] = lead.MiddleName,
				["lastName"] = lead.LastName,
				["dateOfBirth"] = lead.DateOfBirth?.ToString("yyyy-MM-dd"),
				["parentGuardianName"] = lead.ParentGuardianName,
				["primaryContactNo"] = lead.PrimaryContactNo,
				["email"] = lead.Email,
				["desiredClassName"] = lead.DesiredClassName,
				["campus"] = lead.CampusBranchName
			},
			Documents = currentDocuments.Select(x => new AdmissionCrmAdmissionHandoffDocumentDto
			{
				RequirementKey = x.RequirementKey,
				Version = x.Version,
				Status = x.Status,
				FileName = x.FileName,
				ValidUntil = x.ValidUntil,
				DecisionAt = x.DecisionAt,
				OriginalSeenAt = x.OriginalSeenAt
			}).ToList(),
			GeneratedAt = Clock.Now
		};
	}
}