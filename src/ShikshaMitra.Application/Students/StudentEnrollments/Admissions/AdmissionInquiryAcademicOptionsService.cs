using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Public reads are bounded by the already authorized offering, never by anonymous staff scope.</summary>
[RemoteService(false)]
public class AdmissionInquiryAcademicOptionsService(AdmissionFormDataSourceRegistry sources,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions,
	IRepository<EducationBoard, Guid> boards) : ApplicationService, ITransientDependency
{
	internal async Task<List<AdmissionFormOptionDto>> ForPublicOfferingAsync(string key, AdmissionOffering offering, Guid? parent = null)
	{
		if (!CurrentTenant.Id.HasValue || !AdmissionAcademicFormOptions.IsAcademic(key)) return [];
		var standard = await standards.FindAsync(item => item.Id == offering.StandardId && item.TenantId == CurrentTenant.Id);
		if (standard == null) return [];
		if (key.Equals("DivisionsByStandard", StringComparison.OrdinalIgnoreCase))
		{
			if (parent.HasValue && parent != standard.Id) return [];
			return (await AsyncExecuter.ToListAsync((await divisions.GetQueryableAsync()).Where(item =>
				item.TenantId == CurrentTenant.Id && item.StandardId == standard.Id)
				.OrderBy(item => item.SortOrder).ThenBy(item => item.Name).ThenBy(item => item.Id)))
				.Select((item, index) => new AdmissionFormOptionDto { Value = item.Id.ToString(), Label = item.Name, DisplayOrder = index }).ToList();
		}
		if (parent.HasValue && (key.Equals("StandardsByMediumStage", StringComparison.OrdinalIgnoreCase)
			? parent != standard.EducationMediumStageId : parent != offering.MediumId)) return [];
		if (!key.Equals("Standards", StringComparison.OrdinalIgnoreCase))
			return [new() { Value = standard.Id.ToString(), Label = standard.Name }];
		var board = await boards.FindAsync(item => item.Id == offering.BoardId && item.TenantId == CurrentTenant.Id);
		return [new() { Value = standard.Id.ToString(), Label = standard.Name, BoardId = board?.Id, BoardLabel = board?.Type.ToString() }];
	}

	internal async Task ValidateAsync(IEnumerable<AdmissionFormFieldDefinition> fields, IReadOnlyDictionary<string, string> values,
		AdmissionOffering offering, Guid year, bool isPublic)
	{
		foreach (var field in fields.Where(field => field.FieldType is AdmissionFormFieldType.Select or AdmissionFormFieldType.Radio or AdmissionFormFieldType.MultiSelect))
		{
			if (!AdmissionFormMasterDataValidator.TrySource(AdmissionAcademicFormOptions.OptionsJson(field.Code, field.FieldType, field.OptionsJson), out var key, out var parentField) ||
				!AdmissionAcademicFormOptions.IsAcademic(key) || string.IsNullOrWhiteSpace(values.GetValueOrDefault(field.Code))) continue;
			var parent = parentField != null && Guid.TryParse(values.GetValueOrDefault(parentField), out var parentId) ? (Guid?)parentId : null;
			if (key.Equals("Standards", StringComparison.OrdinalIgnoreCase) && !parent.HasValue) parent = offering.MediumId;
			var options = isPublic ? await ForPublicOfferingAsync(key, offering, parent) : await sources.GetOptionsAsync(key, parent, year);
			var allowed = options.Select(option => option.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
			var selected = field.FieldType == AdmissionFormFieldType.MultiSelect
				? ReadMultiple(values[field.Code]) : [values[field.Code]];
			if (selected.Any(value => !allowed.Contains(value)))
				throw new BusinessException(AdmissionApplicationErrorCodes.ValidationFailed).WithData("Fields", field.Code);
		}
	}

	private static List<string> ReadMultiple(string value)
	{
		try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(value) ?? []; }
		catch (System.Text.Json.JsonException) { return [value]; }
	}
}
