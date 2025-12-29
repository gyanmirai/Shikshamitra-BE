using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Mapper for converting between contract DTOs and domain DTOs for earning components.
/// Handles field name mappings, type conversions, and enum parsing.
/// </summary>
public static class EarningComponentMapper
{
	/// <summary>
	/// Maps PayrollComponentCreateRequestDto to CreateEarningComponentDto
	/// </summary>
	public static CreateEarningComponentDto ToCreateDto(PayrollComponentCreateRequestDto request)
	{
		var componentType = ParseComponentType(request.ComponentType, request.IsFixed);
		var dependOn = ParseDependency(request.DependOn);
		var amount = FormatAmount(Convert.ToDecimal(request.Amount ?? 0), componentType, dependOn);

		return new CreateEarningComponentDto
		{
			Name = request.ComponentName,
			Description = request.Description,
			ComponentType = componentType,
			DependOn = dependOn,
			CustomComponentId = request.CustomComponentId,
			Amount = amount,
			VisibleInPayslip = request.VisibleInPayslip,
			DisplayOrder = 0
		};
	}
	
	/// <summary>
	/// Maps PayrollComponentCreateRequestDto to UpdateEarningComponentDto
	/// </summary>
	public static UpdateEarningComponentDto ToUpdateDto(PayrollComponentCreateRequestDto request, int existingDisplayOrder = 0)
	{
		var componentType = ParseComponentType(request.ComponentType, request.IsFixed);
		var dependOn = ParseDependency(request.DependOn);
		var amount = FormatAmount(Convert.ToDecimal(request.Amount ?? 0), componentType, dependOn);

		return new UpdateEarningComponentDto
		{
			Name = request.ComponentName,
			Description = request.Description,
			ComponentType = componentType,
			DependOn = dependOn,
			CustomComponentId = request.CustomComponentId,
			Amount = amount,
			VisibleInPayslip = request.VisibleInPayslip,
			DisplayOrder = existingDisplayOrder
		};
	}

	/// <summary>
	/// Maps PagedResultDto to DeductionComponentPaginatedResponseDto
	/// Note: Using DeductionComponentPaginatedResponseDto as it's the same structure for both
	/// </summary>
	public static DeductionComponentPaginatedResponseDto ToPaginatedResponse(PagedResultDto<EarningComponentDto> pagedResult, int page, int pageSize)
	{
		var totalPages = (int)Math.Ceiling((double)pagedResult.TotalCount / pageSize);

		return new DeductionComponentPaginatedResponseDto
		{
			TotalCount = (int)pagedResult.TotalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = pagedResult.Items.Select(ToEarningComponentResponse).ToList()
		};
	}

	/// <summary>
	/// Maps EarningComponentDto to DeductionComponentResponseDto
	/// </summary>
	private static DeductionComponentResponseDto ToEarningComponentResponse(EarningComponentDto dto)
	{
		return new DeductionComponentResponseDto
		{
			Id = dto.Id,
			ComponentName = dto.Name,
			Description = dto.Description,
			IsFixed = dto.ComponentType == EarningComponentType.Fixed,
			ComponentType = dto.ComponentType.ToString(),
			Amount = ParseAmountToDouble(dto.Amount),
			VisibleInPayslip = dto.VisibleInPayslip,
			CustomComponent = dto.CustomComponentId.HasValue ? new CustomComponentDto
			{
				Id = dto.CustomComponentId.Value,
				ComponentName = string.Empty,
				DependOn = dto.DependOn.ToString(),
				Description = null,
				IsFixed = dto.ComponentType == EarningComponentType.Fixed,
				VisibleInPayslip = dto.VisibleInPayslip
			} : null
		};
	}

	/// <summary>
	/// Parses the amount string to double, removing % if present
	/// </summary>
	private static double ParseAmountToDouble(string amountStr)
	{
		if (string.IsNullOrWhiteSpace(amountStr))
		{
			return 0;
		}

		var trimmed = amountStr.Trim();
		if (trimmed.EndsWith("%"))
		{
			trimmed = trimmed.TrimEnd('%');
		}

		if (double.TryParse(trimmed, out var result))
		{
			return result;
		}

		return 0;
	}
	
