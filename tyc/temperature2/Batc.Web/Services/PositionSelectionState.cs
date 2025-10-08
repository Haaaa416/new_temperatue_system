// Services/PositionSelectionState.cs
using System;

namespace Batc.Web.Services
{
    public class PositionSelectionState
    {
        public record Selection(double X, double Y, bool Front, string Label);

        private Selection? _current;
        public Selection? Current => _current;
        public bool HasSelection => _current is not null;

        public event Action? OnChange;

        public void Set(double x, double y, bool front, string label)
        {
            _current = new Selection(x, y, front, label);
            OnChange?.Invoke();
        }

        public void Clear()
        {
            _current = null;
            OnChange?.Invoke();
        }
    }
}
