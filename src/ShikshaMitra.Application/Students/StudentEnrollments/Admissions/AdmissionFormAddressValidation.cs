using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionFormAddressValidation
{
	internal static List<AdmissionFormInheritanceIssueDto> Validate(AdmissionFormDto form, bool checkOrder = true)
	{
		var selectors = OrderedSections(form)
			.SelectMany(section => section.Fields.OrderBy(field => field.DisplayOrder))
			.Select(ReadSelector).OfType<LocationSelector>().ToList();
		// Field codes are the keys used by the runtime to read the parent's answer.
		var byCode = new Dictionary<string, LocationSelector>(StringComparer.Ordinal);
		foreach (var selector in selectors) byCode.TryAdd(selector.Field.Code, selector);

		var issues = new List<AdmissionFormInheritanceIssueDto>();
		foreach (var selector in selectors)
		{
			if (selector.Role == LocationRole.Country) continue;
			var country = FindParent(byCode, selector, selector.CountryField, LocationRole.Country);
			var label = string.IsNullOrWhiteSpace(selector.Field.Label) ? selector.Role.ToString() : selector.Field.Label.Trim();
			if (selector.Role == LocationRole.State)
			{
				if (country == null)
					issues.Add(Issue(selector, AdmissionFormErrorCodes.AddressMissingCountry,
						$"{label} needs a Country field. Add Country or remove {label} to publish."));
				else if (checkOrder && country.Position >= selector.Position)
					issues.Add(OrderIssue(selector));
				continue;
			}

			var state = FindParent(byCode, selector, selector.StateField, LocationRole.State);
			if (country == null || state == null || FindParent(byCode, state, state.CountryField, LocationRole.Country) != country)
				issues.Add(Issue(selector, AdmissionFormErrorCodes.AddressMissingCityDependencies,
					$"{label} needs Country and State fields. Add them or remove {label} to publish."));
			else if (checkOrder && (state.Position >= selector.Position || country.Position >= selector.Position))
				issues.Add(OrderIssue(selector));
		}
		return issues;
	}

	internal static bool IsAddressError(string code) =>
		code.StartsWith(AdmissionFormErrorCodes.AddressMissingCountry + ":", StringComparison.Ordinal) ||
		code.StartsWith(AdmissionFormErrorCodes.AddressMissingCityDependencies + ":", StringComparison.Ordinal) ||
		code.StartsWith(AdmissionFormErrorCodes.AddressOrder + ":", StringComparison.Ordinal);

	private static IEnumerable<AdmissionFormSectionDto> OrderedSections(AdmissionFormDto form)
	{
		if (form.LayoutType != AdmissionFormLayoutType.Stepper) return form.Sections.OrderBy(section => section.DisplayOrder);
		// Published steppers take their step metadata from the first section in each group.
		return form.Sections.GroupBy(section => section.StepId ?? section.Id)
			.OrderBy(step => step.First().StepDisplayOrder ?? step.First().DisplayOrder)
			.SelectMany(step => step.OrderBy(section => section.DisplayOrder));
	}

	private static LocationSelector? ReadSelector(AdmissionFormFieldDto field, int position)
	{
		if (field.FieldType == AdmissionFormFieldType.Static || string.IsNullOrWhiteSpace(field.ValidationJson)) return null;
		try
		{
			using var document = JsonDocument.Parse(field.ValidationJson);
			var root = document.RootElement;
			var role = ReadString(root, "format") switch
			{
				"country" => LocationRole.Country,
				"state" => LocationRole.State,
				"city" => LocationRole.City,
				_ => (LocationRole?)null
			};
			return role == null ? null : new(field, role.Value, position, AddressFamily(field, role.Value),
				ReadString(root, "countryField"), ReadString(root, "stateField"));
		}
		catch (JsonException)
		{
			// The form's existing validation reports malformed rule JSON.
			return null;
		}
	}

	private static string? ReadString(JsonElement root, string name) =>
		root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString() : null;

	private static string? AddressFamily(AdmissionFormFieldDto field, LocationRole role)
	{
		if (field.BindingType != AdmissionFormBindingType.System) return null;
		if (string.Equals(field.BindingKey, $"Address.{role}", StringComparison.OrdinalIgnoreCase)) return "Address";
		return string.Equals(field.BindingKey, $"PermanentAddress.{role}", StringComparison.OrdinalIgnoreCase) ? "PermanentAddress" : null;
	}

	private static LocationSelector? FindParent(IReadOnlyDictionary<string, LocationSelector> byCode, LocationSelector child,
		string? fieldCode, LocationRole role)
	{
		if (fieldCode == null || !byCode.TryGetValue(fieldCode, out var parent) || parent.Role != role) return null;
		if (child.Family != null && !string.Equals(parent.Family, child.Family, StringComparison.Ordinal)) return null;
		return parent;
	}

	private static AdmissionFormInheritanceIssueDto OrderIssue(LocationSelector selector) =>
		Issue(selector, AdmissionFormErrorCodes.AddressOrder, "Country must come before State, and State before City.");

	private static AdmissionFormInheritanceIssueDto Issue(LocationSelector selector, string code, string message) => new()
	{
		Code = $"{code}:{selector.Field.Code}", Message = message, FieldIdentity = selector.Field.FieldIdentity
	};

	private enum LocationRole { Country, State, City }
	private sealed record LocationSelector(AdmissionFormFieldDto Field, LocationRole Role, int Position, string? Family,
		string? CountryField, string? StateField);
}
