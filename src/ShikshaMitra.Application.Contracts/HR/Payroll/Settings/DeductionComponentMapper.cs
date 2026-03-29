using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Payroll.Settings;

public static class DeductionComponentMapper
{
	public static CreateDeductionComponentDto ToCreateDto(DeductionComponentCreateRequestDto request)
	{
		var componentType = ParseDeductionType(request.DeductionType);
		var calculationType = ParseCalculationType(request.CalculationType);
		var dependOn = InferDependency(calculationType, request.SelectedDeductionComponentId);

		return new CreateDeductionComponentDto
		{
			Name = request.DeductionComponentName,
			Description = request.Description,
			ComponentType = componentType,
			CalculationType = calculationType,
			Amount = request.Amount,
			Percentage = request.Percentage,
			SelectedDeductionComponentId = request.SelectedDeductionComponentId,
			DependOn = dependOn,
			VisibleInPayslip = request.VisibleInPayslip,
			DisplayOrder = 0
		};
	}

	public static UpdateDeductionComponentDto ToUpdateDto(DeductionComponentUpdateRequestDto request, int existingDisplayOrder = 0)
	{
		var componentType = ParseDeductionType(request.DeductionType);
		var calculationType = ParseCalculationType(request.CalculationType);
		var dependOn = InferDependency(calculationType, request.SelectedDeductionComponentId);

		return new UpdateDeductionComponentDto
		{
			Name = request.DeductionComponentName,
			Description = request.Description,
			ComponentType = componentType,
			CalculationType = calculationType,
			Amount = request.Amount,
			Percentage = request.Percentage,
			SelectedDeductionComponentId = request.SelectedDeductionComponentId,
			DependOn = dependOn,
			VisibleInPayslip = request.VisibleInPayslip,
			DisplayOrder = existingDisplayOrder
		};
	}

	public static DeductionComponentPaginatedResponseDto ToPaginatedResponse(
		PagedResultDto<DeductionComponentDto> pagedResult, int page, int pageSize)
	{
		var totalPages = pageSize > 0
			? (int)Math.Ceiling((double)pagedResult.TotalCount / pageSize)
			: 0;

		return new DeductionComponentPaginatedResponseDto
		{
			TotalCount = (int)pagedResult.TotalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = pagedResult.Items.Select(ToResponseDto).ToList()
		};
	}

	public static DeductionComponentResponseDto ToResponseDto(DeductionComponentDto dto)
	{
		return new DeductionComponentResponseDto
		{
			Id = dto.Id,
			DeductionComponentName = dto.Name,
			Description = dto.Description,
			DeductionType = dto.ComponentType.ToString().ToUpperInvariant(),
			CalculationType = dto.CalculationType.ToString().ToUpperInvariant(),
			Amount = dto.Amount,
			Percentage = dto.Percentage,
			SelectedDeductionComponentId = dto.SelectedDeductionComponentId,
			VisibleInPayslip = dto.VisibleInPayslip,
			CustomComponent = dto.SelectedDeductionComponentId.HasValue
				? new CustomComponentDto
				{
					Id = dto.SelectedDeductionComponentId.Value,
					ComponentName = string.Empty,
					ComponentOf = dto.DependOn.ToString().ToUpperInvariant(),
					Description = null,
					VisibleInPayslip = dto.VisibleInPayslip
				}
				: null
		};
	}

	private static DeductionComponentType ParseDeductionType(string deductionType)
	{
		var normalized = (deductionType ?? string.Empty).Trim().ToUpperInvariant();
		return normalized switch
		{
			"FIXED" => DeductionComponentType.Fixed,
			"VARIABLE" => DeductionComponentType.Variable,
			_ => throw new Volo.Abp.BusinessException("ShikshaMitra:InvalidDeductionType")
				.WithData("message", $"deductionType must be FIXED or VARIABLE, got: '{deductionType}'.")
		};
	}

	private static Settings.CalculationType ParseCalculationType(string calculationType)
	{
		var normalized = (calculationType ?? string.Empty).Trim().ToUpperInvariant();
		return normalized switch
		{
			"AMOUNT" => Settings.CalculationType.Amount,
			"PERCENTAGE" => Settings.CalculationType.Percentage,
			_ => throw new Volo.Abp.BusinessException("ShikshaMitra:InvalidCalculationType")
				.WithData("message", $"calculationType must be AMOUNT or PERCENTAGE, got: '{calculationType}'.")
		};
	}

	private static ComponentDependency InferDependency(Settings.CalculationType calculationType, Guid? selectedComponentId)
	{
		if (calculationType == Settings.CalculationType.Percentage && selectedComponentId.HasValue)
			return ComponentDependency.CustomComponent;

		return ComponentDependency.None;
	}
}
