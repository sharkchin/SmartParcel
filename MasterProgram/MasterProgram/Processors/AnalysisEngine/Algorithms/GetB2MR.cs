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
using System.Diagnostics;
using Dmp.Neptune.Utils.ShapeFile;
using System.IO;



namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class GetB2MR : IGeometryAlgorithm
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        //private static string logfile = GetValidFileName(@"./GetB2MRlog.txt");
        private static object writerlock = new object();
        private static HashSet<string> blacklist = new HashSet<string> { "100660192_138334838" };
        private static Queue<KeyValuePair<string, SqlGeometry>> BigParcelDict = new Queue<KeyValuePair<string, SqlGeometry>>();
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
            //Stopwatch t1 = Stopwatch.StartNew();
            object o = record["GEOMETRY_BIN"];
            if (o is DBNull)
                return null;
            //otherwise
            SqlGeometry rd = (SqlGeometry)o;
            o = record["RD_BUFFER"];
            if (o == null)
                return null;
            //otherwise
            SqlGeometry buffer = (SqlGeometry)o;
            o = record["NAME"];
            string rdName = o is DBNull ? "N/A" : (string)o;
            List<AbstractRecord> candidates = new List<AbstractRecord>(), parcels = new List<AbstractRecord>();
            
            bool found = false;
            for (int i = 0; i < impactors.Count; i++)
            {                
                AbstractRecord impactor = impactors.ElementAt(i);                
                o = impactor["LANDUSE_CATEGORY"];
                string landuse = o is DBNull ? null : (string)o;
                if (landuse == "TRANSPORT" || landuse == "VACANT LAND" || landuse == "AGRICULTURAL")
                    continue;
                o = impactor["CAL_ACREAGE"];
                double area = o is DBNull ? 0 : (double)o;
                o = impactor[MasterProgramConstants.GEOMETRY_BIN];
                if (o is DBNull)
                    continue;
                SqlGeometry imp = (SqlGeometry)o;
                o = impactor["DMPID"];
                string dmpid = o is DBNull ? null : o.ToString();
                
                if (dmpid!=null && blacklist.Contains(dmpid))
                {
                    o = record["ID"];
                    double id = o is DBNull ? -1 : (double)o;
                    string latlon = rd.STStartPoint().STY.Value.ToString() + ", " + rd.STStartPoint().STX.Value.ToString();
                    //Write2Log(logfile, "impactor1, " + id + ", " + latlon + ", " + dmpid);
                    imp = SqlGeometry.Null;
                    //return null;
                }
                #region old code to delete
                //if (imp.STNumPoints() > 100000)
                //{
                //    if (landuse != "RESIDENTIAL" && area > 1)
                //        continue;
                //    lock (BigParcelDict)
                //    {
                //        found=false;
                //        foreach (var v in BigParcelDict)
                //        {
                //            if (v.Key == dmpid)
                //            {
                //                imp = v.Value;
                //                found = true;
                //                break;
                //            }
                //        }
                //        if (!found)
                //        {
                //            try
                //            {
                //                imp = GeoUtils.Reduce2NumPoints(imp, 1000);
                //            }
                //            catch (Exception e)
                //            {
                //                o = record["ID"];
                //                string id = o is DBNull ? "null" : (string)o;
                //                string latlon = rd.STStartPoint().STY.Value.ToString() + ", " + rd.STStartPoint().STX.Value.ToString();
                //                Write2Log(logfile, "impactor2, " + id + ", " + latlon + ", " + dmpid);
                //                Console.WriteLine("too many points (" + imp.STNumPoints() + ") in parcel _cftid=" + impactor["_CFTID"]);
                //                imp = SqlGeometry.Null;
                //                if (dmpid != null)
                //                    blacklist.Add(dmpid);                                
                //                return null;
                //            }
                //            finally
                //            {
                //                BigParcelDict.Enqueue(new KeyValuePair<string, SqlGeometry>(dmpid, imp));
                //                if (BigParcelDict.Count > 50)
                //                    BigParcelDict.Dequeue();
                //                Console.WriteLine("BigParcelDict now contains: " + impactor["LOCATION_ID"] + ", having " + BigParcelDict.Count + " elements");
                //            }
                //        }
                //    }
                //}
                #endregion
                if (buffer.Filter(imp))                
                {
                    if (imp.STNumPoints() > 1000)
                    {
                        lock (BigParcelDict)
                        {
                            found = false;
                            foreach (var v in BigParcelDict)
                            {
                                if (v.Key == dmpid)
                                {
                                    imp = v.Value;
                                    found = true;
                                    break;
                                }
                            }
                            if (!found)
                            {

                                if (landuse != "RESIDENTIAL" && area > 1)
                                    continue;
                                try
                                {
                                    imp = GeoUtils.Reduce2NumPoints(imp, 1000);
                                }
                                catch (Exception e)
                                {
                                    //o = record["ID"];
                                    //double id = o is DBNull ? -1 : (double)o;
                                    //string latlon = rd.STStartPoint().STY.Value.ToString() + ", " + rd.STStartPoint().STX.Value.ToString();
                                    //Write2Log(logfile, "impactor2, " + id + ", " + latlon + ", " + dmpid);
                                    Console.WriteLine("too many points in impactor with CFTID " + impactor["_CFTID"] + " due to error:" + e.Message);
                                    imp = SqlGeometry.Null;
                                    if (dmpid != null)
                                        blacklist.Add(dmpid);
                                    //return null;
                                }
                                finally
                                {
                                    BigParcelDict.Enqueue(new KeyValuePair<string, SqlGeometry>(dmpid, imp));
                                    if (BigParcelDict.Count > 20)
                                        BigParcelDict.Dequeue();
                                    Console.WriteLine("BigParcelDict now contains: " + impactor["LOCATION_ID"] + ", having " + BigParcelDict.Count + " elements");
                                }

                            }
                        }
                    }                    
                    SqlGeometry impbuf = imp.STBuffer(0.00005);
                    buffer = buffer.STDifference(impbuf);
                    imp = SqlGeometry.Null;
                    if (buffer.STIsEmpty())
                    {
                        //o = record["ID"];
                        //double id = o is DBNull ? -1 : (double)o;
                        //string latlon = rd.STStartPoint().STY.Value.ToString() + ", " + rd.STStartPoint().STX.Value.ToString();
                        //Write2Log(logfile, "impactor3, " + id + ", " + latlon + ", " + dmpid);
                        return null;
                    }
                    // t2.Stop();
                    //DataRecord parcel = new DataRecord(new string[] { "DMPID", "LOCATIONID", "Lon", "Lat", "GEOMETRY","RdName", "Buffer", "B2MR" });
                    DataRecord parcel = new DataRecord(new string[] { });                    
                    parcel["DMPID"] = impactor["DMPID"];                    
                    parcel["LOCATIONID"] = impactor["LOCATION_ID"];
                    parcel["fips"] = impactor["FIPS_CODE"];
                    parcel["cftid"] = impactor["_CFTID"];
                    parcel["Lon"] = impactor["_X_COORD"];
                    parcel["Lat"] = impactor["_Y_COORD"];                    
                    parcel["GEOMETRY"] = impactor["GEOMETRY_BIN"];
                    parcel["Buffer"] = impbuf.STAsBinary().Value;
                    parcel["RdName"] = rdName;
                    parcel["B2RD"] = 1;
                    //o = impactor["CAL_ACREAGE"];
                    //double area = o is DBNull ? 0 : (double)o;
                        
                    if (area < 3 && landuse == "RESIDENTIAL")
                        candidates.Add(parcel);
                    
                }
            }
            //Console.WriteLine("t2:" + TimeSpan.FromMilliseconds(t2.ElapsedMilliseconds));
            //Console.WriteLine("GetB2MR region 2:" + TimeSpan.FromMilliseconds(t1.ElapsedMilliseconds));
           // t1 = Stopwatch.StartNew();
            int k = 0;
            found = false;
            SqlGeometry realbuf = new SqlGeometry(), realbuf2 = new SqlGeometry();
            double minDist = double.MaxValue;
            for (int i = 0; i < buffer.STNumGeometries(); i++)
            {
                SqlGeometry sg = buffer.STGeometryN(i + 1);
                if (sg.Filter(rd))
                {
                    found = true;
                    if (k++ == 0)
                        realbuf = sg;
                    else
                        realbuf = realbuf.STUnion(sg);
                }
                else //if road is weirdly aligned, find the closest one
                {
                    if (!found)
                    {
                        double dist = sg.STDistance(rd).Value;
                        if (dist < minDist)
                        {
                            minDist = dist;
                            realbuf2 = sg;
                        }
                    }
                }
            }
            if (!found) //if road is weirdly aligned, use the closest one as buffer
                realbuf = realbuf2;
            //Console.WriteLine("GetB2MR region 3:" + TimeSpan.FromMilliseconds(t1.ElapsedMilliseconds));
            //t1 = Stopwatch.StartNew();
            foreach (DataRecord parcel in candidates)
            {
                //if (parcel["LOCATIONID"].ToString() == "US_08_031_0606305038000" || parcel["LOCATIONID"].ToString() == "US_08_031_0606305037000"
                //    || parcel["LOCATIONID"].ToString() == "US_08_031_0606310035000" || parcel["LOCATIONID"].ToString() == "US_08_031_0606305012000")
                //    Console.WriteLine();
                byte[] b = (byte[])parcel["Buffer"];
                SqlGeometry sg = SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(b), 4269);
                if (sg.STDistance(realbuf) < 0.00002)
                {
                    parcels.Add(parcel);
                }
            }
            //Console.WriteLine("GetB2MR " + TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds));
            //Console.WriteLine("GetB2MR region 4:" + TimeSpan.FromMilliseconds(t1.ElapsedMilliseconds));
           // t1 = Stopwatch.StartNew();
            return parcels;
            

        }
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
        public SqlGeometry GetBufferWithoutCap(SqlGeometry rd, double bufsize, double indentLen)
        {
            
            int n=rd.STNumPoints().Value;
            Vector startpt = new Vector(rd.STPointN(1).STX.Value, rd.STPointN(1).STY.Value),
                startpt2 = new Vector(rd.STPointN(2).STX.Value, rd.STPointN(2).STY.Value),
                endpt = new Vector(rd.STPointN(n).STX.Value, rd.STPointN(n).STY.Value),
                endpt2 = new Vector(rd.STPointN(n - 1).STX.Value, rd.STPointN(n - 1).STY.Value),
                startln = startpt - startpt2, endln = endpt - endpt2,
                startNormln = new Vector(startln.Y * -1, startln.X), endNormln = new Vector(endln.Y * -1, endln.X);
            if (startpt == endpt)
                return rd.Reduce(0.00001).STBuffer(bufsize);
            startNormln.Normalize(); startln.Normalize();
            endNormln.Normalize(); endln.Normalize();
            SqlGeometry startCap = Point2LineGeometry(new Vector[2] { startpt-startln*indentLen + startNormln * bufsize, startpt-startln*indentLen - startNormln * bufsize }),
                endCap = Point2LineGeometry(new Vector[2] { endpt-endln*indentLen + endNormln * bufsize, endpt-endln*indentLen - endNormln * bufsize }),
                buffer = rd.Reduce(0.00001).STBuffer(bufsize).STDifference(startCap.STBuffer(0.00001)).STDifference(endCap.STBuffer(0.00001));
            SqlGeometry realbuf=new SqlGeometry();
            int k = 0;
            SqlGeometry centpt = GetCenterPtFromLn(rd).STBuffer(0.00001);
            for (int i = 0; i < buffer.STNumGeometries(); i++)
            {
                SqlGeometry sg=buffer.STGeometryN(i + 1);
                if (sg.Filter(centpt))
                {
                    if (k++ == 0)
                        realbuf = sg;
                    else
                        realbuf = realbuf.STUnion(sg);
                }
            }
            
            return realbuf;
        }
        private SqlGeometry GetCenterPtFromLn(SqlGeometry rd)
        {
            int n = rd.STNumPoints().Value;
            Vector centpt;
            if (n % 2 == 0)
            {
                Vector pt1 = new Vector(rd.STPointN(n / 2).STX.Value, rd.STPointN(n / 2).STY.Value),
                    pt2 = new Vector(rd.STPointN(n / 2 + 1).STX.Value, rd.STPointN(n / 2 + 1).STY.Value);
                centpt = (pt1 + pt2) / 2;
            }
            else
                centpt=new Vector(rd.STPointN((n+1) / 2).STX.Value, rd.STPointN((n+1) / 2).STY.Value);
            return Point2PointGeometry(centpt);
        }
        private SqlGeometry Point2LineGeometry(Vector[] pts)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("LINESTRING (");
            int k = 0;
            foreach (Vector pt in pts)
            {
                if (k++ == 0)
                    sb.Append(pt.X).Append(" ").Append(pt.Y);
                else
                    sb.Append(",").Append(pt.X).Append(" ").Append(pt.Y);
            }
            sb.Append(")");
            return SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(sb.ToString()), 4269);
        }
        private SqlGeometry Point2PointGeometry(Vector pt)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("POINT (").Append(pt.X).Append(" ").Append(pt.Y).Append(")");            
            return SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(sb.ToString()), 4269);
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
