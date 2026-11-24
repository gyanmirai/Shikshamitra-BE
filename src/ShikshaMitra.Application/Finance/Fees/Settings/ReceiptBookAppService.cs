using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public partial class ReceiptBookAppService(
	IRepository<ReceiptBook, Guid> receiptBookRepository,
	IAcademicYearRepository academicYearRepository,
	IEducationBoardRepository educationBoardRepository,
	IEducationMediumRepository educationMediumRepository,
	IEducationMediumStageRepository educationMediumStageRepository,
	IUserScopeAccessService scopeAccess,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
	IFeePaymentReceiptRepository paymentReceiptRepository,
	IDataFilter dataFilter
) : ApplicationService, IReceiptBookAppService
{
	private sealed record ReceiptBookScope(Guid? AcademicYearId, Guid? EducationBoardId,
		Guid? EducationMediumId, Guid? EducationMediumStageId) : IUserScopedEntity
	{
		public Guid? StandardId => null;
		public Guid? DivisionId => null;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ReceiptBook.Add)]
	public async Task<ReceiptBookResponse> CreateAsync(Guid academicYearId, ReceiptBookCreateRequest input)
	{
		await academicYearRepository.GetAsync(academicYearId);
		await educationBoardRepository.GetAsync(input.BoardId);
		await educationMediumRepository.GetAsync(input.MediumId);
		await educationMediumStageRepository.GetAsync(input.MediumStageId);

		try
		{
			var receiptBook = new ReceiptBook(
				GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
				input.BoardId, input.MediumId, input.MediumStageId,
				input.Name, input.Prefix, input.Suffix,
				input.StartingNumber, input.EndingNumber, input.Description);

			await receiptBookRepository.InsertAsync(receiptBook, autoSave: true);

			var queryable = await receiptBookRepository.WithDetailsAsync(
				x => x.EducationBoard, x => x.EducationMedium, x => x.EducationMediumStage);
			var created = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == receiptBook.Id));
			return MapToResponse(created ?? receiptBook);
		}
		catch (Exception ex)
		{
			if (ex is BusinessException || ex is Volo.Abp.Domain.Entities.EntityNotFoundException)
				throw;
			throw new BusinessException("Failed to create receipt book", ex.Message);
		}
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ReceiptBook.View)]
	public async Task<ReceiptBookResponse> GetAsync(Guid receiptBookId)
	{
		var queryable = await receiptBookRepository.WithDetailsAsync(
			x => x.EducationBoard, x => x.EducationMedium, x => x.EducationMediumStage);
		var receiptBook = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == receiptBookId));

		if (receiptBook == null)
			throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(ReceiptBook), receiptBookId);

		return MapToResponse(receiptBook);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ReceiptBook.Add)]
	public async Task<MessageResponse> UpdateAsync(Guid receiptBookId, ReceiptBookUpdateRequest input)
	{
		await educationBoardRepository.GetAsync(input.BoardId);
		await educationMediumRepository.GetAsync(input.MediumId);
		await educationMediumStageRepository.GetAsync(input.MediumStageId);

		var receiptBook = await receiptBookRepository.GetAsync(receiptBookId);
		receiptBook.Update(
			input.BoardId, input.MediumId, input.MediumStageId,
			input.Name, input.Prefix, input.Suffix,
			input.StartingNumber, input.EndingNumber, input.Description);

		await receiptBookRepository.UpdateAsync(receiptBook, autoSave: true);
		return new MessageResponse { Message = "Receipt book updated successfully" };
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ReceiptBook.Delete)]
	[UnitOfWork(isTransactional: true)]
	public async Task<MessageResponse> DeleteAsync(Guid receiptBookId)
	{
		var book = await receiptBookRepository.GetAsync(receiptBookId);
		await scopeAccess.EnsureCurrentUserCanAccessAsync(
			new ReceiptBookScope(book.AcademicYearId, book.BoardId, book.MediumId, book.MediumStageId));

		if (await feeStructureMetadataRepository.AnyAsync(metadata => metadata.ReceiptBookId == receiptBookId) ||
			await feeStructureRepository.AnyAsync(structure => structure.ReceiptBookId == receiptBookId) ||
			await HasIssuedReceiptAsync(receiptBookId))
		{
			throw new BusinessException(ReceiptBookErrorCodes.CannotDeleteInUse).WithData("Name", book.Name);
		}

		await receiptBookRepository.DeleteAsync(book);
		return new MessageResponse { Message = "Receipt book deleted successfully" };
	}

	private async Task<bool> HasIssuedReceiptAsync(Guid receiptBookId)
	{
		using var historicalReceipts = dataFilter.Disable<ISoftDelete>();
		return await paymentReceiptRepository.AnyAsync(receipt => receipt.ReceiptBookId == receiptBookId);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ReceiptBook.Add)]
	public async Task<ReceiptBookStatusUpdateResponse> UpdateStatusAsync(Guid receiptBookId, ReceiptBookStatusUpdateRequest input)
	{
		var receiptBook    = await receiptBookRepository.GetAsync(receiptBookId);
		var previousStatus = receiptBook.IsActive;
		receiptBook.SetActiveStatus(input.IsActive);
		await receiptBookRepository.UpdateAsync(receiptBook);

		return new ReceiptBookStatusUpdateResponse
		{
			Id             = receiptBook.Id,
			PreviousStatus = previousStatus,
			CurrentStatus  = receiptBook.IsActive,
			UpdatedAt      = DateTime.UtcNow,
		};
	}
}
