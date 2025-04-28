using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.StudentRecords.BulkUpload;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students
{
    [Route("api/student")]
    public class StudentController : AbpController
    {
        private readonly IStudentAppService _studentAppService;

        public StudentController(IStudentAppService studentAppService)
        {
            _studentAppService = studentAppService;
        }

        [HttpPost("bulk-upload")]
        public async Task<StudentCsvUploadResultDto> UploadStudentsCsv(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new UserFriendlyException("No file was uploaded.");
            }

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFriendlyException("Only CSV files are supported.");
            }

            using var stream = file.OpenReadStream();
            return await _studentAppService.ImportStudentsFromCsvAsync(stream, file.FileName);
        }
    }
}