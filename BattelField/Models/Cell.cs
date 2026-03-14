using BattelField.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattelField.Models
{
    public class Cell : BaseViewModel
    {
        public int Row { get; set; }
        public int Column { get; set; }

        private bool _isHit;
        public bool IsHit
        {
            get => _isHit;
            set { _isHit = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); }
        }

        private bool _hasShip;
        public bool HasShip
        {
            get => _hasShip;
            set { _hasShip = value; OnPropertyChanged(); }
        }

        // Logic for UI color: Gray = Unknown, Red = Hit, Blue = Miss
        public string StatusColor => !IsHit ? "#34495E" : (HasShip ? "#E74C3C" : "#3498DB");
    }
}
