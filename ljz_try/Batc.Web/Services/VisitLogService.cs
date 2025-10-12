using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Batc.Web.Services
{
    public class VisitLogService
    {
        public class VisitRecord
        {
            public string Label { get; set; } = "";
            public DateTime Date { get; set; }
        }

        private readonly IJSRuntime _js;
        private const string StorageKey = "visits-log";
        private bool _loaded;
        private readonly List<VisitRecord> _visits = new();

        public VisitLogService(IJSRuntime js) => _js = js;

        public IReadOnlyList<VisitRecord> Visits => _visits;
        public event Action? OnChange;

        public async Task EnsureLoadedAsync()
        {
            if (_loaded) return;
            try
            {
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var data = JsonSerializer.Deserialize<List<VisitRecord>>(json);
                    if (data is not null)
                    {
                        _visits.Clear();
                        _visits.AddRange(data);
                    }
                }
            }
            catch { /* 忽略存取失敗 */ }
            _loaded = true;
            OnChange?.Invoke();
        }

        private async Task SaveAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_visits);
                await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
            }
            catch { /* 忽略存取失敗 */ }
        }

        public void Add(string label, DateTime? at = null)
        {
            _visits.Add(new VisitRecord { Label = label, Date = at ?? DateTime.Now });
            OnChange?.Invoke();
            _ = SaveAsync(); // fire-and-forget 持久化
        }

        public void Clear()
        {
            _visits.Clear();
            OnChange?.Invoke();
            _ = SaveAsync();
        }
    }
}
