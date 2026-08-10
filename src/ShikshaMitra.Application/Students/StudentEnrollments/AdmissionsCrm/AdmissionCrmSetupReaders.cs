using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSetupReadinessReader(
	IRepository<AdmissionCrmAdmissionYearSetup, Guid> admissionYearRepository,
	IRepository<AdmissionCrmInquirySource, Guid> inquirySourceRepository,
	IRepository<AdmissionCrmInquiryType, Guid> inquiryTypeRepository,
	IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
	IRepository<AdmissionCrmRegistrationTemplate, Guid> registrationTemplateRepository,
	IRepository<AdmissionCrmDocumentChecklistTemplate, Guid> documentTemplateRepository,
	IRepository<AdmissionCrmMessageTemplate, Guid> messageTemplateRepository) : ApplicationService, ITransientDependency
{
	internal async Task<AdmissionCrmSetupReadinessSnapshot> LoadAsync(Guid academicYearId)
	{
		var tenantId = CurrentTenant.Id;
		var admissionYear = await AsyncExecuter.FirstOrDefaultAsync((await admissionYearRepository.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId &&
				x.Status == AdmissionCrmAdmissionYearSetupStatus.Published));
		var hasSource = await AsyncExecuter.AnyAsync((await inquirySourceRepository.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && x.IsActive));
		var hasType = await AsyncExecuter.AnyAsync((await inquiryTypeRepository.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && x.IsActive));
		var hasExamPolicy = await AsyncExecuter.AnyAsync((await playbookRepository.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId &&
				x.Status == AdmissionCrmPlaybookStatus.Published && x.ExamDefinitionsJson != "[]"));
		var hasRegistration = await AsyncExecuter.AnyAsync((await registrationTemplateRepository.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId &&
				x.Status == AdmissionCrmRegistrationTemplateStatus.Published));
		var hasDocuments = await AsyncExecuter.AnyAsync((await documentTemplateRepository.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId &&
				x.Status == AdmissionCrmDocumentChecklistTemplateStatus.Published));
		var hasMessage = await AsyncExecuter.AnyAsync((await messageTemplateRepository.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId &&
				x.Status == AdmissionCrmMessageTemplateStatus.Approved && x.IsActive));
		return new AdmissionCrmSetupReadinessSnapshot(admissionYear, hasSource, hasType,
			hasExamPolicy, hasRegistration, hasDocuments, hasMessage);
	}
}

public class AdmissionCrmPresetAdoptionReader(
	IRepository<AdmissionCrmPresetAdoption, Guid> adoptionRepository,
	IRepository<AdmissionCrmPlatformPreset, Guid> presetRepository) : ApplicationService, ITransientDependency
{
	internal async Task<AdmissionCrmPresetAdoptionSnapshot> LoadAsync(Guid academicYearId)
	{
		var adoption = (await adoptionRepository.GetListAsync(x =>
			x.AcademicYearId == academicYearId && x.RolledBackAt == null))
			.OrderByDescending(x => x.AppliedAt).FirstOrDefault();
		AdmissionCrmPlatformPreset? preset = null;
		if (adoption is not null)
		{
			using (CurrentTenant.Change(null)) preset = await presetRepository.FindAsync(adoption.PresetId);
		}
		return new AdmissionCrmPresetAdoptionSnapshot(adoption, preset);
	}
}

public class AdmissionCrmApplicantCountReader(
	IRepository<AdmissionInquiryLead, Guid> leadRepository) : ApplicationService, ITransientDependency
{
	internal async Task<System.Collections.Generic.Dictionary<Guid, int>> GetAsync(
		System.Collections.Generic.IReadOnlyList<Guid> academicYearIds)
	{
		if (academicYearIds.Count == 0) return [];
		var query = (await leadRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && academicYearIds.Contains(x.AcademicYearId));
		var counts = await AsyncExecuter.ToListAsync(query.GroupBy(x => x.AcademicYearId)
			.Select(group => new { AcademicYearId = group.Key, Count = group.Count() }));
		return counts.ToDictionary(x => x.AcademicYearId, x => x.Count);
	}
}

internal sealed record AdmissionCrmSetupReadinessSnapshot(
	AdmissionCrmAdmissionYearSetup? PublishedAdmissionYear,
	bool HasActiveSource,
	bool HasActiveType,
	bool HasPublishedExamPolicy,
	bool HasPublishedRegistration,
	bool HasPublishedDocuments,
	bool HasApprovedMessage);

internal sealed record AdmissionCrmPresetAdoptionSnapshot(
	AdmissionCrmPresetAdoption? Adoption,
	AdmissionCrmPlatformPreset? Preset);