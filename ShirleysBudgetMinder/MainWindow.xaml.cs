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
using System.Globalization;
using System.ComponentModel;  // for SortDescription
using System.Media;
using System.Diagnostics;  // for Process.start

namespace ShirleysBudgetMinder
{
    // TODO  List
    //      MySQL data storage
    //      temp save data to text file
    //      generate report for current month
    //      modify transaction/entry
    //      add button to open log file
    //      Nov 13, 2013 Add validation to not allow apostrophy in any name - causes fatal error ::: Fixed 1/5/14 so that apostrophy works
    // May 2 - items added;  load Payee & Category comboBtn using all transactions in DB

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool mColor;
        string mLocalPath = string.Empty;
        string mLogFile = string.Empty;

        // General data object tools
        List<Transaction> mTransactionList = new List<Transaction>();
        Dictionary<string, string> mPayeeDict = new Dictionary<string, string>();
        public Dictionary<string, Category> mCategoryDict = new Dictionary<string, Category>();
        Dictionary<string, string> mMonthSelectDict = new Dictionary<string, string>{
            {"01","January"}, {"02","February"}, {"03","March"}, {"04","April"}, {"05","May"}, {"06","June"}
            , {"07","July"}, {"08","August"}, {"09","September"}, {"10","October"}, {"11","November"}, {"12","December"}};

        // Transaction items
        int mTransActnNumber = 0;
        string mPayee = string.Empty;
        string mCategory = string.Empty;
        string mMonthEndingDate = string.Empty;
        string mTransactionDate = string.Empty;
        float mAmount = 0;

        const string DEFAULT_PAYEE_CAPTION = "Enter or select Payee";
        const string DEFAULT_CATEGORY_CAPTION = "Enter or select Category";

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

            mColor = false;

            EnableControls(false);  // Need user to first select the transaction month we are using and then we'll enable controls

            // Bind tbMonthEndingDate text box to datePicker control so we can get yyyy-MMMM-dd formatting (seemed to be the practical way to get that format)
            Binding binding = new Binding();  //
            binding.Source = dpMonthEnd;
            binding.Path = new PropertyPath("SelectedDate");
            binding.StringFormat = "yyyy-MMMM-dd"; // the key item to make this work so I get correct format without lots of extra code
            binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            BindingOperations.SetBinding(tbMonthEndingDate, TextBox.TextProperty, binding);  //

            // Bind tbTranDate text box to datePicker control dpTran so we can get yyyy-MMMM-dd formatting (seemed to be the practical way to get that format)
            binding = new Binding();  //
            binding.Source = dpTran;
            binding.Path = new PropertyPath("SelectedDate");
            binding.StringFormat = "yyyy-MMMM-dd"; // the key item to make this work so I get correct format without lots of extra code
            binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            BindingOperations.SetBinding(tbTranDate, TextBox.TextProperty, binding);

