using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.Setting.BankAccount;
using ShikshaMitra.Application.Contracts.Finance.Setting.BankAccount.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Setting;

[RemoteService(false)]
public class BankAccountAppService : ApplicationService, IBankAccountAppService
{
    private readonly IRepository<BankAccount, Guid> _bankAccountRepository;

    public BankAccountAppService(IRepository<BankAccount, Guid> bankAccountRepository)
    {
        _bankAccountRepository = bankAccountRepository;
    }

    public async Task<BankAccountResponse> CreateAsync(Guid academicYearId, CreateBankAccountRequest input)
    {
        var bankAccount = new BankAccount(
            GuidGenerator.Create(),
            academicYearId,
            input.BankName,
            input.BranchName,
            input.BranchAddress,
            input.AccountHolderName,
            input.AccountType,
            input.AccountNumber,
            input.Purpose,
            input.IfscCode,
            input.UpiId
        );

        await _bankAccountRepository.InsertAsync(bankAccount);

        return MapToResponse(bankAccount);
    }

    public async Task<PaginatedBankAccountResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null)
    {
        var query = await _bankAccountRepository.GetQueryableAsync();
        
        query = query.Where(x => x.AcademicYearId == academicYearId);

        if (!search.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.BankName.Contains(search) || 
                                     x.BranchName.Contains(search) || 
                                     x.AccountNumber.Contains(search) ||
                                     x.AccountHolderName.Contains(search));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        
        var items = await AsyncExecuter.ToListAsync(
            query
            .OrderByDescending(x => x.CreationTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
        );

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedBankAccountResponse
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Items = items.Select(MapToResponse).ToList()
        };
    }

    public async Task<BankAccountResponse> GetAsync(Guid id)
    {
        var bankAccount = await _bankAccountRepository.GetAsync(id);
        return MapToResponse(bankAccount);
    }

    public async Task<BankAccountResponse> UpdateAsync(Guid id, UpdateBankAccountRequest input)
    {
        var bankAccount = await _bankAccountRepository.GetAsync(id);

        bankAccount.BankName = input.BankName;
        bankAccount.BranchName = input.BranchName;
        bankAccount.BranchAddress = input.BranchAddress;
        bankAccount.AccountHolderName = input.AccountHolderName;
        bankAccount.AccountType = input.AccountType;
        bankAccount.AccountNumber = input.AccountNumber;
        bankAccount.Purpose = input.Purpose;
        bankAccount.IfscCode = input.IfscCode;
        bankAccount.UpiId = input.UpiId;
        bankAccount.IsActive = input.IsActive;

        await _bankAccountRepository.UpdateAsync(bankAccount);

        return MapToResponse(bankAccount);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _bankAccountRepository.DeleteAsync(id);
    }

    private BankAccountResponse MapToResponse(BankAccount entity)
    {
        return new BankAccountResponse
        {
            Id = entity.Id,
            BankName = entity.BankName,
            BranchName = entity.BranchName,
            BranchAddress = entity.BranchAddress,
            AccountHolderName = entity.AccountHolderName,
            AccountType = entity.AccountType,
            AccountNumber = entity.AccountNumber,
            Purpose = entity.Purpose,
            IfscCode = entity.IfscCode,
            UpiId = entity.UpiId,
            IsActive = entity.IsActive
        };
    }
}
