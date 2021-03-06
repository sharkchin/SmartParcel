﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using DMP.MasterProgram.Processors.AnalysisEngine.Algorithms;
using DMP.MasterProgram.Processors.AnalysisEngine.Geometries;
using Dmp.Neptune.Collections;
using Dmp.Neptune.Data;
using Dmp.Neptune.Utils.ShapeFile;
using DMP.MasterProgram.Utils;
using Dmp.Neptune.DatabaseManager;
using log4net;
using log4net.Config;
using Microsoft.SqlServer.Types;
using System.IO;
using System.Text.RegularExpressions;
using System.Data.SQLite;
using System.Windows;


namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class GetSqliteCL : IGeometryAlgorithm
    {
        public const int SRID = 4269;
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private List<AbstractRecord> impactors;
        private Dictionary<String, String> parameters;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="record">Subject Record</param>
        /// <returns>List of Impactors intersect with Subject Record</returns>
        public object ProcessRecord(AbstractRecord record, bool isSubByTask)
        {
            object o = record[MasterProgramConstants.GEOMETRY_BIN];
            if (o is DBNull)
                return null;
            SqlGeometry parcel = (SqlGeometry)o, ev = parcel.STEnvelope();
            o = record["LOCATION_ID"];
            string locid = o is DBNull ? "" : (string)o;
            o = record["lat"];
            double lat = o is DBNull || o == null ? -9999 : (double)o;
            o = record["lon"];
            double lon = o is DBNull || o == null ? -9999 : (double)o;
            Vector parcelvec = new Vector(lon, lat);
            List<AbstractRecord> reclist = new List<AbstractRecord>();
            string sourceSqlite = @"\\dmpdpu3\d$\Projects\ParcelAnalytics\rset_s03_data\A_TESTACCOUNT\G100701\U233184\LAYERS\ParcelAnalytics_old\ver1.14\parcelanalytics.s3db";
            using (SQLiteConnection lConn = new SQLiteConnection(String.Format(@"Data Source={0};Version=3;New=True;Compress=True;", sourceSqlite)))
            {
                lConn.Open();
                SQLiteCommand lCmd = new SQLiteCommand(
                    String.Format("select * from parcelanalytics where locationid='{0}'", locid),
                    lConn);
                SQLiteDataReader lReader = lCmd.ExecuteReader();
                while (lReader.Read())
                {
                    DataRecord rec = new DataRecord(new String[] { });
                    for (int i = 0; i < lReader.FieldCount; i++)
                    {
                        string name = lReader.GetName(i);
                        Object value = lReader.GetValue(i);
                        rec[name] = value;
                    }
                    reclist.Add(rec);
                }
            }
            if (reclist.Count == 0)
                return false;
            if (reclist.Count == 1)
                return reclist[0]["CL"];
            double maxdist = double.MaxValue;
            AbstractRecord bestRec = null;
            for (int i = 0; i < reclist.Count; i++)
            {
                o = reclist[i]["lat"];
                double reclat = o is DBNull || o == null ? -9999 : (double)o;
                o = reclist[i]["lon"];
                double reclon = o is DBNull || o == null ? -9999 : (double)o;
                Vector recvec = new Vector(reclon, reclat);
                if ((parcelvec - recvec).Length < maxdist)
                {
                    maxdist = (parcelvec - recvec).Length;
                    bestRec = reclist[i];
                }
            }
            return bestRec["CL"];
        }


        /// <summary>
        /// set the Impactor List
        /// </summary>
        /// <param name="impactors">impactor List</param>
        public void InitializeImpactors(List<AbstractRecord> impactors)
        {
            this.impactors = impactors;

        }

        /// <summary>
        /// set the parameter List
        /// </summary>
        /// <param name="parameters"></param>
        public void InitializeParameters(Dictionary<String, String> parameters)
        {
            this.parameters = parameters;
        }
    }
}
