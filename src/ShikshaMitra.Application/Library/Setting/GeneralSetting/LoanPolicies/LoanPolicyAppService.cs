using AutoMapper.Internal.Mappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LoanPolicies;
public class LoanPolicyAppService(IRepository<LoanPolicy, Guid> _loanPolicyRepository,IGuidGenerator guidGenerator) : ApplicationService, ILoanPolicyAppService
{
	public async Task<LoanPolicyDto> CreateAsync(CreateUpdateLoanPolicyDto createLoanPolicyDto)
	{
		try
		{
			var loanPolicyEntity = new LoanPolicy(guidGenerator.Create(),
										 createLoanPolicyDto.RoleId,
										 createLoanPolicyDto.LoanDurationInDays,
										 createLoanPolicyDto.MaxBooksAllowed,
										 createLoanPolicyDto.LateReturnFinePerDay,
										 CurrentTenant.Id);

			await _loanPolicyRepository.InsertAsync(loanPolicyEntity, autoSave: true);
			return ObjectMapper.Map<LoanPolicy, LoanPolicyDto>(loanPolicyEntity);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while creating the loan policy.", ex);
		}
	}

	public async Task<LoanPolicyDto> UpdateAsync(Guid id, CreateUpdateLoanPolicyDto updateLoanPolicyDto)
	{
		try
		{
			var loanPolicyEntity = await _loanPolicyRepository.FindAsync(id);
			if (loanPolicyEntity == null)
			{
				throw new EntityNotFoundException(typeof(LoanPolicy), id);
			}

			ObjectMapper.Map(updateLoanPolicyDto, loanPolicyEntity);

			await _loanPolicyRepository.UpdateAsync(loanPolicyEntity, autoSave: true);

			return ObjectMapper.Map<LoanPolicy, LoanPolicyDto>(loanPolicyEntity);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while updating the loan policy.", ex);
		}
	}

	public async Task DeleteAsync(Guid id)
	{
		try
		{
			var loanPolicyEntity = await _loanPolicyRepository.FindAsync(id);
			if (loanPolicyEntity == null)
			{
				throw new EntityNotFoundException(typeof(LoanPolicy), id);
			}

			await _loanPolicyRepository.DeleteAsync(loanPolicyEntity);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while deleting the loan policy.", ex);
		}
	}

	 public async Task<LoanPolicyDto> GetAsync(Guid id)
	  {
		try
		{
			var loanPolicyEntity = await _loanPolicyRepository.GetAsync(id);


			return ObjectMapper.Map<LoanPolicy, LoanPolicyDto>(loanPolicyEntity);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while retrieving the loan policy.", ex);
		}
	  }
     public async Task<PagedResultDto<LoanPolicyDto>> GetListAsync(PagedAndSortedResultRequestDto loanPolicyDto)
      {
        try
        {
            var tenantId = CurrentTenant.Id;

            var queryable = await _loanPolicyRepository.GetQueryableAsync();

            if (tenantId.HasValue)
            {
                queryable = queryable.Where(x => x.TenantId == tenantId.Value);
            }

            var totalCount = await AsyncExecuter.CountAsync(queryable);

            var sortedQuery = queryable
                .OrderBy(x => loanPolicyDto.Sorting ?? nameof(LoanPolicy.RoleId)) /
                .Skip(loanPolicyDto.SkipCount)
                .Take(loanPolicyDto.MaxResultCount);

            var items = await AsyncExecuter.ToListAsync(sortedQuery);

            var dtoList = ObjectMapper.Map<List<LoanPolicy>, List<LoanPolicyDto>>(items);

            return new PagedResultDto<LoanPolicyDto>(totalCount, dtoList);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while retrieving loan policies.", ex);
        }
    }
}