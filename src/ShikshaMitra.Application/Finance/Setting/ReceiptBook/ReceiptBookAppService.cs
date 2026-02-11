using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Application.Contracts.Finance.Setting.ReceiptBook;
using ShikshaMitra.Application.Contracts.Finance.Setting.ReceiptBook.Dtos;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos; // For MessageResponse
using ShikshaMitra.Finance.Setting.ReceiptBook;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;

namespace ShikshaMitra.Application.Finance.Setting.ReceiptBook;

[Authorize]
[RemoteService(false)]
public class ReceiptBookAppService : ApplicationService, IReceiptBookAppService
{
    private readonly IRepository<ShikshaMitra.Finance.Setting.ReceiptBook.ReceiptBook, Guid> _receiptBookRepository;

    public ReceiptBookAppService(
        IRepository<ShikshaMitra.Finance.Setting.ReceiptBook.ReceiptBook, Guid> receiptBookRepository)
    {
        _receiptBookRepository = receiptBookRepository;
    }

    public async Task<ReceiptBookResponse> CreateAsync(Guid academicYearId, ReceiptBookCreateRequest input)
    {
        var receiptBook = new ShikshaMitra.Finance.Setting.ReceiptBook.ReceiptBook(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            input.BoardId,
            input.MediumId,
            input.MediumStageId,
            input.Name,
            input.Prefix,
            input.Suffix,
            input.StartingNumber,
            input.EndingNumber,
            input.Description
        );

        await _receiptBookRepository.InsertAsync(receiptBook);

        return await MapToResponseAsync(receiptBook);
    }

    public async Task<ReceiptBookListResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string search = null)
    {
        var query = await _receiptBookRepository.GetQueryableAsync();
        
        query = query.Where(x => x.AcademicYearId == academicYearId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search));
        }

        var totalCount = await _receiptBookRepository.AsyncExecuter.CountAsync(query);

        query = query.OrderByDescending(x => x.CreationTime)
                     .Skip((page - 1) * pageSize)
                     .Take(pageSize);

        var items = await _receiptBookRepository.AsyncExecuter.ToListAsync(query);

        return new ReceiptBookListResponse
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items.Select(MapToResponse).ToList()
        };
    }

    public async Task<ReceiptBookResponse> GetAsync(Guid receiptBookId)
    {
        var receiptBook = await _receiptBookRepository.GetAsync(receiptBookId);
        return await MapToResponseAsync(receiptBook);
    }

    public async Task<MessageResponse> UpdateAsync(Guid receiptBookId, ReceiptBookUpdateRequest input)
    {
        var receiptBook = await _receiptBookRepository.GetAsync(receiptBookId);
        
        receiptBook.Update(
            input.BoardId,
            input.MediumId,
            input.MediumStageId,
            input.Name,
            input.Prefix,
            input.Suffix,
            input.StartingNumber,
            input.EndingNumber,
            input.Description
        );

        await _receiptBookRepository.UpdateAsync(receiptBook);

        return new MessageResponse
        {
            Message = "Receipt book updated successfully"
        };
    }

    public async Task<MessageResponse> DeleteAsync(Guid receiptBookId)
    {
        await _receiptBookRepository.DeleteAsync(receiptBookId);

        return new MessageResponse
        {
            Message = "Receipt book deleted successfully"
        };
    }

    public async Task<ReceiptBookStatusUpdateResponse> UpdateStatusAsync(Guid receiptBookId, ReceiptBookStatusUpdateRequest input)
    {
        var receiptBook = await _receiptBookRepository.GetAsync(receiptBookId);
        var previousStatus = receiptBook.IsActive;

        receiptBook.SetActiveStatus(input.IsActive);

        await _receiptBookRepository.UpdateAsync(receiptBook);

        return new ReceiptBookStatusUpdateResponse
        {
            Id = receiptBook.Id,
            PreviousStatus = previousStatus,
            CurrentStatus = receiptBook.IsActive,
            UpdatedAt = DateTime.UtcNow // Using UTC for consistency
        };
    }

    private Task<ReceiptBookResponse> MapToResponseAsync(ShikshaMitra.Finance.Setting.ReceiptBook.ReceiptBook receiptBook)
    {
        return Task.FromResult(MapToResponse(receiptBook));
    }

    private ReceiptBookResponse MapToResponse(ShikshaMitra.Finance.Setting.ReceiptBook.ReceiptBook receiptBook)
    {
        return new ReceiptBookResponse
        {
            Id = receiptBook.Id,
            Name = receiptBook.Name,
            Prefix = receiptBook.Prefix,
            Suffix = receiptBook.Suffix,
            BoardId = receiptBook.BoardId,
            MediumId = receiptBook.MediumId,
            MediumStageId = receiptBook.MediumStageId,
            StartingNumber = receiptBook.StartingNumber,
            EndingNumber = receiptBook.EndingNumber,
            Description = receiptBook.Description,
            IsActive = receiptBook.IsActive,
            CreatedAt = receiptBook.CreationTime
        };
    }
}
