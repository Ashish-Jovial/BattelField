using BattelField.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattelField.Models
{
    public class Player : BaseViewModel
    {
        public string Name { get; set; }
        public ObservableCollection<Cell> Board { get; set; }
        public int Score { get; set; }
        public bool IsActive { get; set; } // Tracks whose turn it is
    }
}
