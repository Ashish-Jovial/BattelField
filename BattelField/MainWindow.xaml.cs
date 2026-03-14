using BattelField.Repositories;
using BattelField.Services;
using BattelField.ViewModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BattelField
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // This is the missing piece causing your error
        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            // 1. Re-create the dependencies (SOLID)
            IBoardRepository repo = new RandomBoardRepository();
            IGameService service = new GameService();
            IHighScoreService scoreService = new FileHighScoreService();

            // 2. Create a fresh ViewModel
            var newVm = new BattleshipViewModel(repo, service, scoreService);

            // 3. Re-assign the DataContext to reset the UI
            this.DataContext = newVm;

            // Optional: Log to debug console
            System.Diagnostics.Debug.WriteLine("Game Restarted with new Ship positions.");
        }
    }
}