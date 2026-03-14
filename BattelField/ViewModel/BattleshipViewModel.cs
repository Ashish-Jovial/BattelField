using BattelField.Models;
using BattelField.Repositories;
using BattelField.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace BattelField.ViewModel
{
    public enum GameStage
    {
        ChooseMode,
        NameEntry,
        Player1Setup,
        Player2Setup,
        Play,
        Finished
    }

    public class BattleshipViewModel : BaseViewModel
    {
        private readonly IBoardRepository _repository;
        private readonly IGameService _gameService;
        private readonly IHighScoreService _scoreService;

        private GameStage _stage;
        private Player _player1;
        private Player _player2;
        private Player _currentSetter;
        private Player _currentShooter;
        private ObservableCollection<Cell> _grid;

        private string _statusText;
        private bool _isGameOver;
        private int _shotCount;
        private int _bestScore;

        // Ship placement support
        private const int MaxShips = 5;
        private int _shipsToDeploy = 1;
        private string _shipNameInput;
        private readonly Dictionary<Player, List<Ship>> _playerShips = new();

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

            ShotCount = 0;
            BestScore = _scoreService.GetBestScore();

            Stage = GameStage.ChooseMode;
            Grid = new ObservableCollection<Cell>(_repository.GenerateBoard());
            StatusText = "Select One Player or Two Players. For two players enter names and press Start Two Player.";

            // Commands
            FireCommand = new RelayCommand(execute: param => ExecuteGridClick(param), canExecute: param => CanExecuteGridClick(param));
            ConfirmSetupCommand = new RelayCommand(execute: _ => ConfirmSetup(), canExecute: _ => CanConfirmSetup());
            StartSinglePlayerCommand = new RelayCommand(_ => StartSinglePlayer());
            CreateShipCommand = new RelayCommand(_ => CreateShip(), _ => CanCreateShip());

            // Timer
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
        }

        // New command to create a named ship from currently selected cells
        public RelayCommand CreateShipCommand { get; }

        public RelayCommand FireCommand { get; }
        public RelayCommand ConfirmSetupCommand { get; }
        public RelayCommand StartSinglePlayerCommand { get; }

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

        public Player CurrentShooter
        {
            get => _currentShooter;
            set { _currentShooter = value; OnPropertyChanged(); RefreshGridForCurrentStage(); }
        }

        public ObservableCollection<Cell> Grid
        {
            get => _grid;
            private set { _grid = value; OnPropertyChanged(); }
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

        public int ShotCount
        {
            get => _shotCount;
            set { _shotCount = value; OnPropertyChanged(); }
        }

        public int BestScore
        {
            get => _bestScore;
            set { _best_score_set(value); }
        }

        private void _best_score_set(int value)
        {
            _bestScore = value;
            OnPropertyChanged(nameof(BestScore));
        }

        // Timer properties
        public int RequestedTimerSeconds
        {
            get => _requestedTimerSeconds;
            set
            {
                _requestedTimerSeconds = Math.Min(value, MaxTimerSeconds);
                OnPropertyChanged();
            }
        }

        public string TimeLeft => TimeSpan.FromSeconds(_remainingSeconds).ToString(@"mm\:ss");

        // New: number of ships to deploy (applies to each player)
        public int ShipsToDeploy
        {
            get => _shipsToDeploy;
            set
            {
                _shipsToDeploy = Math.Max(1, Math.Min(value, MaxShips));
                OnPropertyChanged();
            }
        }

        // New: user-entered ship name for creation
        public string ShipNameInput
        {
            get => _shipNameInput;
            set { _shipNameInput = value; OnPropertyChanged(); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_remainingSeconds > 0)
            {
                _remainingSeconds--;
                OnPropertyChanged(nameof(TimeLeft));
            }
            else
            {
                _timer.Stop();
                IsGameOver = true;
                Stage = GameStage.Finished;
                StatusText = "Time's up! Game over.";
            }
        }

        private bool CanExecuteGridClick(object parameter)
        {
            if (parameter is Cell cell)
            {
                if (Stage == GameStage.Player1Setup || Stage == GameStage.Player2Setup)
                    return true; // allow toggling ship placement during setup
                if (Stage == GameStage.Play)
                    return !cell.IsHit; // allow firing on un-hit cells
            }
            return false;
        }

        private void ExecuteGridClick(object parameter)
        {
            if (!(parameter is Cell clicked)) return;

            if (Stage == GameStage.Player1Setup || Stage == GameStage.Player2Setup)
            {
                // Toggle HasShip for selection while creating a ship; do not finalize grouping until user clicks CreateShip.
                var own = CurrentSetter.Board.First(c => c.Row == clicked.Row && c.Column == clicked.Column);

                // Toggle selection
                own.HasShip = !own.HasShip;

                // If removing selection that already belonged to a named ship, remove association
                if (!own.HasShip && !string.IsNullOrEmpty(own.ShipName))
                {
                    var existing = _playerShips[CurrentSetter].FirstOrDefault(s => s.Name == own.ShipName);
                    if (existing != null)
                    {
                        existing.Cells.Remove(own);
                        if (existing.Cells.Count == 0) _playerShips[CurrentSetter].Remove(existing);
                    }
                    own.ShipName = null;
                }

                StatusText = $"{CurrentSetter.Name}: selected cells = {CountShips(CurrentSetter.Board)}";
                RefreshGridForCurrentStage();
            }
            else if (Stage == GameStage.Play)
            {
                // Clicking attacks opponent's board (Grid is bound to opponent board)
                var opponent = GetOpponent(CurrentShooter);
                var target = opponent.Board.First(c => c.Row == clicked.Row && c.Column == clicked.Column);
                if (target.IsHit) return;

                bool wasHit = _gameService.ProcessMove(target);
                ShotCount++;

                if (wasHit)
                {
                    CurrentShooter.Score += 5;
                    StatusText = $"{CurrentShooter.Name} HIT at ({target.Row},{target.Column})! +5";
                }
                else
                {
                    StatusText = $"{CurrentShooter.Name} missed at ({target.Row},{target.Column}).";
                }

                // Check victory
                bool opponentHasShips = opponent.Board.Any(c => c.HasShip && !c.IsHit);
                if (!opponentHasShips)
                {
                    IsGameOver = true;
                    Stage = GameStage.Finished;
                    StatusText = $"{CurrentShooter.Name} wins! Fleet sunk in {ShotCount} shots.";
                    TrySaveScore();
                    _timer.Stop();
                    return;
                }

                // Switch turn
                CurrentShooter = GetOpponent(CurrentShooter);
                StatusText += $" Now it's {CurrentShooter.Name}'s turn.";
                RefreshGridForCurrentStage();
            }
        }

        private void TrySaveScore()
        {
            try
            {
                _scoreService.SaveScore(ShotCount);
                BestScore = _scoreService.GetBestScore();
            }
            catch { /* ignore persistence errors */ }
        }

        private Player GetOpponent(Player p) => p == Player1 ? Player2 : Player1;

        // Public API: start single player
        public void StartSinglePlayer()
        {
            Player1 = new Player
            {
                Name = "Player",
                Board = new ObservableCollection<Cell>(_repository.GenerateBoard()),
                Score = 0,
                IsActive = true
            };
            Player2 = new Player
            {
                Name = "Computer",
                Board = new ObservableCollection<Cell>(_repository.GenerateBoard()),
                Score = 0,
                IsActive = false
            };

            // initialize player ship collections so single-player placement could be extended later
            _playerShips[Player1] = new List<Ship>();
            _playerShips[Player2] = new List<Ship>();

            Stage = GameStage.Play;
            CurrentShooter = Player1;
            ShotCount = 0;
            StatusText = "Single player: Attack the enemy grid!";
            RefreshGridForCurrentStage();
            StartTimerIfRequested();
        }

        // Public API: start two player by names (prepare boards and placement)
        public void StartTwoPlayer(string p1Name, string p2Name)
        {
            Player1 = new Player
            {
                Name = string.IsNullOrWhiteSpace(p1Name) ? "Player 1" : p1Name,
                Board = new ObservableCollection<Cell>(_repository.GenerateBoard().Select(c => new Cell { Row = c.Row, Column = c.Column })),
                Score = 0,
                IsActive = true
            };
            Player2 = new Player
            {
                Name = string.IsNullOrWhiteSpace(p2Name) ? "Player 2" : p2Name,
                Board = new ObservableCollection<Cell>(_repository.GenerateBoard().Select(c => new Cell { Row = c.Row, Column = c.Column })),
                Score = 0,
                IsActive = false
            };

            // initialize player ship listings
            _playerShips[Player1] = new List<Ship>();
            _playerShips[Player2] = new List<Ship>();

            Stage = GameStage.Player1Setup;
            CurrentSetter = Player1;
            StatusText = $"{CurrentSetter.Name}: place your ships (select cells), create ships and press Set.";
            ShotCount = 0;
            RefreshGridForCurrentStage();
        }

        // Confirm setup button pressed
        public void ConfirmSetup()
        {
            if (Stage == GameStage.Player1Setup)
            {
                Stage = GameStage.Player2Setup;
                CurrentSetter = Player2;
                StatusText = $"{CurrentSetter.Name}: place your ships (select cells), create ships and press Set.";
                RefreshGridForCurrentStage();
            }
            else if (Stage == GameStage.Player2Setup)
            {
                // both players finished placement, begin play
                Stage = GameStage.Play;
                CurrentShooter = Player1;
                StatusText = $"Setup complete. {CurrentShooter.Name} begins.";
                RefreshGridForCurrentStage();
                StartTimerIfRequested();
            }
        }

        private bool CanConfirmSetup()
        {
            if (Stage == GameStage.Player1Setup || Stage == GameStage.Player2Setup)
            {
                if (CurrentSetter == null) return false;
                // require that the player created exactly the selected number of ships
                return _playerShips.TryGetValue(CurrentSetter, out var ships) && ships.Count == ShipsToDeploy;
            }
            return false;
        }

        private int CountShips(IEnumerable<Cell> board) => board.Count(c => c.HasShip);

        private void RefreshGridForCurrentStage()
        {
            if (Stage == GameStage.Player1Setup || Stage == GameStage.Player2Setup)
            {
                // placement mode: reveal ships on the setter's board
                foreach (var c in CurrentSetter.Board) c.IsPlacementMode = true;
                Grid = new ObservableCollection<Cell>(CurrentSetter.Board);
            }
            else if (Stage == GameStage.Play && CurrentShooter != null)
            {
                var opponent = GetOpponent(CurrentShooter);
                // hide ships on the opponent board (only hits/revealed shown)
                foreach (var c in opponent.Board) c.IsPlacementMode = false;
                Grid = new ObservableCollection<Cell>(opponent.Board);
            }
            else
            {
                Grid = new ObservableCollection<Cell>(_repository.GenerateBoard());
            }
        }

        private void StartTimerIfRequested()
        {
            if (RequestedTimerSeconds <= 0) return;
            _remainingSeconds = Math.Min(RequestedTimerSeconds, MaxTimerSeconds);
            OnPropertyChanged(nameof(TimeLeft));
            _timer.Stop();
            _timer.Start();
        }

        // Helper to allow coordinate-based placement programmatically (caller must be responsible for valid placements)
        public void SetShipAt(Player player, int row, int column, bool hasShip)
        {
            var cell = player.Board.FirstOrDefault(c => c.Row == row && c.Column == column);
            if (cell != null) cell.HasShip = hasShip;
            if (player == CurrentSetter) RefreshGridForCurrentStage();
        }

        // New: create a Ship from currently selected/unassigned cells on CurrentSetter.Board
        private void CreateShip()
        {
            if (CurrentSetter == null) return;
            if (!_playerShips.ContainsKey(CurrentSetter)) _playerShips[CurrentSetter] = new List<Ship>();

            var board = CurrentSetter.Board;
            // pick selected cells that are not already part of a named ship
            var selected = board.Where(c => c.HasShip && string.IsNullOrEmpty(c.ShipName)).ToList();
            if (selected.Count == 0) return;
            if (_playerShips[CurrentSetter].Count >= ShipsToDeploy) return;

            var shipName = string.IsNullOrWhiteSpace(ShipNameInput) ? $"Ship {_playerShips[CurrentSetter].Count + 1}" : ShipNameInput.Trim();
            var ship = new Ship { Name = shipName };
            foreach (var c in selected)
            {
                c.ShipName = shipName;
                ship.Cells.Add(c);
            }
            _playerShips[CurrentSetter].Add(ship);

            // clear input
            ShipNameInput = string.Empty;
            StatusText = $"{CurrentSetter.Name}: created \"{shipName}\" ({ship.Cells.Count} cells). { _playerShips[CurrentSetter].Count}/{ShipsToDeploy} ships done.";
            RefreshGridForCurrentStage();
        }

        private bool CanCreateShip()
        {
            if (CurrentSetter == null) return false;
            if (!_playerShips.ContainsKey(CurrentSetter)) _playerShips[CurrentSetter] = new List<Ship>();
            if (_playerShips[CurrentSetter].Count >= ShipsToDeploy) return false;
            if (string.IsNullOrWhiteSpace(ShipNameInput)) return false;
            // must have at least one selected, unassigned cell
            var hasSelected = CurrentSetter.Board.Any(c => c.HasShip && string.IsNullOrEmpty(c.ShipName));
            return hasSelected;
        }
    }

    // Extension to count ships on a player's board
    internal static class PlayerExtensions
    {
        public static int BoardShipCount(this Player player) => player.Board.Count(c => c.HasShip);
    }
}
