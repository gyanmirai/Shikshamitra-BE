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
	string? localAddress,
	string? localState,
	string? localCity,
	string? localPinCode,
	string? permanentAddress,
	string? permanentState,
	string? permanentCity,
	string? permanentPinCode,
	string? emergencyAddress,
	string? emergencyState,
	string? emergencyCity,
	string? emergencyPinCode,
	string? remarks
	) : ValueObject
{
	public string? PrimaryContactNo { get; private  set; } = primaryContactNo;
	public string? ParentContactNo { get; private  set; } = parentContactNo;
	public string? ParentEmail { get; private  set; } = parentEmail;
	public string? StudentContactNo { get; private  set; } = studentContactNo;
	public string? StudentEmail { get; private  set; } = studentEmail;
	public string? WhatsAppNo { get; private  set; } = whatsAppNo;
	public string? LocalAddress { get; private  set; } = localAddress;
	public string? LocalState { get; private  set; } = localState;
	public string? LocalCity { get; private  set; } = localCity;
	public string? LocalPinCode { get; private  set; } = localPinCode;
	public string? PermanentAddress { get; private  set; } = permanentAddress;
	public string? PermanentState { get; private  set; } = permanentState;
	public string? PermanentCity { get; private  set; } = permanentCity;
	public string? PermanentPinCode { get; private  set; } = permanentPinCode;
	public string? EmergencyAddress { get; private  set; } = emergencyAddress;
	public string? EmergencyState { get; private  set; } = emergencyState;
	public string? EmergencyCity { get; private  set; } = emergencyCity;
	public string? EmergencyPinCode { get; private  set; } = emergencyPinCode;
	public string? Remarks { get; private  set; } = remarks;

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return PrimaryContactNo;
		yield return ParentContactNo;
		yield return ParentEmail;
		yield return StudentContactNo;
		yield return StudentEmail;
		yield return WhatsAppNo;
		yield return LocalAddress;
		yield return LocalState;
		yield return LocalCity;
		yield return LocalPinCode;
		yield return PermanentAddress;
		yield return PermanentState;
		yield return PermanentCity;
		yield return PermanentPinCode;
		yield return EmergencyAddress;
		yield return EmergencyState;
		yield return EmergencyCity;
		yield return EmergencyPinCode;
		yield return Remarks;
	}
}