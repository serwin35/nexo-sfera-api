using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace GeneratorDanych
{
	public sealed class LogOperacji : PropertyChangedNotifier, ILogOperacji
	{
		private readonly Dispatcher _dispatcher;

		public LogOperacji() 
		{
			Operacje = new ObservableCollection<string>();
			_dispatcher = Dispatcher.CurrentDispatcher;	
		}

        public ObservableCollection<string>	Operacje { get; }

		public void Zaloguj(string komunikat)
		{
			_dispatcher.Invoke(() => Operacje.Add(komunikat));
		}
	}
}