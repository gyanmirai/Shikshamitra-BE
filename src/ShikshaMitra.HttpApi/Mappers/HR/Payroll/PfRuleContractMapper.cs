using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Mappers.HR.Payroll;

public static class PfRuleContractMapper
{
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

    public static PfRuleCreateResponseDto ToCreateResponse(ProvidentFundDto dto)
    {
        return new PfRuleCreateResponseDto
        {
            Id = dto.Id,
            CreatedAt = dto.CreationTime
        };
    }

    public static PfRuleUpdateResponseDto ToUpdateResponse(ProvidentFundDto dto)
    {
        return new PfRuleUpdateResponseDto
        {
            Id = dto.Id,
            UpdatedAt = dto.LastModificationTime ?? DateTime.UtcNow
        };
    }

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
