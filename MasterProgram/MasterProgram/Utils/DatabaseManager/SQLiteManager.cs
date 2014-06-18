using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DMP.MasterProgram.Processors;
using Dmp.Neptune.Collections;
using System.Data.SQLite;
using log4net;
using log4net.Config;
using DMP.MasterProgram.Utils;
using System.Data.Common;
using DMP.MasterProgram.Utils.DatabaseManager;
using System.IO;
using DMP.MasterProgram.ProcessorMetadata;
using System.Data;
using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;
using System.Threading;
using Dmp.Neptune.DataLoader;

namespace DMP.MasterProgram.Utils.DatabaseManager
{
    class SQLiteManager :MasterProgDBManager
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
       

        /// <summary>
        /// 
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public override DbConnection ConnectToDatabase(string database)
        {
            DbConnection connection;
            string _connString = "Data source=" + database;
            try
            {
                connection = new SQLiteConnection(_connString);
                connection.Open();
            }
            catch (Exception e)
            {
                logger.Error("Error while opening  SQLite Database Connection to " + database, e);
                throw new ApplicationException("Error while opening  SQLite Database Connection to " + database + " : " + e.Message);
            }
            return connection;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        public override void CloseTheConnection(DbConnection connection)
        {
            try
            {
                //check if connection is open using connection.State
                connection.Close();
            }
            catch (Exception e)
            {
                logger.Error("Error while closing  SQLite Database Connection", e);
                throw new ApplicationException("Error while closing  SQLite Database Connection: "+e.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn"></param>
        public override void ValidateConnection(DbConnection conn)
        {
            if (!(conn is SQLiteConnection))
                throw new ApplicationException(
                    "Invalid DbConnection. You must use a DbConnection obtained from DBManager.CreateDbConnection().");
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
            List<AbstractRecord> records = null;

            SqlGeometry inclusionGeom = null;
            SqlGeometry exclusionGeom = null;

            string inclusionWKT = dataSet.InclusionWKT;
            string exclusionWKT = dataSet.ExclusionWKT;

            query = ConstructSelectQuery(tableName, attributeCriteria, fields, null, null);

            try
            {
                if (!String.IsNullOrEmpty(inclusionWKT))
                {
                    inclusionGeom = SqlGeometry.STGeomFromText(new SqlChars(inclusionWKT), 4269);
                }

                if (!String.IsNullOrEmpty(exclusionWKT))
                {
                    exclusionGeom = SqlGeometry.STGeomFromText(new SqlChars(exclusionWKT), 4269);
                }

                records = FetchAndProcessRecords(database, query, inclusionGeom, exclusionGeom, recordRetrievedCallback);
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return records;
        }

        public override List<AbstractRecord> FetchRecords(string database,
           string query,
           SqlGeometry inclusionGeom,
           SqlGeometry exclusionGeom)
        {
            return FetchAndProcessRecords(database, query, inclusionGeom, exclusionGeom, null);
        }

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


            SqlGeometry sqlGeometry = null;
            DataRecord record = null;
            DbConnection connection = null;
            DbCommand sqlComm = null;
            DbDataReader reader = null;
            Object lockObj = new object();
            ManualResetEvent manualResetEvent = null;
            if (recordRetrievedCallback != null)
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
                    record = new DataRecord(new String[] { });

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
                            sqlGeometry = SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes((byte[])reader.GetValue(fc)), 4269);
                            record[MasterProgramConstants.GEOMETRY_BIN] = sqlGeometry;
                            continue;
                        }
                        else
                        {
                            record[columnName] = reader.GetValue(fc);
                        }
                    }

                    bool include = true;
                    bool exclude = false;
                    if (inclusionGeom != null)
                    {
                        include =(bool) sqlGeometry.STIntersects(inclusionGeom);
                    }

                    if(exclusionGeom != null)
                    {
                        exclude = (bool)sqlGeometry.STIntersects(exclusionGeom);
                    }

                    if (include && !exclude)
                    {
                        if (recordRetrievedCallback != null)
                        {
                           recordRetrievedCallback(record, lockObj, manualResetEvent, exceptionHandler);
                            int numWorkers, numIoThreads;
                            ThreadPool.GetAvailableThreads(out numWorkers, out numIoThreads);
                            if (numWorkers == 0 || numIoThreads==0)
                            {
                                if (exceptionHandler.IsExceptionPresent)
                                    throw exceptionHandler.Exception;
                                manualResetEvent.WaitOne();
                                manualResetEvent.Reset();
                            }
                        }
                        else
                        {
                            records.Add(record);
                        }
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
                throw new ApplicationException(e.Message);
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

                     sqlGeometry = null;
                }
                catch (Exception e)
                {
                    logger.Error("Error while closing   Database Connection", e);

                }
            }

            return records;
        }

        /// <summary>
        /// stor records into the database
        /// </summary>
        /// <param name="recordsList">list of Records</param>
        /// <param name="dbResource">Data base Name</param>
        /// <param name="tableName">Table Name</param>
        /// <param name="attributes">Record Columns</param>
        /// <param name="indexedFields">Array of indexed fields</param>
        /// <returns></returns>
        public override bool PopulateRecords(List<AbstractRecord> recordsList,
            string database,
            string tableName,
            Dictionary<string,string> attributes,
            String[] indexedFields,
            bool deleteExistingTable)
        {
            DbConnection connection = null;
            DbCommand sqlComm = null;

            try
            {
                connection = ConnectToDatabase(database);
                sqlComm = connection.CreateCommand();
                DataTable table = new DataTable();

                table.TableName = tableName;

                foreach (KeyValuePair<string, string> attribute in attributes)
                {

                    table.Columns.Add(attribute.Key, GetDotNetType(attribute.Value));
                }

                List<string> attributeNameArray = attributes.Keys.ToList();
                CreateDmpSqlTable(connection, table, deleteExistingTable, false, false, false, indexedFields);

                String insertQuery = ConstructInsertQuery(tableName, attributeNameArray);

                using (DbTransaction mytransaction = connection.BeginTransaction())
                {

                    using (sqlComm)
                    {

                        sqlComm.CommandText = insertQuery;
                        for (int i = 0; i < attributes.Count; i++)
                        {
                            sqlComm.Parameters.Add(sqlComm.CreateParameter());
                        }

                        sqlComm.Prepare();

                        for (int i = 0; i < recordsList.Count; i++)
                        {
                            AbstractRecord record = recordsList.ElementAt(i);

                            for (int k = 0; k < attributeNameArray.Count; k++)
                            {
                                if (attributeNameArray.ElementAt(k).Equals(MasterProgramConstants.GEOMETRY_BIN))
                                {
                                   sqlComm.Parameters[k].Value =((SqlGeometry) record[attributeNameArray.ElementAt(k)]).STAsBinary().Value;
                                }
                                else
                                {
                                    sqlComm.Parameters[k].Value = record[attributeNameArray.ElementAt(k)];
                                }
                            }

                            sqlComm.ExecuteNonQuery();

                        }
                    }
                    mytransaction.Commit();
                }



            }
            catch (Exception e)
            {
                logger.Error("Error while populating data into the  database", e);
                throw new ApplicationException("Error while populating data into the  database: " + e.Message);

            }
            finally
            {
                try
                {
                    CloseTheConnection(connection);
                }
                catch (Exception e)
                {
                    logger.Error("Database Coonection not closed correctly");

                }

            }
            return true;
        }

        public override bool PopulateRecords(List<AbstractRecord> recordsList,
           string database,
           string tableName,
           Dictionary<string, string> schemaFields,
           DataLoaderUtils dlu)
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
            return null;
        }

        
    }
}
