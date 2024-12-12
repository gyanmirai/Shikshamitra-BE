using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees;

public class DocumentDto : DocumentMetadataDto
{
	public byte[] Content { get; set; }
}