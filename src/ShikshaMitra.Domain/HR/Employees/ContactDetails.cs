using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.HR.Employees;

public class ContactDetails : ValueObject
{
	public string? Name { get; private set; }
	public string? Occupation { get; private set; }
	public string? ContactNo { get; private set; }
	public string? Address { get; private set; }
	public string? City { get; private set; }
	public string? State { get; private set; }
	public string? PinCode { get; private set; }

	public ContactDetails(string? name, string? occupation, string? contactNo, string? address, string? city,
		string? state, string? pinCode)
	{
		Name = name;
		Occupation = occupation;
		ContactNo = contactNo;
		Address = address;
		City = city;
		State = state;
		PinCode = pinCode;
	}

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return Name;
		yield return Occupation;
		yield return ContactNo;
		yield return Address;
		yield return City;
		yield return State;
		yield return PinCode;
	}
}