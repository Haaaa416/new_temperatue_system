// Models/Visit.cs
using System;

namespace Batc.Web.Models
{
	public class Visit
	{
		public DateTime Date { get; set; }
		public string Label { get; set; } = "";
		// �i��G�Y�A�� body part �� Id �]�i�@�_�s
		public string? PartId { get; set; }
	}
}
