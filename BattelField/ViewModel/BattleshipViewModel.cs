using BattelField.Models;
using BattelField.Repositories;
using BattelField.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace BattelField.ViewModel
{
    public class BattleshipViewModel : BaseViewModel
    {
        private readonly IBoardRepository _repository;
        private readonly IGameService _gameService;
        private readonly IHighScoreService _scoreService; // New Dependency
        private string _statusText;
        private bool _isGameOver;
        private int _shotCount;
        private int _bestScore;

        private GameStage _stage;
        private Player _player1;
        private Player _player2;
        private Player _currentSetter; // Player placing ships during setup
        private Player _currentShooter; // Player whose turn it is
        private ObservableCollection<Cell> _grid;

        // Timer
        private readonly DispatcherTimer _timer;
        private int _remainingSeconds;
        private int _requestedTimerSeconds = 120; // default 2 minutes
        private const int MaxTimerSeconds = 4 * 60;

        public BattleshipViewModel(IBoardRepository repository, IGameService gameService, IHighScoreService scoreService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _scoreService = scoreService ?? throw new ArgumentNullException(nameof(scoreService));

            BestScore = _scoreService.GetBestScore();
            ShotCount = 0;

            Stage = GameStage.ChooseMode;

            FireCommand = new RelayCommand(execute: param => ExecuteGridClick(param), canExecute: param => CanExecuteGridClick(param));
            ConfirmSetupCommand = new RelayCommand(execute: _ => ConfirmSetup(), canExecute: _ => CanConfirmSetup());
            StartSinglePlayerCommand = new RelayCommand(_ => StartSinglePlayer());
            StartTwoPlayerCommand = new RelayCommand(_ => Stage = GameStage.NameEntry);

            // Timer setup
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;

            // Initialize a default grid so UI binds safely
            Grid = new ObservableCollection<Cell>(_repository.GenerateBoard());
            StatusText = "Select mode: One player or Two players.";
        }

        public BattleshipViewModel(IBoardRepository repository, IGameService gameService, IHighScoreService scoreService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _scoreService = scoreService ?? throw new ArgumentNullException(nameof(scoreService));

            BestScore = _scoreService.GetBestScore();
            Grid = new ObservableCollection<Cell>(_repository.GenerateBoard());
            StatusText = "Fleet deployed. Select a coordinate to fire!";

            FireCommand = new RelayCommand(
                execute: param => ExecuteFire(param),
                canExecute: param => CanExecuteFire(param)
            );
        }

        public GameStage Stage
        {
            get => _stage;
            set { _stage = value; OnPropertyChanged(); }
        }

        public Player Player1
        {
            get => _player1;
            set { _player1 = value; OnPropertyChanged(); }
        }

        public Player Player2
        {
            get => _player2;
            set { _player2 = value; OnPropertyChanged(); }
        }

        public Player CurrentSetter
        {
            get => _currentSetter;
            set { _currentSetter = value; OnPropertyChanged(); RefreshGridForCurrentStage(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool IsGameOver
        {
            get => _isGameOver;
            private set
            {
                _isGameOver = value;
                OnPropertyChanged();
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        public Player CurrentShooter
        {
            get => _currentShooter;
            set { _currentShooter = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Cell> Grid
        {
            get => _grid;
            private set { _grid = value; OnPropertyChanged(); }
        }
        private Player _currentPlayer;
        public Player CurrentPlayer
        {
            get => _currentPlayer;
            set { _currentPlayer = value; OnPropertyChanged(); }
        }
        public int ShotCount
        {
            get => _shotCount;
            set { _shotCount = value; OnPropertyChanged(); }
        }

        public int BestScore
        {
            get => _bestScore;
            set { _bestScore = value; OnPropertyChanged(); }
        }
        public ObservableCollection<Cell> Grid { get; private set; }


        public RelayCommand FireCommand { get; }

        

        private bool CanExecuteFire(object parameter)
        {
            if (IsGameOver) return false;
            if (parameter is Cell cell) return !cell.IsHit;
            return false;
        }

        private void ExecuteFire(object parameter)
        {
            if (parameter is Cell targetCell)
            {
                bool wasHit = _gameService.ProcessMove(targetCell);
                ShotCount++;
                StatusText = wasHit ? $"HIT at ({targetCell.Row}, {targetCell.Column})!" : "Miss... check your radar.";

                CheckGameStatus();
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        private void CheckGameStatus()
        {
            // Game is won if no cells with ships remain un-hit
            bool shipsRemaining = Grid.Any(c => c.HasShip && !c.IsHit);

            if (!shipsRemaining)
            {
                IsGameOver = true;
                StatusText = "VICTORY! The enemy fleet has been sunk.";

                _scoreService.SaveScore(ShotCount);
                BestScore = _scoreService.GetBestScore(); // Refresh display
            }
        }

    }
}
