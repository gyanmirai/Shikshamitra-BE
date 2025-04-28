using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentRecords.BulkUpload;

/// <summary>
/// Represents the result of a CSV student upload operation
/// </summary>
public class StudentCsvUploadResultDto
{
    /// <summary>
    /// Number of records successfully imported
    /// </summary>
    public int SuccessCount { get; set; }
    
    /// <summary>
    /// Number of records that failed to import
    /// </summary>
    public int FailureCount { get; set; }
    
    /// <summary>
    /// Total number of records processed
    /// </summary>
    public int TotalCount { get; set; }
    
    /// <summary>
    /// Collection of validation errors encountered during import
    /// </summary>
    public List<StudentCsvValidationError> ValidationErrors { get; set; } = new();
}

/// <summary>
/// Represents a validation error for a particular row in the CSV file
/// </summary>
public class StudentCsvValidationError
{
    /// <summary>
    /// Row number in the CSV file (1-based)
    /// </summary>
    public int RowNumber { get; set; }
    
    /// <summary>
    /// Identifier for the record (could be form number, name, etc.)
    /// </summary>
    public string? Identifier { get; set; }
    
    /// <summary>
    /// Error message describing the validation failure
    /// </summary>
    public string ErrorMessage { get; set; } = null!;
}