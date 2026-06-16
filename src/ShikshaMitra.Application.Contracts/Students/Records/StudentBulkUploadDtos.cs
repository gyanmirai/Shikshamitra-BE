using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.Records;

public class StudentBulkUploadFileDto
{
	public required string FileName { get; set; }
	public string? ContentType { get; set; }
	public required byte[] Content { get; set; }
}

public class StudentBulkUploadResultDto
{
	public int TotalRows { get; set; }
	public int ImportedCount { get; set; }
	public int FailedCount => Errors.Count;
	public List<Guid> StudentIds { get; set; } = [];
	public List<StudentBulkUploadErrorDto> Errors { get; set; } = [];
}

public class StudentBulkUploadErrorDto
{
	public int RowNumber { get; set; }
	public string Message { get; set; } = string.Empty;
}