            textBlk_StartFlag.Foreground = Brushes.Red;
            textBlk_StartFlag.Text = "<---------  ";
        }

        private void EnableControls(bool enable)
        {
            comboBoxPayee.IsEnabled = enable;
            comboBoxCategory.IsEnabled = enable;
            tbAmt.IsEnabled = enable;
            btnSaveRec.IsEnabled = enable;
            btnDisplayReport.IsEnabled = enable;
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
                    if (mPayeeDict[payeeSelected] != "")  // payee has an associated category
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

        void LoadInitData(string monthEndingDate, string sortBy="TransId")
        {
            string methodName = "LoadInitData";
            try
            {
                Log(methodName, string.Format(" Entering . . .    Sort by [{0}]", sortBy));

                // reset / clear History panel, Category panel, and all primary objects
                wpHistory.Children.Clear();
                wpCategoryTotals.Children.Clear();
                mTransactionList = new List<Transaction>();
                mCategoryDict = new Dictionary<string, Category>();
                mPayeeDict = new Dictionary<string, string>();

                List<Transaction> tranList = new List<Transaction>();
                //
                //  GET CURRENT MONTH TRANSACTIONS FROM DATABASE     tranList = Select * from TRANSACTIONS where TransMonth = <transMonthSelected>
                // 
                DataStorage data = new DataStorage();
                tranList = data.GetData(string.Format("TransMonth={0}", monthEndingDate));

                if (sortBy == "Date")
                {
                    Log(methodName, string.Format("Sorting by Date before displaying all transactions."));
                    // found this line of code on SO to sort a list of objects
                    tranList = tranList.OrderBy(o => o.Date).ToList();
                }
                else if (sortBy == "TransId")
                {
                    Log(methodName, string.Format("Sorting by TransId before displaying all transactions."));
                    // found this line of code on SO to sort a list of objects
                    tranList = tranList.OrderBy(o => o.TransId).ToList();
                }
                else if (sortBy == "Payee")
                {
                    Log(methodName, string.Format("Sorting by Payee before displaying all transactions."));
                    // found this line of code on SO to sort a list of objects
                    tranList = tranList.OrderBy(o => o.Payee).ToList();
                }

                //if (tranList.Count == 0)
                if (false)
                {
                    // for testing purposes, populate dummy data
                    Transaction tmpTrans = new Transaction() { TransId = 101, Date = "2013-02-13", Payee = "Maverik", Category = "Gas", Amount = (float)32.24, Notes = "86 Honda", TransMonth = "201302" };
                    tranList.Add(tmpTrans);
                    tmpTrans = new Transaction() { TransId = 102, Date = "2013-02-13", Payee = "Winco", Category = "Groceries", Amount = (float)132.24, Notes = "", TransMonth = "201302" };
                    tranList.Add(tmpTrans);
                    tmpTrans = new Transaction() { TransId = 103, Date = "2013-02-17", Payee = "Walmart", Category = "Clothes", Amount = (float)43.19, Notes = "Natalia", TransMonth = "201302" };
                    tranList.Add(tmpTrans);
                    tmpTrans = new Transaction() { TransId = 104, Date = "2013-02-18", Payee = "Maverik", Category = "Gas", Amount = (float)49.02, Notes = "", TransMonth = "201302" };
                    tranList.Add(tmpTrans);
                    tmpTrans = new Transaction() { TransId = 105, Date = "2013-02-21", Payee = "Fiesta Guadalahara", Category = "Dining Out", Amount = (float)132.24, Notes = "", TransMonth = "201302" };
                    tranList.Add(tmpTrans);
                    tmpTrans = new Transaction() { TransId = 106, Date = "2013-02-23", Payee = "Delsa's", Category = "Dining Out", Amount = (float)12.29, Notes = "", TransMonth = "201302" };
                    tranList.Add(tmpTrans);
                    tmpTrans = new Transaction() { TransId = 107, Date = "2013-02-23", Payee = "Schucks", Category = "Automotive Repair", Amount = (float)29.99, Notes = "", TransMonth = "201302" };
                    tranList.Add(tmpTrans);
                    tmpTrans = new Transaction() { TransId = 108, Date = "2013-02-23", Payee = "Guido's", Category = "Dining Out", Amount = (float)14.32, Notes = "", TransMonth = "201302" };
                    tranList.Add(tmpTrans);
                }

                //
                // get payee and category info from database
                // 
                LoadPayeeAndCategoryComboBtns();


                int maxId = 0;
                foreach (Transaction trans in tranList)
                {
                    UpdateHistoryPanel(trans.TransId.ToString(), trans.Date, trans.Payee, trans.Category, trans.Amount.ToString("0.00"), trans.Notes);
                    UpdateCategoryTotals(trans.Category, trans.Amount);
                    mTransactionList.Add(trans);
                    if (trans.TransId > maxId) maxId = trans.TransId;
                }

                comboBoxPayee.SelectedItem = DEFAULT_PAYEE_CAPTION;
                comboBoxCategory.SelectedItem = DEFAULT_CATEGORY_CAPTION;

                //
                // get next transaction number from DB  (Select max(TransId) from Transactions)  and then increment by one
                //
                mTransActnNumber = maxId + 1;
                tbTrnNum.Text = mTransActnNumber.ToString();

                lbl_HistoryHdr.Content = " ID        Date                          Payee                               Category                   Amount                 Notes";
                lbl_HistoryHdr.Background = Brushes.Aquamarine;
            }
            catch (Exception ex)
            {
                Log(methodName, string.Format("\n {0}   ERROR !! Error msg: {1}     stack trace: {2}", methodName, ex.Message, ex.StackTrace));
                MessageBox.Show("Fatal error !  Rick needs to look at logs - C:\\ProgramData\\Rick\\SureBudgetMinder", "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.None);
            }
        }

        private string AddPayee(string payee, string category)
        {
            string methodName = "AddPayee";
            TextInfo myTI = new CultureInfo("en-US",false).TextInfo;
            payee = myTI.ToTitleCase(payee);
            payee = payee.Trim();
            if (!mPayeeDict.ContainsKey(payee))
            {
                mPayeeDict.Add(payee, category);

                Log(methodName, string.Format("Adding NEW Payee: {0}    Category: {1}", payee, category));

                // SORT the comboBox items
                comboBoxPayee.Items.Clear();
                List<string> tmpPayeeList = new List<string>();
                tmpPayeeList = mPayeeDict.Keys.ToList();
                tmpPayeeList.Sort();
                comboBoxPayee.Items.Add(DEFAULT_PAYEE_CAPTION);
                foreach (string p in tmpPayeeList)
                {
                    comboBoxPayee.Items.Add(p);
                }
                Log(methodName, string.Format("comboBoxPayee was cleared, sorted and reloaded.  comboBoxPayee.Items.Count: {0}", comboBoxPayee.Items.Count));
            }

            return payee;
        }

        private string AddCategory(string categoryAdd)
        {
            string methodName = "AddCategory";

            TextInfo myTI = new CultureInfo("en-US", false).TextInfo;
            categoryAdd = myTI.ToTitleCase(categoryAdd);
            categoryAdd = categoryAdd.Trim();

            if (!mCategoryDict.ContainsKey(categoryAdd) && categoryAdd != "")
            {
                Category category = new Category();
                category.Name = categoryAdd;
                category.NewTotalAmount = 0;
                category.HaveTotalTextblock = false;
                mCategoryDict.Add(categoryAdd, category);
                Log(methodName, string.Format("Adding NEW Category: {0}", categoryAdd));

                // Sort the comboBox items
                comboBoxCategory.Items.Clear();
                List<string> tmpCatList = new List<string>();
                tmpCatList = mCategoryDict.Keys.ToList();
                tmpCatList.Sort();
                comboBoxCategory.Items.Add(DEFAULT_CATEGORY_CAPTION);
                foreach (string cat in tmpCatList)
                {
                    comboBoxCategory.Items.Add(cat);
                }
            }
            return categoryAdd;
        }

        private void dpTran_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            string methodName = "dpTran_SelectedDateChanged";
            //string[] partsDate = dpTran.SelectedDate.ToString().Split(' ');
            //mTransactionDate = partsDate[0];
            Log(methodName, string.Format("Date selected: {0}", dpTran.SelectedDate.ToString()));
        }

        private void dpMonthEnd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            string methodName = "dpMonthEnd_SelectedDateChanged";
            //string[] partsDate = dpMonthEnd.SelectedDate.ToString().Split(' ');
            //mMonthEndingDate = partsDate[0];
            Log(methodName, string.Format("Date selected: {0}", dpMonthEnd.SelectedDate.ToString()));
        }

        private void tbTranDate_TextChanged(object sender, RoutedEventArgs e)
        {
            //  TextBox tbTranDate  is binded to dataPicker dpTran (See Constructor). dataPicker dpTran will return M/D/YYYY.  The binding sets 
            //    format for tbTranDate to YYYY-MMMMM_DD  which returns the full month name - easier for User to read.  But we want mTransactionDate (used 
            //    as database value saved and other controlling logic) to be in the format of YYYY-MM-DD  where MM is the month number (ie: 'March' translates to '03').
            string methodName = "tbTranDate_TextChanged";
            Log(methodName, string.Format("Entered date: {0}", tbTranDate.Text));

            mTransactionDate = ConvertDisplayDateToShort(tbTranDate.Text);
            if (mTransactionDate == "error")
            {
                mTransactionDate = "";
            }
            else
            {
                Log(methodName, string.Format("mTransactionDate Validated - set to: {0}    Display value: {1}", mTransactionDate, tbTranDate.Text));
            }
        }


        private void tbMonthEndingDate_TextChanged(object sender, RoutedEventArgs e)
        {
            //  TextBox tbMonthEndingDate  is binded to dataPicker dpMonthEnd (See Constructor). dataPicker dpMonthEnd will return M/D/YYYY.  The binding sets 
            //    format for tbMonthEndingDate to YYYY-MMMMM_DD  which returns the full month name - easier for User to read.  But we want mMonthEndingDate (used 
            //    as database value saved and other controlling logic) to be in the format of YYYY-MM-DD  where MM is the month number (ie: 'March' translates to '03').
            string methodName = "tbMonthEndingDate_TextChanged";
            Log(methodName, string.Format("Entered Month ending date: {0}", tbMonthEndingDate.Text));

            mMonthEndingDate = ConvertDisplayDateToShort(tbMonthEndingDate.Text);
            if (mMonthEndingDate == "error")
            {
                mMonthEndingDate = "";
                EnableControls(false);
            }
            else
            {
                Log(methodName, string.Format("mMonthEndingDate Validated - set to: {0}    Display value: {1}", mMonthEndingDate, tbMonthEndingDate.Text));
                textBlk_StartFlag.Text = "";
                LoadInitData(mMonthEndingDate);
                EnableControls(true);
            }
        }

        private string ConvertDisplayDateToShort(string displayDate)
        {
            string methodName = "ConvertDisplayDateToShort";
            Log(methodName, string.Format("Entered Month ending date: {0}", displayDate));
            displayDate = displayDate.Trim();
            string shortDate = "error";
            string dictKey = string.Empty;
            string[] dateParts = displayDate.Split('-');
            if (dateParts.Length > 1)
            {
                //Log(methodName, string.Format("dateParts[1]: {0}", dateParts[1]));   //  dateParts[1] will be like 'January' or 'May', etc.
                dictKey = mMonthSelectDict.FirstOrDefault(x => x.Value == dateParts[1]).Key;  // returns Dict key where Dict value = dateParts[1]
                //Log(methodName, string.Format("Found mMonthSelectDict key: {0}", dictKey));
            }
            if ((displayDate.Length > 10 && displayDate.Length < 18) && displayDate != "YYYY-MMMM-DD")
            {
                string tmpDate = displayDate.Replace(dateParts[1], dictKey);  // tmpDate looks like YYYY-MM-DD
                if (ValidateDate(tmpDate))  // VALIDATE date value
                {
                    shortDate = tmpDate;
                }
                else
                {
                    Log(methodName, string.Format(" FAIL -Invalid date:: {0}", displayDate));
                }
            }
            else
            {
                Log(methodName, string.Format(" FAIL -Invalid date: {0}", displayDate));
            }
            return shortDate;
        }

        private bool ValidateDate(string dateToValidate)
        {
            string methodName = "ValidateDate";
            bool success = true;
            Log(methodName, string.Format("Date to evaluate: {0}", dateToValidate));
            try
            {
                string dateToEvaluate = dateToValidate.Substring(0, 4) + dateToValidate.Substring(5, 2) + dateToValidate.Substring(8, 2);
                string maxValidDate = "20181231";
                string minValidDate = "20130101";

                var enUS = new System.Globalization.CultureInfo("en-US");

                DateTime maxD = DateTime.ParseExact(maxValidDate, "yyyyMMdd", enUS);
                DateTime minD = DateTime.ParseExact(minValidDate, "yyyyMMdd", enUS);
                DateTime myD = DateTime.ParseExact(dateToEvaluate, "yyyyMMdd", enUS);

                if (DateTime.Compare(myD, maxD) > 0)
                {
                    Log(methodName, string.Format(" FAIL - {0} greater than {1}", dateToEvaluate, maxValidDate));   // date is greater than max date
                    success = false;
                }
                if (DateTime.Compare(myD, minD) < 0)
                {
                    Log(methodName, string.Format(" FAIL - {0} less than {1}", dateToEvaluate, minValidDate));  //  date is less than min
                    success = false;
                }
            }
            catch (Exception ex)
            {
                Log(methodName, string.Format(" errMsg: {0}", ex.Message));
                Log(methodName, string.Format(" FAIL -Invalid date: {0}", dateToValidate));
                success = false;
            }
            return success;
        }

        private void btnDisplayLog_Click(object sender, RoutedEventArgs e)
        {
            string methodName = "btnDisplayLog_Click";
            Log(methodName, string.Format("Enterring {0}  ++++++++++++++++++", methodName));
            try
            {
                Process.Start("notepad.exe", mLogFile);
            }
            catch (Exception ex)
            {
                Log(methodName, string.Format("ERROR trying to Open and Display Log file.  Err msg: {0}", ex.Message));
            }
        }

        private void btnDisplayReport_Click(object sender, RoutedEventArgs e)
        {
            string methodName = "btnDisplayReport_Click";

            try
            {
                Log(methodName, string.Format("Enterring {0}  ++++++++++++++++++", methodName));
                //MessageBox.Show("The report function is not yet built.");

                //  Create text file to write a copy to
                var desktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string rptName = string.Format("SBM_Report.txt");
                //string rptFile = System.IO.Path.Combine("C:\\", rptName);
                string rptFile = System.IO.Path.Combine(desktopFolder, rptName);
                if (File.Exists(rptFile))
                {
                    File.Delete(rptFile);
                }
                StreamWriter sw = new StreamWriter(rptFile, true);
                //
                //


                Report report = new Report();

                List<string> tranDateSortedList = new List<string>();
                foreach (Transaction tran in mTransactionList)
                {
                    if (!tranDateSortedList.Contains(tran.Date))
                    {
                        tranDateSortedList.Add(tran.Date);
                    }
                }
                tranDateSortedList.Sort();
                List<string> categorySortedList = new List<string>();
                categorySortedList = mCategoryDict.Keys.ToList();
                categorySortedList.Sort();
                foreach (string cat in categorySortedList)
                {
                    string cat_filtered = cat.Replace(' ', '_');
                    cat_filtered = cat_filtered.Replace("'", "");

                    //
                    //    Build Category Header line
                    //
                    StackPanel spCategoryHdr = new StackPanel();
                    spCategoryHdr.Name = string.Format("spCategoryHdr_{0}", cat_filtered);
                    spCategoryHdr.Orientation = Orientation.Horizontal;
                    spCategoryHdr.Width = 500;

                    TextBlock tbCategoryHdr = new TextBlock();
                    //tbTranNo.Name = string.Format("tbTranNo_{0}", tranNo);
                    tbCategoryHdr.Text = cat;
                    tbCategoryHdr.FontWeight = FontWeights.Bold;
                    tbCategoryHdr.FontSize = 14;
                    tbCategoryHdr.Margin = new Thickness(50, 30, 0, 0);
                    tbCategoryHdr.Width = 100;
                    tbCategoryHdr.TextAlignment = TextAlignment.Left;
                    //tbCategoryHdr.Background = brush;
                    tbCategoryHdr.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);

                    spCategoryHdr.Children.Add(tbCategoryHdr);
                    report.wpReport.Children.Add(spCategoryHdr);

                    //
                    sw.WriteLine(cat);

                    Log(methodName, string.Format("Looking for Category: {0}", cat));
                    foreach (string sortedDate in tranDateSortedList)
                    {
                        Log(methodName, string.Format("Looking for Category: {0} and Date: {1}", cat, sortedDate));
                        foreach (Transaction tran in mTransactionList)
                        {
                            if (tran.Category != cat) continue;
                            if (tran.Date != sortedDate) continue;

                            // Build row
                            //Log(methodName, string.Format("{0}   {1}    {2}       {3}        {4}", tran.TransId, tran.Date, tran.Payee, tran.Category, tran.Amount));

                            string tranNo = tran.TransId.ToString();
                            var brush = Brushes.Gainsboro;
                            if (mColor)
                            {
                                //brush = Brushes.WhiteSmoke;
                                brush = Brushes.Gainsboro;
                                mColor = false;
                            }
                            else
                            {
                                brush = Brushes.White;
                                mColor = true;
                            }

                            StackPanel spRpt = new StackPanel();
                            spRpt.Name = string.Format("spRpt_{0}", tranNo);
                            spRpt.Orientation = Orientation.Horizontal;
                            //spHist.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);

                            TextBlock tbTranNo = new TextBlock();
                            tbTranNo.Name = string.Format("tbTranNo_{0}", tranNo);
                            tbTranNo.Text = tranNo;
                            tbTranNo.Margin = new Thickness(25, 0, 0, 0);
                            tbTranNo.Width = 40;
                            tbTranNo.TextAlignment = TextAlignment.Left;
                            tbTranNo.Background = brush;
                            tbTranNo.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);

                            TextBlock tbDate = new TextBlock();
                            tbDate.Name = string.Format("tbDate_{0}", tranNo);
                            tbDate.Text = tran.Date;
                            tbDate.Width = 100;
                            tbDate.TextAlignment = TextAlignment.Left;
                            tbDate.Background = brush;
                            tbDate.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);

                            TextBlock tbPayee = new TextBlock();
                            tbPayee.Name = string.Format("tbPayee_{0}", tranNo);
                            tbPayee.Text = tran.Payee;
                            tbPayee.Width = 150;
                            tbPayee.TextAlignment = TextAlignment.Left;
                            tbPayee.Background = brush;
                            tbPayee.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);


                            TextBlock tbCategory = new TextBlock();
                            tbCategory.Name = string.Format("tbCategory_{0}", tranNo);
                            tbCategory.Text = tran.Category;
                            tbCategory.Width = 120;
                            tbCategory.TextAlignment = TextAlignment.Left;
                            tbCategory.Background = brush;
                            tbCategory.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);

                            TextBlock tbAmt = new TextBlock();
                            tbAmt.Name = string.Format("tbAmt_{0}", tranNo);
                            tbAmt.Text = tran.Amount.ToString("0.00");
                            tbAmt.Width = 60;
                            tbAmt.TextAlignment = TextAlignment.Right;
                            tbAmt.Background = brush;
                            tbAmt.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);

                            TextBlock tbSpacer1 = new TextBlock();
                            tbSpacer1.Name = string.Format("tbSpacer1_{0}", tranNo);
                            tbSpacer1.Width = 20;
                            tbSpacer1.Background = brush;
                            tbSpacer1.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);

                            TextBlock tbNotes = new TextBlock();
                            tbNotes.Name = string.Format("tbNotes_{0}", tranNo);
                            tbNotes.Text = tran.Notes;
                            tbNotes.Width = 180;
                            tbNotes.TextAlignment = TextAlignment.Left;
                            tbNotes.Background = brush;
                            tbNotes.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);

                            spRpt.Children.Add(tbTranNo);
                            spRpt.Children.Add(tbDate);
                            spRpt.Children.Add(tbPayee);
                            spRpt.Children.Add(tbCategory);
                            spRpt.Children.Add(tbAmt);
                            spRpt.Children.Add(tbSpacer1);
                            spRpt.Children.Add(tbNotes);

                            report.wpReport.Children.Add(spRpt);
                            report.scrollReport.ScrollToTop();

                            // report text file
                            sw.WriteLine(string.Format("{0}    {1}    {2}    {3}    {4}",
                                                    tranNo,
                                                    tran.Date,
                                                    tran.Payee,
                                                    tran.Amount.ToString("0.00"),
                                                    tran.Notes));

                        }

                    }
                    //
                    //    Build  line separator
                    //
                    StackPanel spCategorySeparator = new StackPanel();
                    spCategorySeparator.Name = string.Format("spCategoryTotal_{0}", cat_filtered);
                    spCategorySeparator.Orientation = Orientation.Horizontal;
                    spCategorySeparator.Width = 700;
                    spCategorySeparator.Margin = new Thickness(2);

                    TextBlock tbCategorySeparator = new TextBlock();
                    tbCategorySeparator.Text = "-------------------------------------------------------------------------------------------------------------------------------------";
                    tbCategorySeparator.Width = 700;
                    tbCategorySeparator.Margin = new Thickness(2);
                    //tbCategorySeparator.TextAlignment = TextAlignment.Right;

                    sw.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------");

                    spCategorySeparator.Children.Add(tbCategorySeparator);
                    report.wpReport.Children.Add(spCategorySeparator);

                    //
                    //    Build Total line
                    //
                    StackPanel spCategoryTotal = new StackPanel();
                    spCategoryTotal.Name = string.Format("spCategoryTotal_{0}", cat_filtered);
                    spCategoryTotal.Orientation = Orientation.Horizontal;
                    spCategoryTotal.Width = 700;
                    spCategoryTotal.Margin = new Thickness(5, 0, 0, 20);

                    TextBlock tbCategoryTotalLabel = new TextBlock();
                    tbCategoryTotalLabel.Text = "Total:";
                    tbCategoryTotalLabel.Width = 410;
                    tbCategoryTotalLabel.Margin = new Thickness(10, 0, 0, 0);
                    tbCategoryTotalLabel.TextAlignment = TextAlignment.Right;

                    TextBlock tbCategoryTotalNum = new TextBlock();
                    tbCategoryTotalNum.Text = mCategoryDict[cat].NewTotalAmount.ToString();
                    tbCategoryTotalNum.FontWeight = FontWeights.Bold;
                    tbCategoryTotalNum.Margin = new Thickness(10, 0, 0, 0);
                    tbCategoryTotalNum.Width = 40;
                    tbCategoryTotalNum.TextAlignment = TextAlignment.Right;

                    spCategoryTotal.Children.Add(tbCategoryTotalLabel);
                    spCategoryTotal.Children.Add(tbCategoryTotalNum);
                    report.wpReport.Children.Add(spCategoryTotal);

                    sw.WriteLine(string.Format("Total: {0}", mCategoryDict[cat].NewTotalAmount));
                    sw.WriteLine("");
                    sw.WriteLine("");
                }

                sw.Close();
                sw.Dispose();

                report.ShowDialog();
            }
            catch (Exception ex)
            {
                Log(methodName, string.Format("ERROR trying to Display Report.  Err msg: {0}", ex.Message));
            }   
        }

        private void btnSaveRec_Click(object sender, RoutedEventArgs e)
        {
            string methodName = "btnSaveRec_Click";
            try
            {
                SaveRecord();
            }
            catch (Exception ex)
            {
                Log(methodName, string.Format("ERROR trying to save record.  Err msg: {0}", ex.Message));
                Application.Current.Shutdown();
            }
        }

        private void SaveRecord()
        {
            string methodName = "SaveRecord";
            Log(methodName, string.Format("Enterring {0}  ++++++++++++++++++", methodName));

            if (comboBoxPayee.SelectedItem == null && comboBoxPayee.Text != "")
            {
                //  Saving NEW PAYEE
                Log(methodName, string.Format("Using manually entered Payee: [{0}] instead of  [{1}]", comboBoxPayee.Text, mPayee));
                mPayee = comboBoxPayee.Text;
                mPayee = AddPayee(mPayee, ""); // Using the payee return value fixes the case issue (ie: send in ricks == returns Ricks)
            }
            else if ((comboBoxPayee.SelectedItem == null && comboBoxPayee.Text == "") || comboBoxPayee.SelectedItem.ToString() == DEFAULT_PAYEE_CAPTION)
            {
                Log(methodName, string.Format("\nERROR - Don't have a value for Payee\n"));
                mPayee = "";
            }

            if (comboBoxCategory.SelectedItem == null && comboBoxCategory.Text != "")
            {
                //  We have a new Category to add to our collection
                Log(methodName, string.Format("Using manually entered Category: [{0}] instead of  [{1}]", comboBoxCategory.Text, mCategory));
                mCategory = comboBoxCategory.Text;
                mCategory = AddCategory(mCategory);  // Using the category return value fixes the case issue (ie: send in clothes == returns Clothes)
            }
            else if ((comboBoxCategory.SelectedItem == null && comboBoxCategory.Text != "") || comboBoxCategory.SelectedItem.ToString() == DEFAULT_CATEGORY_CAPTION)
            {
                Log(methodName, string.Format("\nERROR - Don't have a value for Category\n"));
                mCategory = "";
            }

            string line = "";
            bool flagError = false;

            //  ASSOCIATE CATEGORY TO PAYEE
            if (mPayee != "" && mCategory != "")
            {
                if (mPayeeDict[mPayee] != mCategory && (chkBoxNoChgAssoc.IsChecked == false))
                {
                    string msgBoxMsg = string.Format("You are using a different category: {0} for  {1}.  Is that ok ?", mCategory, mPayee);
                    MessageBoxResult msgResult = MessageBox.Show(msgBoxMsg, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (msgResult == MessageBoxResult.No)
                    {
                        line = string.Format("  Alert !!  User want to change category association - don't save yet. ");
                        flagError = true;
                    }
                    else
                    {
                        // if category associated to payee is changed AND checkbox "Don't chg Association" is NOT checked, save assoc.
                        Log(methodName, string.Format("Associating category: {0} to payee: {1}", mCategory, mPayee));
                        mPayeeDict[mPayee] = mCategory;
                    }
                }
            }

            if (mMonthEndingDate == "")
            {
                Log(methodName, string.Format("\nERROR - Don't have a Transaction Month selected\n"));
            }

            try
            {
                mAmount = float.Parse(tbAmt.Text, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
            }
            catch
            {
                Log(methodName, string.Format("\nERROR - Invalid Amount: {0}\n", tbAmt.Text));
                mAmount = 0;
            }

            // DATA VALIDATION
            if (mPayee == "" || mCategory == "" || mAmount == 0 || mMonthEndingDate == "" || mTransactionDate == "")
            {
                if (mPayee == "") line = string.Format("   ERROR !!      Payee not selected");
                else if (mCategory == "") line = string.Format("   ERROR !!      Category not selected");
                else if (mAmount == 0) line = string.Format("   ERROR !!      Amount not enterred or invalid");
                else if (mMonthEndingDate == "") line = string.Format("   ERROR !!      Transaction Month not selected");
                else if (mTransactionDate == "") line = string.Format("   ERROR !!      Transaction Date not selected");
                else
                {
                    line = string.Format("   ERROR !!      Payee: {0}       Category: {1}      Amount: {2}", mPayee.PadRight(25), mCategory.PadRight(20), tbAmt.Text);
                }
                flagError = true;
            }
            else if(!flagError) // if not set above 
            {
                line = string.Format(" Saving::    {0}   {1}   {2,-25}   {3,-20}   {4}     {5}",
                                                                  mTransActnNumber, mTransactionDate, mPayee, mCategory, tbAmt.Text, tbNote.Text);
            }
            Log(methodName, string.Format("\n {0}\n", line));

            if (!flagError)
            {                
                Transaction trans = new Transaction(){TransId = mTransActnNumber,
                                                    Date = mTransactionDate, 
                                                    Payee = mPayee, 
                                                    Category = mCategory, 
                                                    Amount = mAmount, 
                                                    Notes = tbNote.Text,
                                                    TransMonth = mMonthEndingDate};


                // SAVE DATA  TO DATABASE
                if (!SaveTransaction(trans))
                {
                    UpdateHistory("ERROR  Saving data to database !!");
                    return;
                }
                mTransactionList.Add(trans);
                UpdateHistoryPanel(mTransActnNumber.ToString(), mTransactionDate, mPayee, mCategory, mAmount.ToString("0.00"), tbNote.Text);
                UpdateCategoryTotals(mCategory, mAmount);

                // reset data
                mPayee = "";
                mCategory = "";
                mAmount = 0;
                comboBoxPayee.SelectedItem = DEFAULT_PAYEE_CAPTION;
                comboBoxCategory.SelectedItem = DEFAULT_CATEGORY_CAPTION;
                tbAmt.Text = "";
                tbNote.Text = "";
                mTransActnNumber++;  // don't increment this unless we have VALID data that is saved to db
                tbTrnNum.Text = mTransActnNumber.ToString();
                chkBoxNoChgAssoc.IsChecked = false;
                
                SoundPlayer sp = new SoundPlayer();
                sp.SoundLocation = "C:\\ProgramData\\Rick\\SureBudgetMinder\\MEDIA\\CASHREG.wav";
                sp.Load();
                sp.Play();
            }
            else
            {
                // Reset nothing ??
                UpdateHistory(line);

                SoundPlayer sp = new SoundPlayer();
                sp.SoundLocation = "C:\\ProgramData\\Rick\\SureBudgetMinder\\MEDIA\\EXPLODE.wav";
                sp.Load();
                sp.Play();
            }
            
        }

        private void UpdateCategoryTotals(string category, float value)
        {
            string methodName = "UpdateCategoryTotals";
            Category cat = new Category();
            cat = mCategoryDict[category];
            if (!cat.HaveTotalTextblock)
            {
                DisplayCategoryTotals(category);
                cat.HaveTotalTextblock = true;
            }
            float oldTotal = cat.NewTotalAmount;
            cat.NewTotalAmount += value;
            Log(methodName, string.Format("Category: {0}   total was: $ {1}    new amt: $ {2}     new TOTAL: $ {3}",
                                                        category, oldTotal, value, cat.NewTotalAmount));
        }

        private void DisplayCategoryTotals(string category)
        {
            string methodName = "DisplayCategoryTotals";

            string modCatName = category.Replace(' ', '_');
            modCatName = modCatName.Replace("'", "");  // stkPanel.Name value can not contain apostrophy, so filter that out.

            Category cat = new Category();
            cat = mCategoryDict[category];

            Log(methodName, string.Format("Adding NEW control for category: {0}", category));
            StackPanel stkPanel = new StackPanel();
            stkPanel.Margin = new Thickness(20, 5, 5, 5);
            stkPanel.Orientation = Orientation.Horizontal;
            stkPanel.Name = string.Format("sp_{0}", modCatName);

            Label label = new Label();
            label.Name = string.Format("lbl_{0}", modCatName);
            label.Content = string.Format("{0}: ", category);

            TextBlock textBlk = new TextBlock();
            textBlk.Name = string.Format("tb_{0}", modCatName);
            //textBlk.Text = mCategoryDict[category].ToString();
            textBlk.Margin = new Thickness(2, 5, 5, 5);
            //textBlk.DataContext = mCategoryDict[category];

            Binding binding = new Binding();  //
            //binding.ElementName = textBlk.Text;
            binding.Source = cat;
            binding.Path = new PropertyPath("NewTotalAmount");
            binding.StringFormat = "0.00";
            binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            BindingOperations.SetBinding(textBlk, TextBlock.TextProperty, binding);  //
            //textBlk.SetBinding(TextBlock.TextProperty, binding);

            stkPanel.Children.Add(label);
            stkPanel.Children.Add(textBlk);

            wpCategoryTotals.Children.Add(stkPanel);
        }

        private void PopulateFormWithPreExistingTransaction(string tranNum)
        {
            string methodName = "PopulateFormWithPreExistingTransaction";

            try
            {
                DataStorage data = new DataStorage();
                List<Transaction> tranList = data.GetData(string.Format("TransId={0};TransMonth={1}", tranNum, mMonthEndingDate));

                if (tranList.Count != 1)
                {
                    Log(methodName, "");
                    Log(methodName, "");
                    Log(methodName, string.Format("Unexpected number of transactions found: {0}", tranList.Count));
                    Log(methodName, string.Format("     Displaying all transactions found;", tranList.Count));
                    foreach (Transaction tranFound in tranList)
                    {
                        Log(methodName, string.Format("     TranId: {0} || MonthEnding: {1} || Payee: {2} || Amt: {3} || TranDate: {4}",
                                                        tranFound.TransId, tranFound.TransMonth, tranFound.Payee, tranFound.Amount, tranFound.Date));
                    }
                    Log(methodName, "");
                    Log(methodName, "");
                    MessageBox.Show("Error - Multiple records found but only expected to find one.  See log.");
                    return;
                }
                string recordId = string.Empty;
                //
                // Populate textBoxes with original values & let user modify as needed and then save record normally
                //
                foreach (Transaction oldTran in tranList)
                {
                    //
                    // delete old transaction
                    //
                    data.RemoveRecord(oldTran.TransId.ToString(), oldTran.TransMonth);

                    //
                    // todo - probably should just call LoadInitData() to simply this code AND then reload the textboxes as I'm already doing below.
                    //

                    // Remove transaction from mTransactionList
                    List<Transaction> tmpTranList = new List<Transaction>();
                    foreach (Transaction tran in mTransactionList)
                    {
                        tmpTranList.Add(tran);
                    }
                    foreach (Transaction tran in tmpTranList)
                    {
                        if (tran.TransId == oldTran.TransId)
                        {
                            mTransactionList.Remove(tran);
                        }
                    }

                    Log(methodName, string.Format("Reset hist and category panels and category Dict"));
                    // reset hist and category panels and category Dict
                    wpHistory.Children.Clear();
                    wpCategoryTotals.Children.Clear();
                    mCategoryDict = new Dictionary<string, Category>();
                    mPayeeDict = new Dictionary<string, string>();

                    LoadPayeeAndCategoryComboBtns();

                    Log(methodName, string.Format("ReLoad category totals and TransactionHistory panel"));
                    // Reload category totals and TransactionHistory panel
                    foreach (Transaction tran in mTransactionList)
                    {
                        UpdateCategoryTotals(tran.Category, tran.Amount);
                        UpdateHistoryPanel(tran.TransId.ToString(), tran.Date, tran.Payee, tran.Category, tran.Amount.ToString("0.00"), tran.Notes);
                    }


                    // Load textBoxes, etc with the original data
                    comboBoxPayee.SelectedItem = oldTran.Payee;
                    comboBoxCategory.SelectedItem = oldTran.Category;
                    tbAmt.Text = oldTran.Amount.ToString();
                    //mTransActnNumber = tran.TransId;  // don't set this - let this modified record have the next avail id num
                    tbTranDate.Text = oldTran.Date;  // might need to use calendar dict to get full month & replace MM with MMMM
                    tbNote.Text = oldTran.Notes;
                    //tbMonthEndingDate.Text = oldTran.TransMonth;  // this should not be needed since it should be unchanged
                }
            }
            catch (Exception ex)
            {
                Log(methodName, string.Format("\n {0}   ERROR !! Error msg: {1}     stack trace: {2}", methodName, ex.Message, ex.StackTrace));
                MessageBox.Show("Fatal error !  Rick needs to look at logs - C:\\ProgramData\\Rick\\SureBudgetMinder", "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.None);
            }

        }

        //private void spHist_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        private void Hist_PreviewMouseLeftButtonUp(object sender, RoutedEventArgs e)
        {
            string methodName = "Hist_PreviewMouseLeftButtonUp";
            FrameworkElement feSource = e.Source as FrameworkElement;
            
            if (feSource.Name != "")
            {
                e.Handled = true;  // stops the tunneling/bubbling and repeatedly firing this method
                string tranNum = feSource.Name.Split('_')[1];
                string msgBoxMsg = string.Format("You are requesting to MODIFY record: {0}. The old entry data will be deleted from the system", tranNum) +
                        " and displayed here so you can re-enter as needed. Are you sure you want to continue?";

                Log(methodName, string.Format("MessageBox: {0}", msgBoxMsg));
                MessageBoxResult msgResult = MessageBox.Show(msgBoxMsg, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (msgResult == MessageBoxResult.No)
                {
                    // do nothing
                    Log(methodName, string.Format("MessageBox Response: No"));
                }
                else
                {
                    Log(methodName, string.Format("MessageBox Response: Yes"));
                    PopulateFormWithPreExistingTransaction(tranNum);
                }
            }
        }

        private void UpdateHistoryPanel(string tranNo, string date, string payee, string category, string amt, string notes)
        {
            var brush = Brushes.Gainsboro;
            if (mColor)
            {
                //brush = Brushes.WhiteSmoke;
                brush = Brushes.Gainsboro;
                mColor = false;
            }
            else
            {
                brush = Brushes.White;
                mColor = true;
            }

            StackPanel spHist = new StackPanel();
            spHist.Name = string.Format("spHist_{0}", tranNo);
            spHist.Orientation = Orientation.Horizontal;
            //spHist.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);
            

            TextBlock tbTranNo = new TextBlock();
            tbTranNo.Name = string.Format("tbTranNo_{0}", tranNo);
            tbTranNo.Text = tranNo;
            tbTranNo.Margin = new Thickness(5, 0, 0, 0);
            tbTranNo.Width = 40;
            tbTranNo.TextAlignment = TextAlignment.Left;
            tbTranNo.Background = brush;
            tbTranNo.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);

            TextBlock tbDate = new TextBlock();
            tbDate.Name = string.Format("tbDate_{0}", tranNo);
            tbDate.Text = date;
            tbDate.Width = 100;
            tbDate.TextAlignment = TextAlignment.Left;
            tbDate.Background = brush;
            tbDate.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);

            TextBlock tbPayee = new TextBlock();
            tbPayee.Name = string.Format("tbPayee_{0}", tranNo);
            tbPayee.Text = payee;
            tbPayee.Width = 150;
            tbPayee.TextAlignment = TextAlignment.Left;
            tbPayee.Background = brush;
            tbPayee.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);


            TextBlock tbCategory = new TextBlock();
            tbCategory.Name = string.Format("tbCategory_{0}", tranNo);
            tbCategory.Text = category;
            tbCategory.Width = 120;
            tbCategory.TextAlignment = TextAlignment.Left;
            tbCategory.Background = brush;
            tbCategory.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);

            TextBlock tbAmt = new TextBlock();
            tbAmt.Name = string.Format("tbAmt_{0}", tranNo);
            tbAmt.Text = amt;
            tbAmt.Width = 60;
            tbAmt.TextAlignment = TextAlignment.Right;
            tbAmt.Background = brush;
            tbAmt.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);

            TextBlock tbSpacer1 = new TextBlock();
            tbSpacer1.Name = string.Format("tbSpacer1_{0}", tranNo);
            tbSpacer1.Width = 20;
            tbSpacer1.Background = brush;
            tbSpacer1.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);

            TextBlock tbNotes = new TextBlock();
            tbNotes.Name = string.Format("tbNotes_{0}", tranNo);
            tbNotes.Text = notes;
            tbNotes.Width = 180;
            tbNotes.TextAlignment = TextAlignment.Left;
            tbNotes.Background = brush;
            tbNotes.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(Hist_PreviewMouseLeftButtonUp);

            //Button btnDummy = new Button();
            //btnDummy.Name = string.Format("btn_{0}", tranNo);
            //btnDummy.Width = 1;

            spHist.Children.Add(tbTranNo);
            spHist.Children.Add(tbDate);
            spHist.Children.Add(tbPayee);
            spHist.Children.Add(tbCategory);
            spHist.Children.Add(tbAmt);
            spHist.Children.Add(tbSpacer1);
            spHist.Children.Add(tbNotes);
            //spHist.Children.Add(btnDummy);
            
            wpHistory.Children.Add(spHist);
            scrollHistory.ScrollToBottom();
        }

        private void UpdateHistory(string errorMsg)
        {
            StackPanel spHist = new StackPanel();
            spHist.Orientation = Orientation.Horizontal;

            TextBlock tbError = new TextBlock();
            tbError.Foreground = Brushes.Red;
            tbError.Background = Brushes.LightCyan;
            tbError.Text = errorMsg;
            tbError.Width = 500;
            tbError.TextAlignment = TextAlignment.Left;

            spHist.Children.Add(tbError);
            wpHistory.Children.Add(spHist);
            scrollHistory.ScrollToBottom();
        }

        private void SaveDataFile()
        {
            DataStorage data = new DataStorage();
            bool success = data.CopyDataFile();
            if (!success)
            {
                MessageBox.Show("Error saving data file to C:\\Users\\Church\\Documents\\ShirleysBudgetMinder_Data.txt - ensure path exists.", "ERROR !!", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
        }

        private void LoadPayeeAndCategoryComboBtns()
        {
            string methodName = "LoadPayeeAndCategoryComboBtns";
            Log(methodName, string.Format("Enterring"));

            if (comboBoxPayee.Items.Count != 0)
            {
                comboBoxPayee.Items.Clear();
            }
            if (comboBoxCategory.Items.Count != 0)
            {
                comboBoxCategory.Items.Clear();
            }

            comboBoxPayee.Items.Add(DEFAULT_PAYEE_CAPTION);
            comboBoxCategory.Items.Add(DEFAULT_CATEGORY_CAPTION);

            List<string> payeeList = new List<string>();
            List<string> catList = new List<string>();

            DataStorage data = new DataStorage();
            List<Transaction> tranList = data.GetData("");

            foreach (Transaction tran in tranList)
            {
                if (!payeeList.Contains(tran.Payee))
                {
                    Log(methodName, string.Format("Adding Payee: {0} to Payee ComboBtn", tran.Payee));
                    AddPayee(tran.Payee, tran.Category);
                }
                if (!catList.Contains(tran.Category))
                {
                    Log(methodName, string.Format("Adding Category: {0} to Category ComboBtn", tran.Category));
                    AddCategory(tran.Category);
                }
            }
        }

        private void btnMoEndHelp_Click(object sender, RoutedEventArgs e)
        {
            string methodName = "btnMoEndHelp_Click";
            Log(methodName, string.Format("Showing months"));
            DataStorage data = new DataStorage();
            List<Transaction> tranList = data.GetData("");
            string monthEnds = string.Empty;
            foreach (Transaction tran in tranList)
            {
                if (!monthEnds.Contains(tran.TransMonth))
                {
                    monthEnds = string.Format("  {0}    {1}  ", monthEnds, tran.TransMonth);
                }
            }
            MessageBox.Show(string.Format("Valid Months: {0}", monthEnds));
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            string methodName = "btnExit_Click";
            Log(methodName, string.Format("\n User request - Shutting down.\n"));
            SaveDataFile();
            Application.Current.Shutdown();
        }

        private void btnSortByDate_Click(object sender, RoutedEventArgs e)
        {
            string methodName = "btnSortByDate_Click";
            Log(methodName, string.Format("\n Sorting transactions by DATE.\n"));
            LoadInitData(mMonthEndingDate, "Date");
        }

        private void btnSortByPayee_Click(object sender, RoutedEventArgs e)
        {
            string methodName = "btnSortByPayee_Click";
            Log(methodName, string.Format("\n Sorting transactions by Payee.\n"));
            LoadInitData(mMonthEndingDate, "Payee");
        }

        private void btnSortByTransId_Click(object sender, RoutedEventArgs e)
        {
            string methodName = "btnSortByTransId_Click";
            Log(methodName, string.Format("\n Sorting transactions by TransId.\n"));
            LoadInitData(mMonthEndingDate, "TransId");
        }

        private bool SaveTransaction(Transaction tran)
        {
            bool success = true;
            DataStorage data = new DataStorage();
            if (!data.SaveData(tran)) success = false;

            return success;
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
                sw.Dispose();
            }
            catch (Exception e)
            {
                //  todo - add a popup msg box so there is some indication to user what the problem might be...
            }
        }

    }
}