	#region Private Helper Methods
	
	/// <summary>
	/// Parses the component type from string and isFixed flag.
	/// The isFixed flag takes precedence over componentType string.
	/// </summary>
	private static EarningComponentType ParseComponentType(string? componentTypeStr, bool isFixed)
	{
		// PRIORITY 1: If isFixed is explicitly true, always return Fixed
		// This ensures isFixed flag is actually prioritized as documented
		if (isFixed)
		{
			return EarningComponentType.Fixed;
		}

		// PRIORITY 2: If componentType string is provided, try to parse it
		if (!string.IsNullOrWhiteSpace(componentTypeStr))
		{
			var normalizedType = componentTypeStr.Trim().ToLowerInvariant();

			// If componentType explicitly specifies "fixed", return Fixed
			if (normalizedType == "fixed")
			{
				return EarningComponentType.Fixed;
			}

			// If componentType explicitly specifies "variable" or "percentage", return Variable
			if (normalizedType == "variable" || normalizedType == "percentage")
			{
				return EarningComponentType.Variable;
			}

			// Try enum parse
			if (Enum.TryParse<EarningComponentType>(componentTypeStr, ignoreCase: true, out var parsed))
			{
				return parsed;
			}
		}

		// PRIORITY 3: Default to Variable when isFixed is false and componentType is not provided/invalid
		return EarningComponentType.Variable;
	}
	
	/// <summary>
	/// Parses the dependency from string to enum
	/// </summary>
	private static ComponentDependency ParseDependency(string? dependOnStr)
	{
		if (string.IsNullOrWhiteSpace(dependOnStr))
		{
			return ComponentDependency.None;
		}

		if (Enum.TryParse<ComponentDependency>(dependOnStr, ignoreCase: true, out var parsed))
		{
			return parsed;
		}

		// Handle common string variations
		var normalized = dependOnStr.Trim().ToLowerInvariant();
		return normalized switch
		{
			"none" => ComponentDependency.None,
			"basic" or "basicsalary" or "basic_salary" or "basic salary" => ComponentDependency.Basic,
			"gross" or "grosssalary" or "gross_salary" or "gross salary" => ComponentDependency.Gross,
			"customcomponent" or "custom_component" or "custom component" or "any custom salary component" => ComponentDependency.CustomComponent,
			_ => ComponentDependency.None
		};
	}
	
	/// <summary>
	/// Formats the amount based on component type and dependency.
	/// For dependent variable components, appends % if not already present.
	/// </summary>
	private static string FormatAmount(decimal amount, EarningComponentType componentType, ComponentDependency dependOn)
	{
		// For fixed components or components with no dependency, just return the numeric value
		if (componentType == EarningComponentType.Fixed || dependOn == ComponentDependency.None)
		{
			return amount.ToString("G29"); // Remove trailing zeros
		}
		
		// For variable components with a dependency, treat as percentage
		// Validate percentage range
		if (amount < 0 || amount > 100)
		{
			throw new BusinessException("ShikshaMitra:InvalidPercentage")
				.WithData("message", "Percentage amount must be between 0 and 100.");
		}
		
		return $"{amount:G29}%";
	}
	
	/// <summary>
	/// Parses the amount string to decimal, removing % if present
	/// </summary>
	private static decimal ParseAmountToDecimal(string amountStr)
	{
		if (string.IsNullOrWhiteSpace(amountStr))
		{
			return 0;
		}
		
		var trimmed = amountStr.Trim();
		
		// Remove % suffix if present
		if (trimmed.EndsWith("%"))
		{
			trimmed = trimmed.TrimEnd('%');
		}
		
		if (decimal.TryParse(trimmed, out var result))
		{
			return result;
		}
		
		return 0;
	}

		#endregion
	}

