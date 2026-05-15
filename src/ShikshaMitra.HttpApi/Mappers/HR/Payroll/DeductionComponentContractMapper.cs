using System;
using System.Linq;
using ShikshaMitra.HR.Payroll.Settings;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Validation;

namespace ShikshaMitra.Mappers.HR.Payroll;

public static class DeductionComponentContractMapper
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
            DisplayOrder = existingDisplayOrder,
            ApplyMode = ParseApplyMode(request.ApplyMode),
            EffectiveMonth = request.EffectiveMonth,
            PreviewHash = request.PreviewHash
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
                    ComponentName = dto.SelectedDeductionComponentName ?? string.Empty,
                    ComponentOf = dto.DependOn.ToString().ToUpperInvariant(),
                    Description = dto.SelectedDeductionComponentDescription,
                    VisibleInPayslip = dto.SelectedDeductionComponentVisibleInPayslip ?? dto.VisibleInPayslip
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
            _ => throw CreateValidationException(
                "deductionType",
                $"deductionType must be FIXED or VARIABLE, got: '{deductionType}'.")
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

    private static PayrollComponentApplyMode ParseApplyMode(string? applyMode)
    {
        if (string.IsNullOrWhiteSpace(applyMode))
            return PayrollComponentApplyMode.SyncedOnly;

        return Enum.TryParse<PayrollComponentApplyMode>(applyMode, ignoreCase: true, out var parsed)
            ? parsed
            : PayrollComponentApplyMode.SyncedOnly;
    }
}
