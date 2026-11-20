using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.InventoryVerification;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Uow;
using StockActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.InventoryVerification;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Authorize]
[Authorize(StockActions.View)]
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/library/stock-verification")]
public class LibraryStockVerificationController(ILibraryStockVerificationAppService counts) : AbpController
{
	[HttpGet("options")]
	public Task<LibraryStockVerificationOptionsDto> GetOptionsAsync() => counts.GetOptionsAsync();

	[HttpPost("preview")]
	[Authorize(StockActions.Add)]
	public Task<LibraryStockCountPreviewDto> PreviewAsync([FromBody] LibraryStockCountInput input) => counts.PreviewAsync(input);

	[HttpPost]
	[Authorize(StockActions.Add)]
	public Task<LibraryStockVerificationSessionDto> OpenAsync([FromBody] LibraryOpenStockCountCommand input) => counts.OpenAsync(input);

	[HttpGet]
	public Task<PaginatedResponse<LibraryStockVerificationSessionDto>> GetListAsync(int page = 1, int pageSize = 20) => counts.GetListAsync(page, pageSize);

	[HttpGet("{id:guid}")]
	public Task<LibraryStockVerificationSessionDto> GetAsync(Guid id) => counts.GetAsync(id);

	[HttpGet("{id:guid}/baseline")]
	public Task<PaginatedResponse<LibraryStockBaselineDto>> GetBaselineAsync(Guid id, int page = 1, int pageSize = 20) => counts.GetBaselineAsync(id, page, pageSize);

	[HttpGet("{id:guid}/observations")]
	public Task<PaginatedResponse<LibraryStockObservationDto>> GetObservationsAsync(Guid id, int page = 1, int pageSize = 20) => counts.GetObservationsAsync(id, page, pageSize);

	[HttpGet("{id:guid}/reconciliation")]
	[UnitOfWork(true)]
	public Task<PaginatedResponse<LibraryStockReconciliationRowDto>> GetReconciliationAsync(Guid id,
		LibraryStockReconciliationSource source = LibraryStockReconciliationSource.Expected, int page = 1, int pageSize = 20) =>
		counts.GetReconciliationAsync(id, source, page, pageSize);

	[HttpPost("{id:guid}/observations")]
	[Authorize(StockActions.Update)]
	public Task<LibraryStockObservationDto> RecordObservationAsync(Guid id, [FromBody] LibraryRecordStockObservationCommand input) => counts.RecordObservationAsync(id, input);

	[HttpPost("{id:guid}/review")]
	[Authorize(StockActions.Update)]
	public Task<LibraryStockTransitionDto> BeginReviewAsync(Guid id, [FromBody] LibraryBeginStockReviewCommand input) => counts.BeginReviewAsync(id, input);

	[HttpPost("{id:guid}/cancel")]
	[Authorize(StockActions.Update)]
	public Task<LibraryStockTransitionDto> CancelAsync(Guid id, [FromBody] LibraryCancelStockCountCommand input) => counts.CancelAsync(id, input);

	[HttpGet("{id:guid}/decisions")]
	public Task<PaginatedResponse<LibraryStockDecisionDto>> GetDecisionsAsync(Guid id, int page = 1, int pageSize = 20) =>
		counts.GetDecisionsAsync(id, page, pageSize);

	[HttpPost("{id:guid}/decisions")]
	[Authorize(StockActions.Update)]
	public Task<LibraryStockDecisionDto> SaveDecisionAsync(Guid id, [FromBody] LibrarySaveStockDecisionCommand input) =>
		counts.SaveDecisionAsync(id, input);

	[HttpGet("{id:guid}/approval-preview")]
	[Authorize(StockActions.Approve)]
	[UnitOfWork(true)]
	public Task<LibraryStockApprovalPreviewDto> GetApprovalPreviewAsync(Guid id) => counts.GetApprovalPreviewAsync(id);

	[HttpPost("{id:guid}/finalize")]
	[Authorize(StockActions.Approve)]
	public Task<LibraryStockTransitionDto> FinalizeAsync(Guid id, [FromBody] LibraryFinalizeStockCountCommand input) => counts.FinalizeAsync(id, input);

	[HttpGet("{id:guid}/result")]
	public Task<LibraryStockPublishedResultDto> GetPublishedResultAsync(Guid id,
		LibraryStockReconciliationSource source = LibraryStockReconciliationSource.Expected, int page = 1, int pageSize = 20) =>
		counts.GetPublishedResultAsync(id, source, page, pageSize);
}
