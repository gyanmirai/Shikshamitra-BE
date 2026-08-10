using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAnalyticsApplicantReader(
	IRepository<AdmissionCrmRegistrationApplication, Guid> registrationRepository,
	IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> checklistRepository,
	IRepository<AdmissionCrmApplicantDocument, Guid> documentRepository,
	IRepository<AdmissionCrmExamSlot, Guid> slotRepository) : ApplicationService, ITransientDependency
{
	internal async Task<AdmissionCrmAnalyticsApplicantSnapshot> LoadAsync(Guid yearId, List<Guid> leadIds,
		GetAdmissionCrmAnalyticsSummaryInput input)
	{
		var registrations = await AsyncExecuter.ToListAsync((await registrationRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		registrations = registrations.Where(x => AdmissionCrmAnalyticsFilters.IsInOptionalDateRange(x.IssuedAt, input)).ToList();
		var checklists = await AsyncExecuter.ToListAsync((await checklistRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		var documents = await AsyncExecuter.ToListAsync((await documentRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		var slots = await AsyncExecuter.ToListAsync((await slotRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId));
		return new AdmissionCrmAnalyticsApplicantSnapshot(registrations, checklists, documents, slots);
	}
}
