using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using log4net.Config;
using System.Data.Common;
using Dmp.Neptune.Collections;
using System.Data;
using Dmp.Neptune.Utils;
using Dmp.Neptune.Data;
using System.IO;
using DMP.MasterProgram.ProcessorMetadata;
using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;
using Dmp.Neptune.DataLoader;


namespace DMP.MasterProgram.Utils.DatabaseManager
{
    public abstract class MasterProgDBManager
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public abstract DbConnection ConnectToDatabase(string database);
        public abstract void CloseTheConnection(DbConnection connection);       
        public abstract void ValidateConnection(DbConnection conn);
        public abstract string GetTransactionClause(string tableName);
        public abstract bool PopulateRecords(List<AbstractRecord> recordsList,
            string database,
            string tableName,
            Dictionary<string,
            string> attributes,
            String[] indexedFields,
            bool deleteExistingTable);
        public abstract bool PopulateRecords(List<AbstractRecord> recordsList,
           string database,
           string tableName,
           Dictionary<string, string> schemaFields,
           DataLoaderUtils dlu);

        public abstract List<AbstractRecord> FetchAndProcessRecords(MasterProgramMetadata.InputDataSet dataSet,
            OnRecordRetrieved recordRetrievedCallback);
        public abstract List<AbstractRecord> FetchRecords(MasterProgramMetadata.InputDataSet dataSet);
        public abstract List<AbstractRecord> FetchRecords(string database,
            string query,
            SqlGeometry inclusionGeom,
            SqlGeometry exclusionGeom);
        public abstract List<AbstractRecord> FetchAndProcessRecords(string database,
            string query,
            SqlGeometry inclusionGeom,
            SqlGeometry exclusionGeom,
            OnRecordRetrieved recordRetrievedCallback);

        /// <summary>
        /// Fetch all records based on given CFTId
        /// </summary>
        /// <param name="dbResource">DataBase Name</param>
        /// <param name="tableName">Table Name</param>
        /// <param name="CFTId"></param>
        /// <returns></returns>
        public  List<string> FetchChildCFTIds(string database,
            string tableName,
            string CFTId)
        {
            DbConnection connection = null;
            DbCommand sqlComm = null;
            DbDataReader reader = null;
            StringBuilder builder = new StringBuilder();

            //return 
            List<string> records = new List<string>();


            if (database == null || String.IsNullOrEmpty(database))
                throw new ApplicationException("DataBaseResource is not defined");

            if (tableName == null || String.IsNullOrEmpty(tableName))
                throw new ApplicationException("Table Name for DataBaseResource " + database + " is not defined");
            try
            {

                connection = ConnectToDatabase(database);
                sqlComm = connection.CreateCommand();
                builder.AppendFormat("Select {0}", MasterProgramConstants.CFTID);
                builder.AppendFormat(" from {0}", tableName);
                builder.AppendFormat(" where {0} Like '{1}'", MasterProgramConstants.CFTID, CFTId);

                sqlComm.CommandText = builder.ToString();
                reader = sqlComm.ExecuteReader();

                while (reader.Read())
                {
                    records.Add(reader.GetString(0));
                }

            }
            catch (Exception e)
            {
                logger.Error("Error while fetching records based on CFTId from  Database", e);
                throw new ApplicationException("Error while fetching records based on CFTId from  Database: " + e.Message);
            }
            finally
            {
                try
                {
                    CloseTheConnection(connection);
                }
                catch (Exception e)
                {
                    logger.Error("Error while closing   Database Connection", e);
                }
            }


            return records;

        }

       
        /// <summary>
        /// Create SQL Table
        /// </summary>
        /// <param name="conn">connection</param>
        /// <param name="table"></param>
        /// <param name="deleteExistingTable"></param>
        /// <param name="addTransactionFields"></param>
        /// <param name="addGeometryFields"></param>
        /// <param name="addGeometryIndexFields"></param>
        /// <param name="indexedFields"></param>
        public void CreateDmpSqlTable(
            DbConnection conn,
            DataTable table,
            bool deleteExistingTable,
            bool addTransactionFields,
            bool addGeometryFields,
            bool addGeometryIndexFields,
            string[] indexedFields)
        {
            if (TableExists(conn, table.TableName) && !deleteExistingTable)
                return;
            ValidateConnection(conn);

            if (table == null || String.IsNullOrEmpty(table.TableName))
                throw new ApplicationException("Table name is empty! Can not create sql string!");

            String CR = " \n\r";

            String tableName = table.TableName;
           

            StringBuilder sb = new StringBuilder();
            if (deleteExistingTable)
            {
                DropTableIfExists(conn, tableName);
            }

            sb.AppendFormat("CREATE TABLE [{0}] ( " + CR, tableName);

            bool bNeedComma = false;

           

            //now add the fields passed by the user in table
            if (table.Columns.Count > 0)
            {
                if (bNeedComma) sb.Append("," + CR);
                int count = table.Columns.Count;

                for (int i = 0; i < count; i++)
                {
                    DataColumn column = table.Columns[i];

                    string columnName = column.ColumnName;

                    string type = GetSQLTypeFromDotNetType(column.DataType, 100);

                    sb.AppendFormat("[{0}] {1} ", columnName, type);

                    if (i + 1 != count)
                    {
                        sb.Append("," + CR);
                    }
                }//for i
            }

            // the comma is left if the last column is a system field, so cut it out
            if (sb.ToString().EndsWith("," + CR))
                sb = sb.Remove(sb.Length - 4, 4);

            sb.Append(")" + CR);

            DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sb.ToString();
            cmd.ExecuteNonQuery();

            // index name, table name
            if (indexedFields != null)
            {
                CreateDmpSqlTableIndexes(cmd, tableName, indexedFields, addGeometryFields, addGeometryIndexFields);
            }
        }

