using BattelField.Repositories;
using BattelField.Services;
using BattelField.ViewModel;
using System.Configuration;
using System.Data;
using System.Windows;

namespace BattelField
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // OLD: IBoardRepository repo = new LocalBoardRepository();
            // NEW: Randomly generated board every time the app starts
            IBoardRepository repo = new RandomBoardRepository();
            IGameService service = new GameService();
            IHighScoreService scoreService = new FileHighScoreService(); // New service
            var vm = new BattleshipViewModel(repo, service, scoreService);

            var win = new MainWindow { DataContext = vm };
            win.Show();
        }
    }
}
