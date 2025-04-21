using System.IO;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentRecords.BulkUpload
{
    /// <summary>
    /// Interface for handling student CSV imports
    /// </summary>
    public interface IStudentCsvImportService
    {
        /// <summary>
        /// Process a CSV file containing student records and import them into the system
        /// </summary>
        /// <param name="stream">Stream containing CSV data</param>
        /// <returns>Result of the import operation</returns>
        Task<StudentCsvUploadResultDto> ImportStudentsFromCsvAsync(Stream stream);
    }
}