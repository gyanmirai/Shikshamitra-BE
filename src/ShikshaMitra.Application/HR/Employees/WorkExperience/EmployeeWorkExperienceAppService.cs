using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.WorkExperienceContracts;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees.WorkExperienceContracts;

/// <summary>
/// Application service for managing employee work experience.
/// </summary>
public class EmployeeWorkExperienceAppService(
	IEmployeeRepository employeeRepository,
	EmployeeManager employeeManager)
	: ApplicationService, IEmployeeWorkExperienceAppService
{
	/// <inheritdoc />
	public async Task<List<WorkExperienceContract>> GetAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		return employee.WorkExperiences
			.Select(MapWorkExperienceToContract)
			.ToList();
	}

	/// <inheritdoc />
	public async Task UpsertAsync(Guid employeeId, List<WorkExperienceContract> input)
	{
		// Map DTOs to domain entities
		var workExperiences = input
			.Select(MapContractToWorkExperience)
			.ToList();

		// Update work experiences using EmployeeManager
		var employee = await employeeManager.UpdateExperienceInfoByEmployeeIdAsync(
			employeeId,
			workExperiences);

		// Persist changes
		await employeeRepository.UpdateAsync(employee);
	}

	private static WorkExperienceContract MapWorkExperienceToContract(WorkExperience experience)
	{
		// Parse address components if stored as combined string
		var (countryName, countryIsoCode, stateName, stateIsoCode, city) = ParseAddress(experience.OrganizationAddress);

		return new WorkExperienceContract
		{
			Id = null, // Work experience doesn't have an Id in the domain model
			MediumStageName = experience.OrganizationName,
			Designation = experience.Designation,
			Department = experience.Department,
			City = city,
			Country = !string.IsNullOrEmpty(countryName)
				? new CountryInfo { Name = countryName, IsoCode = countryIsoCode ?? string.Empty }
				: null,
			State = !string.IsNullOrEmpty(stateName)
				? new StateInfo { Name = stateName, IsoCode = stateIsoCode ?? string.Empty }
				: null,
			StartDate = experience.FromDate?.ToDateTime(TimeOnly.MinValue),
			EndDate = experience.ToDate?.ToDateTime(TimeOnly.MinValue)
		};
	}

	private static WorkExperience MapContractToWorkExperience(WorkExperienceContract contract)
	{
		// Combine address components into a single string for storage
		var addressParts = new List<string>();
		if (!string.IsNullOrEmpty(contract.City))
			addressParts.Add(contract.City);
		if (contract.State != null && !string.IsNullOrEmpty(contract.State.Name))
			addressParts.Add($"{contract.State.Name} ({contract.State.IsoCode})");
		if (contract.Country != null && !string.IsNullOrEmpty(contract.Country.Name))
			addressParts.Add($"{contract.Country.Name} ({contract.Country.IsoCode})");

		var organizationAddress = addressParts.Count > 0 ? string.Join(", ", addressParts) : null;

		return new WorkExperience(
			organizationName: contract.MediumStageName,
			organizationAddress: organizationAddress,
			fromDate: contract.StartDate.HasValue ? DateOnly.FromDateTime(contract.StartDate.Value) : null,
			toDate: contract.EndDate.HasValue ? DateOnly.FromDateTime(contract.EndDate.Value) : null,
			department: contract.Department,
			designation: contract.Designation,
			responsibilities: null, // Not in contract
			references: null // Not in contract
		);
	}

	/// <summary>
	/// Parses address string into components.
	/// Expected format: "City, StateName (StateIsoCode), CountryName (CountryIsoCode)"
	/// </summary>
	private static (string? countryName, string? countryIsoCode, string? stateName, string? stateIsoCode, string? city) ParseAddress(string? address)
	{
		if (string.IsNullOrEmpty(address))
		{
			return (null, null, null, null, null);
		}

		var parts = address.Split(',').Select(p => p.Trim()).ToArray();

		string? city = null;
		string? stateName = null;
		string? stateIsoCode = null;
		string? countryName = null;
		string? countryIsoCode = null;

		// Try to parse the parts
		foreach (var part in parts)
		{
			var parsed = ParseAddressPart(part);
			if (parsed.isoCode != null)
			{
				// This is a state or country (has ISO code)
				if (countryName == null)
				{
					countryName = parsed.name;
					countryIsoCode = parsed.isoCode;
				}
				else if (stateName == null)
				{
					// Shift country to the end and set this as state
					stateName = countryName;
					stateIsoCode = countryIsoCode;
					countryName = parsed.name;
					countryIsoCode = parsed.isoCode;
				}
			}
			else if (city == null)
			{
				city = parsed.name;
			}
		}

		return (countryName, countryIsoCode, stateName, stateIsoCode, city);
	}

	/// <summary>
	/// Parses a single address part like "StateName (StateIsoCode)" or just "City"
	/// </summary>
	private static (string? name, string? isoCode) ParseAddressPart(string part)
	{
		var openParen = part.IndexOf('(');
		var closeParen = part.IndexOf(')');

		if (openParen > 0 && closeParen > openParen)
		{
			var name = part[..openParen].Trim();
			var isoCode = part[(openParen + 1)..closeParen].Trim();
			return (name, isoCode);
		}

		return (part.Trim(), null);
	}
}
