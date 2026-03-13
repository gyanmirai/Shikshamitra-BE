using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Import;

/// <summary>
/// Validator for attendance import operations with comprehensive validation rules
/// </summary>
public class AttendanceImportValidator : ITransientDependency
{
	private const int MaxWorkingHoursPerDay = 24;
	private const int MinWorkingHoursForPresent = 4;
	private const double HoursMismatchToleranceMinutes = 15;

	/// <summary>
	/// Validates a single attendance row with comprehensive business rules
	/// </summary>
	public AttendanceRowValidationResult ValidateRow(
		AttendanceImportRow row,
		DateTimeOffset targetDate,
		Dictionary<string, Employee> employeeLookup,
		HashSet<string> processedEmployeeCodes)
	{
		var result = new AttendanceRowValidationResult
		{
			RowNumber = row.RowNumber,
			EmployeeCode = row.EmployeeCode,
			EmployeeName = row.EmployeeName
		};

		// Validation 1: Employee Code - Required and must exist
		if (string.IsNullOrWhiteSpace(row.EmployeeCode))
		{
			result.AddError("EmployeeCode", "Employee Code is required", "Please provide a valid employee code");
			return result; // Cannot continue without employee code
		}

		var employeeCodeUpper = row.EmployeeCode.Trim().ToUpperInvariant();

		if (!employeeLookup.TryGetValue(employeeCodeUpper, out var employee))
		{
			var suggestions = FindSimilarEmployeeCodes(employeeCodeUpper, employeeLookup.Keys);
			var suggestionText = suggestions.Any()
				? $"Similar codes found: {string.Join(", ", suggestions.Take(3))}"
				: "Check if the employee code is correct or if the employee is active";

			result.AddError(
				"EmployeeCode",
				$"Employee with code '{row.EmployeeCode}' not found in the system",
				suggestionText,
				"Valid employee code",
				row.EmployeeCode);
			return result; // Cannot continue without valid employee
		}

		result.Employee = employee;

		// Validation 2: Duplicate employee in same file
		if (processedEmployeeCodes.Contains(employeeCodeUpper))
		{
			result.AddWarning(
				"EmployeeCode",
				$"Duplicate entry for employee '{row.EmployeeCode}' in this file",
				"Only the last occurrence will be imported. Consider removing duplicate rows.",
				"Unique employee per file",
				row.EmployeeCode);
		}

		// Validation 3: Employee Name - Cross validation
		if (!string.IsNullOrWhiteSpace(row.EmployeeName))
		{
			var expectedName = FormatEmployeeName(employee.FirstName, employee.MiddleName, employee.LastName);
			if (!string.Equals(row.EmployeeName.Trim(), expectedName, StringComparison.OrdinalIgnoreCase))
			{
				result.AddWarning(
					"EmployeeName",
					"Employee name does not match the name in the system",
					"Verify that the correct employee code is used. Import will continue using the employee code.",
					expectedName,
					row.EmployeeName);
			}
		}

		// Validation 4: Contact Number - Optional cross validation
		if (!string.IsNullOrWhiteSpace(row.ContactNo) && !string.IsNullOrWhiteSpace(employee.MobileNo))
		{
			var cleanRowContact = CleanPhoneNumber(row.ContactNo);
			var cleanEmployeeContact = CleanPhoneNumber(employee.MobileNo);

			if (!string.Equals(cleanRowContact, cleanEmployeeContact, StringComparison.OrdinalIgnoreCase))
			{
				result.AddWarning(
					"ContactNo",
					"Contact number does not match employee record",
					"Verify employee identity. Import will continue.",
					employee.MobileNo,
					row.ContactNo);
			}
		}

		// Validation 5: Time In - Required and format validation
		var timeInResult = ParseAndValidateDateTime(row.TimeIn, targetDate, "TimeIn");
		if (!timeInResult.IsValid)
		{
			result.AddError("TimeIn", timeInResult.ErrorMessage!, timeInResult.Suggestion!);
			return result; // Cannot continue without valid time in
		}
		result.ParsedTimeIn = timeInResult.ParsedValue;

		// Validation 6: Time Out - Optional but must be valid if provided
		if (!string.IsNullOrWhiteSpace(row.TimeOut))
		{
			var timeOutResult = ParseAndValidateDateTime(row.TimeOut, targetDate, "TimeOut");
			if (!timeOutResult.IsValid)
			{
				result.AddError("TimeOut", timeOutResult.ErrorMessage!, timeOutResult.Suggestion!);
			}
			else
			{
				result.ParsedTimeOut = timeOutResult.ParsedValue;

				// Validation 7: Time Out must be after Time In
				if (result.ParsedTimeOut.HasValue && result.ParsedTimeOut.Value <= result.ParsedTimeIn!.Value)
				{
					result.AddError(
						"TimeOut",
						"Time Out must be after Time In",
						"Ensure Time Out is later than Time In. Check for AM/PM mistakes.",
						$"After {result.ParsedTimeIn.Value:hh:mm tt}",
						row.TimeOut);
				}

				// Validation 8: Working hours cannot exceed 24 hours
				if (result.ParsedTimeOut.HasValue && result.ParsedTimeIn.HasValue)
				{
					var duration = result.ParsedTimeOut.Value - result.ParsedTimeIn.Value;
					if (duration.TotalHours > MaxWorkingHoursPerDay)
					{
						result.AddWarning(
							"TimeOut",
							$"Working hours ({duration.TotalHours:F2}h) exceed {MaxWorkingHoursPerDay} hours",
							"Verify time values are correct. This might indicate overnight work or data entry error.",
							$"≤ {MaxWorkingHoursPerDay} hours",
							$"{duration.TotalHours:F2} hours");
					}
				}
			}
		}

		// Validation 9: Hours Worked - Validate consistency if provided
		if (!string.IsNullOrWhiteSpace(row.HoursWorked))
		{
			var hoursResult = ParseHoursWorked(row.HoursWorked);
			if (!hoursResult.IsValid)
			{
				result.AddWarning(
					"HoursWorked",
					hoursResult.ErrorMessage!,
					"Hours will be calculated from Time In/Out. Format: '8.5' or '8:30'");
			}
			else if (result.ParsedTimeIn.HasValue && result.ParsedTimeOut.HasValue)
			{
				var calculatedHours = (result.ParsedTimeOut.Value - result.ParsedTimeIn.Value).TotalHours;
				var providedHours = hoursResult.ParsedValue!.Value;
				var differenceMinutes = Math.Abs(calculatedHours - providedHours) * 60;

				if (differenceMinutes > HoursMismatchToleranceMinutes)
				{
					result.AddWarning(
						"HoursWorked",
						"Hours worked mismatch between provided value and calculated from times",
						"Using calculated hours based on Time In/Out",
						$"{calculatedHours:F2} hours (calculated)",
						$"{providedHours:F2} hours (provided)");
				}
			}
		}

		// Validation 10: Status - Validate if provided
		if (!string.IsNullOrWhiteSpace(row.Status))
		{
			var statusResult = ValidateStatus(row.Status, result.ParsedTimeIn, result.ParsedTimeOut);
			if (!statusResult.IsValid)
			{
				result.AddWarning("Status", statusResult.ErrorMessage!, statusResult.Suggestion!);
			}
			result.ParsedStatus = statusResult.ParsedStatus;
		}
		else
		{
			// Auto-determine status from times
			result.ParsedStatus = DetermineStatusFromTimes(result.ParsedTimeIn, result.ParsedTimeOut);
		}

		return result;
	}

