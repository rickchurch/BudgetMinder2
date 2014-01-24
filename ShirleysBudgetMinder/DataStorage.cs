using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ShirleysBudgetMinder
{
    class DataStorage
    {
        string mTransactionStorageFile = string.Empty;
        string mLogFile = string.Empty;
        char mDelimeter = '^';
        Dictionary<string, string> mFieldsToQualifyDict = new Dictionary<string, string>();


        public DataStorage()
        {
            DateTime now = DateTime.Now;
            string showDate = now.ToString("yyyyMMdd");

            string localPath = "C:\\ProgramData\\Rick\\SureBudgetMinder\\";
            string logName = string.Format("{0}_SbmLog.txt", showDate);

            mTransactionStorageFile = System.IO.Path.Combine(localPath, "Data.txt");
            mLogFile = System.IO.Path.Combine(localPath, logName);
        }

        public bool CopyDataFile()
        {
            string method = "CopyDataFile";
            bool success = true;
            try
            {
                File.Copy(mTransactionStorageFile, "C:\\Users\\Church\\Documents\\ShirleysBudgetMinder_Data.txt");
            }
            catch (Exception ex)
            {
                success = false;
                Log(method, string.Format("ERROR saving data file: {0}", ex.Message));
            }
            return success;
        }

        public bool SaveData(Transaction tran)
        {
            bool success = true;
            // for MySQL
            //success = SaveToDatabase(tran);


            // for file storage system
            success = SaveToFile(tran);

            return success;
        }

        /// <summary>
        /// Get data from source object.  query format should be a list of query qualifications string delimeted by semicolon
        /// ie:  "Payee='Winco';TransMonth='2013-04-21'"
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public List<Transaction> GetData(string query)
        {
            // query should be a list of query qualifications string delimeted by semicolon
            //  ie:  "Payee='Winco';TransMonth='2013-04-21'"  Convert to std SQL query for DB access
            string method = "GetData";

            Log(method, string.Format("Retrieving data with query: {0}", query));

            List<Transaction> transList = new List<Transaction>();
            // for MySQL
            //transList = GetDataFromDatabase(query);


            // for file storage system
            transList = GetDataFromFile(query);

            return transList;
        }

        private bool SaveToDatabase(Transaction tran)
        {
            bool success = true;

            // connect to MySQL and save data

            return success;
        }

        private bool SaveToFile(Transaction tran)
        {
            string method = "SaveToFile";
            bool success = true;
            try
            {
                StreamWriter swData = new StreamWriter(mTransactionStorageFile, true);

                int id = tran.TransId;
                string tranDate = tran.Date;
                string payee = tran.Payee;
                string category = tran.Category;
                float amt = tran.Amount;
                string notes = tran.Notes;  //
                string tranMonth = tran.TransMonth;

                if (payee.Contains(mDelimeter))
                {
                    Log(method, string.Format("ERROR !!  Payee contains invalid chr <->.  Payee: {0}", payee));
                    return false;
                }
                if (category.Contains(mDelimeter))
                {
                    Log(method, string.Format("ERROR !!  Category contains invalid chr <->.  Category: {0}", category));
                    return false;
                }
                if (notes.Contains(mDelimeter))
                {
                    Log(method, string.Format("ERROR !!  Notes contains invalid chr <->.  Notes: {0}", notes));
                    return false;
                }
                if (payee.Contains(mDelimeter))
                {
                    Log(method, string.Format("ERROR !!  Payee contains invalid chr <->.  Payee: {0}", payee));
                    return false;
                }

                swData.WriteLine("{0}{7}{1}{7}{2}{7}{3}{7}{4}{7}{5}{7}{6}", id, tranDate, payee, category, amt, notes, tranMonth, mDelimeter);
                swData.Close();
                swData.Dispose();
            }
            catch (Exception e)
            {
                //
                return false;
            }
            return success;
        }

        public void RemoveRecord(string transactionId, string monthEndingDate)
        {
            string method = "RemoveRecord";
            StringBuilder sb = new StringBuilder();

            List<Transaction> resultsList = new List<Transaction>();

            try
            {
                string line;

                System.IO.StreamReader dataFile = new System.IO.StreamReader(mTransactionStorageFile);
                while ((line = dataFile.ReadLine()) != null)
                {
                    //if (line.StartsWith("##")) continue;  // ignore lines that start with ##
                    Transaction tmpTran = ConvertDataToTransactionObject(line);
                    if (tmpTran.TransId.ToString() == transactionId
                        &&
                        tmpTran.TransMonth == monthEndingDate)
                    {
                        Log(method, string.Format("Found record {0} and removing from collection.", transactionId));
                        continue;
                    }
                    sb.AppendLine(line);
                }
                dataFile.Close();
                dataFile.Dispose();
            }
            catch (Exception ex)
            {
                Log(method, string.Format("ERROR reading data file.  ErrMsg: {0}", ex.Message));
            }

            try
            {
                StreamWriter swData = new StreamWriter(mTransactionStorageFile);  // 2nd param of true causes this to append instead of overwriting file
                swData.Write(sb);
                swData.Close();
                swData.Dispose();
                
            }
            catch (Exception ex)
            {
                Log(method, string.Format("ERROR writing data to file.  ErrMsg: {0}", ex.Message));
            }

        }

        private List<Transaction> GetDataFromFile(string query)
        {
            // query should be a list of query qualifications string delimeted by semicolon
            //  ie:  "Payee='Winco';TransMonth='2013-04-21'"
            string method = "GetDataFromFile";

            List<Transaction> resultsList = new List<Transaction>();
            List<Transaction> SortedTrans = new List<Transaction>();

            try
            {
                string line;
                // set search criteria object - mFieldsToQualifyDict
                LoadSearchObject(query);
                System.IO.StreamReader dataFile = new System.IO.StreamReader(mTransactionStorageFile);
                while ((line = dataFile.ReadLine()) != null)
                {
                    if (line.StartsWith("##")) continue;  // ignore lines that start with ##

                    // Read and Process 'line' here
                    Transaction tmpTran = ConvertDataToTransactionObject(line);

                    if (DataMatchesCriteria(tmpTran))
                    {
                        Log(method, string.Format("Record #: {0} matches and so it's included in return obj.", tmpTran.TransId));
                        resultsList.Add(tmpTran);
                    }
                }
                dataFile.Close();
                dataFile.Dispose();
                // found this line of code on SO to sort a list of objects
                SortedTrans = resultsList.OrderBy(o => o.TransId).ToList();
            }
            catch (Exception ex)
            {
                Log(method, string.Format("ERROR reading data file.  ErrMsg: {0}", ex.Message));
            }
            return SortedTrans;
        }

        private void LoadSearchObject(string query)
        {
            string method = "LoadSearchObject";

            mFieldsToQualifyDict = new Dictionary<string, string>(){
            {"TransId", "*"},{"Date", "*"},{"Payee", "*"},{"Category", "*"},{"Amount", "*"},{"Notes", "*"},{"TransMonth", "*"}};

            if(query == "") return;  // this would indicate a req to return ALL records

            // load query criteria into mFieldsToQualifyDictDict
            string[] items = query.Split(';');
            Log(method, string.Format("query string: {0}    items count: {1}", query, items.Length));

            foreach (string item in items)
            {
              
                string[] parts = item.Split('=');
                string qualifier = parts[0];
                string value = parts[1];
                if (mFieldsToQualifyDict.ContainsKey(qualifier))
                {
                    mFieldsToQualifyDict[qualifier] = value;
                    Log(method, string.Format("Setting Transaction field: {0} search value: {1}", qualifier, mFieldsToQualifyDict[qualifier]));
                }
            }
        }

        private Transaction ConvertDataToTransactionObject(string line)
        {
            Transaction tran = new Transaction();
            string[] parts = line.Split(mDelimeter);
            if (parts.Length == 7)
            {
                tran.TransId = Convert.ToInt16(parts[0]);
                tran.Date = parts[1];
                tran.Payee = parts[2];
                tran.Category = parts[3];
                tran.Amount = float.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                tran.Notes = parts[5];
                tran.TransMonth = parts[6];
            }

            return tran;    
        }

        private bool DataMatchesCriteria(Transaction tran)
        {
            // query should be a list of query qualifications string delimeted by semicolon
            //  ie:  "Payee='Winco';TransMonth='2013-04-21'"
            string method = "DataMatchesCriteria";
            bool matchCriteria = true;
            Log(method, string.Format("Checking record id: {0}", tran.TransId));

            if (tran.TransId.ToString() != mFieldsToQualifyDict["TransId"] && mFieldsToQualifyDict["TransId"] != "*")
            {
                Log(method, string.Format("Record id: {0} does NOT match TransId", tran.TransId));
                matchCriteria = false;
            }
            if (tran.Date != mFieldsToQualifyDict["Date"] && mFieldsToQualifyDict["Date"] != "*")
            {
                Log(method, string.Format("Record id: {0} does NOT match Date", tran.TransId));
                matchCriteria = false;
            }
            if (tran.Payee != mFieldsToQualifyDict["Payee"] && mFieldsToQualifyDict["Payee"] != "*")
            {
                Log(method, string.Format("Record id: {0} does NOT match Payee", tran.TransId));
                matchCriteria = false;
            }
            if (tran.Category != mFieldsToQualifyDict["Category"] && mFieldsToQualifyDict["Category"] != "*")
            {
                Log(method, string.Format("Record id: {0} does NOT match Category", tran.TransId));
                matchCriteria = false;
            }
            if (tran.Amount.ToString() != mFieldsToQualifyDict["Amount"] && mFieldsToQualifyDict["Amount"] != "*")
            {
                Log(method, string.Format("Record id: {0} does NOT match Amount", tran.TransId));
                matchCriteria = false;
            }
            if (tran.Notes != mFieldsToQualifyDict["Notes"] && mFieldsToQualifyDict["Notes"] != "*")
            {
                Log(method, string.Format("Record id: {0} does NOT match Notes", tran.TransId));
                matchCriteria = false;
            }
            if (tran.TransMonth != mFieldsToQualifyDict["TransMonth"] && mFieldsToQualifyDict["TransMonth"] != "*")
            {
                Log(method, string.Format("Record id: {0} does NOT match TransMonth", tran.TransId));
                matchCriteria = false;
            }

            return matchCriteria;
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
                //
            }
        }

    }
}
