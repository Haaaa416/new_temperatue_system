// Models/Visit.cs
using System;

namespace Batc.Web.Models
{
	public class Visit
	{
		public DateTime Date { get; set; }
		public string Label { get; set; } = "";
		// 可選：若你有 body part 的 Id 也可一起存
		public string? PartId { get; set; }
	}
}
