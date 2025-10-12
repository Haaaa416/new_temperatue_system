using System.Collections.Generic;
using Batc.Web.Models;

namespace Batc.Web.Services
{
    public class AppState
    {
        public List<Patient> Patients { get; } = new();  // 病患的全域清單
    }
}
