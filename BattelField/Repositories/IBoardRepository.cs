using BattelField.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattelField.Repositories
{
    public interface IBoardRepository { List<Cell> GenerateBoard(); }
}
