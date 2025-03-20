using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentCommunicationDetails(
	string? primaryContactNo,
	string? parentContactNo,
	string? parentEmail,
	string? studentContactNo,
	string? studentEmail,
	string? whatsAppNo,
	string? localAddressLine1,
	string? localAddressLine2,
	string? localAddressLine3,
	string? localCountryIsoCode,
	string? localCountryName,
	string? localStateIsoCode,
	string? localStateName,
	string? localCity,
	string? localPinCode,
	string? permanentAddressLine1,
	string? permanentAddressLine2,
	string? permanentAddressLine3,
	string? permanentCountryIsoCode,
	string? permanentCountryName,
	string? permanentStateIsoCode,
	string? permanentStateName,
	string? permanentCity,
	string? permanentPinCode,
	string? emergencyAddressLine1,
	string? emergencyAddressLine2,
	string? emergencyAddressLine3,
	string? emergencyCountry,
	string? emergencyState,
	string? emergencyCity,
	string? emergencyPinCode,
	string? remarks
	) : ValueObject
{
	public string? PrimaryContactNo { get; private set; } = primaryContactNo;
	public string? ParentContactNo { get; private set; } = parentContactNo;
	public string? ParentEmail { get; private set; } = parentEmail;
	public string? StudentContactNo { get; private set; } = studentContactNo;
	public string? StudentEmail { get; private set; } = studentEmail;
	public string? WhatsAppNo { get; private set; } = whatsAppNo;
	public string? LocalAddressLine1 { get; private set; } = localAddressLine1;
	public string? LocalAddressLine2 { get; private set; } = localAddressLine2;
	public string? LocalAddressLine3 { get; private set; } = localAddressLine3;

	public string? LocalCountryIsoCode { get; private set; } = localCountryIsoCode;
	public string? LocalCountryName { get; private set; } = localCountryName;
	public string? LocalStateIsoCode { get; private set; } = localStateIsoCode;
	public string? LocalStateName { get; private set; } = localStateName;
	public string? LocalCity { get; private set; } = localCity;
	public string? LocalPinCode { get; private set; } = localPinCode;
	public string? PermanentAddressLine1 { get; private set; } = permanentAddressLine1;
	public string? PermanentAddressLine2 { get; private set; } = permanentAddressLine2;
	public string? PermanentAddressLine3 { get; private set; } = permanentAddressLine3;
	public string? PermanentCountryIsoCode { get; private set; } = permanentCountryIsoCode;
	public string? PermanentCountryName { get; private set; } = permanentCountryName;
	public string? PermanentStateIsoCode { get; private set; } = permanentStateIsoCode;
	public string? PermanentStateName { get; private set; } = permanentStateName;
	public string? PermanentCity { get; private set; } = permanentCity;
	public string? PermanentPinCode { get; private set; } = permanentPinCode;
	public string? EmergencyAddressLine1 { get; private set; } = emergencyAddressLine1;
	public string? EmergencyAddressLine2 { get; private set; } = emergencyAddressLine2;
	public string? EmergencyAddressLine3 { get; private set; } = emergencyAddressLine3;
	public string? EmergencyCountry { get; private set; } = emergencyCountry;
	public string? EmergencyState { get; private set; } = emergencyState;
	public string? EmergencyCity { get; private set; } = emergencyCity;
	public string? EmergencyPinCode { get; private set; } = emergencyPinCode;
	public string? Remarks { get; private set; } = remarks;

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return PrimaryContactNo;
		yield return ParentContactNo;
		yield return ParentEmail;
		yield return StudentContactNo;
		yield return StudentEmail;
		yield return WhatsAppNo;
		yield return LocalAddressLine1;
		yield return LocalAddressLine2;
		yield return LocalAddressLine3;
		yield return LocalCountryName;
		yield return LocalCountryIsoCode;
		yield return LocalStateName;
		yield return LocalStateIsoCode;
		yield return LocalCity;
		yield return LocalPinCode;
		yield return PermanentAddressLine1;
		yield return PermanentAddressLine2;
		yield return PermanentAddressLine3;
		yield return PermanentCountryName;
		yield return PermanentCountryIsoCode;
		yield return PermanentStateName;
		yield return PermanentStateIsoCode;
		yield return PermanentCity;
		yield return PermanentPinCode;
		yield return EmergencyAddressLine1;
		yield return EmergencyAddressLine2;
		yield return EmergencyAddressLine3;
		yield return EmergencyCountry;
		yield return EmergencyState;
		yield return EmergencyCity;
		yield return EmergencyPinCode;
		yield return Remarks;
	}
}