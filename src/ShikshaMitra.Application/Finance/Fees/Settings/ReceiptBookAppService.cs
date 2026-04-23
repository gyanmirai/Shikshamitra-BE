using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.Finance.Setting;
using ShikshaMitra.Finance.Fees.Settings.FeeCategory.Dtos;
using ShikshaMitra.Finance.Fees.Settings.ReceiptBook;
using ShikshaMitra.Finance.Fees.Settings.ReceiptBook.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class ReceiptBookAppService(
	IRepository<ReceiptBook, Guid> receiptBookRepository,
	IAcademicYearRepository academicYearRepository,
	IEducationBoardRepository educationBoardRepository,
	IEducationMediumRepository educationMediumRepository,
	IEducationMediumStageRepository educationMediumStageRepository
) : ApplicationService, IReceiptBookAppService

{
	public async Task<ReceiptBookResponse> CreateAsync(Guid academicYearId, ReceiptBookCreateRequest input)
	{
		// Validate Academic Year
		await academicYearRepository.GetAsync(academicYearId);

		// Validate other related entities
		await educationBoardRepository.GetAsync(input.BoardId);
		await educationMediumRepository.GetAsync(input.MediumId);
		await educationMediumStageRepository.GetAsync(input.MediumStageId);

		try
		{
			var receiptBook = new ReceiptBook(
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

			await receiptBookRepository.InsertAsync(receiptBook, autoSave: true);

			var queryable = await receiptBookRepository.WithDetailsAsync(
				x => x.EducationBoard,
				x => x.EducationMedium,
				x => x.EducationMediumStage
			);
			var createdReceiptBook = await AsyncExecuter.FirstOrDefaultAsync(
				queryable.Where(x => x.Id == receiptBook.Id)
			);

			return MapToResponseWithNavigationProps(createdReceiptBook ?? receiptBook);
		}
		catch (Exception ex)
		{
			if (ex is BusinessException || ex is Volo.Abp.Domain.Entities.EntityNotFoundException)
			{
				throw;
			}
			throw new BusinessException("Failed to create receipt book", ex.Message);
		}

	}

	public async Task<ReceiptBookListResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string search = null)
	{
		var query = await receiptBookRepository.WithDetailsAsync(
			x => x.EducationBoard,
			x => x.EducationMedium,
			x => x.EducationMediumStage
		);

		query = query.Where(x => x.AcademicYearId == academicYearId);

		if (!string.IsNullOrWhiteSpace(search))
		{
			query = query.Where(x => x.Name.Contains(search));
		}

		var totalCount = await AsyncExecuter.CountAsync(query);

		if (totalCount == 0)
		{
			return new ReceiptBookListResponse
			{
				TotalCount = 0,
				CurrentPage = page,
				PageSize = pageSize,
				TotalPages = 0,
				Items = new List<ReceiptBookResponse>()
			};
		}

		query = query
			.OrderByDescending(x => x.CreationTime)
			.Skip((page - 1) * pageSize)
			.Take(pageSize);

		var items = await AsyncExecuter.ToListAsync(query);

		var responseItems = items.Select(item => MapToResponseWithNavigationProps(item)).ToList();

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
		var queryable = await receiptBookRepository.WithDetailsAsync(
			x => x.EducationBoard,
			x => x.EducationMedium,
			x => x.EducationMediumStage
		);
		var receiptBook = await AsyncExecuter.FirstOrDefaultAsync(
			queryable.Where(x => x.Id == receiptBookId)
		);

		if (receiptBook == null)
			throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(ReceiptBook), receiptBookId);

		return MapToResponseWithNavigationProps(receiptBook);
	}

	public async Task<MessageResponse> UpdateAsync(Guid receiptBookId, ReceiptBookUpdateRequest input)
	{
		// Validate related entities
		await educationBoardRepository.GetAsync(input.BoardId);
		await educationMediumRepository.GetAsync(input.MediumId);
		await educationMediumStageRepository.GetAsync(input.MediumStageId);

		var receiptBook = await receiptBookRepository.GetAsync(receiptBookId);

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

		await receiptBookRepository.UpdateAsync(receiptBook, autoSave: true);

		return new MessageResponse
		{
			Message = "Receipt book updated successfully"
		};
	}


	public async Task<MessageResponse> DeleteAsync(Guid receiptBookId)
	{
		await receiptBookRepository.DeleteAsync(receiptBookId);

		return new MessageResponse
		{
			Message = "Receipt book deleted successfully"
		};
	}

	public async Task<ReceiptBookStatusUpdateResponse> UpdateStatusAsync(Guid receiptBookId, ReceiptBookStatusUpdateRequest input)
	{
		var receiptBook = await receiptBookRepository.GetAsync(receiptBookId);
		var previousStatus = receiptBook.IsActive;

		receiptBook.SetActiveStatus(input.IsActive);

		await receiptBookRepository.UpdateAsync(receiptBook);

		return new ReceiptBookStatusUpdateResponse
		{
			Id = receiptBook.Id,
			PreviousStatus = previousStatus,
			CurrentStatus = receiptBook.IsActive,
			UpdatedAt = DateTime.UtcNow // Using UTC for consistency
		};
	}

	private ReceiptBookResponse MapToResponseWithNavigationProps(ReceiptBook receiptBook)
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
			CreatedAt = receiptBook.CreationTime,
			BoardName = receiptBook.EducationBoard?.Type.ToString() ?? string.Empty,
			MediumName = receiptBook.EducationMedium?.Name ?? string.Empty,
			MediumStageName = receiptBook.EducationMediumStage?.Name ?? string.Empty
		};
	}
}
