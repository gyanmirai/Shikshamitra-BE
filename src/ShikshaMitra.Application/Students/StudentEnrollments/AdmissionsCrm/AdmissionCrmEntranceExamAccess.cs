using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEntranceExamAccess(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionInquiryLead> GetLeadAsync(Guid id, string operation)
	{
		var lead = await leadRepository.GetAsync(id);
		if (lead.TenantId != CurrentTenant.Id)
			throw new BusinessException("AdmissionCrm:LeadNotFound").WithData("LeadId", id);
		await scopeAccess.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return lead;
	}

	public async Task<AdmissionCrmEntranceExamRecord> GetExamAsync(Guid id, string operation)
	{
		var exam = await examRepository.GetAsync(id);
		if (exam.TenantId != CurrentTenant.Id)
			throw new BusinessException("AdmissionCrm:ExamRecordNotFound").WithData("ExamRecordId", id);
		var lead = await leadRepository.GetAsync(exam.LeadId);
		await scopeAccess.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return exam;
	}

	public async Task<AdmissionCrmEntranceExamRecord?> FindExamByLeadAsync(Guid leadId)
	{
		var queryable = await examRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
	}

	public async Task<AdmissionCrmCycleConfiguration?> FindCycleAsync(Guid academicYearId)
	{
		var queryable = await cycleRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
	}

	public Task EnsureYearAsync(Guid academicYearId, string operation) =>
		scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, operation);

	public Task<IQueryable<AdmissionInquiryLead>> ApplyLeadScopeAsync(IQueryable<AdmissionInquiryLead> query) =>
		scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(query);
}
