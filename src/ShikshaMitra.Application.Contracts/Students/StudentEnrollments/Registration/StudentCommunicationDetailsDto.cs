using ShikshaMitra.Common;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentCommunicationDetailsDto
{
	public string? PrimaryContactNo { get; set; }
	public string? ParentContactNo { get; set; }
	public string? ParentEmail { get; set; }
	public string? StudentContactNo { get; set; }
	public string? StudentEmail { get; set; }
	public string? WhatsAppNo { get; set; }
	public string? LocalAddressLine1 { get; set; }
	public string? LocalAddressLine2 { get; set; }
	public string? LocalAddressLine3 { get; set; }
	public string? LocalCountryIsoCode { get; set; }
	public string? LocalCountryName { get; set; }
	public string? LocalStateIsoCode { get; set; }
	public string? LocalStateName { get; set; }
	public string? LocalCity { get; set; }
	public string? LocalPinCode { get; set; }
	public string? PermanentAddressLine1 { get; set; }
	public string? PermanentAddressLine2 { get; set; }
	public string? PermanentAddressLine3 { get; set; }
	public string? PermanentCountryIsoCode { get; set; }
	public string? PermanentCountryName { get; set; }
	public string? PermanentStateIsoCode { get; set; }
	public string? PermanentStateName { get; set; }
	public string? PermanentCity { get; set; }
	public string? PermanentPinCode { get; set; }
	public string? EmergencyAddressLine1 { get; set; }
	public string? EmergencyAddressLine2 { get; set; }
	public string? EmergencyAddressLine3 { get; set; }
	public string? EmergencyCountry { get; set; }
	public string? EmergencyState { get; set; }
	public string? EmergencyCity { get; set; }
	public string? EmergencyPinCode { get; set; }
	public string? Remarks { get; set; }
}