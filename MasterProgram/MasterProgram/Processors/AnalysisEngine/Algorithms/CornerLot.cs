using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DMP.MasterProgram.Processors.AnalysisEngine.Algorithms;
using DMP.MasterProgram.Processors.AnalysisEngine.Geometries;
using Dmp.Neptune.Collections;
using DMP.MasterProgram.Utils;
using log4net;
using log4net.Config;
using MonoGIS.NetTopologySuite.Geometries;
using MonoGIS.NetTopologySuite.IO;
using Microsoft.SqlServer.Types;
using System.Windows;
using Dmp.Neptune.Utils.ShapeFile;
using System.Diagnostics;
using System.IO;
//using Wintellect.PowerCollections;



namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class CornerLot : IGeometryAlgorithm
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        //private static string logfile = GetValidFileName(@"./CornerLotlog.txt");
        private static object writerlock = new object();
        //private static HashSet<string> blacklist = new HashSet<string> { "100660192_138334838" };
        //private static Queue<KeyValuePair<string, SqlGeometry>> BigParcelDict = new Queue<KeyValuePair<string, SqlGeometry>>();
        private List<AbstractRecord> impactors;
        private Dictionary<String, String> parameters;
        public static string GetValidFileName(string fName)
        {
            int f = 0;
            string name = Path.GetFileNameWithoutExtension(fName);
            string path = Path.GetDirectoryName(fName);
            string ext = Path.GetExtension(fName);
            char div = Path.DirectorySeparatorChar;
            while (File.Exists(fName))
            {
                f += 1;
                fName = String.Format(@"{0}{4}{1}{2:d2}{3}", path, name, f, ext, div);
            }
            return fName;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="record">Subject Record</param>
        /// <returns>List of Impactors intersect with Subject Record</returns>
        public object ProcessRecord(AbstractRecord record, bool isSubByTask)
        {
            //Stopwatch timer = Stopwatch.StartNew();
            
            object o = record[MasterProgramConstants.GEOMETRY_BIN];
            if (o is DBNull)
                return null;
            //otherwise
            SqlGeometry jnct = (SqlGeometry)o,
                clline = (SqlGeometry)record["CornerLotLine"];
            if (clline == null || clline.STIsEmpty())
                return null;
            List<AbstractRecord> candidates = new List<AbstractRecord>(), parcels = new List<AbstractRecord>();
            double METER_PER_DEGREE = GeoUtils.MetersPerDegree(jnct.STX.Value, jnct.STY.Value, jnct.STSrid.Value);
            //Stopwatch t1 = new Stopwatch(), t2 = new Stopwatch(), t3 = new Stopwatch();
            for (int i = 0; i < impactors.Count; i++)
            {                
                AbstractRecord impactor = impactors.ElementAt(i);
                o = impactor["LANDUSE_CATEGORY"];
                string landuse = (o == null) || (o is DBNull) ? "" : (string)o;
                
                o = impactor["CAL_ACREAGE"];
                double area = (o == null) || (o is DBNull) ? 0 : (double)o;
                o = impactor[MasterProgramConstants.GEOMETRY_BIN];
                if (o is DBNull)
                    continue;
                //otherwise
                SqlGeometry imp = (SqlGeometry)o;
                o = impactor["DMPID"];  //NOTICE this DMPID might not equal to _DMP_ID due to idenfication purpose for multi-location parcels
                string dmpid = (o == null) || (o is DBNull) ? "" : o.ToString();
                //if (dmpid != "" && blacklist.Contains(dmpid))
                //{
                //    imp = SqlGeometry.Null;
                //    return null;
                //}
                
                if (clline.STDistance(imp)<0.00008)
                {
                    #region obsolete code
                    //if (imp.STNumPoints() > 1000)
                    //{
                        
                    //    bool found = false;
                    //    lock (BigParcelDict)
                    //    {
                    //        foreach (var v in BigParcelDict)
                    //        {
                    //            if (v.Key == dmpid)
                    //            {
                    //                imp = v.Value;
                    //                found = true;
                    //                break;
                    //            }
                    //        }
                    //    }
                    //    if (!found)
                    //    {
                    //        //if (landuse != "RESIDENTIAL" && area > 1)
                    //        //    continue;
                    //        try
                    //        {
                    //            imp = GeoUtils.Reduce2NumPoints(imp, 1000);
                    //        }
                    //        catch (Exception e)
                    //        {
                    //            o = record["ID"];
                    //            double id = o is DBNull ? -1 : (double)o;
                    //            string latlon = jnct.STStartPoint().STY.Value.ToString() + ", " + jnct.STStartPoint().STX.Value.ToString();
                    //            //Write2Log(logfile, "impactor1, " + id + ", " + latlon + ", "+ dmpid);
                    //            Console.WriteLine("too many points in impactor with CFTID " + impactor["_CFTID"] + " due to error:" + e.Message);
                    //            imp = SqlGeometry.Null;
                    //            if (dmpid != null)
                    //            {
                    //                lock (blacklist)
                    //                    blacklist.Add(dmpid);
                    //            }
                    //            return null;
                    //        }
                    //        finally
                    //        {
                    //            lock (BigParcelDict)
                    //            {
                    //                BigParcelDict.Enqueue(new KeyValuePair<string, SqlGeometry>(dmpid, imp));
                    //                if (BigParcelDict.Count > 20)
                    //                    BigParcelDict.Dequeue();
                    //                Console.WriteLine("BigParcelDict now contains: " + impactor["LOCATION_ID"] + ", having " + BigParcelDict.Count + " elements");
                    //            }
                    //        }
                    //    }                        
                    //}
                    #endregion
                    clline = clline.STDifference(imp);
                    if (clline.STIsEmpty())
                    {
                        //o = record["ID"];
                        //double id = o is DBNull ? -1 : (double)o;
                        //string latlon = jnct.STStartPoint().STY.Value.ToString() + ", " + jnct.STStartPoint().STX.Value.ToString();
                        //Write2Log(logfile, "impactor2, " + id + ", " + latlon);
                        return null;
                    }
                    imp = SqlGeometry.Null;
                    DataRecord parcel = new DataRecord(new string[] { "DMPID","LOCATIONID","CFTID", "Lon", "Lat", "GEOMETRY", "CL" });
                    o = impactor["DMPID"];
                    parcel["DMPID"] = (o == null) || (o is DBNull) ? null : o.ToString();
                    o = impactor["LOCATION_ID"];
                    parcel["LOCATIONID"] = (o == null) || (o is DBNull) ? null : o.ToString();
                    o = impactor["_CFTID"];
                    parcel["CFTID"] = (o == null) || (o is DBNull) ? null : o.ToString();
                    o = impactor["_X_COORD"];
                    parcel["Lon"] = (o == null) || (o is DBNull) ? double.NaN : (double)o;
                    o = impactor["_Y_COORD"];
                    parcel["Lat"] = (o == null) || (o is DBNull) ? double.NaN : (double)o;
                    o = impactor["GEOMETRY_BIN"];
                    SqlGeometry sg = (o == null) || (o is DBNull) ? SqlGeometry.Null : (SqlGeometry)o;
                    //parcel["GEOMETRY"] = (o == null) || (o is DBNull) ? null : sg.STAsBinary().Value;
                    parcel["GEOMETRY"] = sg;
                    parcel["CL"] = 1;
                    parcel["InterCFTID"] = record["_CFTID"];
                    parcel["InterGeometry"] = jnct;
                    parcel["Dist2Inter"] = 0;
                    parcel["CalcAcreage"] = area;
                    parcel["LandUse"] = landuse;
                    parcel["APN"] = impactor["APN"];
                    //if (area < 3 && landuse == "RESIDENTIAL")
                    candidates.Add(parcel);                                        
                }                
            }            
            int k = 0;
            SqlGeometry realbuf = new SqlGeometry();
            for (int i = 0; i < clline.STNumGeometries(); i++)
            {
                SqlGeometry sg = clline.STGeometryN(i + 1);
                if (sg.STDistance(jnct)<0.00003)
                {
                    if (k++ == 0)
                        realbuf = sg;
                    else
                        realbuf = realbuf.STUnion(sg);
                }
            }
            if (realbuf.STIsEmpty())
                return null;
            //otherwise    
            Dictionary<int, DataRecord> dict = new Dictionary<int, DataRecord>();
            int n=realbuf.STNumGeometries().Value;
            List<AbstractRecord> candidatescp = new List<AbstractRecord>(candidates);
            foreach (DataRecord parcel in candidatescp)
            {
                //byte[] b = (byte[])parcel["GEOMETRY"];
                //SqlGeometry sg = SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(b), 4269);
                
                SqlGeometry sg = (SqlGeometry)parcel["GEOMETRY"];
                for (int i = 1; i <= n; i++)
                {
                    SqlGeometry cli = realbuf.STGeometryN(i);
                    double dist = sg.STDistance(jnct).Value * METER_PER_DEGREE;
                    parcel["Dist2Inter"] = dist;
                    if (sg.STDistance(cli) < 0.00001)
                    {
                        if (!dict.ContainsKey(i))
                        {
                            //parcel["dist"] = dist;
                            dict.Add(i, parcel);                            
                        }
                        else
                        {
                            DataRecord oldp=dict[i];
                            if ((double)oldp["Dist2Inter"] > dist)
                            {
                                //parcel["dist"] = dist;
                                dict[i] = parcel;                                
                            }
                        }
                        parcels.Add(parcel);
                        candidates.Remove(parcel);
                    }
                }
                //if (sg.STDistance(realbuf) < 0.00008)
                //{
                //    parcels.Add(parcel);
                //}
            }
            //check all the neighbors
            Dictionary<int,DataRecord> dictcp=new Dictionary<int,DataRecord>(dict);
            foreach (DataRecord parcel in candidates)
            {
                SqlGeometry sg = (SqlGeometry)parcel["GEOMETRY"];
                foreach (var v in dict)
                {
                    DataRecord oldp = v.Value;
                    SqlGeometry oldg = (SqlGeometry)oldp["GEOMETRY"];
                    if (sg.STDistance(oldg) < 0.00001)
                    {
                        //double dist = sg.STDistance(jnct).Value;
                        if ((double)parcel["Dist2Inter"] < (double)oldp["Dist2Inter"])
                        {                            
                            //dictcp[v.Key] = parcel;
                            parcels.Add(parcel);
                        }
                    }
                }
            }
            dict = null;
            dictcp = null;
            //foreach (var v in dictcp)
            //{
            //    parcels.Add(v.Value);
            //}
           // Console.WriteLine("CornerLot:" + TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds));
            //foreach (DataRecord r in parcels)
            //{
            //    if (!(r["locationid"] is DBNull))
            //    {
            //        if ((string)r["locationid"] == "US_53_033_5266300015")
            //        {
            //            Console.WriteLine("");
            //        }
            //    }
            //}
            return parcels;


        }
        //public bool ValidateGeometry(ref Queue<KeyValuePair<string, SqlGeometry>> BigParcelDict, ref SqlGeometry g, string dmpid)
        //{
        //    if (g.STNumPoints() < 1000)
        //        return true;
        //    else
        //    { 
                
        //    }
        //}
        public void Write2Log(string logfile, string msg)
        {
            lock (writerlock)
            {
                using (StreamWriter writer = new StreamWriter(logfile, true))
                {
                    writer.WriteLine(DateTime.Now + ", " + msg);
                }
            }
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
