using System;
using System.Linq;
using ShikshaMitra.HR.Payroll.Settings;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Validation;

namespace ShikshaMitra.Mappers.HR.Payroll;

public static class EarningComponentContractMapper
{
    public static CreateEarningComponentDto ToCreateDto(EarningComponentCreateRequestDto request)
    {
        var componentType = ParseEarningType(request.EarningType);
        var calculationType = ParseCalculationType(request.CalculationType);
        var dependOn = InferDependency(calculationType, request.SelectedEarningComponentId);

        return new CreateEarningComponentDto
        {
            Name = request.EarningComponentName,
            Description = request.Description,
            ComponentType = componentType,
            CalculationType = calculationType,
            Amount = request.Amount,
            Percentage = request.Percentage,
            SelectedEarningComponentId = request.SelectedEarningComponentId,
            DependOn = dependOn,
            VisibleInPayslip = request.VisibleInPayslip,
            DisplayOrder = 0
        };
    }

    public static UpdateEarningComponentDto ToUpdateDto(EarningComponentUpdateRequestDto request, int existingDisplayOrder = 0)
    {
        var componentType = ParseEarningType(request.EarningType);
        var calculationType = ParseCalculationType(request.CalculationType);
        var dependOn = InferDependency(calculationType, request.SelectedEarningComponentId);

        return new UpdateEarningComponentDto
        {
            Name = request.EarningComponentName,
            Description = request.Description,
            ComponentType = componentType,
            CalculationType = calculationType,
            Amount = request.Amount,
            Percentage = request.Percentage,
            SelectedEarningComponentId = request.SelectedEarningComponentId,
            DependOn = dependOn,
            VisibleInPayslip = request.VisibleInPayslip,
            DisplayOrder = existingDisplayOrder
        };
    }

    public static EarningComponentPaginatedResponseDto ToPaginatedResponse(
        PagedResultDto<EarningComponentDto> pagedResult, int page, int pageSize)
    {
        var totalPages = pageSize > 0
            ? (int)Math.Ceiling((double)pagedResult.TotalCount / pageSize)
            : 0;

        return new EarningComponentPaginatedResponseDto
        {
            TotalCount = (int)pagedResult.TotalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Items = pagedResult.Items.Select(ToResponseDto).ToList()
        };
    }

    public static EarningComponentResponseDto ToResponseDto(EarningComponentDto dto)
    {
        return new EarningComponentResponseDto
        {
            Id = dto.Id,
            EarningComponentName = dto.Name,
            Description = dto.Description,
            EarningType = dto.ComponentType.ToString().ToUpperInvariant(),
            CalculationType = dto.CalculationType.ToString().ToUpperInvariant(),
            Amount = dto.Amount,
            Percentage = dto.Percentage,
            SelectedEarningComponentId = dto.SelectedEarningComponentId,
            VisibleInPayslip = dto.VisibleInPayslip,
            CustomComponent = dto.SelectedEarningComponentId.HasValue
                ? new CustomComponentDto
                {
                    Id = dto.SelectedEarningComponentId.Value,
                    ComponentName = dto.SelectedEarningComponentName ?? string.Empty,
                    ComponentOf = dto.DependOn.ToString().ToUpperInvariant(),
                    Description = dto.SelectedEarningComponentDescription,
                    VisibleInPayslip = dto.SelectedEarningComponentVisibleInPayslip ?? dto.VisibleInPayslip
                }
                : null
        };
    }

    private static EarningComponentType ParseEarningType(string earningType)
    {
        var normalized = (earningType ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "FIXED" => EarningComponentType.Fixed,
            "VARIABLE" => EarningComponentType.Variable,
            _ => throw CreateValidationException(
                "earningType",
                $"earningType must be FIXED or VARIABLE, got: '{earningType}'.")
        };
    }

    private static CalculationType ParseCalculationType(string calculationType)
    {
        var normalized = (calculationType ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "AMOUNT" => CalculationType.Amount,
            "PERCENTAGE" => CalculationType.Percentage,
            _ => throw CreateValidationException(
                "calculationType",
                $"calculationType must be AMOUNT or PERCENTAGE, got: '{calculationType}'.")
        };
    }

    private static AbpValidationException CreateValidationException(string memberName, string message)
    {
        return new AbpValidationException(
            "Validation failed",
            new List<ValidationResult>
            {
                new(message, new[] { memberName })
            });
    }

    private static ComponentDependency InferDependency(CalculationType calculationType, Guid? selectedComponentId)
    {
        if (calculationType == CalculationType.Percentage && selectedComponentId.HasValue)
            return ComponentDependency.CustomComponent;

        return ComponentDependency.None;
    }
}
