using System.Collections.Generic;

namespace BattelField.Models
{
    public class Ship
    {
        public string Name { get; set; }
        public List<Cell> Cells { get; } = new List<Cell>();
    }
}