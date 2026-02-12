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
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;

namespace ShikshaMitra.Application.Finance.Setting.ReceiptBook;

[Authorize]
[RemoteService(false)]
public class ReceiptBookAppService : ApplicationService, IReceiptBookAppService
{
	private readonly IRepository<ShikshaMitra.Finance.Setting.ReceiptBook.ReceiptBook, Guid> _receiptBookRepository;
	private readonly IRepository<EducationBoard, Guid> _educationBoardRepository;
	private readonly IRepository<EducationMedium, Guid> _educationMediumRepository;
	private readonly IRepository<EducationMediumStage, Guid> _educationMediumStageRepository;


	public ReceiptBookAppService(
		IRepository<ShikshaMitra.Finance.Setting.ReceiptBook.ReceiptBook, Guid> receiptBookRepository,
		IRepository<EducationBoard, Guid> educationBoardRepository,
		IRepository<EducationMedium, Guid> educationMediumRepository,
		IRepository<EducationMediumStage, Guid> educationMediumStageRepository)
	{
		_receiptBookRepository = receiptBookRepository;
		_educationBoardRepository = educationBoardRepository;
		_educationMediumRepository = educationMediumRepository;
		_educationMediumStageRepository = educationMediumStageRepository;
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

		var boardIds = items.Select(x => x.BoardId).Distinct().ToArray();
		var mediumIds = items.Select(x => x.MediumId).Distinct().ToArray();
		var mediumStageIds = items.Select(x => x.MediumStageId).Distinct().ToArray();

		var boards = await _educationBoardRepository.GetListAsync(x => boardIds.Contains(x.Id));
		var mediums = await _educationMediumRepository.GetListAsync(x => mediumIds.Contains(x.Id));
		var mediumStages = await _educationMediumStageRepository.GetListAsync(x => mediumStageIds.Contains(x.Id));

		var boardMap = boards.ToDictionary(x => x.Id, x => x.Type.ToString());
		var mediumMap = mediums.ToDictionary(x => x.Id, x => x.Name);
		var mediumStageMap = mediumStages.ToDictionary(x => x.Id, x => x.Name);

		var responseItems = items.Select(item =>
		{
			var dto = MapToResponse(item);
			dto.BoardName = boardMap.ContainsKey(item.BoardId) ? boardMap[item.BoardId] : null;
			dto.MediumName = mediumMap.ContainsKey(item.MediumId) ? mediumMap[item.MediumId] : null;
			dto.MediumStageName = mediumStageMap.ContainsKey(item.MediumStageId) ? mediumStageMap[item.MediumStageId] : null;
			return dto;
		}).ToList();

		return new ReceiptBookListResponse
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = (int) Math.Ceiling(totalCount / (double) pageSize),
			Items = responseItems
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

	private async Task<ReceiptBookResponse> MapToResponseAsync(ShikshaMitra.Finance.Setting.ReceiptBook.ReceiptBook receiptBook)
	{
		var dto = MapToResponse(receiptBook);

		var board = await _educationBoardRepository.FindAsync(receiptBook.BoardId);
		dto.BoardName = board?.Type.ToString();

		var medium = await _educationMediumRepository.FindAsync(receiptBook.MediumId);
		dto.MediumName = medium?.Name;

		var mediumStage = await _educationMediumStageRepository.FindAsync(receiptBook.MediumStageId);
		dto.MediumStageName = mediumStage?.Name;

		return dto;
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
