using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Registration answers are the only authority for the service joining date.</summary>
public class AdmissionJoiningDateReader(IRepository<AdmissionFormInstance, Guid> instances,
	IRepository<AdmissionFormFieldDefinition, Guid> fields, IRepository<AdmissionFormFieldValue, Guid> values,
	ICurrentTenant tenant) : ITransientDependency
{
	public async Task<DateTime?> ReadAsync(AdmissionApplication application)
	{
		if (application.TenantId != tenant.Id) throw new Volo.Abp.Authorization.AbpAuthorizationException();
		var instance = (await instances.GetListAsync(x => x.TenantId == tenant.Id && x.ApplicationId == application.Id &&
			x.FormDefinitionId == application.FormDefinitionId)).OrderByDescending(x => x.FormVersion).ThenByDescending(x => x.LastSavedAt).FirstOrDefault();
		if (instance == null) return null;
		var definitions = await fields.GetListAsync(x => x.FormDefinitionId == instance.FormDefinitionId && x.FieldType == AdmissionFormFieldType.Date);
		var bound = definitions.Where(x => x.BindingType == AdmissionFormBindingType.System && x.BindingKey == "Academic.JoiningDate").ToList();
		// Explicit legacy codes only; labels and unrelated dates never become financial facts.
		var codes = (bound.Count > 0 ? bound : definitions.Where(x => x.Code is "JOINING_DATE" or "ACTUAL_JOINING_DATE").ToList()).Select(x => x.Code).ToArray();
		var answers = await values.GetListAsync(x => x.TenantId == tenant.Id && x.FormInstanceId == instance.Id && Enumerable.Contains(codes, x.FieldCode));
		var dates = answers.Select(x => AdmissionApplicationValueConverter.Unwrap(x.ValueJson)).Where(x => !string.IsNullOrWhiteSpace(x)).Select(Parse).Distinct().ToList();
		if (dates.Count > 1) throw new BusinessException(ProrationPolicyErrorCodes.JoiningDateInvalid);
		return dates.Count == 0 ? null : dates[0];
	}
	private static DateTime Parse(string value)
	{
		if (value.Length < 10 || !DateTime.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
			(value.Length > 10 && (value[10] != 'T' || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))))
			throw new BusinessException(ProrationPolicyErrorCodes.JoiningDateInvalid);
		return DateTime.SpecifyKind(date, DateTimeKind.Utc);
	}
}
