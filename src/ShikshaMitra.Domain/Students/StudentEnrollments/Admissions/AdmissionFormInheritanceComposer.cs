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
		var authoredEmptySections = result.Definition.Sections.Where(section => section.Fields.Count == 0).Select(section => section.Id).ToHashSet();
		if (targetFields.GroupBy(field => field.Code, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
		{
			Issue(result, "DuplicateRegistrationCode", "Give each Registration question a unique field code.");
			return result;
		}
		var targetsByCode = targetFields.ToDictionary(field => field.Code, StringComparer.OrdinalIgnoreCase);
		var targetGroups = targetFields.GroupBy(field => MatchIdentity(field, registration.Id), StringComparer.OrdinalIgnoreCase).ToList();
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
				!mappings.TryAdd(source.Code, MatchIdentity(target, registration.Id)))
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
			var identity = mappings.GetValueOrDefault(field.Code) ?? MatchIdentity(field, inquiry.Id);
			identities[field.Code] = identity;
			if (!seenSourceIdentities.Add(identity))
				Issue(result, "DuplicateSourceIdentity", "The Inquiry form includes the same question more than once.", identity);
			codeMap[field.Code] = targets.TryGetValue(identity, out var target) ? target.Code : UniqueCode(field.Code, usedCodes);
		}
		if (result.Issues.Count > 0) return result;
		// Legacy requiredness/placement overrides remain readable policy metadata, but cannot
		// alter Inquiry-owned definitions. Frozen application schemas are never recomposed.
		var inquiryStepper = inquiry.LayoutType == AdmissionFormLayoutType.Stepper;
		var combinedStepper = registration.LayoutType == AdmissionFormLayoutType.Stepper || inquiryStepper;
		result.Definition.LayoutType = combinedStepper ? AdmissionFormLayoutType.Stepper : AdmissionFormLayoutType.SinglePage;
		if (registration.LayoutType == AdmissionFormLayoutType.SinglePage)
			foreach (var section in result.Definition.Sections)
				SetSinglePageStep(section, registration, combinedStepper, registration.Id, "REGISTRATION_PAGE", 0);
		var languages = registration.SupportedLanguages.Concat(inquiry.SupportedLanguages).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		var sectionCodes = result.Definition.Sections.Select(section => section.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var nextSectionOrder = 0;
		var nextStepOrder = 0;
		var sourceSectionIds = new HashSet<Guid>();
		var sourceStepOrders = inquiry.Sections
			.GroupBy(section => section.StepId ?? section.Id).OrderBy(group => group.Min(section => section.StepDisplayOrder ?? section.DisplayOrder))
			.Select((group, index) => new { group.Key, Order = nextStepOrder + index }).ToDictionary(item => item.Key, item => item.Order);
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
		}
		foreach (var sourceSection in inquiry.Sections.OrderBy(section => section.DisplayOrder))
		{
			var inheritedSection = Clone(sourceSection);
			inheritedSection.FormDefinitionId = registration.Id;
			inheritedSection.Code = UniqueCode("INQ_" + sourceSection.Code, sectionCodes);
			inheritedSection.Id = StableId(registration.Id, inquiry.Id, inheritedSection.Code);
			sourceSectionIds.Add(inheritedSection.Id);
			inheritedSection.DisplayOrder = nextSectionOrder++;
			inheritedSection.FieldsPerRow = inquiry.FieldsPerRow;
			inheritedSection.Fields = [];
			FillTranslations(inheritedSection.Translations, inquiry.DefaultLanguage, languages, sourceSection.Name, null, null);
			inheritedSection.VisibilityRuleJson = Rewrite(sourceSection.VisibilityRuleJson, codeMap);
			inheritedSection.StepVisibilityRuleJson = Rewrite(sourceSection.StepVisibilityRuleJson, codeMap);
			if (inquiryStepper)
			{
				inheritedSection.StepId = StableId(registration.Id, inquiry.Id, "STEP:" + (sourceSection.StepId ?? sourceSection.Id));
				inheritedSection.StepCode = "INQ_" + (sourceSection.StepCode ?? sourceSection.Code);
				inheritedSection.StepName = sourceSection.StepName ?? sourceSection.Name;
				inheritedSection.StepDisplayOrder = sourceStepOrders[sourceSection.StepId ?? sourceSection.Id];
			}
			else
				SetSinglePageStep(inheritedSection, inquiry, combinedStepper, registration.Id, "INQUIRY_PAGE", nextStepOrder);
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
				// The source definition owns all answer requirements and settings.
				inherited.Required = source.Required;
				if (source.BindingKey?.StartsWith("Inquiry.", StringComparison.OrdinalIgnoreCase) == true)
				{
					inherited.ReadOnly = true;
					inherited.Required = false;
					contextSection.Fields.Add(inherited);
					continue;
				}
				if (source.FieldType == AdmissionFormFieldType.File)
					Issue(result, "UnsupportedField", "This Inquiry includes a field format unavailable in Registration.", identity);
				if (target != null)
				{
					var targetSection = result.Definition.Sections.Single(section => section.Fields.Contains(target));
					if (source.FieldType != target.FieldType)
						Issue(result, "FieldTypeConflict", "The linked question has incompatible answer types. Correct its definition before enabling inheritance.", identity);
					if (IsChoice(source.FieldType) && !EquivalentOptions(inherited.OptionsJson, target.OptionsJson))
						Issue(result, "OptionsConflict", "The linked question has different option values. Review the source question before enabling inheritance.", identity);
					if (sourceSection.IsRepeatable != targetSection.IsRepeatable ||
						(sourceSection.IsRepeatable && (sourceSection.MinEntries != targetSection.MinEntries || sourceSection.MaxEntries != targetSection.MaxEntries)))
						Issue(result, "RepeatableConflict", "A shared question must retain the same repeatable-entry structure.", identity);
					if (!string.IsNullOrWhiteSpace(targetSection.VisibilityRuleJson) || !string.IsNullOrWhiteSpace(targetSection.StepVisibilityRuleJson) ||
						(!string.IsNullOrWhiteSpace(target.VisibilityRuleJson) && !string.Equals(target.VisibilityRuleJson, inherited.VisibilityRuleJson, StringComparison.Ordinal)))
						Issue(result, "VisibilityConflict", "Move this shared question to an unconditional Registration section so its Inquiry visibility stays authoritative.", identity);
					if (!string.IsNullOrWhiteSpace(target.RequiredRuleJson) && !EquivalentJson(target.RequiredRuleJson, inherited.RequiredRuleJson))
						Issue(result, "RequiredRuleConflict", "The Registration question has a different conditional requirement. Keep the Inquiry rule unchanged before reusing it.", identity);
					if (!string.IsNullOrWhiteSpace(target.ValidationJson) && !EquivalentJson(target.ValidationJson, inherited.ValidationJson))
						Issue(result, "ValidationConflict", "The Registration question has different validation. Keep the Inquiry validation unchanged before reusing it.", identity);
					// Historical starter fields can be custom while the Registration slot uses
					// a canonical Student binding. Keep the original source key for prefill.
					if (source.BindingType == AdmissionFormBindingType.Custom && target.BindingType == AdmissionFormBindingType.System)
					{
						inherited.BindingType = target.BindingType;
						inherited.BindingKey = target.BindingKey;
					}
					targetSection.Fields.Remove(target);
				}
				if (identity == "system:APPLICANT.NAME" && targetFields.Any(field => MatchIdentity(field, registration.Id) is
					"system:APPLICANT.FIRSTNAME" or "system:APPLICANT.MIDDLENAME" or "system:APPLICANT.LASTNAME"))
				{
					// A captured full name is useful historical context; never split it into
					// structured Student names or ask the applicant to enter it again.
					inherited.ReadOnly = true;
					inherited.Required = false;
					inherited.RequiredRuleJson = null;
				}
				inherited.SectionId = inheritedSection.Id;
				inheritedSection.Fields.Add(inherited);
			}
			if (inheritedSection.Fields.Count > 0) result.Definition.Sections.Add(inheritedSection);
			if (contextSection.Fields.Count > 0) result.InquiryContextSections.Add(contextSection);
		}
		OrderInquiryFirst(result.Definition, sourceSectionIds, authoredEmptySections);
		result.Definition.SupportedLanguages = languages;
		return result;
	}

	public static string Identity(AdmissionFormSchemaField field, Guid formId) =>
		field.BindingType == AdmissionFormBindingType.System && !string.IsNullOrWhiteSpace(field.BindingKey)
			? "system:" + field.BindingKey.Trim().ToUpperInvariant()
			: !string.IsNullOrWhiteSpace(field.FieldIdentity) ? field.FieldIdentity : "custom:" + formId.ToString("N") + ":" + field.Code.ToUpperInvariant();

	private static string MatchIdentity(AdmissionFormSchemaField field, Guid formId)
	{
		if (field.FieldType != AdmissionFormFieldType.Static && AdmissionClassInterestQuestion.Matches(field.Code, field.Label, field.BindingType, field.BindingKey))
			return AdmissionClassInterestQuestion.Identity;
		if (field.BindingType == AdmissionFormBindingType.System ||
			(!string.IsNullOrWhiteSpace(field.BindingKey) && !string.Equals(field.BindingKey, field.Code, StringComparison.OrdinalIgnoreCase)))
			return Identity(field, formId);
		// These are the published starter-code aliases already supported by Inquiry
		// capture. Labels, translations and arbitrary custom names are not matches.
		var binding = field.Code.Trim().ToUpperInvariant() switch
		{
			"STUDENT_NAME" or "APPLICANT_NAME" or "CHILD_NAME" => "APPLICANT.NAME",
			"STUDENT_FIRST_NAME" or "APPLICANT_FIRST_NAME" or "FIRST_NAME" => "APPLICANT.FIRSTNAME",
			"STUDENT_MIDDLE_NAME" or "APPLICANT_MIDDLE_NAME" or "MIDDLE_NAME" => "APPLICANT.MIDDLENAME",
			"STUDENT_LAST_NAME" or "APPLICANT_LAST_NAME" or "LAST_NAME" => "APPLICANT.LASTNAME",
			"DATE_OF_BIRTH" or "DOB" or "STUDENT_DOB" or "APPLICANT_DOB" => "APPLICANT.DATEOFBIRTH",
			"GUARDIAN_FIRST_NAME" or "PARENT_FIRST_NAME" or "GUARDIAN_NAME" or "PARENT_NAME" => "GUARDIAN.FIRSTNAME",
			"GUARDIAN_LAST_NAME" or "PARENT_LAST_NAME" => "GUARDIAN.LASTNAME",
			"GUARDIAN_MOBILE" or "GUARDIAN_PHONE" or "PARENT_MOBILE" or "PARENT_PHONE" => "GUARDIAN.MOBILE",
			"GUARDIAN_EMAIL" or "PARENT_EMAIL" => "GUARDIAN.EMAIL",
			"GUARDIAN_RELATION" => "GUARDIAN.RELATIONSHIP",
			_ => null
		};
		return binding == null ? Identity(field, formId) : "system:" + binding;
	}

	private static void OrderInquiryFirst(AdmissionFormSchema definition, IReadOnlySet<Guid> sourceSectionIds, IReadOnlySet<Guid> authoredEmptySections)
	{
		var source = definition.Sections.Where(section => sourceSectionIds.Contains(section.Id)).OrderBy(section => section.DisplayOrder).ToList();
		var registration = definition.Sections.Where(section => !sourceSectionIds.Contains(section.Id) &&
			(section.Fields.Count > 0 || authoredEmptySections.Contains(section.Id)))
			.OrderBy(section => section.DisplayOrder).ToList();
		definition.Sections = source.Concat(registration).ToList();
		for (var index = 0; index < definition.Sections.Count; index++) definition.Sections[index].DisplayOrder = index;
		if (definition.LayoutType != AdmissionFormLayoutType.Stepper) return;
		var stepOrder = 0;
		foreach (var owner in new[] { source, registration })
			foreach (var group in owner.GroupBy(section => section.StepId ?? section.Id)
				.OrderBy(group => group.Min(section => section.StepDisplayOrder ?? section.DisplayOrder)))
			{
				foreach (var section in group) section.StepDisplayOrder = stepOrder;
				stepOrder++;
			}
	}

	private static void SetSinglePageStep(AdmissionFormSchemaSection section, AdmissionFormSchema form,
		bool stepper, Guid registrationId, string code, int order)
	{
		section.StepId = stepper ? StableId(registrationId, form.Id, code) : null;
		section.StepCode = stepper ? code : null;
		section.StepName = stepper ? form.Name : null;
		section.StepDisplayOrder = stepper ? order : null;
		// Dormant step rules on a single-page source must not hide its sections.
		section.StepVisibilityRuleJson = null;
	}

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

	private static bool EquivalentJson(string? first, string? second)
	{
		try
		{
			static JsonNode? Parse(string? value)
			{
				var node = string.IsNullOrWhiteSpace(value) ? null : JsonNode.Parse(value);
				return node is JsonObject { Count: 0 } ? null : node;
			}
			return JsonNode.DeepEquals(Parse(first), Parse(second));
		}
		catch (JsonException) { return false; }
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
