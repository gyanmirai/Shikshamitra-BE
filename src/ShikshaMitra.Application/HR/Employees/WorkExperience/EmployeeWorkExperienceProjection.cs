using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Employees.WorkExperienceContracts;

public static class EmployeeWorkExperienceProjection
{
	public static WorkExperienceContract ToContract(WorkExperience experience)
	{
		var (countryName, countryIsoCode, stateName, stateIsoCode, city) = ParseAddress(experience.OrganizationAddress);

		return new WorkExperienceContract
		{
			Id = null,
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

	public static WorkExperience ToEntity(WorkExperienceContract contract)
	{
		var organizationAddress = BuildOrganizationAddress(contract);

		return new WorkExperience(
			organizationName: contract.MediumStageName,
			organizationAddress: organizationAddress,
			fromDate: contract.StartDate.HasValue ? DateOnly.FromDateTime(contract.StartDate.Value) : null,
			toDate: contract.EndDate.HasValue ? DateOnly.FromDateTime(contract.EndDate.Value) : null,
			department: contract.Department,
			designation: contract.Designation,
			responsibilities: null,
			references: null);
	}

	private static string? BuildOrganizationAddress(WorkExperienceContract contract)
	{
		var addressParts = new List<string>();
		if (!string.IsNullOrEmpty(contract.City))
		{
			addressParts.Add(contract.City);
		}

		if (contract.State != null && !string.IsNullOrEmpty(contract.State.Name))
		{
			addressParts.Add($"{contract.State.Name} ({contract.State.IsoCode})");
		}

		if (contract.Country != null && !string.IsNullOrEmpty(contract.Country.Name))
		{
			addressParts.Add($"{contract.Country.Name} ({contract.Country.IsoCode})");
		}

		return addressParts.Count > 0 ? string.Join(", ", addressParts) : null;
	}

	private static (string? countryName, string? countryIsoCode, string? stateName, string? stateIsoCode, string? city)
		ParseAddress(string? address)
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

		foreach (var part in parts)
		{
			var parsed = ParseAddressPart(part);
			if (parsed.isoCode != null)
			{
				if (countryName == null)
				{
					countryName = parsed.name;
					countryIsoCode = parsed.isoCode;
				}
				else if (stateName == null)
				{
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
