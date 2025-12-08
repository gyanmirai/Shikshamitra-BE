using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Students.ValueObjects;

/// <summary>
/// Value object representing address details
/// </summary>
public class AddressDetails : ValueObject
{
	public string BuildingNo { get; private set; }
	public string? AreaLocalityRoadName { get; private set; }
	public string? Landmark { get; private set; }
	public string? State { get; private set; }
	public string? City { get; private set; }
	public string? Pincode { get; private set; }
	public string? Country { get; private set; }

	private AddressDetails()
	{
		/* For ORM/Serialization */
	}

	public AddressDetails(
		string? buildingNo,
		string? areaLocalityRoadName,
		string? landmark,
		string? state,
		string? city,
		string? pincode,
		string? country)
	{
		BuildingNo = buildingNo;
		AreaLocalityRoadName = areaLocalityRoadName;
		Landmark = landmark;
		State = state;
		City = city;
		Pincode = pincode;
		Country = country;
	}

	protected override IEnumerable<object?> GetAtomicValues()
	{
		yield return BuildingNo;
		yield return AreaLocalityRoadName;
		yield return Landmark;
		yield return State;
		yield return City;
		yield return Pincode;
		yield return Country;
	}
}
