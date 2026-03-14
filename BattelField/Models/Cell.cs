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
            set { _hasShip = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); }
        }

        // new: ship grouping name (empty when unassigned)
        private string _shipName;
        public string ShipName
        {
            get => _shipName;
            set { _shipName = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); }
        }

        // new: reveal mode used during placement so placed cells show yellow
        private bool _isPlacementMode;
        public bool IsPlacementMode
        {
            get => _isPlacementMode;
            set { _isPlacementMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); }
        }

        // Logic for UI color:
        // - During placement show placed ship cells as yellow.
        // - Otherwise: Gray = Unknown, Red = Hit, Blue = Miss
        public string StatusColor
        {
            get
            {
                if (IsPlacementMode && HasShip) return "#F1C40F"; // yellow during placement
                if (!IsHit) return "#34495E";
                return HasShip ? "#E74C3C" : "#3498DB";
            }
        }
    }
}
