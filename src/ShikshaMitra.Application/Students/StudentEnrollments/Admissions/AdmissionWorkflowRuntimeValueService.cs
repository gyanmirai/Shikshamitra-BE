using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionWorkflowRuntimeValueService(
	IRepository<AdmissionFormInstance, Guid> forms,
	IRepository<AdmissionFormFieldValue, Guid> values,
	IRepository<AdmissionInquiry, Guid> inquiries,
	AdmissionAssessmentRuntimeService assessments) : ITransientDependency
{
	public async Task<IReadOnlyDictionary<string, string>> ReadAsync(AdmissionApplication application, Guid? assessmentStageId = null, bool protectManagedFacts = false)
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["application.status"] = application.Status.ToString()
		};
		var form = await forms.FindAsync(item => item.ApplicationId == application.Id);
		if (form != null)
		foreach (var value in await values.GetListAsync(item => item.FormInstanceId == form.Id))
		{
			var unwrapped = AdmissionApplicationValueConverter.Unwrap(value.ValueJson);
			result[value.FieldCode] = unwrapped;
			result[$"application.{value.FieldCode}"] = unwrapped;
		}
		if (assessmentStageId.HasValue || protectManagedFacts) result["application.status"] = application.Status.ToString();
		if (protectManagedFacts)
		{
			foreach (var key in result.Keys.Where(key => key.StartsWith("assessment.", StringComparison.OrdinalIgnoreCase) || key.StartsWith("documents.", StringComparison.OrdinalIgnoreCase) || key.StartsWith("inquiry.", StringComparison.OrdinalIgnoreCase)).ToList()) result.Remove(key);
			await AddInquiryFactsAsync(application, result);
		}
		await assessments.AddFactsAsync(application.Id, result, assessmentStageId);
		return result;
	}
	private async Task AddInquiryFactsAsync(AdmissionApplication application, Dictionary<string, string> result)
	{
		if (!application.InquiryId.HasValue) return;
		var inquiry = await inquiries.FindAsync(application.InquiryId.Value);
		if (inquiry == null || inquiry.ApplicantId != application.ApplicantId) return;
		if (!string.IsNullOrWhiteSpace(inquiry.FormResponseSnapshotJson))
		{
			using var document = JsonDocument.Parse(inquiry.FormResponseSnapshotJson);
			if (document.RootElement.ValueKind == JsonValueKind.Object)
				foreach (var item in document.RootElement.EnumerateObject())
					result[$"inquiry.{item.Name}"] = AdmissionApplicationValueConverter.Unwrap(item.Value.ValueKind == JsonValueKind.String ? item.Value.GetString() : item.Value.GetRawText());
		}
		result["inquiry.status"] = inquiry.Status.ToString();
		result["inquiry.source"] = inquiry.Source.ToString();
	}
}
