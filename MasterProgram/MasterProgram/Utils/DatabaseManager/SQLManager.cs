using System;
using System.Collections.Generic;
using Dmp.Neptune.Collections;
using log4net;
using System.Data.Common;
using System.Data.SqlClient;
using Dmp.Neptune.DatabaseManager;
using System.Data;
using System.IO;
using DMP.MasterProgram.ProcessorMetadata;
using Dmp.Neptune.Query;
using Dmp.Neptune.DataLoader;
using Dmp.Neptune.Utils;
using Dmp.Neptune.TransactionEngine;
using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;
using System.Threading;


namespace DMP.MasterProgram.Utils.DatabaseManager
{
    class SQLManager : MasterProgDBManager
    {
        public string ActiveVersionId { get; set; }

        private Dmp.Neptune.DatabaseManager.DBManager _dbManager = null;

        /// <summary>
        /// Gets all the transaction field names except for computed fields.
        /// </summary>
        public virtual List<string> TransactionFieldNames
        {
            get
            {
                List<string> list = new List<string>();

                list.Add(DmpSystemFields._SESSIONID);
                list.Add(DmpSystemFields._RECSEQ);
                list.Add(DmpSystemFields._CREATEDBY);
                list.Add(DmpSystemFields._CREATEDDATE);
                list.Add(DmpSystemFields._RETIREDBY);
                list.Add(DmpSystemFields._RETIREDDATE);
                list.Add(DmpSystemFields._STATUS);

                return list;
            }
        }

        public SQLManager(string database)
        {
            _dbManager = DBManagerFactory.GetDBManager(database, Dmp.Neptune.DatabaseManager.DBManager.AccessLevel.READER);
        }

        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public override DbConnection ConnectToDatabase(string database)
        {
            DBManager dbManager = DBManagerFactory.GetDBManager(database, Dmp.Neptune.DatabaseManager.DBManager.AccessLevel.READER);
            DbConnection conn = dbManager.CreateDbConnection(null);
            return conn;
        }

        public override  void CloseTheConnection(DbConnection connection)
        {

            try
            {
                //check if connection is open using connection.State
                connection.Close();
            }
            catch (Exception e)
            {
                logger.Error("Error while closing  SQLite Database Connection", e);
                throw new ApplicationException("Error while closing  SQLite Database Connection: " + e.Message);
            }
        }

       
        public override void ValidateConnection(DbConnection conn)
        {
            if (!(conn is SqlConnection))
                throw new ApplicationException(
                    "Invalid DbConnection. ");
        }