	/// <summary>
	/// Finds similar employee codes using Levenshtein distance
	/// </summary>
	private List<string> FindSimilarEmployeeCodes(string input, IEnumerable<string> allCodes)
	{
		return allCodes
			.Select(code => new { Code = code, Distance = LevenshteinDistance(input, code) })
			.Where(x => x.Distance <= 3) // Max 3 character differences
			.OrderBy(x => x.Distance)
			.Select(x => x.Code)
			.Take(5)
			.ToList();
	}

	/// <summary>
	/// Calculates Levenshtein distance between two strings
	/// </summary>
	private int LevenshteinDistance(string s, string t)
	{
		if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
		if (string.IsNullOrEmpty(t)) return s.Length;

		int[,] d = new int[s.Length + 1, t.Length + 1];

		for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
		for (int j = 0; j <= t.Length; j++) d[0, j] = j;

		for (int i = 1; i <= s.Length; i++)
		{
			for (int j = 1; j <= t.Length; j++)
			{
				int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
				d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
			}
		}

		return d[s.Length, t.Length];
	}

	/// <summary>
	/// Parses and validates date/time values
	/// </summary>
	private DateTimeParseResult ParseAndValidateDateTime(string value, DateTimeOffset targetDate, string fieldName)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return new DateTimeParseResult
			{
				IsValid = false,
				ErrorMessage = $"{fieldName} is required",
				Suggestion = "Provide a valid time in format: 'HH:MM AM/PM' or '2024-01-15 09:00'"
			};
		}

		// Try multiple date/time formats
		var formats = new[]
		{
			"yyyy-MM-dd HH:mm:ss",
			"yyyy-MM-dd HH:mm",
			"yyyy-MM-dd h:mm:ss tt",
			"yyyy-MM-dd h:mm tt",
			"dd/MM/yyyy HH:mm:ss",
			"dd/MM/yyyy HH:mm",
			"dd/MM/yyyy h:mm:ss tt",
			"dd/MM/yyyy h:mm tt",
			"MM/dd/yyyy HH:mm:ss",
			"MM/dd/yyyy HH:mm",
			"MM/dd/yyyy h:mm:ss tt",
			"MM/dd/yyyy h:mm tt",
			"HH:mm:ss",
			"HH:mm",
			"h:mm:ss tt",
			"h:mm tt"
		};

		DateTimeOffset parsedDateTime = default;
		bool parsed = false;

		foreach (var format in formats)
		{
			if (DateTimeOffset.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDateTime))
			{
				parsed = true;
				break;
			}
		}

		// Fallback to general parsing
		if (!parsed && !DateTimeOffset.TryParse(value, out parsedDateTime))
		{
			return new DateTimeParseResult
			{
				IsValid = false,
				ErrorMessage = $"Invalid {fieldName} format: '{value}'",
				Suggestion = "Use format: 'HH:MM AM/PM' (e.g., '09:00 AM') or '2024-01-15 09:00'"
			};
		}

		// If time-only, combine with target date
		if (parsedDateTime.Year == 1)
		{
			parsedDateTime = new DateTimeOffset(
				targetDate.Year, targetDate.Month, targetDate.Day,
				parsedDateTime.Hour, parsedDateTime.Minute, parsedDateTime.Second,
				targetDate.Offset);
		}

		// Validation: Cannot be future date
		if (parsedDateTime > DateTimeOffset.UtcNow.AddHours(1)) // 1 hour buffer for timezone differences
		{
			return new DateTimeParseResult
			{
				IsValid = false,
				ErrorMessage = $"{fieldName} cannot be a future date/time",
				Suggestion = "Ensure the date and time are in the past or present"
			};
		}

		return new DateTimeParseResult
		{
			IsValid = true,
			ParsedValue = parsedDateTime
		};
	}

	/// <summary>
	/// Parses hours worked value
	/// </summary>
	private HoursParseResult ParseHoursWorked(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return new HoursParseResult { IsValid = true, ParsedValue = null };
		}

		// Try decimal format (8.5)
		if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours))
		{
			if (hours < 0 || hours > MaxWorkingHoursPerDay)
			{
				return new HoursParseResult
				{
					IsValid = false,
					ErrorMessage = $"Hours worked must be between 0 and {MaxWorkingHoursPerDay}"
				};
			}
			return new HoursParseResult { IsValid = true, ParsedValue = hours };
		}

		// Try HH:MM format (8:30)
		var timeMatch = Regex.Match(value, @"^(\d{1,2}):(\d{2})$");
		if (timeMatch.Success)
		{
			var h = int.Parse(timeMatch.Groups[1].Value);
			var m = int.Parse(timeMatch.Groups[2].Value);
			hours = h + (m / 60.0);

			if (hours < 0 || hours > MaxWorkingHoursPerDay)
			{
				return new HoursParseResult
				{
					IsValid = false,
					ErrorMessage = $"Hours worked must be between 0 and {MaxWorkingHoursPerDay}"
				};
			}
			return new HoursParseResult { IsValid = true, ParsedValue = hours };
		}

		return new HoursParseResult
		{
			IsValid = false,
			ErrorMessage = $"Invalid hours format: '{value}'"
		};
	}

	/// <summary>
	/// Validates status field
	/// </summary>
	private StatusValidationResult ValidateStatus(string status, DateTimeOffset? timeIn, DateTimeOffset? timeOut)
	{
		var normalizedStatus = status.Trim().ToLowerInvariant();
		EmployeeAttendanceStatusType? parsedStatus = normalizedStatus switch
		{
			"present" => EmployeeAttendanceStatusType.Present,
			"halfday" or "half-day" or "half day" or "halfdayleave" => EmployeeAttendanceStatusType.HalfDayLeave,
			"absent" or "leave" or "fulldayleave" or "full-day" => EmployeeAttendanceStatusType.FullDayLeave,
			_ => null
		};

		if (parsedStatus == null)
		{
			return new StatusValidationResult
			{
				IsValid = false,
				ErrorMessage = $"Invalid status value: '{status}'",
				Suggestion = "Valid values: Present, HalfDay, Absent, Leave"
			};
		}

		// Validate status consistency with times
		if (parsedStatus == EmployeeAttendanceStatusType.Present && timeIn.HasValue && timeOut.HasValue)
		{
			var hoursWorked = (timeOut.Value - timeIn.Value).TotalHours;
			if (hoursWorked < MinWorkingHoursForPresent)
			{
				return new StatusValidationResult
				{
					IsValid = false,
					ErrorMessage = $"Status 'Present' but only {hoursWorked:F2} hours worked",
					Suggestion = "Consider using 'HalfDay' status for less than 4 hours",
					ParsedStatus = EmployeeAttendanceStatusType.HalfDayLeave
				};
			}
		}

		return new StatusValidationResult
		{
			IsValid = true,
			ParsedStatus = parsedStatus.Value
		};
	}

	/// <summary>
	/// Auto-determines status from times
	/// </summary>
	private EmployeeAttendanceStatusType DetermineStatusFromTimes(DateTimeOffset? timeIn, DateTimeOffset? timeOut)
	{
		if (!timeIn.HasValue)
			return EmployeeAttendanceStatusType.FullDayLeave;

		if (!timeOut.HasValue)
			return EmployeeAttendanceStatusType.Present;

		var hoursWorked = (timeOut.Value - timeIn.Value).TotalHours;

		if (hoursWorked < 0)
			return EmployeeAttendanceStatusType.Present;

		if (hoursWorked < MinWorkingHoursForPresent)
			return EmployeeAttendanceStatusType.HalfDayLeave;

		return EmployeeAttendanceStatusType.Present;
	}

	private static string FormatEmployeeName(string? firstName, string? middleName, string? lastName)
	{
		var parts = new[] { firstName, middleName, lastName }
			.Where(s => !string.IsNullOrWhiteSpace(s));
		return string.Join(" ", parts);
	}

	private static string CleanPhoneNumber(string phone)
	{
		return Regex.Replace(phone, @"[^\d]", "");
	}
}

