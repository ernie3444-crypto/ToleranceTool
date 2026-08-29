using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ToleranceTool.Wpf.Mvvm
{
    /// <summary>Minimal <see cref="INotifyPropertyChanged"/> base for the view models.</summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void Raise([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            Raise(name);
            return true;
        }
    }
}
