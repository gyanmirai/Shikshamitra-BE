using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShikshaMitra.HR.Payroll.Statutory;

// Historical payroll/configuration JSON remains readable without rewriting immutable evidence.
public sealed class EsiCalculationSettingsJsonConverter : JsonConverter<EsiCalculationSettings>
{
	public override EsiCalculationSettings Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
	{
		using var document = JsonDocument.ParseValue(ref reader);
		var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
		if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("ESIC settings must be an object.");
		foreach (var property in document.RootElement.EnumerateObject()) values[property.Name] = property.Value;
		string? Text(string key) => values.TryGetValue(key, out var value) && value.ValueKind != JsonValueKind.Null ? value.ValueKind == JsonValueKind.String ? value.GetString() : throw new JsonException("Expected a text value in ESIC settings.") : null;
		Guid? Id(string key) => Text(key) is { } text ? Guid.TryParse(text, out var id) ? id : throw new JsonException("Invalid component ID.") : null;
		decimal? Number(string key) => values.TryGetValue(key, out var value) && value.ValueKind != JsonValueKind.Null ? value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number : throw new JsonException("Expected a percentage in ESIC settings.") : null;
		var eligibility = Text("EligibilityBasis") ?? Text("WageBasis") ?? "CLASSIFIED_EARNINGS";
		var calculation = Text("CalculationBasis") ?? Text("WageBasis") ?? "CLASSIFIED_EARNINGS";
		var legacy = values.ContainsKey("WageBasis") || values.ContainsKey("BasicComponentId") || eligibility == "BASIC_COMPONENT" || calculation == "BASIC_COMPONENT";
		return new EsiCalculationSettings
		{
			EligibilityBasis = eligibility == "BASIC_COMPONENT" ? "COMPONENT" : eligibility,
			CalculationBasis = calculation == "BASIC_COMPONENT" ? "COMPONENT" : calculation,
			EligibilityComponentId = eligibility == "BASIC_COMPONENT" ? Id("BasicComponentId") : Id("EligibilityComponentId"),
			CalculationComponentId = calculation == "BASIC_COMPONENT" ? Id("BasicComponentId") : Id("CalculationComponentId"),
			EmployeePercentage = Number("EmployeePercentage"), EmployerPercentage = Number("EmployerPercentage"),
			OverrideReason = Text("OverrideReason"), EvidenceReference = Text("EvidenceReference"), IsLegacyRead = legacy
		};
	}
	public override void Write(Utf8JsonWriter writer, EsiCalculationSettings settings, JsonSerializerOptions options)
	{
		string Name(string name) => options.PropertyNamingPolicy?.ConvertName(name) ?? name;
		writer.WriteStartObject();
		writer.WriteString(Name(nameof(settings.EligibilityBasis)), settings.EligibilityBasis);
		writer.WriteString(Name(nameof(settings.CalculationBasis)), settings.CalculationBasis);
		if (settings.EligibilityComponentId is { } eligibility) writer.WriteString(Name(nameof(settings.EligibilityComponentId)), eligibility);
		if (settings.CalculationComponentId is { } calculation) writer.WriteString(Name(nameof(settings.CalculationComponentId)), calculation);
		if (settings.EmployeePercentage is { } employee) writer.WriteNumber(Name(nameof(settings.EmployeePercentage)), employee);
		if (settings.EmployerPercentage is { } employer) writer.WriteNumber(Name(nameof(settings.EmployerPercentage)), employer);
		writer.WriteString(Name(nameof(settings.OverrideReason)), settings.OverrideReason);
		writer.WriteString(Name(nameof(settings.EvidenceReference)), settings.EvidenceReference);
		writer.WriteEndObject();
	}
}