#region Supporting Classes

public class AttendanceImportRow
{
	public int RowNumber { get; set; }
	public string? EmployeeName { get; set; }
	public string? HoursWorked { get; set; }
	public string? TimeIn { get; set; }
	public string? TimeOut { get; set; }
	public string? Status { get; set; }
	public string? EmployeeCode { get; set; }
	public string? ContactNo { get; set; }
}

public class AttendanceRowValidationResult
{
	public int RowNumber { get; set; }
	public string? EmployeeCode { get; set; }
	public string? EmployeeName { get; set; }
	public Employee? Employee { get; set; }
	public DateTimeOffset? ParsedTimeIn { get; set; }
	public DateTimeOffset? ParsedTimeOut { get; set; }
	public EmployeeAttendanceStatusType? ParsedStatus { get; set; }
	public List<ValidationMessage> Messages { get; set; } = new();

	public bool HasErrors => Messages.Any(m => m.Severity == ValidationSeverity.Error);
	public bool IsSuccess => !HasErrors;

	public void AddError(string fieldName, string message, string? suggestion = null, string? expectedValue = null, string? actualValue = null)
	{
		Messages.Add(new ValidationMessage
		{
			Severity = ValidationSeverity.Error,
			FieldName = fieldName,
			Message = message,
			Suggestion = suggestion,
			ExpectedValue = expectedValue,
			ActualValue = actualValue
		});
	}

