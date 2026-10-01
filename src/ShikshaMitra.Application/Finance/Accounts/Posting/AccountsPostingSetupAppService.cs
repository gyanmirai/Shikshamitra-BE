using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Fees.Collections;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Accounts.Posting;

/// <summary>Configuration only. Activation is withheld until source posting is fully wired.</summary>
[RemoteService(false)]
public class AccountsPostingSetupAppService(
    IRepository<AccountingPeriod, Guid> periodRepository,
    IRepository<AccountsPostingConfiguration, Guid> configurationRepository,
    IRepository<PaymentDestinationRule, Guid> destinationRepository,
    IRepository<AccountHead, Guid> headRepository,
    IRepository<BankAccount, Guid> bankRepository,
    FinanceScopeAccessService scopeAccessService,
    IUnitOfWorkManager unitOfWorkManager) : ApplicationService, IAccountsPostingSetupAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_PostingSetup.View)]
    public async Task<AccountsPostingSetupDto> GetAsync(Guid academicYearId)
    {
        await scopeAccessService.EnsureAcademicContextAccessibleAsync(academicYearId, null, null, null, null);
        var configuration = await configurationRepository.FirstOrDefaultAsync(x =>
            x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
        var periods = (await periodRepository.GetListAsync(x => x.TenantId == CurrentTenant.Id))
            .OrderBy(x => x.StartsOn).ToList();
        var destinations = (await destinationRepository.GetListAsync(x =>
            x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId))
            .OrderBy(x => x.Direction).ThenBy(x => x.Method).ThenBy(x => x.EffectiveFrom).ToList();
        var issues = new List<string>();
        if (configuration?.FirstPostingDate is not { } firstDate)
            issues.Add("Set the first live posting date.");
        else if (!periods.Any(x => x.IsOpen && x.Contains(firstDate)))
            issues.Add("Create an open financial period containing the first posting date.");
        if (configuration?.CashAccountHeadId is not { } cashId)
            issues.Add("Choose the school's cash drawer account.");
        else
        {
            var cashHead = await headRepository.FindAsync(cashId);
            if (cashHead == null || cashHead.TenantId != CurrentTenant.Id ||
                cashHead.AcademicYearId != academicYearId || !cashHead.IsActive ||
                cashHead.HeadType != AccountHeadType.Asset)
                issues.Add("The cash drawer must be an active Asset account in this academic year.");
            else if (await bankRepository.AnyAsync(x => x.AccountHeadId == cashId))
                issues.Add("Choose a dedicated cash drawer Asset account, not a school bank head.");
        }
        foreach (var method in Enum.GetValues<PaymentMethodEnum>())
        {
            if (method == PaymentMethodEnum.Cash) continue;
            if (configuration?.FirstPostingDate is { } from && !destinations.Any(x =>
                x.Method == method && x.Direction == PaymentDirection.Incoming && x.AppliesOn(from)))
                issues.Add($"Set an incoming {method} destination for the first posting date.");
        }
        // This gate is intentional: a configured account alone cannot create a safe journal.
        issues.Add("Source allocation posting and accountant-approved refund treatment are not active yet.");
        return new AccountsPostingSetupDto(academicYearId, configuration?.IsEnabled ?? false,
            configuration?.FirstPostingDate, configuration?.CashAccountHeadId,
            periods.Select(x => new AccountingPeriodDto(x.Id, x.Code, x.StartsOn, x.EndsOn, x.IsOpen)).ToList(),
            destinations.Select(x => new PaymentDestinationDto(x.Id, x.Method, x.Direction,
                x.AssetAccountHeadId, x.BankAccountId, x.EffectiveFrom, x.EffectiveTo, x.IsActive)).ToList(),
            issues);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_PostingSetup.Edit)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<AccountsPostingSetupDto> ConfigureAsync(Guid academicYearId, ConfigureAccountsPostingDto input)
    {
        await scopeAccessService.EnsureAcademicContextAccessibleAsync(academicYearId, null, null, null, null);
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        if (input.FirstPostingDate == default)
            throw new UserFriendlyException("Enter the first live posting date.");
        await EnsureAssetHeadAsync(academicYearId, input.CashAccountHeadId);
        if (await bankRepository.AnyAsync(x => x.AccountHeadId == input.CashAccountHeadId))
            throw new UserFriendlyException("Choose a dedicated cash drawer Asset account, not a bank head.");
        var configuration = await configurationRepository.FirstOrDefaultAsync(x =>
            x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
        if (configuration == null)
        {
            configuration = new AccountsPostingConfiguration(GuidGenerator.Create(), CurrentTenant.Id, academicYearId);
            configuration.Configure(input.FirstPostingDate, input.CashAccountHeadId);
            await configurationRepository.InsertAsync(configuration, autoSave: true);
        }
        else
        {
            configuration.Configure(input.FirstPostingDate, input.CashAccountHeadId);
            await configurationRepository.UpdateAsync(configuration, autoSave: true);
        }
        await uow.CompleteAsync();
        uow.Dispose();
        return await GetAsync(academicYearId);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_PostingSetup.Edit)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<AccountsPostingSetupDto> AddPeriodAsync(Guid academicYearId, CreateAccountingPeriodDto input)
    {
        await scopeAccessService.EnsureAcademicContextAccessibleAsync(academicYearId, null, null, null, null);
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        if (string.IsNullOrWhiteSpace(input.Code) || input.StartsOn == default ||
            input.EndsOn == default || input.EndsOn < input.StartsOn)
            throw new UserFriendlyException("Enter valid financial period dates.");
        var periods = await periodRepository.GetListAsync(x => x.TenantId == CurrentTenant.Id);
        if (periods.Any(x => string.Equals(x.Code, input.Code?.Trim(), StringComparison.OrdinalIgnoreCase) ||
            (input.StartsOn <= x.EndsOn && x.StartsOn <= input.EndsOn)))
            throw new UserFriendlyException("The financial period code or date range already exists.");
        await periodRepository.InsertAsync(new AccountingPeriod(GuidGenerator.Create(), CurrentTenant.Id,
            input.Code, input.StartsOn, input.EndsOn), autoSave: true);
        await uow.CompleteAsync();
        uow.Dispose();
        return await GetAsync(academicYearId);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_PostingSetup.Edit)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<AccountsPostingSetupDto> AddDestinationAsync(Guid academicYearId, CreatePaymentDestinationDto input)
    {
        await scopeAccessService.EnsureAcademicContextAccessibleAsync(academicYearId, null, null, null, null);
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        if (input.EffectiveFrom == default || input.Method == PaymentMethodEnum.Cash ||
            input.EffectiveTo < input.EffectiveFrom)
            throw new UserFriendlyException("Choose a non-cash method and valid effective dates.");
        await EnsureAssetHeadAsync(academicYearId, input.AssetAccountHeadId);
        var configurationForMethod = await configurationRepository.FirstOrDefaultAsync(x =>
            x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
        if (configurationForMethod?.CashAccountHeadId == input.AssetAccountHeadId)
            throw new UserFriendlyException("Non-cash methods need a school bank or clearing Asset account.");
        var mappedBank = await bankRepository.FirstOrDefaultAsync(x =>
            x.AccountHeadId == input.AssetAccountHeadId);
        if (mappedBank != null && input.BankAccountId != mappedBank.Id)
            throw new UserFriendlyException("Select the school bank linked to this Asset account.");
        if (input.BankAccountId is { } bankId)
        {
            var bank = await bankRepository.FindAsync(bankId);
            if (bank == null || bank.TenantId != CurrentTenant.Id ||
                bank.AcademicYearId != academicYearId || !bank.IsActive ||
                bank.AccountHeadId != input.AssetAccountHeadId ||
                (bank.EffectiveFrom.HasValue &&
                 DateOnly.FromDateTime(bank.EffectiveFrom.Value.Date) > input.EffectiveFrom) ||
                (bank.EffectiveTo.HasValue &&
                 DateOnly.FromDateTime(bank.EffectiveTo.Value.Date) < (input.EffectiveTo ?? input.EffectiveFrom)))
                throw new UserFriendlyException("The school bank must be active and mapped to the selected Asset account.");
        }
        var existing = await destinationRepository.GetListAsync(x => x.TenantId == CurrentTenant.Id &&
            x.AcademicYearId == academicYearId && x.Method == input.Method &&
            x.Direction == input.Direction);
        if (existing.Any(x => input.EffectiveFrom <= (x.EffectiveTo ?? DateOnly.MaxValue) &&
            x.EffectiveFrom <= (input.EffectiveTo ?? DateOnly.MaxValue)))
            throw new UserFriendlyException("This payment destination overlaps an existing effective rule.");
        await destinationRepository.InsertAsync(new PaymentDestinationRule(GuidGenerator.Create(),
            CurrentTenant.Id, academicYearId, input.Method, input.Direction,
            input.AssetAccountHeadId, input.BankAccountId, input.EffectiveFrom, input.EffectiveTo), autoSave: true);
        var configuration = await configurationRepository.FirstOrDefaultAsync(x =>
            x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
        if (configuration?.IsEnabled == true)
        {
            configuration.Disable();
            await configurationRepository.UpdateAsync(configuration, autoSave: true);
        }
        await uow.CompleteAsync();
        uow.Dispose();
        return await GetAsync(academicYearId);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_PostingSetup.Edit)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<AccountsPostingSetupDto> EndDestinationAsync(Guid academicYearId, Guid id,
        EndPaymentDestinationDto input)
    {
        await scopeAccessService.EnsureAcademicContextAccessibleAsync(academicYearId, null, null, null, null);
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        var rule = await destinationRepository.FindAsync(id);
        if (rule == null || rule.TenantId != CurrentTenant.Id ||
            rule.AcademicYearId != academicYearId || !rule.IsActive ||
            input.LastEffectiveDate < rule.EffectiveFrom)
            throw new UserFriendlyException("Select an active destination and a valid last effective date.");
        rule.EndOn(input.LastEffectiveDate);
        await destinationRepository.UpdateAsync(rule, autoSave: true);
        var configuration = await configurationRepository.FirstOrDefaultAsync(x =>
            x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
        if (configuration?.IsEnabled == true)
        {
            configuration.Disable();
            await configurationRepository.UpdateAsync(configuration, autoSave: true);
        }
        await uow.CompleteAsync();
        uow.Dispose();
        return await GetAsync(academicYearId);
    }

    private async Task EnsureAssetHeadAsync(Guid academicYearId, Guid id)
    {
        var head = await headRepository.FindAsync(id);
        if (head == null || head.TenantId != CurrentTenant.Id ||
            head.AcademicYearId != academicYearId || !head.IsActive || head.HeadType != AccountHeadType.Asset)
            throw new UserFriendlyException("Choose an active Asset account in the selected academic year.");
    }
}
