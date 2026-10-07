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

	/// <summary>Reads the same form and managed assessment facts for scoped workbench contexts in two bounded queries.</summary>
	public async Task<Dictionary<Guid, Dictionary<string, string>>> ReadAssessmentBatchAsync(
		IReadOnlyCollection<AdmissionAssessmentContext> contexts, IReadOnlyCollection<AdmissionAssessmentAttempt> attemptRows)
	{
		if (contexts.Count == 0) return [];
		var ids = contexts.Select(context => context.Application.Id).Distinct().ToArray();
		var formRows = await forms.GetListAsync(form => Enumerable.Contains(ids, form.ApplicationId));
		var formIds = formRows.Select(form => form.Id).ToArray();
		var fieldRows = await values.GetListAsync(value => Enumerable.Contains(formIds, value.FormInstanceId));
		var applicationByForm = formRows.ToDictionary(form => form.Id, form => form.ApplicationId);
		var result = contexts.ToDictionary(context => context.Stage.Id,
			context => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
		var fieldsByApplication = fieldRows.ToLookup(field => applicationByForm[field.FormInstanceId]);
		var attemptsByStage = attemptRows.ToLookup(attempt => attempt.StageInstanceId);
		foreach (var context in contexts)
		{
			var facts = result[context.Stage.Id];
			foreach (var field in fieldsByApplication[context.Application.Id])
			{
				var value = AdmissionApplicationValueConverter.Unwrap(field.ValueJson);
				facts[field.FieldCode] = value;
				facts[$"application.{field.FieldCode}"] = value;
			}
			facts["application.status"] = context.Application.Status.ToString();
			foreach (var key in facts.Keys.Where(key => key.StartsWith("assessment.", StringComparison.OrdinalIgnoreCase)).ToArray()) facts.Remove(key);
			var attempt = attemptsByStage[context.Stage.Id].Where(attempt => attempt.Status != "cancelled")
				.OrderByDescending(attempt => attempt.CreationTime).ThenByDescending(attempt => attempt.AttemptNumber).FirstOrDefault();
			foreach (var pair in AdmissionAssessmentEvidence.GetFacts(attempt)) facts[pair.Key] = pair.Value;
		}
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
