using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments;
public class StudentRegistrationFormDto : StudentInquiryDto
{
	public required string FormNo { get; set; }
}
