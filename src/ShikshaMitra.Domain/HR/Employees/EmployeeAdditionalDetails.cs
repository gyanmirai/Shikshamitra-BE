using System;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Value object for employee additional details.
/// Stores supplementary information like BPL card, disability status, language preferences, and memberships.
/// </summary>
public class EmployeeAdditionalDetails
{
	/// <summary>
	/// Below Poverty Line card number
	/// </summary>
	public string? BplCardNo { get; private set; }

	/// <summary>
	/// Indicates if person has disability
	/// Allowed values: "Yes", "No", null
	/// </summary>
	public string? PersonWithDisability { get; private set; }

	/// <summary>
	/// Second language known by employee
	/// </summary>
	public string? SecondLanguage { get; private set; }

	/// <summary>
	/// Library membership status
	/// </summary>
	public bool LibraryMember { get; private set; }

	/// <summary>
	/// Transport facility membership status
	/// </summary>
	public bool TransportMember { get; private set; }

	// For ORM
	private EmployeeAdditionalDetails()
	{
	}

	public EmployeeAdditionalDetails(
		string? bplCardNo,
		string? personWithDisability,
		string? secondLanguage,
		bool libraryMember = false,
		bool transportMember = false)
	{
		// Validate PersonWithDisability values
		if (personWithDisability != null &&
			!string.Equals(personWithDisability, "Yes", StringComparison.OrdinalIgnoreCase) &&
			!string.Equals(personWithDisability, "No", StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException(
				"PersonWithDisability must be either 'Yes', 'No', or null",
				nameof(personWithDisability));
		}

		BplCardNo = bplCardNo?.Trim();
		PersonWithDisability = personWithDisability?.Trim();
		SecondLanguage = secondLanguage?.Trim();
		LibraryMember = libraryMember;
		TransportMember = transportMember;
	}

	/// <summary>
	/// Creates a default/empty additional details instance
	/// </summary>
	public static EmployeeAdditionalDetails CreateDefault()
	{
		return new EmployeeAdditionalDetails(
			bplCardNo: null,
			personWithDisability: null,
			secondLanguage: null,
			libraryMember: false,
			transportMember: false);
	}

	/// <summary>
	/// Updates the additional details
	/// </summary>
	public EmployeeAdditionalDetails Update(
		string? bplCardNo,
		string? personWithDisability,
		string? secondLanguage,
		bool libraryMember,
		bool transportMember)
	{
		return new EmployeeAdditionalDetails(
			bplCardNo,
			personWithDisability,
			secondLanguage,
			libraryMember,
			transportMember);
	}
}
