using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;
using BattelField.Models;
using BattelField.Services;
using BattelField.ViewModel;

namespace BattelField.Converters
{
    public class CellToBrushConverter : IValueConverter
    {
        private static readonly Brush Unknown = new SolidColorBrush(Color.FromRgb(52,73,94)); // dark
        private static readonly Brush Miss = new SolidColorBrush(Color.FromRgb(52,152,219)); // blue
        private static readonly Brush Hit = new SolidColorBrush(Color.FromRgb(231,76,60)); // red
        private static readonly Brush PlacementEmpty = new SolidColorBrush(Color.FromRgb(44,62,80)); // slate

        // A small palette for ship colors - will cycle by ShipName hash
        private static readonly Brush[] ShipPalette = new Brush[]
        {
            new SolidColorBrush(Color.FromRgb(46,204,113)), // green
            new SolidColorBrush(Color.FromRgb(155,89,182)), // purple
            new SolidColorBrush(Color.FromRgb(241,196,15)), // yellow
            new SolidColorBrush(Color.FromRgb(230,126,34)), // orange
            new SolidColorBrush(Color.FromRgb(52,152,219)), // light blue
            new SolidColorBrush(Color.FromRgb(231,76,60)),  // red
        };

        private GameStage Stage { get; set; }

        // Add these fields to the class to resolve the missing context
        private PlayerViewModel CurrentSetter { get; set; }
        private PlayerViewModel Player1 { get; set; }
        private PlayerViewModel Player2 { get; set; }
        private PlayerViewModel CurrentShooter { get; set; }
        private List<Ship> _player1Ships { get; set; }
        private List<Ship> _player2Ships { get; set; }
        private GameService _gameService { get; set; }
        private string StatusText { get; set; }
        private bool IsGameOver { get; set; }
        private int ShotCount { get; set; }
        private System.Windows.Threading.DispatcherTimer _timer { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Cell cell) return Unknown;

            // If cell has been hit: show hit/miss explicitly
            if (cell.IsHit)
            {
                return cell.HasShip ? Hit : Miss;
            }

            // In placement mode: show ship blocks by ShipName if assigned, or muted if not
            if (cell.IsPlacementMode)
            {
                if (!string.IsNullOrEmpty(cell.ShipName))
                {
                    int idx = Math.Abs(cell.ShipName.GetHashCode()) % ShipPalette.Length;
                    return ShipPalette[idx];
                }

                return PlacementEmpty;
            }

            // Default during play: unknown / fog of war
            return Unknown;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();

        // Replace existing ExecuteGridClick method body with this implementation
        private void ExecuteGridClick(object parameter)
        {
            if (!(parameter is Cell clicked)) return;

            if (Stage == GameStage.Player1Setup || Stage == GameStage.Player2Setup)
            {
                if (CurrentSetter == null) return;

                // Find the board cell reference
                var own = CurrentSetter.Board.First(c => c.Row == clicked.Row && c.Column == clicked.Column);

                // Toggle selection intention
                bool newValue = !own.HasShip;

                if (newValue)
                {
                    var board = CurrentSetter.Board;
                    // currently selected unassigned cells (before adding this click)
                    var selected = board.Where(c => c.HasShip && string.IsNullOrEmpty(c.ShipName)).ToList();
                    // include the new cell candidate
                    selected.Add(own);

                    // enforce all in same row OR all in same column
                    bool allSameRow = selected.All(c => c.Row == selected.First().Row);
                    bool allSameCol = selected.All(c => c.Column == selected.First().Column);
                    if (!allSameRow && !allSameCol)
                    {
                        StatusText = $"{CurrentSetter.Name}: selection must be entirely horizontal or vertical.";
                        return;
                    }

                    // check contiguous and length limit (max 6)
                    int count = selected.Count;
                    if (count > 6)
                    {
                        StatusText = $"{CurrentSetter.Name}: ship cannot be longer than 6 cells.";
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

                    // ensure within 10x10 bounds (assumes 0..9 indices)
                    bool withinBounds = selected.All(c => c.Row >= 0 && c.Row <= 9 && c.Column >= 0 && c.Column <= 9);
                    if (!withinBounds)
                    {
                        StatusText = $"{CurrentSetter.Name}: selection out of board bounds (max 10x10).";
                        return;
                    }
                }

                // apply toggle
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

        // Add stubs for methods used in ExecuteGridClick
        private PlayerViewModel GetOpponent(PlayerViewModel player) => null;
        private int CountShips(IEnumerable<Cell> board) => 0;
        private void RefreshGridForCurrentStage() { }
        private void TrySaveScore() { }
    }
}