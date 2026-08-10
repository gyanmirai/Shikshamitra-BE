using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadAccess(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmFollowUpTask, Guid> taskRepository,
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<AdmissionCrmAdmissionYearSetup, Guid> admissionYearRepository,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	internal Task EnsureCanAccessAcademicYearAsync(Guid academicYearId, string operation) =>
		scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, operation);

	internal Task EnsureCanAccessStandardAsync(Guid standardId, string operation) =>
		scopeAccess.EnsureCanAccessStandardAsync(standardId, operation);

	internal Task EnsureCanUseInquirySourceAsync(
		Guid? sourceId,
		Guid academicYearId,
		AdmissionInquiryChannel channel,
		string operation) =>
		scopeAccess.EnsureCanUseInquirySourceAsync(sourceId, academicYearId, channel, operation);

	internal Task EnsureCanAccessAdmissionLeadAsync(AdmissionInquiryLead lead, string operation) =>
		scopeAccess.EnsureCanAccessAdmissionLeadAsync(lead, operation);

	internal async Task<AdmissionInquiryLead> GetLeadForCurrentTenantAsync(
		Guid id,
		string operation,
		bool enforceScope = true)
	{
		var lead = await leadRepository.GetAsync(id);
		if (lead.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:LeadNotFound")
				.WithData("LeadId", id);
		}

		if (enforceScope)
		{
			await scopeAccess.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		}

		return lead;
	}

	internal async Task<AdmissionCrmFollowUpTask> GetTaskForCurrentTenantAsync(Guid id, string operation)
	{
		var task = await taskRepository.GetAsync(id);
		if (task.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:TaskNotFound")
				.WithData("TaskId", id);
		}

		var lead = await leadRepository.GetAsync(task.LeadId);
		await scopeAccess.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return task;
	}

	internal async Task<AdmissionCrmCycleConfiguration> GetCycleConfigurationOrDefaultAsync(Guid academicYearId)
	{
		var queryable = await cycleRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId));

		return existing ?? new AdmissionCrmCycleConfiguration(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			"Default Admission Year");
	}

	internal async Task EnsureAcademicYearExistsAsync(Guid academicYearId)
	{
		var queryable = await academicYearRepository.GetQueryableAsync();
		var exists = await AsyncExecuter.AnyAsync(queryable.Where(year =>
			year.Id == academicYearId &&
			year.TenantId == CurrentTenant.Id));
		if (!exists)
		{
			throw new BusinessException("AdmissionCrm:AcademicYearNotFound")
				.WithData("AcademicYearId", academicYearId);
		}
	}

	internal async Task<AdmissionCrmAdmissionYearSetup?> GetAdmissionYearSetupForCaptureAsync(
		Guid academicYearId)
	{
		var setups = await admissionYearRepository.GetListAsync(setup =>
			setup.TenantId == CurrentTenant.Id &&
			setup.AcademicYearId == academicYearId);
		if (setups.Count == 0)
		{
			return null;
		}

		var published = setups.SingleOrDefault(setup =>
			setup.Status == AdmissionCrmAdmissionYearSetupStatus.Published);
		if (published is null)
		{
			var latest = setups.OrderByDescending(setup => setup.Version).First();
			throw new BusinessException("AdmissionCrm:AdmissionYearNotOpen")
				.WithData("AcademicYearId", academicYearId)
				.WithData("Status", latest.Status)
				.WithData("Version", latest.Version);
		}

		published.EnsureInquiryCaptureAllowed(Clock.Now);
		if (string.IsNullOrWhiteSpace(published.PublishedSnapshotJson))
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSnapshotMissing")
				.WithData("SetupId", published.Id)
				.WithData("Version", published.Version);
		}

		return published;
	}
}