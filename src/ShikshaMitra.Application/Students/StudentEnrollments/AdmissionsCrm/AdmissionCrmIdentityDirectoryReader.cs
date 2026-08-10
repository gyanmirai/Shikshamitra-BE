using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmIdentityDirectoryReader(
	IRepository<AdmissionCrmPerson, Guid> personRepository,
	IRepository<AdmissionCrmHousehold, Guid> householdRepository,
	IRepository<AdmissionCrmHouseholdMembership, Guid> membershipRepository) : ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionCrmPersonDto>> SearchPersonsAsync(SearchAdmissionCrmPersonsInput input)
	{
		var queryable = await personRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(person => person.TenantId == CurrentTenant.Id));
		var contact = AdmissionCrmIdentityMapper.NormalizeContact(input.Contact);
		var name = input.Name?.Trim();
		var filtered = entities
			.Where(person => string.IsNullOrWhiteSpace(name) ||
				AdmissionCrmIdentityMapper.FullName(person).Contains(name, StringComparison.OrdinalIgnoreCase))
			.Where(person => string.IsNullOrWhiteSpace(contact) || person.NormalizedPhone == contact ||
				person.NormalizedEmail == input.Contact?.Trim().ToUpperInvariant())
			.Where(person => !input.DateOfBirth.HasValue || person.DateOfBirth == input.DateOfBirth.Value.Date)
			.OrderBy(person => person.FirstName).ThenBy(person => person.LastName).ToList();
		return new PagedResultDto<AdmissionCrmPersonDto>(
			filtered.Count,
			filtered.Skip(input.SkipCount).Take(input.MaxResultCount).Select(AdmissionCrmIdentityMapper.Map).ToList());
	}

	public async Task<PagedResultDto<AdmissionCrmHouseholdSummaryDto>> SearchHouseholdsAsync(
		SearchAdmissionCrmHouseholdsInput input)
	{
		var queryable = await householdRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(household =>
			household.TenantId == CurrentTenant.Id && household.IsActive));
		var searchText = input.SearchText?.Trim();
		var filtered = entities.Where(household => string.IsNullOrWhiteSpace(searchText) ||
			household.Code.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
			household.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
			.OrderBy(household => household.DisplayName).ToList();
		var membershipQueryable = await membershipRepository.GetQueryableAsync();
		var memberships = await AsyncExecuter.ToListAsync(membershipQueryable.Where(membership =>
			membership.TenantId == CurrentTenant.Id && membership.IsActive));
		return new PagedResultDto<AdmissionCrmHouseholdSummaryDto>(
			filtered.Count,
			filtered.Skip(input.SkipCount).Take(input.MaxResultCount).Select(household => new AdmissionCrmHouseholdSummaryDto
			{
				Id = household.Id,
				Code = household.Code,
				DisplayName = household.DisplayName,
				PreferredLanguage = household.PreferredLanguage,
				ActiveMemberCount = memberships.Count(membership => membership.HouseholdId == household.Id),
				Addresses = AdmissionCrmIdentityMapper.Deserialize<IReadOnlyList<AdmissionCrmAddressDto>>(household.AddressesJson) ?? []
			}).ToList());
	}
}
