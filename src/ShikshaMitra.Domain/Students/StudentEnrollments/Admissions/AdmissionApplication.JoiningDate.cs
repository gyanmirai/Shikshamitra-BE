using System;
using System.Globalization;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionApplication
{
	private const string JoiningDateProperty = "Academic.JoiningDate";
	public DateTime? GetJoiningDate()
	{
		if (!ExtraProperties.TryGetValue(JoiningDateProperty, out var raw) || raw == null) return null;
		// ABP's ExtraProperties JSON converter can materialize an ISO date string as
		// DateTime after persistence. Preserve the calendar day in both representations.
		if (raw is DateTime storedDate) return DateTime.SpecifyKind(storedDate.Date, DateTimeKind.Utc);
		if (!DateTime.TryParseExact(raw.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
			throw new BusinessException(AdmissionFeeSelectionErrorCodes.InvalidSelection, "The saved actual joining date is invalid.");
		return DateTime.SpecifyKind(date, DateTimeKind.Utc);
	}

	public void ConfirmJoiningDate(DateTime date, bool locked)
	{
		if ((locked || HasFinanceFeePayment) && GetJoiningDate()?.Date != date.Date)
			throw new BusinessException(AdmissionFeeSelectionErrorCodes.Locked, "Fees have already been prepared. Ask Finance to review a joining date correction.");
		this.SetProperty(JoiningDateProperty, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
	}
}
