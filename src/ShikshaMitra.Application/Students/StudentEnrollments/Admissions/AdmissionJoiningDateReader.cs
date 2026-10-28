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

/// <summary>Reads the shared admission date, retaining Registration and explicit legacy compatibility.</summary>
public class AdmissionJoiningDateReader(IRepository<AdmissionFormInstance, Guid> instances,
	IRepository<AdmissionFormFieldDefinition, Guid> fields, IRepository<AdmissionFormFieldValue, Guid> values,
	ICurrentTenant tenant) : ITransientDependency
{
	public async Task<DateTime?> ReadAsync(AdmissionApplication application)
	{
		if (application.TenantId != tenant.Id) throw new Volo.Abp.Authorization.AbpAuthorizationException();
		var current = (await instances.GetListAsync(x => x.TenantId == tenant.Id && x.ApplicationId == application.Id))
			.GroupBy(x => x.FormDefinitionId).Select(g => g.OrderByDescending(x => x.FormVersion).ThenByDescending(x => x.LastSavedAt).First()).ToList();
		if (current.Count == 0) return application.GetJoiningDate();
		var definitionIds = current.Select(i => i.FormDefinitionId).ToArray();
		var instanceIds = current.Select(i => i.Id).ToArray();
		var definitions = await fields.GetListAsync(x => Enumerable.Contains(definitionIds, x.FormDefinitionId) && x.FieldType == AdmissionFormFieldType.Date);
		var answers = await values.GetListAsync(x => x.TenantId == tenant.Id && Enumerable.Contains(instanceIds, x.FormInstanceId));
		var dateAnswers = current.SelectMany(instance =>
		{
			var formFields = definitions.Where(f => f.FormDefinitionId == instance.FormDefinitionId).ToList();
			var bound = formFields.Where(f => f.BindingType == AdmissionFormBindingType.System && f.BindingKey == "Academic.JoiningDate").ToList();
			// Explicit legacy codes only; labels and unrelated dates never become financial facts.
			var codes = (bound.Count > 0 ? bound : formFields.Where(f => f.Code is "JOINING_DATE" or "ACTUAL_JOINING_DATE").ToList()).Select(f => f.Code).ToHashSet();
			return answers.Where(a => a.FormInstanceId == instance.Id && codes.Contains(a.FieldCode));
		});
		var dates = dateAnswers.Select(x => AdmissionApplicationValueConverter.Unwrap(x.ValueJson)).Where(x => !string.IsNullOrWhiteSpace(x)).Select(Parse).Distinct().ToList();
		if (dates.Count > 1) throw new BusinessException(ProrationPolicyErrorCodes.JoiningDateInvalid);
		if (dates.Count == 1 && application.GetJoiningDate() is { } confirmed && confirmed != dates[0])
			throw new BusinessException(ProrationPolicyErrorCodes.JoiningDateInvalid,
				"The Registration joining date differs from the confirmed admission date. Review the saved date before preparing fees.");
		return dates.Count == 0 ? application.GetJoiningDate() : dates[0];
	}
	internal static DateTime Parse(string value)
	{
		if (value.Length < 10 || !DateTime.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
			(value.Length > 10 && (value[10] != 'T' || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))))
			throw new BusinessException(ProrationPolicyErrorCodes.JoiningDateInvalid);
		return DateTime.SpecifyKind(date, DateTimeKind.Utc);
	}
}
