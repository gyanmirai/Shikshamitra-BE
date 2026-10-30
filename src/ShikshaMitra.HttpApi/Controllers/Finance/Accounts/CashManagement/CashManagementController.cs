using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using ShikshaMitra.Finance.Accounts.CashManagement;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.CashManagement;

[Authorize, Area("app"), RemoteService(Name = "Default"), Route("api/finance/cash-management")]
public class CashManagementController(ICashManagementAppService service) : AbpController
{
	[HttpGet] public Task<CashPage> GetListAsync([FromQuery] CashQuery input) => service.GetListAsync(input);
	[HttpPost("bulk-deposit"), RequestSizeLimit(6 * 1024 * 1024)]
	public async Task<CashBulkDepositResult> BulkDepositAsync([FromForm] CashBulkDepositForm input)
	{
		byte[]? content = null;
		if (input.File != null)
		{
			if (input.File.Length is <= 0 or > 5 * 1024 * 1024)
				throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
			using var stream = new MemoryStream();
			await input.File.CopyToAsync(stream);
			content = stream.ToArray();
		}
		return await service.BulkDepositAsync(new CashBulkDeposit { AcademicYearId = input.AcademicYearId,
			BankAccountId = input.BankAccountId, ExpectedAmount = input.ExpectedAmount, IdempotencyKey = input.IdempotencyKey,
			Notes = input.Notes, FileName = input.File?.FileName, FileContent = content });
	}
	[HttpGet("options")] public Task<CashOptions> GetOptionsAsync([FromQuery] Guid academicYearId, [FromQuery] string operation) => service.GetOptionsAsync(academicYearId, operation);
	[HttpGet("{id:guid}")] public Task<CashDetail> GetAsync(Guid id, [FromQuery] Guid academicYearId) => service.GetAsync(id, academicYearId);
	[HttpGet("{id:guid}/supporting-document")]
	public async Task<FileContentResult> GetDocumentAsync(Guid id, [FromQuery] Guid academicYearId)
	{
		var document = await service.GetDocumentAsync(id, academicYearId);
		return File(document.Content, document.ContentType, document.FileName);
	}
	[HttpPost("{id:guid}/approve")] public Task<CashDetail> ApproveAsync(Guid id, [FromBody] CashApproval input) => service.ApproveAsync(id, input);
	[HttpPost("{id:guid}/deposits")] public Task<CashDetail> DepositAsync(Guid id, [FromBody] CashDepositInput input) => service.DepositAsync(id, input);
	[HttpPost("{id:guid}/deposits/{depositId:guid}/confirm")] public Task<CashDetail> ConfirmAsync(Guid id, Guid depositId, [FromBody] CashCreditInput input) => service.ConfirmAsync(id, depositId, input);
}

public class CashBulkDepositForm
{
	public Guid AcademicYearId { get; set; }
	public Guid BankAccountId { get; set; }
	public decimal ExpectedAmount { get; set; }
	public Guid IdempotencyKey { get; set; }
	public string? Notes { get; set; }
	[DisableAuditing] public IFormFile? File { get; set; }
}
