using System.Text.Json;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp.Data;

namespace ShikshaMitra.HR.Payroll.Services;

public static class SalaryProtectionSnapshot
{
	private const string PropertyName = "SalaryProtectionV1";
	public static SalaryProtectionResult? Read(IHasExtraProperties entity)
	{
		var json = entity.GetProperty<string>(PropertyName);
		return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<SalaryProtectionResult>(json);
	}
	public static void Write(IHasExtraProperties entity, SalaryProtectionResult? result)
	{
		if (result == null)
			entity.ExtraProperties.Remove(PropertyName);
		else
			entity.SetProperty(PropertyName, JsonSerializer.Serialize(result));
	}
}