        public override List<AbstractRecord> FetchRecords(MasterProgramMetadata.InputDataSet dataSet)
        {
            return FetchAndProcessRecords(dataSet, null);

        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataSet"></param>
        /// <returns></returns>
        public override List<AbstractRecord> FetchAndProcessRecords(MasterProgramMetadata.InputDataSet dataSet,
            OnRecordRetrieved recordRetrievedCallback)
        {
            string database = dataSet.Database;
            string tableName = dataSet.TableName;
            string attributeCriteria = dataSet.AttributeCriteria;
            List<string> fields = dataSet.Fields;
            string query = null;

            if (database == null || String.IsNullOrEmpty(database))
                throw new ApplicationException("DataBaseResource for is not defined");

            if (tableName == null || String.IsNullOrEmpty(tableName))
                throw new ApplicationException("Table Name for DataBaseResource " + database + " is not defined");

            string inclusionWKT = dataSet.InclusionWKT;
            string exclusionWKT = dataSet.ExclusionWKT;

            query = ConstructSelectQuery(tableName, attributeCriteria, fields, inclusionWKT, exclusionWKT);

            try
            {
                return FetchAndProcessRecords(database, query, null, null, recordRetrievedCallback);

            }
            catch(Exception ex)
            {
                throw ex;
            }
            
        }

        public override List<AbstractRecord> FetchRecords(string database,
            string query,
            SqlGeometry inclusionGeom,
            SqlGeometry exclusionGeom)
        {
            return FetchAndProcessRecords(database, query, inclusionGeom, exclusionGeom, null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="database"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public override List<AbstractRecord> FetchAndProcessRecords(string database,
            string query,
            SqlGeometry inclusionGeom,
            SqlGeometry exclusionGeom,
            OnRecordRetrieved recordRetrievedCallback)
        {

            if (database == null || String.IsNullOrEmpty(database))
                throw new ApplicationException("DataBaseResource is not defined");

            if (query == null || String.IsNullOrEmpty(query))
                throw new ApplicationException("Query is not defined");

            string columnName = null;
            int geometryType = 0;

            DbConnection connection = null;
            DbCommand sqlComm = null;
            DbDataReader reader = null;
            Object lockObj = new Object();
            ManualResetEvent manualResetEvent = null;
            if(recordRetrievedCallback!=null)
              manualResetEvent = new ManualResetEvent(false);

            //return 
            List<AbstractRecord> records = new List<AbstractRecord>();
            ExceptionHandler exceptionHandler = new ExceptionHandler();
            exceptionHandler.IsExceptionPresent = false;

            try
            {
                connection = ConnectToDatabase(database);
                sqlComm = connection.CreateCommand();
                sqlComm.CommandText = query;
                reader = sqlComm.ExecuteReader();
                int fieldCount = reader.FieldCount;
                


                while (reader.Read())
                {
                   DataRecord  record = new DataRecord(new String[] { });

                    for (int fc = 0; fc < fieldCount; fc++)
                    {
                        columnName = reader.GetName(fc);
                        if (columnName.Equals("_GEO_TYPE"))
                        {
                            geometryType = reader.GetInt16(fc);
                            record[columnName] = geometryType;
                        }
                        else if (columnName.Equals("GEOMETRY_BIN"))
                        {

                            SqlGeometry sqlGeometry = (SqlGeometry)reader.GetValue(fc);
                            record[MasterProgramConstants.GEOMETRY_BIN] = sqlGeometry;

                            continue;
                        }
                        else
                        {
                            if (reader.GetValue(fc) is DBNull)
                            {
                                record[columnName] = null;
                            }
                            else
                            {
                                record[columnName] = reader.GetValue(fc);
                            }
                        }
                    }

                   
                    if (recordRetrievedCallback != null)
                    {

                        recordRetrievedCallback(record, lockObj, manualResetEvent,exceptionHandler);
                        if (exceptionHandler.IsExceptionPresent)
                            throw exceptionHandler.Exception;
                    }
                    else
                    {
                     
                        records.Add(record);
                    }

                   
                } // while

                if (recordRetrievedCallback != null)
                {
                    recordRetrievedCallback(null, lockObj, manualResetEvent, exceptionHandler);
                    lock (lockObj)
                    {
                        Monitor.Wait(lockObj);
                        if (exceptionHandler.IsExceptionPresent)
                            throw exceptionHandler.Exception;
                    }
               
                }           

            }
            catch (Exception e)
            {
                logger.Error(e.Message);
                throw e;
            }
            finally
            {
                try
                {
                    if (sqlComm != null) sqlComm.Dispose();
                    if (reader != null) reader.Close();
                    CloseTheConnection(connection);

                    if (lockObj != null)
                    {
                        lockObj = null;
                    }

                    if (exceptionHandler != null)
                    {
                        exceptionHandler = null;
                    }
                }
                catch (Exception e)
                {
                    logger.Error("Error while closing Database Connection", e);

                }
            }

            return records;

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="recordsList"></param>
        /// <param name="database"></param>
        /// <param name="tableName"></param>
        /// <param name="schemaFields"></param>
        /// <param name="accountId"></param>
        /// <param name="groupId"></param>
        /// <param name="userId"></param>
        /// <param name="versionId"></param>
        /// <returns></returns>
        public override bool PopulateRecords(List<AbstractRecord> recordsList, 
            string database, 
            string tableName, 
            Dictionary<string, string> schemaFields, 
            DataLoaderUtils dlu)
        {
            DbConnection conn = null;
            DbDataReader reader = null;
            DbCommand cmd = null;

            try
            {
                conn = ConnectToDatabase(database);
                cmd = conn.CreateCommand();
                cmd.CommandTimeout = 0;
                cmd.CommandText = ConstructSelectQuery(tableName, null, null, null, null);

                reader = cmd.ExecuteReader();

                int batchSize = 10000;
                object[] itemArray = new object[reader.FieldCount];
                DataTable table = GetTableFromReader(reader);

                List<string> transactionFieldNames = TransactionFieldNames;
                List<string> nontransactionFieldNames = new List<string>();
                

                foreach (KeyValuePair<string, string> field in schemaFields)
                {
                    if (!transactionFieldNames.Contains(field.Key))
                    {
                        nontransactionFieldNames.Add(field.Key);
                    }

                }

                nontransactionFieldNames.Remove(DmpSystemFields._DMP_ID);
                if (nontransactionFieldNames.Contains(DmpSystemFields.GEOMETRY))
                {
                    nontransactionFieldNames.Remove(DmpSystemFields.GEOMETRY);
                    nontransactionFieldNames.Add(DmpSystemFields.GEOMETRY_BIN);
                }

                nontransactionFieldNames.Remove(DmpSystemFields.DRAW_TYPE);
                nontransactionFieldNames.Remove(DmpSystemFields._SYMBOLOGY);

                for (int i = 0; i < recordsList.Count; i++)
                {
                    // copy row from reader to table
                    DataRow row = table.NewRow();

                    for (int j = 0; j < nontransactionFieldNames.Count; j++)
                    {

                        row[nontransactionFieldNames[j]] = recordsList[i][nontransactionFieldNames[j]];

                        
                    }

                    table.Rows.Add(row);

                    // if batch size reached, bulk copy the table.
                    if (table.Rows.Count == batchSize)
                    {
                        BulkCopyTable(database, table, dlu, tableName, batchSize);
                        table.Clear();
                        table = null;
                        table = GetTableFromReader(reader);
                    }
                }
                // bulk copy any remaining rows
                if (table != null && table.Rows.Count > 0)
                {
                    BulkCopyTable(database, table, dlu, tableName, batchSize);
                }

                dlu.updateVersion(TEVersion.VersionStatus.PUBLISHED);
            }
            catch (Exception ex)
            {
                try
                {
                    if (dlu != null)
                        dlu.updateVersion(TEVersion.VersionStatus.FAILED);
                }
                catch (Exception ex2)
                {
                    if (logger.IsErrorEnabled)
                        logger.Error("Error updating version to 'FAILED'.", ex2);
                }
                finally
                {
                    throw new ApplicationException("Error while populating records into the database: " + ex.Message) ;
                }
                
            }
            finally
            {
                cmd.Dispose();
                reader.Dispose();
                conn.Close();

            }

            return true;
        }


        /// <summary>
        /// Gets an empty DataTable from the specified data reader.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private  DataTable GetTableFromReader(DbDataReader reader)
        {
            DataTable schemaTable = reader.GetSchemaTable();
            DataTable table = new DataTable();
            foreach (DataRow row in schemaTable.Rows)
            {
                DataColumn column = new DataColumn(row["ColumnName"].ToString(), (Type)(row["DataType"]));
                table.Columns.Add(column);
            }
            return table;
        }


        /// <summary>
        /// Adds system fields to the specified table then bulk copies it to the destination table.
        /// </summary>
        /// <param name="table"></param>
        /// <param name="dlu"></param>
        /// <param name="destDbm"></param>
        /// <param name="destTableName"></param>
        private  void BulkCopyTable(string database, DataTable table, DataLoaderUtils dlu, string destTableName,
            int batchSize)
        {
            //get rid of this
            //destDbm.AppendSystemColumns(table, true/*transaction*/, false/*geometry*/);

            table.Columns[DmpSystemFields._SESSIONID].AllowDBNull = true;
            table.Columns[DmpSystemFields._RECSEQ].AllowDBNull = true;
            table.Columns[DmpSystemFields._CREATEDBY].AllowDBNull = true;

            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                PopulateTransactionFields(ref row, dlu);
            }

            table.Columns[DmpSystemFields._SESSIONID].AllowDBNull = false;
            table.Columns[DmpSystemFields._RECSEQ].AllowDBNull = false;
            table.Columns[DmpSystemFields._CREATEDBY].AllowDBNull = false;

            using (DbConnection conn = ConnectToDatabase(database))
            {
                this.BulkCopy(conn, table, destTableName, batchSize, 60/*timeOut*/);
            }
        }

        /// <summary>
        /// Efficiently copies data from a DataTable to a table in the database.
        /// Note: Computed columns will be removed from the DataTable before performing BulkCopy
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="table">DataTable to copy from</param>
        /// <param name="destinationTableName">database table to copy to</param>
        /// <param name="batchSize">
        /// Number of rows in each batch to send to server. (ignored in Sqlite)</param>
        /// <param name="timeOut">
        /// Number of seconds for operation to complete before it times out. 
        /// (ignored in Sqlite)</param>
        private  void BulkCopy(DbConnection conn, DataTable table, string destinationTableName,
            int batchSize, int timeOut)
        {
            if (table.Columns.Contains(DmpSystemFields._DMP_ID))
                table.Columns.Remove(DmpSystemFields._DMP_ID);

          //  int bulkCopyCostLimit = 3000;
            ValidateConnection(conn);

            using (SqlBulkCopy bcp = new SqlBulkCopy((SqlConnection)conn))
            {
                bcp.BulkCopyTimeout = timeOut;
                bcp.BatchSize = batchSize;
                bcp.DestinationTableName = "[" + destinationTableName + "]";

                for (int i = 0; i < table.Columns.Count; i++)
                    bcp.ColumnMappings.Add(table.Columns[i].ColumnName, table.Columns[i].ColumnName);
                int limit = GetSystemQueryGovernorCostLimit(conn);
                try
                {
                  //  if (bulkCopyCostLimit >= 0)
                 //   {
                  //      SetQueryGovernorCostLimit(conn, bulkCopyCostLimit);
                  //  }
                    bcp.WriteToServer(table);
                    bcp.ColumnMappings.Clear();
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                   // if (bulkCopyCostLimit >= 0)
                    {
                  //      SetQueryGovernorCostLimit(conn, limit);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="costLimit"></param>
        private void SetQueryGovernorCostLimit(DbConnection conn, int costLimit)
        {
            ValidateConnection(conn);

            // assume connection is already open
            DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = "SET QUERY_GOVERNOR_COST_LIMIT " + costLimit;
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Returns the Query Governor Cost Limit.
        /// </summary>
        /// <param name="conn">Can be open or closed. If open, it will remain open.</param>
        private int GetSystemQueryGovernorCostLimit(DbConnection conn)
        {
            ValidateConnection(conn);

            SqlCommand cmd = null;
            SqlDataReader reader = null;
            bool remainOpen;

            ValidateConnection(conn);

            // have the connection remain open if it is already open
            remainOpen = (conn.State == ConnectionState.Open);

            try
            {
                if (!remainOpen) conn.Open();
                cmd = (SqlCommand)conn.CreateCommand();
                cmd.CommandText = "select value_in_use from sys.configurations where name = 'query governor cost limit'";

                reader = cmd.ExecuteReader();
                if (reader != null && reader.Read())
                {
                    return reader.GetInt32(0);
                }
            }
            finally
            {
                if (reader != null) reader.Close();
                if (cmd != null) cmd.Cancel();
                if (conn != null && !remainOpen) conn.Close();
            }
            return 0;
        }

        /// <summary>
        /// Populates all the transaction fields, except _DMP_ID if it is a computed field in the database.
        /// </summary>
        /// <param name="row"></param>
        private void PopulateTransactionFields(ref DataRow row,
            Dmp.Neptune.DataLoader.DataLoaderUtils dlu)
        {
            //List<string> SystemFieldNames = GetDmpSystemFieldNames(SystemGroupType.TRANSACTION);
            List<string> transactionFieldNames = TransactionFieldNames;
            DateTime date = System.DateTime.Now;

            int sessionID = dlu.getSessionId();
            int recSeq = dlu.getNextRecordSequence();

            for (int i = 0; i < transactionFieldNames.Count; i++)
            {
                if (!row.Table.Columns.Contains(transactionFieldNames[i]))
                {
                    if (logger.IsErrorEnabled) logger.Error("no " + transactionFieldNames[i] + " field in the table structure");
                    throw new MissingFieldException("populateSystemFields - no " + transactionFieldNames[i] + " field in the table structure");
                }

                switch (transactionFieldNames[i])
                {
                    case DmpSystemFields._SESSIONID:
                        row[transactionFieldNames[i]] = sessionID; break;
                    case DmpSystemFields._RECSEQ:
                        row[transactionFieldNames[i]] = recSeq; break;
                    case DmpSystemFields._CREATEDBY:
                        row[transactionFieldNames[i]] = dlu.getVersionId(); break;
                    case DmpSystemFields._CREATEDDATE:
                        row[transactionFieldNames[i]] = date; break;
                    case DmpSystemFields._STATUS:
                        row[transactionFieldNames[i]] = TETransaction.RecordStatus.Active; break;
                    default:
                        row[transactionFieldNames[i]] = DBNull.Value; break;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="recordsList"></param>
        /// <param name="database"></param>
        /// <param name="tableName"></param>
        /// <param name="schemaFields"></param>
        /// <param name="indexedFields"></param>
        /// <returns></returns>
        public override bool PopulateRecords(List<AbstractRecord> recordsList,
           string database,
           string tableName,
           Dictionary<string,
           string> schemaFields,
           String[] indexedFields,
           bool deleteExistingTable)
        {
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
         public override string GetTransactionClause(string tableName)
        {
            // add transaction stuff
            TransactionFilter transFilter = new TransactionFilter();
            transFilter.ActiveVersion = ActiveVersionId;
            transFilter.TableAlias = tableName;
            return transFilter.GetCriteria();
        }
        
       
    }
}