        /// <summary>
        /// Give SQLType from DotNetType
        /// </summary>
        /// <param name="type"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public virtual string GetSQLTypeFromDotNetType(Type type, int length)
        {
            // Note: float has double-precision by default in sql server
            // always use double-precision in database
            if (type == typeof(double) || type == typeof(float))
                return "float";
            else if (type == typeof(bool))
                return "bit";
            else if (type == typeof(string) || type == typeof(char[]))
                return string.Format("varchar({0}) ", length);
            else if (type == typeof(byte[]))
                return "image";
            else if (type == typeof(short) || type == typeof(Int16))
                return "smallint";
            else if (type == typeof(long) || type == typeof(Int64))
                return "bigint";
            else if (type == typeof(int) || type == typeof(Int32))
                return "int";
            else if (type == typeof(DateTime))
                return "datetime";
            else if (type == typeof(DmpDateTime))
                return "datetime2";
            else if (type == typeof(System.Xml.XmlDocument) || type == typeof(DmpXmlString))
                return "xml";
            else if (type == typeof(Microsoft.SqlServer.Types.SqlGeography))
                return "geography";
            else if (type == typeof(Microsoft.SqlServer.Types.SqlGeometry))
                return "blob";
            throw new NotSupportedException("GetSQLTypeFromDotNetType Need to add the SQL type for " + type.Name);
        }

        /// <summary>
        /// Get DotNet Type based on string
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public virtual Type GetDotNetType(string type)
        {
            // Note: float has double-precision by default in sql server
            // always use double-precision in database
            type = type.ToLower();

            if (type.Contains("real")) return typeof(double);
            if (type.Contains("float")) return typeof(double);
            if (type.Contains("double")) return typeof(double);
            if (type.Contains("bit")) return typeof(bool);
            if (type.Contains("string")) return typeof(string);
            if (type.Contains("varchar")) return typeof(string);
            if (type.Contains("image")) return typeof(byte[]);
            if (type.Contains("smallint")) return typeof(short);
            if (type.Contains("bigint")) return typeof(long);
            if (type.Contains("integer")) return typeof(int);
            if (type.Equals("datetime")) return typeof(DateTime);
            if (type.Equals("datetime2")) return typeof(DmpDateTime);
            if (type.Contains("varbinary")) return typeof(SqlGeometry);
            

            throw new NotSupportedException("GetDotNetTypeFromSQLType : Need to add the SQL type for " + type);
        }

        /// <summary>
        /// drop the existing table
        /// </summary>
        /// <param name="conn">connection</param>
        /// <param name="tableName">Table Name</param>
        public virtual void DropTableIfExists(DbConnection conn, string tableName)
        {
            if (TableExists(conn, tableName))
                DropTable(conn, tableName);
        }

        
        
        /// <summary>
        /// Returns true if the table exists in the database.
        /// </summary>
        /// <param name="conn">connection </param>
        /// <param name="tableName">Table Name</param>
        /// <returns>true/false</returns>
        public virtual bool TableExists(DbConnection conn, string tableName)
        {
            return ObjectExists(conn, tableName, "table");
        }

        /// <summary>
        /// Returns true if an object, such as a table or view, with the give name exists in the database.
        /// Warning: prone to sql injection.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="objectName"></param>
        /// <param name="objectType">
        /// 'u' for user table.
        /// 'v' for view.</param>
        protected virtual bool ObjectExists(DbConnection conn, string objectName, string objectType)
        {
            ValidateConnection(conn);

            bool objectExists;
            DbCommand comm = conn.CreateCommand();
            comm.CommandText = string.Format(
                "select 1 from Sqlite_master where type='{0}' and name='{1}' limit 1",
                objectType, objectName);
            DbDataReader reader = null;

            try
            {
                reader = comm.ExecuteReader();
                objectExists = reader.HasRows;
            }
            finally
            {
                if (reader != null) reader.Close();
            }
            return objectExists;
        }

