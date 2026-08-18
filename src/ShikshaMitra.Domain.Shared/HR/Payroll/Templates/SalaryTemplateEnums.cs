using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShikshaMitra.HR.Payroll.Templates;

public enum SalaryTemplateStatus
{
	Draft = 0,
	Published = 1,
	Archived = 2
}

public enum SalaryInputBasis
{
	Gross = 0,
	TakeHome = 1
}

public enum SalaryTemplateLineKind
{
	Earning = 0,
	Deduction = 1
}

public enum SalaryTemplateFormulaType
{
	PercentageOfGross = 0,
	PercentageOfBasic = 1,
	PercentageOfComponent = 2,
	FixedAmount = 3,
	RemainderOfGross = 4,
	EmployeeProvidentFund = 5,
	ProfessionalTax = 6
}

[JsonConverter(typeof(PayrollComponentSemanticTypeJsonConverter))]
public enum PayrollComponentSemanticType
{
	Ordinary = 0,
	Basic = 1,
	BalancingEarning = 2,
	EmployeeProvidentFund = 3,
	ProfessionalTax = 4,
	DearnessAllowance = 5,
	EmployeeStateInsurance = 6
}

public static class PayrollComponentSemanticTypeCodes
{
	public static string ToCode(PayrollComponentSemanticType value) => value switch
	{
		PayrollComponentSemanticType.Ordinary => "ORDINARY",
		PayrollComponentSemanticType.Basic => "BASIC",
		PayrollComponentSemanticType.BalancingEarning => "BALANCING_EARNING",
		PayrollComponentSemanticType.EmployeeProvidentFund => "EMPLOYEE_PROVIDENT_FUND",
		PayrollComponentSemanticType.ProfessionalTax => "PROFESSIONAL_TAX",
		PayrollComponentSemanticType.DearnessAllowance => "DEARNESS_ALLOWANCE",
		PayrollComponentSemanticType.EmployeeStateInsurance => "EMPLOYEE_STATE_INSURANCE",
		_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid payroll component semantic type.")
	};

	public static bool TryParse(string? value, out PayrollComponentSemanticType semanticType)
	{
		var normalized = (value ?? string.Empty)
			.Replace("_", string.Empty, StringComparison.Ordinal)
			.Replace("-", string.Empty, StringComparison.Ordinal)
			.Replace(" ", string.Empty, StringComparison.Ordinal);
		return Enum.TryParse(normalized, true, out semanticType) &&
			Enum.IsDefined(typeof(PayrollComponentSemanticType), semanticType);
	}
}

public sealed class PayrollComponentSemanticTypeJsonConverter
	: JsonConverter<PayrollComponentSemanticType>
{
	public override PayrollComponentSemanticType Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numericValue) &&
			Enum.IsDefined(typeof(PayrollComponentSemanticType), numericValue))
		{
			return (PayrollComponentSemanticType)numericValue;
		}

		if (reader.TokenType == JsonTokenType.String)
		{
			if (PayrollComponentSemanticTypeCodes.TryParse(reader.GetString(), out var semanticType))
			{
				return semanticType;
			}
		}

		throw new JsonException("Invalid payroll component semantic type.");
	}

	public override void Write(
		Utf8JsonWriter writer,
		PayrollComponentSemanticType value,
		JsonSerializerOptions options)
	{
		writer.WriteStringValue(PayrollComponentSemanticTypeCodes.ToCode(value));
	}
}
