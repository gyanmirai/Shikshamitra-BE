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
	public async Task<LoanPolicyDto> CreateAsync(CreateUpdateLoanPolicyDto input)
	{
		try
		{
			var loanPolicyEntity = new LoanPolicy(guidGenerator.Create(),
										 input.RoleId,
										 input.LoanDurationInDays,
										 input.MaxBooksAllowed,
										 input.LateReturnFinePerDay,
										 CurrentTenant.Id);

			await _loanPolicyRepository.InsertAsync(loanPolicyEntity, autoSave: true);
			return ObjectMapper.Map<LoanPolicy, LoanPolicyDto>(loanPolicyEntity);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while creating the loan policy.", ex);
		}
	}

	public async Task<LoanPolicyDto> UpdateAsync(Guid id, CreateUpdateLoanPolicyDto input)
	{
		try
		{
			var entity = await _loanPolicyRepository.FindAsync(id);
			if (entity == null)
			{
				throw new EntityNotFoundException(typeof(LoanPolicy), id);
			}

			ObjectMapper.Map(input, entity);

			await _loanPolicyRepository.UpdateAsync(entity, autoSave: true);

			return ObjectMapper.Map<LoanPolicy, LoanPolicyDto>(entity);
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
			var entity = await _loanPolicyRepository.FindAsync(id);
			if (entity == null)
			{
				throw new EntityNotFoundException(typeof(LoanPolicy), id);
			}

			await _loanPolicyRepository.DeleteAsync(entity);
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
			var entity = await _loanPolicyRepository.GetAsync(id);


			return ObjectMapper.Map<LoanPolicy, LoanPolicyDto>(entity);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while retrieving the loan policy.", ex);
		}
	  }
     public async Task<PagedResultDto<LoanPolicyDto>> GetListAsync(PagedAndSortedResultRequestDto input)
      {
        try
        {
            // Retrieve the tenant ID  
            var tenantId = CurrentTenant.Id;

            // Get the queryable from the repository  
            var queryable = await _loanPolicyRepository.GetQueryableAsync();

            // Filter by TenantId if available  
            if (tenantId.HasValue)
            {
                queryable = queryable.Where(x => x.TenantId == tenantId.Value);
            }

            // Get the total count before pagination  
            var totalCount = await AsyncExecuter.CountAsync(queryable);

            // Apply sorting, skip, and take  
            var sortedQuery = queryable
                .OrderBy(x => input.Sorting ?? nameof(LoanPolicy.RoleId)) // Explicitly specify the type for OrderBy  
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount);

            // Fetch the paged items  
            var items = await AsyncExecuter.ToListAsync(sortedQuery);

            // Map entities to DTOs  
            var dtoList = ObjectMapper.Map<List<LoanPolicy>, List<LoanPolicyDto>>(items);

            // Return paged result  
            return new PagedResultDto<LoanPolicyDto>(totalCount, dtoList);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while retrieving loan policies.", ex);
        }
    }
}