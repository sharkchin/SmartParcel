/**
 * Cache for Impactor Data
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using log4net;
using log4net.Config;
using DMP.MasterProgram.ProcessorMetadata;
using Dmp.Neptune.Collections;
using DMP.MasterProgram.Utils.DatabaseManager;



namespace DMP.MasterProgram.Utils.Caching
{
    public class RecordData
    {
        public string CFTID { get; set; }
        public List<AbstractRecord> Records{ get; set; }

    }
    public class RecordCache : LRUCache<RecordData>
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
     
        //private static ImpactorCache instance = null;
        private IIndex<string> _findByCFTID = null;

        MasterProgramMetadata.InputDataSet inputDataSet;
        string originalAttributeCriteria = null;
        public MasterProgDBManager dbManager;       
        private Object iLock = new Object();
        HashSet<string> recordNotInDb = new HashSet<string>();


        /// <summary>retrieve items by CFTId</summary>
        public List<AbstractRecord> FindByCFTID(string CFTID)
        {
            List<AbstractRecord> recList = null;
            try
            {
               

                if (!recordNotInDb.Contains(CFTID))
                {
                    RecordData  record=  _findByCFTID[CFTID];
                    if(record!=null)
                    {
                        recList = record.Records;
                    }
                    else
                    {
                        lock (iLock)
                        {
                            if (recordNotInDb.Count > 10000)
                            {
                                recordNotInDb.Clear();
                            }
                            if (!recordNotInDb.Contains(CFTID))
                                recordNotInDb.Add(CFTID);

                        }
                    }
                    
                }
            }
            catch (Exception e)
            {
                throw e;
            }

            return recList;
        }


        /// <summary>constructor creates cache and multiple indexes</summary>
        public RecordCache(MasterProgramMetadata.InputDataSet inputDataSet) 
            : base(10000, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), null)
        {
            this.inputDataSet = inputDataSet;
            this.originalAttributeCriteria = inputDataSet.AttributeCriteria;
            if ("DOFS".Equals(inputDataSet.StorageType))
            {
                dbManager =new  SQLiteManager();
            }
            else if ("DB".Equals(inputDataSet.StorageType))
            {
                dbManager = new SQLManager(inputDataSet.Database);
            }
            _findByCFTID = AddIndex<string>("UserID", recordData => recordData.CFTID, LoadFromCFTID);

        }

        private delegate DataType LoadData<DataType>(IDataRecord reader);


        /// <summary>when FindByUserID can't find a user, this method loads the data from the db</summary>
        private RecordData LoadFromCFTID(string CFTID)
        {
            RecordData data = null;
            try
            {
                if (!String.IsNullOrEmpty(originalAttributeCriteria))
                    inputDataSet.AttributeCriteria = "_CFTID Like '" + CFTID + "'" + " and " + originalAttributeCriteria;
                else
                    inputDataSet.AttributeCriteria = "_CFTID Like '" + CFTID + "'";
        
                List<AbstractRecord> records = dbManager.FetchRecords(this.inputDataSet);


                if (records!=null && records.Count > 0)
                {
                    data = new RecordData();
                    data.CFTID = CFTID;
                    data.Records = records;
                     
                }
                else if (records.Count == 0)
                {
                    return null;
                }
              
            }
            catch (Exception e)
            {
                throw e;
            }
            return data;
            
        }

    }
}
