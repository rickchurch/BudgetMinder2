using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShirleysBudgetMinder
{
    class Transaction
    {
        public int TransId = 0;
        public string Date = string.Empty;
        public string Payee = string.Empty;
        public string Category = string.Empty;
        public float Amount = 0;
        public string Notes = string.Empty;
        public string TransMonth = string.Empty;  //  should look something like  201302  (for Feb 2013)
    }
}
