using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Composes one applicant's source schema; never mutates either published definition.</summary>
public static class AdmissionFormInheritanceComposer
{
	public static AdmissionFormInheritanceComposition Compose(
		AdmissionFormSchema registration, AdmissionFormSchema? inquiry, AdmissionFormInheritancePolicy policy)
	{
		var result = new AdmissionFormInheritanceComposition { Definition = Clone(registration) };
		if (!policy.Enabled || inquiry == null) return result;
		var sourcePolicy = policy.Sources.SingleOrDefault(source => source.InquiryFormDefinitionId == inquiry.Id);
		if (sourcePolicy == null)
		{
			Issue(result, "SourceNotApproved", "Review and approve this exact Inquiry version before using it for Registration.");
			return result;
		}
		var targetFields = result.Definition.Sections.SelectMany(section => section.Fields).ToList();
		if (targetFields.GroupBy(field => field.Code, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
		{
			Issue(result, "DuplicateRegistrationCode", "Give each Registration question a unique field code.");
			return result;
		}
		var targetsByCode = targetFields.ToDictionary(field => field.Code, StringComparer.OrdinalIgnoreCase);
		var targetGroups = targetFields.GroupBy(field => Identity(field, registration.Id), StringComparer.OrdinalIgnoreCase).ToList();
		if (targetGroups.Any(group => group.Count() > 1))
		{
			Issue(result, "DuplicateRegistrationIdentity", "The Registration form includes the same question more than once.");
			return result;
		}
		var targets = targetGroups.ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
		var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var sourceFields = inquiry.Sections.SelectMany(section => section.Fields).ToList();
		var contextCodes = sourceFields.Where(field => field.BindingKey?.StartsWith("Inquiry.", StringComparison.OrdinalIgnoreCase) == true)
			.Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var mapping in sourcePolicy.FieldMappings)
		{
			var source = sourceFields.SingleOrDefault(field => string.Equals(field.Code, mapping.SourceFieldCode, StringComparison.OrdinalIgnoreCase));
			if (source == null || !targetsByCode.TryGetValue(mapping.RegistrationFieldCode, out var target) ||
				source.BindingType != AdmissionFormBindingType.Custom || target.BindingType != AdmissionFormBindingType.Custom ||
				!mappings.TryAdd(source.Code, Identity(target, registration.Id)))
			{
				Issue(result, "InvalidMapping", "Choose one existing custom question on each side when confirming a shared question.");
			}
		}
		var usedCodes = targetFields.Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var codeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var identities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var seenSourceIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var field in sourceFields)
		{
			var identity = mappings.GetValueOrDefault(field.Code) ?? Identity(field, inquiry.Id);
			identities[field.Code] = identity;
			if (!seenSourceIdentities.Add(identity))
				Issue(result, "DuplicateSourceIdentity", "The Inquiry form includes the same question more than once.", identity);
			codeMap[field.Code] = targets.TryGetValue(identity, out var target) ? target.Code : UniqueCode(field.Code, usedCodes);
		}
		if (result.Issues.Count > 0) return result;
		if (sourcePolicy.RequiredFieldIdentities.Any(identity => !seenSourceIdentities.Contains(identity)) ||
			sourcePolicy.Layouts.Any(layout => !seenSourceIdentities.Contains(layout.FieldIdentity)))
		{
			Issue(result, "UnknownOverrideField", "Review overrides that refer to questions missing from this Inquiry version.");
			return result;
		}
		var languages = registration.SupportedLanguages.Concat(inquiry.SupportedLanguages).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		var sectionCodes = result.Definition.Sections.Select(section => section.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var nextSectionOrder = result.Definition.Sections.Select(section => section.DisplayOrder).DefaultIfEmpty(-1).Max() + 1;
		var nextStepOrder = result.Definition.Sections.Select(section => section.StepDisplayOrder ?? section.DisplayOrder).DefaultIfEmpty(-1).Max() + 1;
		var sourceStepOrders = inquiry.Sections.Where(section => section.StepId.HasValue)
			.GroupBy(section => section.StepId!.Value).OrderBy(group => group.Min(section => section.StepDisplayOrder ?? section.DisplayOrder))
			.Select((group, index) => new { group.Key, Order = nextStepOrder + index }).ToDictionary(item => item.Key, item => item.Order);
		var repeatedDestinations = new Dictionary<Guid, AdmissionFormSchemaSection>();
		foreach (var repeated in inquiry.Sections.Where(section => section.IsRepeatable))
		{
			var dataFields = repeated.Fields.Where(field => field.FieldType != AdmissionFormFieldType.Static).ToList();
			var mapped = dataFields.Where(field => targets.ContainsKey(identities[field.Code]))
				.Select(field => targets[identities[field.Code]]).ToList();
			if (mapped.Count == 0) continue;
			var destinations = result.Definition.Sections.Where(section => mapped.Any(section.Fields.Contains)).ToList();
			if (mapped.Count != dataFields.Count || destinations.Count != 1 || !destinations[0].IsRepeatable ||
				destinations[0].Fields.Count(field => field.FieldType != AdmissionFormFieldType.Static) != mapped.Count)
			{
				Issue(result, "RepeatableGroupConflict", "Keep this complete repeated Inquiry section together, or map all of its questions into one matching repeated Registration section.");
				return result;
			}
			repeatedDestinations[repeated.Id] = destinations[0];
		}
		foreach (var sourceSection in inquiry.Sections.OrderBy(section => section.DisplayOrder))
		{
			var inheritedSection = Clone(sourceSection);
			inheritedSection.FormDefinitionId = registration.Id;
			inheritedSection.Code = UniqueCode("INQ_" + sourceSection.Code, sectionCodes);
			inheritedSection.Id = StableId(registration.Id, inquiry.Id, inheritedSection.Code);
			inheritedSection.DisplayOrder = nextSectionOrder++;
			inheritedSection.Fields = [];
			FillTranslations(inheritedSection.Translations, inquiry.DefaultLanguage, languages, sourceSection.Name, null, null);
			inheritedSection.VisibilityRuleJson = Rewrite(sourceSection.VisibilityRuleJson, codeMap);
			inheritedSection.StepVisibilityRuleJson = Rewrite(sourceSection.StepVisibilityRuleJson, codeMap);
			if (sourceSection.StepId.HasValue)
			{
				inheritedSection.StepId = StableId(registration.Id, inquiry.Id, "STEP:" + sourceSection.StepId);
				inheritedSection.StepCode = "INQ_" + sourceSection.StepCode;
				inheritedSection.StepDisplayOrder = sourceStepOrders[sourceSection.StepId.Value];
			}
			var contextSection = Clone(inheritedSection);
			foreach (var source in sourceSection.Fields.OrderBy(field => field.DisplayOrder))
			{
				var identity = identities[source.Code];
				targets.TryGetValue(identity, out var target);
				var inherited = Clone(source);
				inherited.Id = target?.Id ?? StableId(registration.Id, inquiry.Id, "FIELD:" + source.Code);
				inherited.Code = codeMap[source.Code];
				inherited.FormDefinitionId = registration.Id;
				inherited.SectionId = inheritedSection.Id;
				inherited.FieldIdentity = identity;
				inherited.InheritedFrom = new()
				{
					InquiryFormDefinitionId = inquiry.Id, SourceFieldCode = source.Code,
					SourceFieldIdentity = Identity(source, inquiry.Id),
					SourceBindingKey = source.BindingKey, FieldIdentity = identity,
					SourceRequired = source.Required, SourceReadOnly = source.ReadOnly
				};
				inherited.VisibilityRuleJson = Rewrite(source.VisibilityRuleJson, codeMap);
				inherited.RequiredRuleJson = Rewrite(source.RequiredRuleJson, codeMap);
				inherited.ValidationJson = Rewrite(source.ValidationJson, codeMap);
				inherited.OptionsJson = Rewrite(source.OptionsJson, codeMap);
				if (source.BindingKey?.StartsWith("Inquiry.", StringComparison.OrdinalIgnoreCase) != true &&
					(ReferencesContext(source.OptionsJson, contextCodes) || ReferencesContext(source.ValidationJson, contextCodes)))
					Issue(result, "ContextDependencyUnsupported", "An editable Registration question cannot use an Inquiry-only detail as an option or validation dependency. Use an editable source question instead.", identity);
				FillTranslations(inherited.Translations, inquiry.DefaultLanguage, languages, source.Label, source.Placeholder, source.HelpText);
				var requireOverride = sourcePolicy.RequiredFieldIdentities.Contains(identity, StringComparer.OrdinalIgnoreCase);
				if ((requireOverride || target?.Required == true) && (source.ReadOnly || target?.ReadOnly == true) && !source.Required)
					Issue(result, "ReadonlyRequiredOverride", "A read-only inherited question cannot be made newly required in Registration.", identity);
				inherited.Required = source.Required || target?.Required == true || requireOverride;
				if (source.BindingKey?.StartsWith("Inquiry.", StringComparison.OrdinalIgnoreCase) == true)
				{
					inherited.ReadOnly = true;
					inherited.Required = false;
					contextSection.Fields.Add(inherited);
					continue;
				}
				if (source.FieldType == AdmissionFormFieldType.File)
					Issue(result, "UnsupportedField", "This Inquiry includes a field format unavailable in Registration.", identity);
				var destination = source.FieldType == AdmissionFormFieldType.Static && repeatedDestinations.TryGetValue(sourceSection.Id, out var repeatedDestination)
					? repeatedDestination : inheritedSection;
				if (target == null && destination != inheritedSection)
					inherited.VisibilityRuleJson = Combine("ALL", inherited.VisibilityRuleJson,
						Rewrite(sourceSection.VisibilityRuleJson, codeMap), Rewrite(sourceSection.StepVisibilityRuleJson, codeMap));
				if (target != null)
				{
					destination = result.Definition.Sections.Single(section => section.Fields.Contains(target));
					if (source.FieldType != target.FieldType)
						Issue(result, "FieldTypeConflict", "The linked question has incompatible answer types. Correct its definition before enabling inheritance.", identity);
					if (IsChoice(source.FieldType) && !EquivalentOptions(inherited.OptionsJson, target.OptionsJson))
						Issue(result, "OptionsConflict", "The linked question has different option values. Review the source question before enabling inheritance.", identity);
					if (sourceSection.IsRepeatable != destination.IsRepeatable ||
						(sourceSection.IsRepeatable && (sourceSection.MinEntries != destination.MinEntries || sourceSection.MaxEntries != destination.MaxEntries)))
						Issue(result, "RepeatableConflict", "A shared question must retain the same repeatable-entry structure.", identity);
					if (!string.IsNullOrWhiteSpace(destination.VisibilityRuleJson) || !string.IsNullOrWhiteSpace(destination.StepVisibilityRuleJson) ||
						(!string.IsNullOrWhiteSpace(target.VisibilityRuleJson) && !string.Equals(target.VisibilityRuleJson, inherited.VisibilityRuleJson, StringComparison.Ordinal)))
						Issue(result, "VisibilityConflict", "Move this shared question to an unconditional Registration section so its Inquiry visibility stays authoritative.", identity);
					inherited.VisibilityRuleJson = Combine("ALL", inherited.VisibilityRuleJson,
						Rewrite(sourceSection.VisibilityRuleJson, codeMap), Rewrite(sourceSection.StepVisibilityRuleJson, codeMap));
					inherited.RequiredRuleJson = Combine("ANY", inherited.RequiredRuleJson, target.RequiredRuleJson);
					inherited.ValidationJson = MergeValidation(inherited.ValidationJson, target.ValidationJson, result, identity);
					inherited.ReadOnly |= target.ReadOnly;
					inherited.DisplayOrder = target.DisplayOrder;
					destination.Fields.Remove(target);
				}
				var layout = sourcePolicy.Layouts.SingleOrDefault(item => string.Equals(item.FieldIdentity, identity, StringComparison.OrdinalIgnoreCase));
				if (layout != null)
				{
					if (!string.IsNullOrWhiteSpace(layout.TargetSectionCode))
					{
						var requested = result.Definition.Sections.SingleOrDefault(section => string.Equals(section.Code, layout.TargetSectionCode, StringComparison.OrdinalIgnoreCase));
						if (requested == null || sourceSection.IsRepeatable || requested.IsRepeatable ||
							!string.IsNullOrWhiteSpace(requested.VisibilityRuleJson) || !string.IsNullOrWhiteSpace(requested.StepVisibilityRuleJson))
							Issue(result, "InvalidLayout", "Choose a compatible Registration section without changing the source question's applicability.", identity);
						else
						{
							destination = requested;
							inherited.VisibilityRuleJson = Combine("ALL", inherited.VisibilityRuleJson,
								Rewrite(sourceSection.VisibilityRuleJson, codeMap), Rewrite(sourceSection.StepVisibilityRuleJson, codeMap));
						}
					}
					if (layout.DisplayOrder.HasValue) inherited.DisplayOrder = layout.DisplayOrder.Value;
				}
				inherited.SectionId = destination.Id;
				destination.Fields.Add(inherited);
			}
			if (inheritedSection.Fields.Count > 0) result.Definition.Sections.Add(inheritedSection);
			if (contextSection.Fields.Count > 0) result.InquiryContextSections.Add(contextSection);
		}
		result.Definition.SupportedLanguages = languages;
		return result;
	}

	public static string Identity(AdmissionFormSchemaField field, Guid formId) =>
		field.BindingType == AdmissionFormBindingType.System && !string.IsNullOrWhiteSpace(field.BindingKey)
			? "system:" + field.BindingKey.Trim().ToUpperInvariant()
			: !string.IsNullOrWhiteSpace(field.FieldIdentity) ? field.FieldIdentity : "custom:" + formId.ToString("N") + ":" + field.Code.ToUpperInvariant();

	private static string UniqueCode(string code, HashSet<string> used)
	{
		var prefix = code.Length > 90 ? code[..90] : code;
		var candidate = prefix;
		for (var suffix = 1; !used.Add(candidate); suffix++) candidate = "INQ_" + prefix + "_" + suffix;
		return candidate;
	}

	private static string? Rewrite(string? json, IReadOnlyDictionary<string, string> codeMap)
	{
		if (string.IsNullOrWhiteSpace(json)) return json;
		try
		{
			var node = JsonNode.Parse(json);
			Visit(node);
			return node?.ToJsonString();
		}
		catch (JsonException) { return json; }
		void Visit(JsonNode? node)
		{
			if (node is JsonObject obj)
				foreach (var key in obj.Select(item => item.Key).ToList())
				{
					if (IsFieldReference(key) && obj[key] is JsonValue value &&
						value.TryGetValue<string>(out var code) && codeMap.TryGetValue(code, out var replacement)) obj[key] = replacement;
					else Visit(obj[key]);
				}
			else if (node is JsonArray array) foreach (var item in array) Visit(item);
		}
	}

	private static bool ReferencesContext(string? json, IReadOnlySet<string> contextCodes)
	{
		if (string.IsNullOrWhiteSpace(json) || contextCodes.Count == 0) return false;
		try { return Visit(JsonNode.Parse(json)); }
		catch (JsonException) { return false; }
		bool Visit(JsonNode? node) => node switch
		{
			JsonObject obj => obj.Any(item => (IsFieldReference(item.Key) && item.Value is JsonValue value &&
				value.TryGetValue<string>(out var code) && contextCodes.Contains(code)) || Visit(item.Value)),
			JsonArray array => array.Any(Visit),
			_ => false
		};
	}
	private static bool IsFieldReference(string key) => key is "field" or "parentField" or "countryField" or "stateField";

	private static string? Combine(string operation, params string?[] rules)
	{
		var nodes = rules.Where(rule => !string.IsNullOrWhiteSpace(rule)).Distinct().Select(rule => JsonNode.Parse(rule!)).ToList();
		return nodes.Count switch { 0 => null, 1 => nodes[0]?.ToJsonString(), _ => new JsonObject { ["operator"] = operation, ["conditions"] = new JsonArray(nodes.ToArray()) }.ToJsonString() };
	}

	private static string? MergeValidation(string? source, string? target, AdmissionFormInheritanceComposition result, string identity)
	{
		if (string.IsNullOrWhiteSpace(target)) return source;
		if (string.IsNullOrWhiteSpace(source)) return target;
		try
		{
			var sourceNode = JsonNode.Parse(source) as JsonObject;
			var targetNode = JsonNode.Parse(target) as JsonObject;
			if (sourceNode == null || targetNode == null) return source;
			foreach (var item in targetNode)
				if (!sourceNode.ContainsKey(item.Key)) sourceNode[item.Key] = item.Value?.DeepClone();
				else if (!JsonNode.DeepEquals(sourceNode[item.Key], item.Value))
					Issue(result, "ValidationConflict", "The linked question has conflicting validation rules. Review the definitions before enabling inheritance.", identity);
			return sourceNode.ToJsonString();
		}
		catch (JsonException) { Issue(result, "InvalidRule", "Review this question's validation rules.", identity); return source; }
	}

	private static bool IsChoice(AdmissionFormFieldType type) => type is AdmissionFormFieldType.Select or AdmissionFormFieldType.MultiSelect or AdmissionFormFieldType.Radio;
	private static bool EquivalentOptions(string? first, string? second)
	{
		if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)) return string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(second);
		try
		{
			var a = JsonNode.Parse(first);
			var b = JsonNode.Parse(second);
			if (a is JsonArray aValues && b is JsonArray bValues)
			{
				static string? Value(JsonNode? node) => node is JsonObject obj ? obj["value"]?.ToString() : node?.ToString();
				return aValues.Select(Value).OrderBy(value => value, StringComparer.Ordinal)
					.SequenceEqual(bValues.Select(Value).OrderBy(value => value, StringComparer.Ordinal));
			}
			return JsonNode.DeepEquals(a, b);
		}
		catch (JsonException) { return false; }
	}
	private static void FillTranslations(Dictionary<string, AdmissionFormSchemaTranslation> translations, string sourceDefault,
		IEnumerable<string> languages, string label, string? placeholder, string? helpText)
	{
		var fallback = translations.GetValueOrDefault(sourceDefault) ?? new();
		foreach (var language in languages)
		{
			if (!translations.TryGetValue(language, out var translation)) translations[language] = translation = new();
			translation.Label ??= fallback.Label ?? label;
			translation.Placeholder ??= fallback.Placeholder ?? placeholder;
			translation.HelpText ??= fallback.HelpText ?? helpText;
			translation.ValidationMessage ??= fallback.ValidationMessage;
			foreach (var choice in fallback.Choices) translation.Choices.TryAdd(choice.Key, choice.Value);
		}
	}
	private static Guid StableId(Guid registration, Guid inquiry, string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(registration + ":" + inquiry + ":" + key))[..16]);
	private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
	private static void Issue(AdmissionFormInheritanceComposition result, string code, string message, string? identity = null) => result.Issues.Add(new() { Code = code, Message = message, FieldIdentity = identity });
}
