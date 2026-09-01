using System.ComponentModel;

namespace CodexConversationManager;

internal abstract class NotifyObject : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged;

	protected void RaisePropertyChanged(string name)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
