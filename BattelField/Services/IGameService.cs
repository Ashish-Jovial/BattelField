using BattelField.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattelField.Services
{
    public interface IGameService { bool ProcessMove(Cell cell); }

    public class GameService : IGameService
    {
        public bool ProcessMove(Cell cell)
        {
            if (cell.IsHit) return false; // Already attacked
            cell.IsHit = true;
            return cell.HasShip; // Returns true if it was a "Hit"
        }
    }
}