        // remove this method (nevermind, need this to drop temp tables)
        /// <summary>
        /// Note: Requires LOADER Access Level
        /// </summary>
        public virtual void DropTable(DbConnection conn, string tableName)
        {
            ValidateConnection(conn);

            DbCommand command = conn.CreateCommand();
            command.CommandText = string.Format("drop table [{0}]", tableName);
            try
            {
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("DBManager.DropTable: Error in dropping table {0}:{1}", tableName, ex.Message);
                throw new ApplicationException("DBManager.DropTable: Error in dropping table: " + tableName);
            }
            finally
            {
                command.Dispose();
            }
        }


        /// <summary>
        /// Create Indesxs for sql Table
        /// </summary>
        /// <param name="cmd">Database Command</param>
        /// <param name="tableName">Table Name</param>
        /// <param name="indexedFields">Array of Indexed Fields</param>
        /// <param name="hasGeometry"></param>
        /// <param name="hasGeometryIndexFields"></param>
        protected void CreateDmpSqlTableIndexes(DbCommand cmd, string tableName, string[] indexedFields, bool hasGeometry, bool hasGeometryIndexFields)
        {
            StringBuilder sql = new StringBuilder();

            // create indices on indexFields
            if (indexedFields != null)
            {
                foreach (string indexField in indexedFields)
                    sql.AppendFormat("CREATE INDEX [idx_{1}_{0}] ON [{0}]([{1}]);\n", tableName, indexField);
            }

            // create indices on SIDX and _CFTID
            if (hasGeometryIndexFields)
            {
                sql.AppendFormat("CREATE INDEX [idx_{1}_{0}] ON [{0}]([{1}]);\n", tableName, DmpSystemFields.SIDX);
                sql.AppendFormat("CREATE INDEX [idx_{1}_{0}] ON [{0}]([{1}]);\n", tableName, DmpSystemFields._CFTID);
            }
            // Note: Sqlite does not support clustered indexes
            cmd.CommandText = sql.ToString();
            cmd.ExecuteNonQuery();

        }

       
        /// <summary>
        /// Construct the SQL Insert Query
        /// </summary>
        /// <param name="tableName">Table Name</param>
        /// <param name="attributeList">Table Column Names</param>
        /// <returns></returns>
        protected string ConstructInsertQuery(string tableName, List<string> attributeList)
        {
            if (attributeList == null || attributeList.Count == 0)
                throw new Exception("Attribute Map is empty");

            StringBuilder builder = new StringBuilder();
            StringBuilder subBuilder = new StringBuilder();


            builder.AppendFormat("INSERT INTO [{0}] (", tableName);

            bool isFirstField = true;

            subBuilder.Append("Values(");

            for (int i = 0; i < attributeList.Count; i++)
            {
                string attributeName = attributeList.ElementAt(i);


                if (!isFirstField)
                {
                    builder.Append(", ");
                    subBuilder.Append(", ");
                }



                builder.Append(attributeName);
                subBuilder.Append("?");
                isFirstField = false;
            }



            subBuilder.Append(")");
            builder.AppendFormat(") {0}", subBuilder.ToString());
            return builder.ToString();
        }
     
        protected string ConstructSelectQuery(string tableName,
            string attributeCriteria,
            List<string> fields,
            string inclusionWKT,
            string exclusionWKT)
        {
            bool isAllFields = false;

            if (fields == null || fields.Count == 0  )
                isAllFields = true;

            StringBuilder builder = new StringBuilder();

            bool isFirstField = true;
            builder.Append("SELECT ");

            //if no fields mention Select all
            if (isAllFields)
                builder.Append("* ");
            else
            {
                for (int i = 0; i < fields.Count; i++)
                {


                    if (!isFirstField)
                        builder.Append(", ");
                    builder.AppendFormat("[{0}]", fields[i]);


                    isFirstField = false;

                }
            }

            builder.AppendFormat(" from [{0}]", tableName);

            //if attributeCriteria present 
            bool isMoreCriteria = false;


            if (!String.IsNullOrEmpty(inclusionWKT)) //|| !String.IsNullOrWhiteSpace(inclusionWKT))
            {
                if (isMoreCriteria)
                    builder.Append(" and ");
                else
                    builder.Append(" where ");

                builder.AppendFormat("[{0}].STIntersects(geometry::STGeomFromText('{1}', '{2}')) = 1", MasterProgramConstants.GEOMETRY_BIN, inclusionWKT, MasterProgramConstants.SRID);

                isMoreCriteria = true;
                //TO_DO and CFTID criteria too

            }

            if (!String.IsNullOrEmpty(exclusionWKT))
            {
                if (isMoreCriteria)
                    builder.Append(" and ");
                else
                    builder.Append(" where ");

                builder.AppendFormat("[{0}].STIntersects(geometry::STGeomFromText('{1}', '{2}')) = 0", MasterProgramConstants.GEOMETRY_BIN, inclusionWKT, MasterProgramConstants.SRID);

                isMoreCriteria = true;
            }

            if (!String.IsNullOrEmpty(attributeCriteria))
            {
                if (isMoreCriteria)
                    builder.Append(" and ");
                else
                    builder.Append(" where ");
                builder.AppendFormat("{0}", attributeCriteria);
                isMoreCriteria = true;

            }


            return builder.ToString();

        }


    }
}
