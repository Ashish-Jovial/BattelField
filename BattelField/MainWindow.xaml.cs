using BattelField.Repositories;
using BattelField.Services;
using BattelField.ViewModel;
using System.Windows;

namespace BattelField
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            IBoardRepository repo = new RandomBoardRepository();
            IGameService service = new GameService();
            IHighScoreService scoreService = new FileHighScoreService();

            var newVm = new BattleshipViewModel(repo, service, scoreService);
            this.DataContext = newVm;
        }

        private void StartTwo_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is BattleshipViewModel vm)
            {
                string p1 = P1NameBox.Text?.Trim();
                string p2 = P2NameBox.Text?.Trim();
                vm.StartTwoPlayer(p1, p2);
            }
        }
    }
}