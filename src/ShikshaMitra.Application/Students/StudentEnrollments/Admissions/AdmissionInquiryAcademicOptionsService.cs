using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Public reads are bounded by the already authorized offering, never by anonymous staff scope.</summary>
[RemoteService(false)]
public class AdmissionInquiryAcademicOptionsService(AdmissionFormDataSourceRegistry sources,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions,
	IRepository<EducationBoard, Guid> boards, IRepository<EducationMedium, Guid> mediums) : ApplicationService, ITransientDependency
{
	internal static List<AdmissionFormOptionDto> MapPublicAcademicOptions(string key, Guid? parent, AdmissionOffering offering,
		string className, string? boardName, string? mediumName)
	{
		if (key.Equals("EducationBoards", StringComparison.OrdinalIgnoreCase))
			return boardName == null ? [] : [new() { Value = offering.BoardId.ToString(), Label = boardName }];
		if (key.Equals("EducationMediums", StringComparison.OrdinalIgnoreCase))
			return mediumName == null || parent.HasValue && parent != offering.BoardId ? []
				: [new() { Value = offering.MediumId.ToString(), Label = mediumName }];
		if (parent.HasValue && (key.Equals("StandardsByMediumStage", StringComparison.OrdinalIgnoreCase)
			? parent != offering.StageId : parent != offering.MediumId)) return [];
		if (key.Equals("StandardsByMedium", StringComparison.OrdinalIgnoreCase) || key.Equals("StandardsByMediumStage", StringComparison.OrdinalIgnoreCase))
			return [new() { Value = offering.StandardId.ToString(), Label = className }];
		if (!key.Equals("Standards", StringComparison.OrdinalIgnoreCase)) return [];
		return [new() { Value = offering.StandardId.ToString(), Label = className, BoardId = offering.BoardId, BoardLabel = boardName,
			MediumId = offering.MediumId, MediumLabel = mediumName }];
	}
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
		var board = await boards.FindAsync(item => item.Id == offering.BoardId && item.TenantId == CurrentTenant.Id);
		var medium = await mediums.FindAsync(item => item.Id == offering.MediumId && item.TenantId == CurrentTenant.Id && item.EducationBoardId == offering.BoardId);
		return MapPublicAcademicOptions(key, parent, offering, standard.Name, board?.Type.ToString(), medium?.Name);
	}

	internal async Task ValidateAsync(IEnumerable<AdmissionFormFieldDefinition> fields, IReadOnlyDictionary<string, string> values,
		AdmissionOffering offering, Guid year, bool isPublic)
	{
		foreach (var field in fields.Where(field => field.FieldType is AdmissionFormFieldType.Select or AdmissionFormFieldType.Radio or AdmissionFormFieldType.MultiSelect))
		{
			if (!AdmissionFormMasterDataValidator.TrySource(AdmissionAcademicFormOptions.OptionsJson(field.Code, field.FieldType, field.OptionsJson), out var key, out var parentField) ||
				!AdmissionAcademicFormOptions.IsAcademic(key) || string.IsNullOrWhiteSpace(values.GetValueOrDefault(field.Code))) continue;
			var parent = parentField != null && Guid.TryParse(values.GetValueOrDefault(parentField), out var parentId) ? (Guid?)parentId : null;
			var requiresParent = !string.IsNullOrWhiteSpace(parentField);
			if (requiresParent && !parent.HasValue)
				throw new BusinessException(AdmissionApplicationErrorCodes.ValidationFailed).WithData("Fields", field.Code);
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
