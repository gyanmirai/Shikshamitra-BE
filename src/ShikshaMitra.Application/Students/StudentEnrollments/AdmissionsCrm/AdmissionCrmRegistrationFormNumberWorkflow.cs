using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationFormNumberWorkflow(
	IRepository<AdmissionCrmFormNumberSeries, Guid> seriesRepository,
	IRepository<AdmissionCrmRegistrationApplication, Guid> applicationRepository,
	StudentEnrollmentManager enrollmentManager,
	AdmissionCrmRegistrationAccess access) : ApplicationService, ITransientDependency
{
	public async Task<string> ResolveAsync(AdmissionInquiryLead lead, IssueAdmissionCrmRegistrationApplicationDto input)
	{
		if (!string.IsNullOrWhiteSpace(input.ManualFormNumber)) return input.ManualFormNumber.Trim().ToUpperInvariant();
		AdmissionCrmFormNumberSeries series;
		if (input.FormNumberSeriesId.HasValue) series = await access.GetSeriesAsync(input.FormNumberSeriesId.Value, "IssueAsync");
		else
		{
			var queryable = await seriesRepository.GetQueryableAsync();
			var candidates = await AsyncExecuter.ToListAsync(queryable.Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == lead.AcademicYearId && x.IsActive));
			var actual = new AdmissionCrmPlaybookApplicabilityDto { Campus = lead.CampusBranchName, DesiredClass = lead.DesiredClassName };
			series = candidates.Select(item => new { Item = item, Scope = AdmissionCrmRegistrationModelMapper.Deserialize<AdmissionCrmPlaybookApplicabilityDto>(item.ApplicabilityJson) })
				.Where(item => AdmissionCrmPlaybookApplicability.Matches(item.Scope, actual))
				.OrderByDescending(item => AdmissionCrmPlaybookApplicability.Specificity(item.Scope)).Select(item => item.Item).FirstOrDefault()
				?? throw new BusinessException("AdmissionCrm:FormNumberSeriesOrManualNumberRequired");
		}
		if (series.AcademicYearId != lead.AcademicYearId) throw new BusinessException("AdmissionCrm:FormNumberSeriesAcademicYearMismatch");
		var number = series.TakeNextNumber();
		await seriesRepository.UpdateAsync(series, autoSave: true);
		return number;
	}

	public async Task EnsureUniqueAsync(string formNumber, Guid yearId)
	{
		await enrollmentManager.ValidateFormNumberUniqueAsync(formNumber, yearId);
		var queryable = await applicationRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && x.FormNumber == formNumber)))
			throw new BusinessException("AdmissionCrm:RegistrationFormNumberAlreadyExists").WithData("FormNumber", formNumber);
	}
}
