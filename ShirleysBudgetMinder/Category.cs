using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;   // needed for INotifyPropertyChanged

namespace ShirleysBudgetMinder
{
    public class Category : INotifyPropertyChanged     // Need INotifyPropertyChanged to sync with Category totals
    {
        float newTotalAmount;
        public float NewTotalAmount
        {

            get { return newTotalAmount; }

            set
            {
                newTotalAmount = value;
                OnPropertyChanged("NewTotalAmount");
            }
        }

        public string Name { get; set; }
        public bool HaveTotalTextblock { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;  // Need PropertyChangedEventHandler to sync with Category totals
        void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }

    }
}
