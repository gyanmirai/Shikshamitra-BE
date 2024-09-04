using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Values;
using Volo.Abp;

namespace ShikshaMitra.Students;

public class AdmissionDetails : ValueObject
{
	public DateOnly? AdmissionDate { get; private set; }
	public string? AdmissionStandard { get; private set; }
	public string? AdmissionFormNumber { get; private set; }
	public string? AdmissionQuota { get; private set; }
	public string? PreviousSchool { get; private set; }
	public DateOnly? PreviousSchoolLeftDate { get; private set; }
	public DateOnly? PreviousLcDate { get; private set; }
	public string? PreviousLcNumber { get; private set; }

	// Constructor for Value Object
	internal AdmissionDetails(
		DateOnly? admissionDate,
		string admissionStandard,
		string admissionFormNumber,
		string admissionQuota,
		string previousSchool,
		DateOnly? previousSchoolLeftDate,
		DateOnly? previousLcDate,
		string previousLcNumber)
	{
		AdmissionDate = admissionDate;
		AdmissionStandard = Check.NotNullOrWhiteSpace(admissionStandard, nameof(admissionStandard));
		AdmissionFormNumber = Check.NotNullOrWhiteSpace(admissionFormNumber, nameof(admissionFormNumber));
		AdmissionQuota = admissionQuota;
		PreviousSchool = previousSchool;
		PreviousSchoolLeftDate = previousSchoolLeftDate;
		PreviousLcDate = previousLcDate;
		PreviousLcNumber = previousLcNumber;
	}

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return AdmissionDate;
		yield return AdmissionStandard;
		yield return AdmissionFormNumber;
		yield return AdmissionQuota;
		yield return PreviousSchool;
		yield return PreviousSchoolLeftDate;
		yield return PreviousLcDate;
		yield return PreviousLcNumber;
	}
}