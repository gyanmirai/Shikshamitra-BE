using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Mapper for converting between OpenAPI contract DTOs and domain DTOs for earning components.
/// Handles field name mappings, type conversions, and enum parsing.
/// </summary>
public static class EarningComponentOpenApiMapper
{
	/// <summary>
	/// Maps ComponentRequestDto (OpenAPI) to CreateEarningComponentDto (Domain)
	/// </summary>
	public static CreateEarningComponentDto ToCreateDto(ComponentRequestDto request)
	{
		var componentType = ParseComponentType(request.ComponentType, request.IsFixed);
		var dependOn = ParseDependency(request.DependOn);
		var amount = FormatAmount(request.Amount, componentType, dependOn);
		
		return new CreateEarningComponentDto
		{
			Name = request.ComponentName,
			Description = CombineDescription(request.Description, request.CustomComponent),
			ComponentType = componentType,
			DependOn = dependOn,
			Amount = amount,
			VisibleInPayslip = request.VisibleInPayslip,
			DisplayOrder = 0 // Default display order
		};
	}
	
	/// <summary>
	/// Maps ComponentRequestDto (OpenAPI) to UpdateEarningComponentDto (Domain)
	/// </summary>
	/// <param name="request">The OpenAPI request DTO</param>
	/// <param name="existingDisplayOrder">The existing display order to preserve (defaults to 0 for new components)</param>
	public static UpdateEarningComponentDto ToUpdateDto(ComponentRequestDto request, int existingDisplayOrder = 0)
	{
		var componentType = ParseComponentType(request.ComponentType, request.IsFixed);
		var dependOn = ParseDependency(request.DependOn);
		var amount = FormatAmount(request.Amount, componentType, dependOn);
		
		return new UpdateEarningComponentDto
		{
			Name = request.ComponentName,
			Description = CombineDescription(request.Description, request.CustomComponent),
			ComponentType = componentType,
			DependOn = dependOn,
			Amount = amount,
			VisibleInPayslip = request.VisibleInPayslip,
			DisplayOrder = existingDisplayOrder // Preserve existing display order
		};
	}
	
	/// <summary>
	/// Maps EarningComponentDto (Domain) to ComponentResponseDto (OpenAPI)
	/// </summary>
	public static ComponentResponseDto ToComponentResponse(EarningComponentDto dto)
	{
		return new ComponentResponseDto
		{
			Id = dto.Id,
			ComponentName = dto.Name,
			Description = dto.Description,
			ComponentType = dto.ComponentType.ToString(),
			DependOn = dto.DependOn.ToString(),
			CustomComponent = null, // Not stored in domain
			Amount = ParseAmountToDecimal(dto.Amount),
			VisibleInPayslip = dto.VisibleInPayslip,
			IsFixed = dto.ComponentType == EarningComponentType.Fixed,
			DisplayOrder = dto.DisplayOrder,
			IsActive = dto.IsActive
		};
	}
	
	/// <summary>
	/// Maps PagedResultDto to ComponentListResponseDto (OpenAPI)
	/// </summary>
	public static ComponentListResponseDto ToListResponse(PagedResultDto<EarningComponentDto> pagedResult)
	{
		return new ComponentListResponseDto
		{
			Total = (int)pagedResult.TotalCount,
			Data = pagedResult.Items.Select(ToComponentResponse).ToList()
		};
	}
	
	/// <summary>
	/// Maps a list of EarningComponentDto to ComponentListResponseDto (OpenAPI)
	/// </summary>
	public static ComponentListResponseDto ToListResponse(IReadOnlyList<EarningComponentDto> items, int totalCount)
	{
		return new ComponentListResponseDto
		{
			Total = totalCount,
			Data = items.Select(ToComponentResponse).ToList()
		};
	}
	
	#region Private Helper Methods
	
	/// <summary>
	/// Parses the component type from string and isFixed flag.
	/// The isFixed flag takes precedence if componentType string parsing fails.
	/// </summary>
	private static EarningComponentType ParseComponentType(string? componentTypeStr, bool isFixed)
	{
		// Try to parse from string first
		if (!string.IsNullOrWhiteSpace(componentTypeStr))
		{
			if (Enum.TryParse<EarningComponentType>(componentTypeStr, ignoreCase: true, out var parsed))
			{
				return parsed;
			}
			
			// Handle common string variations
			var normalizedType = componentTypeStr.Trim().ToLowerInvariant();
			return normalizedType switch
			{
				"fixed" => EarningComponentType.Fixed,
				"variable" => EarningComponentType.Variable,
				"percentage" => EarningComponentType.Variable,
				_ => isFixed ? EarningComponentType.Fixed : EarningComponentType.Variable
			};
		}
		
		// Fall back to isFixed flag
		return isFixed ? EarningComponentType.Fixed : EarningComponentType.Variable;
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
			"basic" or "basicsalary" or "basic_salary" => ComponentDependency.Basic,
			"gross" or "grosssalary" or "gross_salary" => ComponentDependency.Gross,
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
	
	/// <summary>
	/// Combines description with custom component info if provided
	/// </summary>
	private static string? CombineDescription(string? description, string? customComponent)
	{
		if (string.IsNullOrWhiteSpace(customComponent))
		{
			return description;
		}
		
		if (string.IsNullOrWhiteSpace(description))
		{
			return $"Custom: {customComponent}";
		}
		
		return $"{description} (Custom: {customComponent})";
	}
	
	#endregion
}

