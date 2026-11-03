using ShikshaMitra.Common;
using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Students.ValueObjects;

/// <summary>
/// Value object representing address details
/// </summary>
public class AddressDetails : ValueObject
{
	public string? Address1 { get; private set; }
	public string? Address2 { get; private set; }
	public string? Address3 { get; private set; }
	public string? City { get; private set; }
	public State? State { get; private set; }
	public Country? Country { get; private set; }
	public string? Pincode { get; private set; }

	private AddressDetails()
	{
		/* For ORM/Serialization */
	}

	public AddressDetails(
		string? address1,
		string? address2,
		string? address3,
		string? city,
		State? state,
		Country? country,
		string? pincode)
	{
		Address1 = address1;
		Address2 = address2;
		Address3 = address3;
		State = state;
		City = city;
		Pincode = pincode;
		Country = country;
	}

	// ABP equality atoms intentionally include null for absent optional values.
	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return Address1!;
		yield return Address2!;
		yield return Address3!;
		yield return State!;
		yield return City!;
		yield return Pincode!;
		yield return Country!;
	}
}
