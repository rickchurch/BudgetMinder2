using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ShirleysBudgetMinder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string mLocalPath = string.Empty;
        string mLogFile = string.Empty;
        Dictionary<string, string> mPayeeDict = new Dictionary<string, string>();
        List<string> mCategoriesList = new List<string>();
        int mTransActnNumber = 0;
        string mTransactionDate = string.Empty;
        string mPayee = string.Empty;
        string mCategory = string.Empty;
        string mMonth = string.Empty;
        string mMonthEndingDate = string.Empty;
        List<string> mMonthsAvailList = new List<string>{
            "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"};
        float mAmount = 0;
        const string DEFAULT_PAYEE_CAPTION = "Enter or select Payee";
        const string DEFAULT_CATEGORY_CAPTION = "Enter or select Category";
        const string DEFAULT_MONTH_CAPTION = "Select Month";
        StringBuilder mHistory = null;

        public MainWindow()
        {
            mLocalPath = "C:\\ProgramData\\Rick\\SureBudgetMinder\\";

            DateTime now = DateTime.Now;
            string showDate = now.ToString("yyyyMMdd");
            string logName = string.Format("{0}_SbmLog.txt", showDate);

            mLogFile = System.IO.Path.Combine(mLocalPath, logName);
            if (!Directory.Exists(mLocalPath))
            {
                Directory.CreateDirectory(mLocalPath);
            }

            try
            {
                StreamWriter sw = new StreamWriter(mLogFile, true);
                sw.WriteLine("---------------------------------------------------------------------------------------------------");
                sw.WriteLine("New Process started at " + now);
                sw.WriteLine("---------------------------------------------------------------------------------------------------");
                sw.Close();
            }
            catch (Exception e)
            {
                //tbStatus.Text = "Error occured in constructor: " + e.ToString();
            }

            InitializeComponent();  // builds the user interface

            LoadInitData();
            comboBoxPayee.SelectedItem = DEFAULT_PAYEE_CAPTION;
            comboBoxCategory.SelectedItem = DEFAULT_CATEGORY_CAPTION;
            comboBoxMonth.SelectedItem = DEFAULT_MONTH_CAPTION;
            mHistory = new StringBuilder();

            // get next transaction number from DB
            mTransActnNumber = 101;

            tbTrnNum.Text = mTransActnNumber.ToString();

            //lbl_HistoryHdr.Content = "   RecordID               Date                          Payee                              Category                     Amount";
            ////////////////////////////////////////////////////Date                                Payee                   Category                  Amount"
        }  

        private void comboBoxPayee_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string methodName = "comboBoxPayee_SelectionChanged";
            string payeeSelected = string.Empty;

            if (comboBoxPayee.SelectedItem == null || comboBoxPayee.SelectedItem.ToString() == "System.Windows.Controls.ComboBoxItem")
            {
                payeeSelected = "SYSTEM Event";
                Log(methodName, string.Format("Payee selected: {0} - Ignore", payeeSelected));
            }
            else if (comboBoxPayee.SelectedItem.ToString() == DEFAULT_PAYEE_CAPTION)
            {
                Log(methodName, string.Format("Payee selected: {0} - Ignore", comboBoxPayee.SelectedItem.ToString()));
            }
            else
            {
                payeeSelected = comboBoxPayee.SelectedItem.ToString();
                Log(methodName, string.Format("Payee selected: {0}", payeeSelected));
                if (mPayeeDict.ContainsKey(payeeSelected))
                {
                    if (mPayeeDict[payeeSelected] != "")  // payee has an associate category
                    {
                        comboBoxCategory.SelectedItem = mPayeeDict[payeeSelected];
                    }
                    else   // payee has NO associated category
                    {
                        comboBoxCategory.SelectedItem = DEFAULT_CATEGORY_CAPTION;
                    }
                }
                else // I have a new payee
                {
                    // add new payee to dict - reload or add to both comboBoxs ???
                    comboBoxCategory.SelectedItem = DEFAULT_CATEGORY_CAPTION;
                }
                mPayee = payeeSelected;
            }
        }

        private void comboBoxCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string methodName = "comboBoxCategory_SelectionChanged";

            if (comboBoxCategory.SelectedItem == null || comboBoxCategory.SelectedItem.ToString() == "System.Windows.Controls.ComboBoxItem")
            {
                Log(methodName, string.Format("SYSTEM Event - Ignore"));
            }
            else if (comboBoxCategory.SelectedItem.ToString() == DEFAULT_CATEGORY_CAPTION)
            {
                Log(methodName, string.Format("Category selected: {0} - Ignore", comboBoxCategory.SelectedItem.ToString()));
            }
            else
            {
                mCategory = comboBoxCategory.SelectedItem.ToString();
                Log(methodName, string.Format("Category selected: {0}", mCategory));
            }
        }

        void LoadInitData()
        {
            string methodName = "LoadPayeeData";
            bool gotDbData = false;
            Dictionary<string, string> dbData = new Dictionary<string, string>();

            if (!gotDbData)
            {
                // this is test data
                dbData.Add("Maverik", "Gas");
                dbData.Add("Winco", "Groceries");
                dbData.Add("Walmart", "");
                dbData.Add("Fiesta Guadalahara", "Dining Out");
            }

            comboBoxPayee.Items.Add(DEFAULT_PAYEE_CAPTION);
            comboBoxCategory.Items.Add(DEFAULT_CATEGORY_CAPTION);

            // Load Payee and Category ComboBoxes
            foreach (KeyValuePair<string, string> pair in dbData)
            {
                if (!mPayeeDict.ContainsKey(pair.Key))
                {
                    mPayeeDict.Add(pair.Key, pair.Value);
                    comboBoxPayee.Items.Add(pair.Key);
                    Log(methodName, string.Format("Adding Payee: {0}    Category: {1}", pair.Key, pair.Value));
                }

                if (!mCategoriesList.Contains(pair.Value) && pair.Value != "")
                {
                    mCategoriesList.Add(pair.Value);
                    comboBoxCategory.Items.Add(pair.Value);
                }
            }

            // Load Month ComboBox
            comboBoxMonth.Items.Add(DEFAULT_MONTH_CAPTION);
            foreach (string month in mMonthsAvailList)
            {
                comboBoxMonth.Items.Add(month);
            }
            
        }

        private void comboBoxMonth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string methodName = "comboBoxMonth_SelectionChanged";
            string monthSelected = string.Empty;

            if (comboBoxCategory.SelectedItem == null)
            {
                monthSelected = "SYSTEM Event";
                Log(methodName, string.Format("SYSTEM Event - Ignore"));
            }
            else if (mMonthsAvailList.Contains(comboBoxMonth.SelectedItem.ToString()))
            {
                mMonth = comboBoxMonth.SelectedItem.ToString();
                Log(methodName, string.Format("Month selected: {0}", mMonth));
            }
            else
            {
                Log(methodName, string.Format("SYSTEM Event - selection: {0}", comboBoxMonth.SelectedItem.ToString()));
            }
        }

        private void dpTran_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            string methodName = "dpTran_SelectedDateChanged";
            string[] partsDate = dpTran.SelectedDate.ToString().Split(' ');
            mTransactionDate = partsDate[0];
            Log(methodName, string.Format("Date selected: {0}", mTransactionDate));
            tbDate.Text = mTransactionDate;
        }

        private void dpMonthEnd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            string methodName = "dpMonthEnd_SelectedDateChanged";
            string[] partsDate = dpMonthEnd.SelectedDate.ToString().Split(' ');
            mMonthEndingDate = partsDate[0];
            Log(methodName, string.Format("Date selected: {0}", mMonthEndingDate));
            tbMonthEndingDate.Text = mMonthEndingDate;
        }

        private void btnDisplayReport_Click(object sender, RoutedEventArgs e)
        {
            string methodName = "btnDisplayReport_Click";
            Log(methodName, string.Format("Enterring {0}  ++++++++++++++++++", methodName));
            MessageBox.Show("The report function is not yet built.");
        }

        private void btnSetMoEndDate_Click(object sender, RoutedEventArgs e)
        {
            string methodName = "btnSetMoEndDate_Click";
            Log(methodName, string.Format("Enterring {0}  ++++++++++++++++++", methodName));
            MessageBox.Show("The set date function is not yet built.");
        }

        private void btnSetDate_Click(object sender, RoutedEventArgs e)
        {
            string methodName = "btnSetDate_Click";
            Log(methodName, string.Format("Enterring {0}  ++++++++++++++++++", methodName));
            MessageBox.Show("The set date function is not yet built.");
        }

        private void btnSaveRec_Click(object sender, RoutedEventArgs e)
        {
            string methodName = "btnSaveRec_Click";
            Log(methodName, string.Format("Enterring {0}  ++++++++++++++++++", methodName));

            // if manually enterred payee - get it
            if (comboBoxPayee.SelectedItem == null && comboBoxPayee.Text != "")
            {
                Log(methodName, string.Format("Using manually enterred Payee: [{0}] instead of  [{1}]", comboBoxPayee.Text, mPayee));
                mPayee = comboBoxPayee.Text;
            }
            else if (comboBoxPayee.SelectedItem == null && comboBoxPayee.Text == "")
            {
                Log(methodName, string.Format("\nERROR - Don't have a value for Payee\n"));
                mPayee = "Error";
            }

            if (comboBoxCategory.SelectedItem == null)
            {
                if (comboBoxCategory.Text != "")
                {
                    mCategory = comboBoxCategory.Text;
                }
                else
                {
                    Log(methodName, string.Format("\nERROR - Don't have a value for Category\n"));
                    mCategory = "Error";
                }
            }

            if (mMonth == "")
            {
                Log(methodName, string.Format("\nERROR - Don't have a Month selected\n"));
            }

            mAmount = float.Parse(tbAmt.Text, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);

            string payeePad = string.Empty;
            if (mPayee.Length < 30) // assumes payee names will be no more than 25 - those oddball ones will just show up a little wierd
            {
                //double payeePadCnt = (25 - mPayee.Length)*1.3;
                //payeePad = new String(' ', Convert.ToInt16(payeePadCnt));
                if (mPayee.Length > 11)
                {
                    payeePad = new String(' ', Convert.ToInt16(2));
                }
                else
                {
                    payeePad = new String(' ', Convert.ToInt16(6));
                }
            }
            string line = "";
            bool flagError = false;

            // DATA VALIDATION

            if (mPayee == "" || mCategory == "" || mAmount == 0 || mMonth == "")
            {
                line = string.Format("\n ERROR !!      Payee: {0}       Category: {1}      Amount: {2}\n", mPayee.PadRight(25), mCategory.PadRight(20), tbAmt.Text);
                flagError = true;
            }
            else
            {
                line = string.Format(" Saving::    {0}         {1}             {2,-25} {3}         {4,-20}  {3}        {5}",
                                                                  mTransActnNumber, tbDate.Text, mPayee, payeePad, mCategory, tbAmt.Text);
            }
            Log(methodName, string.Format("\n {0}\n", line));
            mHistory.AppendLine(line);
            //tblkHistory.Text = mHistory.ToString();
            //scrollHistory.ScrollToBottom();
            

            if (!flagError)
            {
                UpdateHistory(mTransActnNumber.ToString(), tbDate.Text, mPayee, mCategory, tbAmt.Text);

                // SAVE DATA

                // reset data
                mTransActnNumber++;  // don't increment this unless we have VALID data that is saved to db
                mPayee = "";
                mCategory = "";
                mAmount = 0;
                comboBoxPayee.SelectedItem = DEFAULT_PAYEE_CAPTION;
                comboBoxCategory.SelectedItem = DEFAULT_CATEGORY_CAPTION;
                tbAmt.Text = "0.00";
                tbNote.Text = "Notes";
                tbTrnNum.Text = mTransActnNumber.ToString();
            }
            else
            {
                // Reset nothing ??

                UpdateHistory(line);
            }

        }

        private void UpdateHistory(string tranNo, string date, string payee, string category, string amt)
        {
            StackPanel spHist = new StackPanel();
            spHist.Orientation = Orientation.Horizontal;

            TextBlock tbTranNo = new TextBlock();
            tbTranNo.Text = tranNo;
            //tbTranNo.Margin = Thickness.ReferenceEquals
            tbTranNo.Width = 40;
            tbTranNo.TextAlignment = TextAlignment.Left;

            TextBlock tbDate = new TextBlock();
            tbDate.Text = date;
            tbDate.Width = 100;
            tbDate.TextAlignment = TextAlignment.Left;

            TextBlock tbPayee = new TextBlock();
            tbPayee.Text = payee;
            tbPayee.Width = 200;
            tbPayee.TextAlignment = TextAlignment.Left;

            TextBlock tbCategory = new TextBlock();
            tbCategory.Text = category;
            tbCategory.Width = 150;
            tbCategory.TextAlignment = TextAlignment.Left;

            TextBlock tbAmt = new TextBlock();
            tbAmt.Text = amt;
            tbAmt.Width = 80;
            tbAmt.TextAlignment = TextAlignment.Left;

            spHist.Children.Add(tbTranNo);
            spHist.Children.Add(tbDate);
            spHist.Children.Add(tbPayee);
            spHist.Children.Add(tbCategory);
            spHist.Children.Add(tbAmt);

            wpHistory.Children.Add(spHist);
            scrollHistory.ScrollToBottom();
        }

        private void UpdateHistory(string error)
        {
            StackPanel spHist = new StackPanel();
            spHist.Orientation = Orientation.Horizontal;

            TextBlock tbError = new TextBlock();
            tbError.Text = error;
            //tbTranNo.Margin = Thickness.ReferenceEquals
            tbError.Width = 500;
            tbError.TextAlignment = TextAlignment.Left;

            spHist.Children.Add(tbError);
            wpHistory.Children.Add(spHist);
            scrollHistory.ScrollToBottom();
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            string methodName = "btnExit_Click";
            Log(methodName, string.Format("\n User request - Shutting down.\n"));
            Application.Current.Shutdown();
        }





        /// <summary>
        /// Writes a line in mLogFile.  Line begins with current timestamp, then calling method, finally the passed in msg to log
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="msg"></param>
        private void Log(string methodName, string msg)
        {
            try
            {
                StreamWriter sw = new StreamWriter(mLogFile, true);
                DateTime now = DateTime.Now;
                string time = now.ToString("HH:mm:ss");
                sw.WriteLine("{0} {1} - {2}", time, methodName, msg);
                sw.Close();
            }
            catch (Exception e)
            {
                //
            }
        }

    }
}
