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

	public ContactDetails(string? name, string? occupation, string? contactNo, string? address)
	{
		Name = Check.NotNullOrWhiteSpace(name, nameof(name));
		Occupation = occupation;
		ContactNo = contactNo;
		Address = address;
	}

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return Name;
		yield return Occupation;
		yield return ContactNo;
		yield return Address;
	}
}