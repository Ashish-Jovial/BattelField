using BattelField.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattelField.Repositories
{
    public class RandomBoardRepository : IBoardRepository
    {
        private readonly Random _rng = new Random();
        private const int BoardSize = 10;

        public List<Cell> GenerateBoard()
        {
            // 1. Initialize empty grid
            var grid = new List<Cell>();
            for (int r = 0; r < BoardSize; r++)
                for (int c = 0; c < BoardSize; c++)
                    grid.Add(new Cell { Row = r, Column = c });

            // 2. Define fleet (Ship Lengths)
            int[] shipLengths = { 5, 4, 3, 3, 2 };

            foreach (int length in shipLengths)
            {
                bool placed = false;
                while (!placed)
                {
                    int startRow = _rng.Next(0, BoardSize);
                    int startCol = _rng.Next(0, BoardSize);
                    bool isHorizontal = _rng.Next(0, 2) == 0;

                    if (CanPlaceShip(grid, startRow, startCol, length, isHorizontal))
                    {
                        PlaceShip(grid, startRow, startCol, length, isHorizontal);
                        placed = true;
                    }
                }
            }
            return grid;
        }

        private bool CanPlaceShip(List<Cell> grid, int row, int col, int length, bool horizontal)
        {
            for (int i = 0; i < length; i++)
            {
                int r = horizontal ? row : row + i;
                int c = horizontal ? col + i : col;

                // Check boundaries
                if (r >= BoardSize || c >= BoardSize) return false;

                // Check if cell already has a ship
                if (grid.First(x => x.Row == r && x.Column == c).HasShip) return false;
            }
            return true;
        }

        private void PlaceShip(List<Cell> grid, int row, int col, int length, bool horizontal)
        {
            for (int i = 0; i < length; i++)
            {
                int r = horizontal ? row : row + i;
                int c = horizontal ? col + i : col;
                grid.First(x => x.Row == r && x.Column == c).HasShip = true;
            }
        }
    }
}
