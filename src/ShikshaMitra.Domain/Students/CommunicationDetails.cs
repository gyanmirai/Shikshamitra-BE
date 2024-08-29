using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Students;

public class CommunicationDetails : ValueObject
{
	public string ParentContactNumber { get; private set; }
	public string ParentEmail { get; private set; }
	public string StudentContactNumber { get; private set; }
	public string StudentEmail { get; private set; }
	public string WhatsAppContactNumber { get; private set; }

	// Constructor for Value Object
	internal CommunicationDetails(
		string parentContactNumber,
		string parentEmail,
		string studentContactNumber,
		string studentEmail,
		string whatsAppContactNumber)
	{
		ParentContactNumber = parentContactNumber;
		ParentEmail = parentEmail;
		StudentContactNumber = studentContactNumber;
		StudentEmail = studentEmail;
		WhatsAppContactNumber = whatsAppContactNumber;
	}

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return ParentContactNumber;
		yield return ParentEmail;
		yield return StudentContactNumber;
		yield return StudentEmail;
		yield return WhatsAppContactNumber;
	}
}