	public void AddWarning(string fieldName, string message, string? suggestion = null, string? expectedValue = null, string? actualValue = null)
	{
		Messages.Add(new ValidationMessage
		{
			Severity = ValidationSeverity.Warning,
			FieldName = fieldName,
			Message = message,
			Suggestion = suggestion,
			ExpectedValue = expectedValue,
			ActualValue = actualValue
		});
	}

	public void AddInfo(string fieldName, string message, string? suggestion = null)
	{
		Messages.Add(new ValidationMessage
		{
			Severity = ValidationSeverity.Info,
			FieldName = fieldName,
			Message = message,
			Suggestion = suggestion
		});
	}
}

public class DateTimeParseResult
{
	public bool IsValid { get; set; }
	public DateTimeOffset? ParsedValue { get; set; }
	public string? ErrorMessage { get; set; }
	public string? Suggestion { get; set; }
}

public class HoursParseResult
{
	public bool IsValid { get; set; }
	public double? ParsedValue { get; set; }
	public string? ErrorMessage { get; set; }
}

public class StatusValidationResult
{
	public bool IsValid { get; set; }
	public EmployeeAttendanceStatusType? ParsedStatus { get; set; }
	public string? ErrorMessage { get; set; }
	public string? Suggestion { get; set; }
}

#endregion
