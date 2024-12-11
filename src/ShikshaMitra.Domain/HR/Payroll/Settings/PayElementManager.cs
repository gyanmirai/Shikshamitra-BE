using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.Payroll.Settings;

public class PayElementManager(IPayElementRepository repository, IGuidGenerator guidGenerator) : DomainService
{
	/// <summary>
	/// Creates a new Pay Element, ensuring the Code and Name are unique.
	/// </summary>
	public async Task<PayElement> CreateAsync(
		string code,
		string name,
		PayElementType type,
		bool isEmployeeSpecific,
		bool isDependent,
		PayElement.DependentProperties? dependent,
		bool isVisibleInPaySlip)
	{
		// Check for duplicate Code
		var existingCode = await repository.FirstOrDefaultAsync(e => e.Code == code);
		if (existingCode != null)
		{
			throw new BusinessException("PayElement:CodeExists")
				.WithData("Code", code);
		}

		// Check for duplicate Name
		var existingName = await repository.FirstOrDefaultAsync(e => e.Name == name);
		if (existingName != null)
		{
			throw new BusinessException("PayElement:NameExists")
				.WithData("Name", name);
		}

		// Create and return a new PayElement
		return new PayElement(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			code,
			name,
			type,
			isEmployeeSpecific,
			isDependent,
			dependent,
			isVisibleInPaySlip);
	}

	public async Task UpdateAsync(
		PayElement payElement,
		string code,
		string name,
		bool isEmployeeSpecific,
		bool isDependent,
		PayElement.DependentProperties? dependent,
		bool isVisibleInPaySlip)
	{
		// Check for duplicate Name (excluding the current entity)
		var existingName = await repository.FirstOrDefaultAsync(
			e => e.Name == name && e.Id != payElement.Id);

		if (existingName != null)
		{
			throw new BusinessException("PayElement:NameExists")
				.WithData("Name", name);
		}

		// Check for duplicate Code (excluding the current entity)
		var existingCode = await repository.FirstOrDefaultAsync(
			e => e.Code == code && e.Id != payElement.Id);

		if (existingCode != null)
		{
			throw new BusinessException("PayElement:CodeExists")
				.WithData("Code", code);
		}


		// Update properties
		payElement.Name = name;
		payElement.Code = code;
		payElement.IsEmployeeSpecific = isEmployeeSpecific;
		payElement.IsDependent = isDependent;
		payElement.Dependent = dependent;
		payElement.IsVisibleInPaySlip = isVisibleInPaySlip;
	}
}