using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BattelField.Models
{
    public class Unit : INotifyPropertyChanged
    {
        private int _hp;
        public string Name { get; set; }
        public int MaxHp { get; set; }
        public int HP
        {
            get => _hp;
            set { _hp = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
