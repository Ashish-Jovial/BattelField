using BattelField.Models;
using BattelField.Repositories;
using BattelField.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
        private readonly IHighScoreService _score_service;

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

        // per-player ship collections (observable for UI)
        private readonly ObservableCollection<Ship> _player1Ships = new();
        private readonly ObservableCollection<Ship> _player2Ships = new();

        // Timer
        private readonly DispatcherTimer _timer;
        private int _remainingSeconds;
        private int _requestedTimerSeconds = 120; // default 2 minutes
        private const int MaxTimerSeconds = 4 * 60;

        public BattleshipViewModel(IBoardRepository repository, IGameService gameService, IHighScoreService scoreService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _score_service = scoreService ?? throw new ArgumentNullException(nameof(scoreService));

            ShotCount = 0;
            BestScore = _score_service.GetBestScore();

            Stage = GameStage.ChooseMode;
            Grid = new ObservableCollection<Cell>(_repository.GenerateBoard());
            StatusText = "Select One Player or Two Players. For two players enter names and press Start Two Player.";

            // Commands
            FireCommand = new RelayCommand(execute: param => ExecuteGridClick(param), canExecute: param => CanExecuteGridClick(param));
            ConfirmSetupCommand = new RelayCommand(execute: _ => ConfirmSetup(), canExecute: _ => CanConfirmSetup());
            StartSinglePlayerCommand = new RelayCommand(_ => StartSinglePlayer());
            CreateShipCommand = new RelayCommand(_ => CreateShip(), _ => CanCreateShip());

            // hook collection changed so UI can react
            _player1Ships.CollectionChanged += PlayerShips_CollectionChanged;
            _player2Ships.CollectionChanged += PlayerShips_CollectionChanged;

            // Timer
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
        }

        private void PlayerShips_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsPlayer1PlacementComplete));
            OnPropertyChanged(nameof(IsPlayer2PlacementComplete));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        // Expose collections for UI binding (read-only)
        public ObservableCollection<Ship> Player1Ships => _player1Ships;
        public ObservableCollection<Ship> Player2Ships => _player2Ships;

        // New command to create a named ship from currently selected cells
        public RelayCommand CreateShipCommand { get; }

        public RelayCommand FireCommand { get; }
        public RelayCommand ConfirmSetupCommand { get; }
        public RelayCommand StartSinglePlayerCommand { get; }

        public GameStage Stage
        {
            get => _stage;
            set
            {
                _stage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInSetup));
                RefreshGridForCurrentStage();
            }
        }

        public bool IsInSetup => Stage == GameStage.Player1Setup || Stage == GameStage.Player2Setup;

        // Name inputs (bind TextBoxes to these)
        private string _p1Name;
        public string P1Name
        {
            get => _p1Name;
            set { _p1Name = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartTwoPlayer)); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); }
        }

        private string _p2Name;
        public string P2Name
        {
            get => _p2Name;
            set { _p2Name = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartTwoPlayer)); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); }
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
                // updating allowed ship count may affect placement completion
                OnPropertyChanged(nameof(IsPlayer1PlacementComplete));
                OnPropertyChanged(nameof(IsPlayer2PlacementComplete));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        // New: user-entered ship name for creation
        public string ShipNameInput
        {
            get => _shipNameInput;
            set { _shipNameInput = value; OnPropertyChanged(); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); }
        }

        // Flags for UI: whether each player completed placement
        public bool IsPlayer1PlacementComplete => _player1Ships.Count == ShipsToDeploy;
        public bool IsPlayer2PlacementComplete => _player2Ships.Count == ShipsToDeploy;

        // Whether the Start Two Player button should be enabled (require names)
        public bool CanStartTwoPlayer => !string.IsNullOrWhiteSpace(P1Name) && !string.IsNullOrWhiteSpace(P2Name);

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

        // Modified ExecuteGridClick selection logic to enforce contiguous same-row selection
        private void ExecuteGridClick(object parameter)
        {
            if (!(parameter is Cell clicked)) return;

            if (Stage == GameStage.Player1Setup || Stage == GameStage.Player2Setup)
            {
                if (CurrentSetter == null) return;

                // Find the board cell reference
                var own = CurrentSetter.Board.First(c => c.Row == clicked.Row && c.Column == clicked.Column);

                // Toggle selection
                bool newValue = !own.HasShip;

                // If selecting, ensure contiguous selection in same row and within 10 columns
                if (newValue)
                {
                    var board = CurrentSetter.Board;
                    // currently selected unassigned cells (before adding this click)
                    var selected = board.Where(c => c.HasShip && string.IsNullOrEmpty(c.ShipName)).ToList();
                    // include the new cell
                    selected.Add(own);

                    // enforce all in same row
                    var sameRow = selected.All(c => c.Row == selected.First().Row);
                    // enforce contiguous columns
                    var minCol = selected.Min(c => c.Column);
                    var maxCol = selected.Max(c => c.Column);
                    var contiguous = (maxCol - minCol + 1) == selected.Count;
                    // enforce not beyond 10 columns (columns indexed 0..9 or 1..10 depending on repo)
                    var withinBounds = selected.All(c => c.Column >= 0 && c.Column <= 9);

                    if (!sameRow)
                    {
                        StatusText = $"{CurrentSetter.Name}: selected cells must be on the same row.";
                        return;
                    }
                    if (!contiguous)
                    {
                        StatusText = $"{CurrentSetter.Name}: selected cells must form a contiguous block.";
                        return;
                    }
                    if (!withinBounds)
                    {
                        StatusText = $"{CurrentSetter.Name}: selection out of column bounds (max 10).";
                        return;
                    }
                }

                own.HasShip = newValue;

                // If removing selection that already belonged to a named ship, remove association
                if (!own.HasShip && !string.IsNullOrEmpty(own.ShipName))
                {
                    var ships = CurrentSetter == Player1 ? _player1Ships : _player2Ships;
                    var existing = ships.FirstOrDefault(s => s.Name == own.ShipName);
                    if (existing != null)
                    {
                        existing.Cells.Remove(own);
                        if (existing.Cells.Count == 0) ships.Remove(existing);
                    }
                    own.ShipName = null;
                }

                StatusText = $"{CurrentSetter.Name}: selected cells = {CountShips(CurrentSetter.Board)}";
                RefreshGridForCurrentStage();
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
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
                _score_service.SaveScore(ShotCount);
                BestScore = _score_service.GetBestScore();
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
            _player1Ships.Clear();
            _player2Ships.Clear();

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
            _player1Ships.Clear();
            _player2Ships.Clear();

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
                var ships = CurrentSetter == Player1 ? _player1Ships : _player2Ships;
                // require that the player created exactly the selected number of ships
                return ships.Count == ShipsToDeploy;
            }
            return false;
        }

        private int CountShips(IEnumerable<Cell> board) => board.Count(c => c.HasShip);

        private void RefreshGridForCurrentStage()
        {
            if (Stage == GameStage.Player1Setup || Stage == GameStage.Player2Setup)
            {
                // placement mode: reveal ships on the setter's board
                if (CurrentSetter != null)
                {
                    foreach (var c in CurrentSetter.Board) c.IsPlacementMode = true;
                    Grid = new ObservableCollection<Cell>(CurrentSetter.Board);
                }
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

            var ships = CurrentSetter == Player1 ? _player1Ships : _player2Ships;

            var board = CurrentSetter.Board;
            // pick selected cells that are not already part of a named ship
            var selected = board.Where(c => c.HasShip && string.IsNullOrEmpty(c.ShipName)).ToList();
            if (selected.Count == 0) return;
            if (ships.Count >= ShipsToDeploy) return;

            // validate orientation (horizontal or vertical)
            bool allSameRow = selected.All(c => c.Row == selected.First().Row);
            bool allSameCol = selected.All(c => c.Column == selected.First().Column);
            if (!allSameRow && !allSameCol)
            {
                StatusText = $"{CurrentSetter.Name}: ship must be horizontal or vertical.";
                return;
            }

            // validate contiguous and length
            int count = selected.Count;
            if (count > 6)
            {
                StatusText = $"{CurrentSetter.Name}: ship cannot exceed 6 cells.";
                return;
            }

            bool contiguous;
            if (allSameRow)
            {
                var minCol = selected.Min(c => c.Column);
                var maxCol = selected.Max(c => c.Column);
                contiguous = (maxCol - minCol + 1) == count;
            }
            else
            {
                var minRow = selected.Min(c => c.Row);
                var maxRow = selected.Max(c => c.Row);
                contiguous = (maxRow - minRow + 1) == count;
            }

            if (!contiguous)
            {
                StatusText = $"{CurrentSetter.Name}: selected cells must form a contiguous block.";
                return;
            }

            // create ship
            var shipName = string.IsNullOrWhiteSpace(ShipNameInput) ? $"Ship {ships.Count + 1}" : ShipNameInput.Trim();
            var ship = new Ship { Name = shipName };
            foreach (var c in selected)
            {
                c.ShipName = shipName;
                ship.Cells.Add(c);
            }
            ships.Add(ship);

            // clear input
            ShipNameInput = string.Empty;
            StatusText = $"{CurrentSetter.Name}: created \"{shipName}\" ({ship.Cells.Count} cells). { ships.Count}/{ShipsToDeploy} ships done.";
            RefreshGridForCurrentStage();

            // notify commands & bindings
            OnPropertyChanged(nameof(IsPlayer1PlacementComplete));
            OnPropertyChanged(nameof(IsPlayer2PlacementComplete));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private bool CanCreateShip()
        {
            if (CurrentSetter == null) return false;
            var ships = CurrentSetter == Player1 ? _player1Ships : _player2Ships;
            if (ships.Count >= ShipsToDeploy) return false;
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
