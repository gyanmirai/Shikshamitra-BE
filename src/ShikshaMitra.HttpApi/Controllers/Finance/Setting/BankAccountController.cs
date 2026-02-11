using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Finance.Setting.BankAccount;
using ShikshaMitra.Application.Contracts.Finance.Setting.BankAccount.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Setting;

[Area("Finance")]
[Route("api/finance/bank-accounts")]
public class BankAccountController : AbpController
{
    private readonly IBankAccountAppService _bankAccountAppService;

    public BankAccountController(IBankAccountAppService bankAccountAppService)
    {
        _bankAccountAppService = bankAccountAppService;
    }

    [HttpPost]
    public Task<BankAccountResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateBankAccountRequest input)
    {
        return _bankAccountAppService.CreateAsync(academicYearId, input);
    }

    [HttpGet]
    public Task<PaginatedBankAccountResponse> GetListAsync(
        [FromQuery] Guid academicYearId, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        [FromQuery] string? search = null)
    {
        return _bankAccountAppService.GetListAsync(academicYearId, page, pageSize, search);
    }

    [HttpGet("{bankAccountId}")]
    public Task<BankAccountResponse> GetAsync(Guid bankAccountId)
    {
        return _bankAccountAppService.GetAsync(bankAccountId);
    }

    [HttpPut("{bankAccountId}")]
    public Task<BankAccountResponse> UpdateAsync(Guid bankAccountId, [FromBody] UpdateBankAccountRequest input)
    {
        return _bankAccountAppService.UpdateAsync(bankAccountId, input);
    }

    [HttpDelete("{bankAccountId}")]
    public Task DeleteAsync(Guid bankAccountId)
    {
        return _bankAccountAppService.DeleteAsync(bankAccountId);
    }
}
