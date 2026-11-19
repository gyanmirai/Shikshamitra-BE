using System;
using System.Linq;

namespace ShikshaMitra.HR.Employees.Documents;

/// <summary>Read-only metadata. Selecting scalar columns avoids materializing the employee aggregate.</summary>
public sealed class EmployeeDocumentReadItem
{
	public Guid Id { get; init; }
	public Guid? DocumentNameId { get; init; }
	public string DocumentName { get; init; } = string.Empty;
	public string ContentType { get; init; } = string.Empty;
	public long Size { get; init; }
	public DateTime CreationTime { get; init; }
	public DateTime? LastModificationTime { get; init; }
	public string BlobName { get; init; } = string.Empty;

	public static IQueryable<EmployeeDocumentReadItem> Query(IQueryable<Employee> employees, Guid employeeId) =>
		employees.Where(employee => employee.Id == employeeId)
			.SelectMany(employee => employee.Documents)
			.Select(document => new EmployeeDocumentReadItem
			{
				Id = document.Id,
				DocumentNameId = document.DocumentNameId,
				DocumentName = document.DocumentName,
				ContentType = document.ContentType,
				Size = document.Size,
				CreationTime = document.CreationTime,
				LastModificationTime = document.LastModificationTime,
				BlobName = document.BlobName
			});
}
