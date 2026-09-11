using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionEligibilityAnswers(Dictionary<string, string> Values, string Identity, string Source)
{
	public Dictionary<string, AdmissionFormFieldType> Types { get; init; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, string> Labels { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

[RemoteService(false)]
public class AdmissionEligibilityAnswerService(IRepository<AdmissionFormInstance, Guid> instances,
	IRepository<AdmissionFormFieldValue, Guid> values, IRepository<AdmissionFormFieldDefinition, Guid> fields,
	IRepository<AdmissionFormDefinition, Guid> forms, IRepository<AdmissionInquiry, Guid> inquiries) : ITransientDependency
{
	public async Task<AdmissionEligibilityAnswers> ReadAsync(AdmissionApplication application, AdmissionEligibilityCriterion criterion)
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var form = await forms.FindAsync(criterion.FormDefinitionId!.Value);
		if (form == null || form.Status == AdmissionFormStatus.Draft) return new(result, "unavailable", "Selected published form is unavailable");
		var definitions = await fields.GetListAsync(item => item.FormDefinitionId == form.Id);
		var types = new Dictionary<string, AdmissionFormFieldType>(StringComparer.OrdinalIgnoreCase);
		var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var requested = AdmissionDocumentCondition.GetFields(criterion.ConditionJson).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var raw = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
		string identity;
		if (criterion.Kind == "application")
		{
			var instance = await instances.FindAsync(item => item.ApplicationId == application.Id && item.FormDefinitionId == form.Id);
			if (instance == null) return new(result, "missing-application-form", form.Name);
			identity = $"application-form:{instance.Id}:{form.Id}:{instance.FormVersion}";
			foreach (var value in await values.GetListAsync(item => item.FormInstanceId == instance.Id)) raw[value.FieldCode] = value.ValueJson;
		}
		else
		{
			var inquiry = application.InquiryId.HasValue ? await inquiries.FindAsync(application.InquiryId.Value) : null;
			if (inquiry?.ApplicantId != application.ApplicantId || inquiry.FormResponseDefinitionId != form.Id)
				return new(result, $"unconfirmed-inquiry:{inquiry?.Id}:{inquiry?.FormResponseDefinitionId}", "Inquiry source form has not been confirmed");
			identity = $"inquiry:{inquiry.Id}:{form.Id}";
			if (!string.IsNullOrWhiteSpace(inquiry.FormResponseSnapshotJson))
			{
				using var json = JsonDocument.Parse(inquiry.FormResponseSnapshotJson);
				foreach (var value in json.RootElement.EnumerateObject()) raw[value.Name] = value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : value.Value.GetRawText();
			}
		}
		foreach (var definition in definitions)
		{
			var key = criterion.Kind == "inquiry" && !string.IsNullOrWhiteSpace(definition.BindingKey) ? definition.BindingKey! : definition.Code;
			var name = criterion.Kind + "." + key;
			types[name] = definition.FieldType;
			labels[name] = definition.Label;
			if (!requested.Contains(name) || !raw.TryGetValue(key, out var value)) continue;
			var unwrapped = AdmissionApplicationValueConverter.Unwrap(value);
			if (IsValidValue(definition.FieldType, value, unwrapped)) result[name] = unwrapped;
		}
		return new(result, identity, $"{form.Name}, version {form.Version}") { Types = types, Labels = labels };
	}
	private static bool IsValidValue(AdmissionFormFieldType type, string? raw, string value)
	{
		if (string.IsNullOrWhiteSpace(value)) return true;
		if (type == AdmissionFormFieldType.Number) return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
		if (type == AdmissionFormFieldType.Checkbox) return bool.TryParse(value, out _);
		try
		{
			using var json = JsonDocument.Parse(raw ?? "null");
			return type == AdmissionFormFieldType.MultiSelect
				? json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String)
				: json.RootElement.ValueKind == JsonValueKind.String;
		}
		catch (JsonException) { return type != AdmissionFormFieldType.MultiSelect; }
	}
}
