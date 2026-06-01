using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

public class AttendanceImportRow : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid BatchId { get; private set; }
	public int RowNumber { get; private set; }
	public string Action { get; private set; } = "invalid";
	public string AttendanceDate { get; private set; } = string.Empty;
	public string EmployeeCode { get; private set; } = string.Empty;
	public string? EmployeeName { get; private set; }
	public string? ContactNo { get; private set; }
	public string? TimeIn { get; private set; }
	public string? TimeOut { get; private set; }
	public string? Status { get; private set; }
	public string? Remarks { get; private set; }
	public List<string> Errors { get; private set; } = [];
	public List<string> Warnings { get; private set; } = [];

	private AttendanceImportRow() { }

	public AttendanceImportRow(
		Guid id,
		Guid? tenantId,
		Guid batchId,
		int rowNumber,
		string action,
		string attendanceDate,
		string employeeCode,
		string? employeeName,
		string? contactNo,
		string? timeIn,
		string? timeOut,
		string? status,
		string? remarks,
		List<string> errors,
		List<string> warnings) : base(id)
	{
		TenantId = tenantId;
		BatchId = batchId;
		RowNumber = rowNumber;
		Action = action;
		AttendanceDate = attendanceDate;
		EmployeeCode = employeeCode;
		EmployeeName = employeeName;
		ContactNo = contactNo;
		TimeIn = timeIn;
		TimeOut = timeOut;
		Status = status;
		Remarks = remarks;
		Errors = errors;
		Warnings = warnings;
	}

	public void UpdateEditableValues(
		string? timeIn,
		string? timeOut,
		string? status,
		string? remarks,
		string? warning = null)
	{
		TimeIn = timeIn;
		TimeOut = timeOut;
		Status = status;
		Remarks = remarks;
		if (!string.IsNullOrWhiteSpace(warning) && !Warnings.Contains(warning))
		{
			Warnings.Add(warning);
		}
	}
}
