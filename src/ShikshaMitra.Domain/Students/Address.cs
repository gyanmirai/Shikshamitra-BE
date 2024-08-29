using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Students;

public class Address : ValueObject
{
	public string Religion { get; private set; }
	public string FamilyCaste { get; private set; }
	public string CasteCategory { get; private set; }
	public string MotherTongue { get; private set; }
	public string Nationality { get; private set; }
	public string BirthPlace { get; private set; }
	public string LocalAddress { get; private set; }
	public string PermanentAddress { get; private set; }
	public string EmergencyAddress { get; private set; }

	// Constructor for Value Object
	internal Address(
		string religion,
		string familyCaste,
		string casteCategory,
		string motherTongue,
		string nationality,
		string birthPlace,
		string localAddress,
		string permanentAddress,
		string emergencyAddress)
	{
		Religion = religion;
		FamilyCaste = familyCaste;
		CasteCategory = casteCategory;
		MotherTongue = motherTongue;
		Nationality = nationality;
		BirthPlace = birthPlace;
		LocalAddress = localAddress;
		PermanentAddress = permanentAddress;
		EmergencyAddress = emergencyAddress;
	}

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return Religion;
		yield return FamilyCaste;
		yield return CasteCategory;
		yield return MotherTongue;
		yield return Nationality;
		yield return BirthPlace;
		yield return LocalAddress;
		yield return PermanentAddress;
		yield return EmergencyAddress;
	}
}