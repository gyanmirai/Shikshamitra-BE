using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Mapper for converting between domain DTOs and OpenAPI DTOs for PF rules.
/// Handles the transformation between internal representations and API contract formats.
/// </summary>
public static class PfRuleOpenApiMapper
{
	/// <summary>
	/// Maps ProvidentFundDto to PfRuleResponseDto (OpenAPI)
	/// </summary>
	public static PfRuleResponseDto ToPfRuleResponse(ProvidentFundDto dto)
	{
		return new PfRuleResponseDto
		{
			Id = dto.Id,
			Name = dto.Name,
			Code = dto.Code,
			EmployeePercentage = dto.EmployeePercentage,
			EmployerPercentage = dto.EmployerPercentage,
			CreatedAt = dto.CreationTime,
			UpdatedAt = dto.LastModificationTime
		};
	}
	
	/// <summary>
	/// Maps ProvidentFundDto to PfRuleCreateResponseDto (OpenAPI)
	/// </summary>
	public static PfRuleCreateResponseDto ToCreateResponse(ProvidentFundDto dto)
	{
		return new PfRuleCreateResponseDto
		{
			Id = dto.Id,
			CreatedAt = dto.CreationTime
		};
	}
	
	/// <summary>
	/// Maps ProvidentFundDto to PfRuleUpdateResponseDto (OpenAPI)
	/// </summary>
	public static PfRuleUpdateResponseDto ToUpdateResponse(ProvidentFundDto dto)
	{
		return new PfRuleUpdateResponseDto
		{
			Id = dto.Id,
			UpdatedAt = dto.LastModificationTime ?? DateTime.UtcNow
		};
	}
	
	/// <summary>
	/// Maps PagedResultDto to PaginatedPfRuleResponseDto (OpenAPI)
	/// </summary>
	/// <param name="pagedResult">The paged result from the application service</param>
	/// <param name="currentPage">Current page number (1-based)</param>
	/// <param name="pageSize">Number of items per page</param>
	public static PaginatedPfRuleResponseDto ToPaginatedResponse(
		PagedResultDto<ProvidentFundDto> pagedResult,
		int currentPage,
		int pageSize)
	{
		var totalPages = pageSize > 0 
			? (int)Math.Ceiling((double)pagedResult.TotalCount / pageSize) 
			: 0;
		
		return new PaginatedPfRuleResponseDto
		{
			TotalCount = (int)pagedResult.TotalCount,
			CurrentPage = currentPage,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = pagedResult.Items.Select(ToPfRuleResponse).ToList()
		};
	}
	
	/// <summary>
	/// Maps a list of ProvidentFundDto to PaginatedPfRuleResponseDto (OpenAPI)
	/// </summary>
	public static PaginatedPfRuleResponseDto ToPaginatedResponse(
		IReadOnlyList<ProvidentFundDto> items,
		int totalCount,
		int currentPage,
		int pageSize)
	{
		var totalPages = pageSize > 0 
			? (int)Math.Ceiling((double)totalCount / pageSize) 
			: 0;
		
		return new PaginatedPfRuleResponseDto
		{
			TotalCount = totalCount,
			CurrentPage = currentPage,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = items.Select(ToPfRuleResponse).ToList()
		};
	}
}

