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
using System.IO;



namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class GetCDS : IGeometryAlgorithm
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        //private static string logfile = GetValidFileName(@"./GetCDSlog.txt");
        private static object writerlock = new object();
        private List<AbstractRecord> impactors;
        private Dictionary<String, String> parameters;
        //private static HashSet<string> invalidImpactors = new HashSet<string> { "0320001221" };
        //private static HashSet<string> blacklist = new HashSet<string> { "100660192_138334838" };
        //private static Queue<KeyValuePair<string, SqlGeometry>> BigParcelDict = new Queue<KeyValuePair<string, SqlGeometry>>();
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
            //Console.WriteLine("GetCDS:" + (string)record["_CFTID"]);
            //int SRID = 4269;
            
            object test = record["ID"];
            record["TEST"] = "HERE TOO";
           
            double idstr = test is DBNull ? 0 : (double)test;
            double r= 0.00026;
            object o = record[MasterProgramConstants.GEOMETRY_BIN];
            if (o is DBNull)
                return null;
            SqlGeometry jnct = (SqlGeometry)o;
            double METER_PER_DEGREE = GeoUtils.MetersPerDegree(jnct.STX.Value, jnct.STY.Value, jnct.STSrid.Value);
            o = record["RD_BUFFER"];
            if (o == null)
                return null;
            //otherwise
            DataRecord rd_buffer = (DataRecord)o;
            SqlGeometry rd = rd_buffer["rd"] as SqlGeometry, cornerLine = rd_buffer["corner_centerln"] as SqlGeometry,    
                inter=rd_buffer["intersection"] as SqlGeometry,
                rdreduce = GeoUtils.Reduce2NumPoints(rd,1000), buffer=rdreduce.STBuffer(r),
                rdmasksmall = rdreduce.STBuffer(0.00002), rdmaskbig = rdreduce.STBuffer(0.0001), buffer2=rdreduce.STBuffer(0.001);
            buffer = BufferCutByCornerLine(rd, buffer, cornerLine);
            //cornerLine = ExtendCornerLine(cornerLine,inter, 0.00105);
            buffer2 = BufferCutByCornerLine(rd, buffer2, cornerLine);
            string rdname = rd_buffer["rdname"] as string;
            bool? cds = rd_buffer["cds"] as bool?;
            double areasmall=rdmasksmall.STArea().Value;            
            List<AbstractRecord> candidates = new List<AbstractRecord>(), parcels = new List<AbstractRecord>();            
            if (buffer2 == null || buffer2.STIsEmpty() ||buffer2.IsNull)
                return null;
            //if (impactors.Count > 1000)
            //    Console.WriteLine(impactors.Count);
            //otherwise           
            //if (impactors.Count > 1000)
            //    Console.WriteLine("GetCDS: warning!!!!!!!! impactors.count=" + impactors.Count);
            
            AbstractRecord bad = null;
            try
            {
                for (int i = 0; i < impactors.Count; i++)
                {
                    AbstractRecord impactor = impactors.ElementAt(i);
                    bad = impactor;
                    //if ((string)impactor["LOCATION_ID"] == "US_53_033_2817550060")
                    //{
                    //    Console.WriteLine();
                    //}                
                    //o = impactor["LANDUSE_CATEGORY"];
                    //string landuse = (o == null) || (o is DBNull) ? null : (string)o;
                    string landuse = impactor["LANDUSE_CATEGORY"] as string;
                    if (landuse == "TRANSPORT" || /*landuse == "VACANT LAND" ||*/ landuse == "AGRICULTURAL")
                        continue;
                    o = impactor["CAL_ACREAGE"];
                    double area = (o == null) || (o is DBNull) ? 0 : (double)o;
                    o = impactor[MasterProgramConstants.GEOMETRY_BIN];
                    if (o is DBNull)
                        continue;
                    SqlGeometry imp = (SqlGeometry)o;
                    if (imp == SqlGeometry.Null || imp==null)
                        //return null;
                        continue;
                    o = impactor["_DMP_ID"];
                    string dmpid = (o == null) || (o is DBNull) ? null : o.ToString();
                    #region obsolete
                    //lock (blacklist)
                    //{
                    //    if (dmpid != null && blacklist.Contains(dmpid))
                    //    {
                    //        //o = record["ID"];
                    //        //double id = o is DBNull ? -1 : (double)o;
                    //        //string latlon = jnct.STStartPoint().STY.Value.ToString() + ", " + jnct.STStartPoint().STX.Value.ToString();
                    //        //Write2Log(logfile, "impactor1, " + id + ", " + latlon + "," + dmpid);
                    //        imp = SqlGeometry.Null;
                    //        continue;
                    //        //return null;
                    //    }
                    //}
                    
                    //if (imp.STNumPoints() > 100000)
                    //{
                    //    if (landuse != "RESIDENTIAL" && area > 1)
                    //        continue;
                    //    lock (BigParcelDict)
                    //    {
                    //        bool found = false;
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
                    //                Console.WriteLine("too many points in impactor with CFTID " + impactor["_CFTID"] + " due to error:" + e.Message);
                    //                imp = SqlGeometry.Null;
                    //                if (dmpid != null)
                    //                {
                    //                    lock (blacklist)
                    //                        blacklist.Add(dmpid);
                    //                }
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
                    //if (buffer.Filter(imp))
                    if (buffer2.Filter(imp))
                    {
                        #region obsolete
                        //if (imp.STNumPoints() > 1000)
                        //{
                        //    lock (BigParcelDict)
                        //    {
                        //        bool found = false;
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

                        //            if (/*landuse != "RESIDENTIAL" &&*/ area > 1)
                        //                continue;
                        //            try
                        //            {
                        //                imp = GeoUtils.Reduce2NumPoints(imp, 1000);
                        //            }
                        //            catch (Exception e)
                        //            {
                        //                o = record["ID"];
                        //                double id = o is DBNull ? -1 : (double)o;
                        //                string latlon = jnct.STStartPoint().STY.Value.ToString() + ", " + jnct.STStartPoint().STX.Value.ToString();
                        //                //Write2Log(logfile, "impactor2, " + id + ", " + latlon + "," + dmpid);
                        //                Console.WriteLine("too many points in impactor with CFTID " + impactor["_CFTID"] + " due to error:" + e.Message);
                        //                imp = SqlGeometry.Null;
                        //                if (dmpid != null)
                        //                {
                        //                    lock (blacklist)
                        //                        blacklist.Add(dmpid);
                        //                }
                                        
                        //                //return null;
                        //            }
                        //            finally
                        //            {
                        //                BigParcelDict.Enqueue(new KeyValuePair<string, SqlGeometry>(dmpid, imp));
                        //                if (BigParcelDict.Count > 20)
                        //                    BigParcelDict.Dequeue();
                        //                Console.WriteLine("GetCDS says: BigParcelDict now contains: " + impactor["LOCATION_ID"] + ", having " + BigParcelDict.Count + " elements");                                        
                        //            }

                        //        }
                        //    }
                        //}
                        #endregion obsolete
                        if (imp.STIntersects(rd) && imp.STIntersection(rdmasksmall).STArea().Value / areasmall > 0.6
                            && imp.STIntersection(rdmaskbig).STArea().Value / imp.STArea() > 0.8) //deal with road-as-parcel problem
                        {
                            //o = impactor["LANDUSE_CODE"];                            
                            continue;
                        }
                        buffer = buffer.STDifference(imp.STBuffer(0.00001));
                        buffer2 = buffer2.STDifference(imp.STBuffer(0.00001));
                        if (buffer.STIsEmpty())
                        {
                            //o = record["ID"];
                            //double id = o is DBNull ? -1 : (double)o;
                            //string latlon = jnct.STStartPoint().STY.Value.ToString() + ", " + jnct.STStartPoint().STX.Value.ToString();
                            //Write2Log(logfile, "impactor3, " + id + ", " + latlon + "," + dmpid);
                            return null;
                        }
                        double dist2end=-9999;
                        SqlGeometry pcent=imp.STCentroid();
                        if(pcent==null)
                            dist2end = jnct.STDistance(imp).Value * METER_PER_DEGREE;
                        else
                            dist2end=jnct.STDistance(pcent).Value*METER_PER_DEGREE;
                        imp = SqlGeometry.Null;
                        //DataRecord parcel = new DataRecord(new string[] { "ID","LocID","Lon","Lat","GEOMETRY","CDS"});
                        DataRecord parcel = new DataRecord(new string[] { "DMPID", "LOCATIONID", "CFTID", "Lon", "Lat", "GEOMETRY", "CDS" });
                        o = impactor["_DMP_ID"];
                        parcel["DMPID"] = (o == null) || (o is DBNull) ? null : o.ToString();
                        o = impactor["LOCATION_ID"];
                        parcel["LOCATIONID"] = (o == null) || (o is DBNull) ? null : o.ToString();
                        parcel["APN"] = impactor["APN"];
                        o = impactor["_CFTID"];
                        parcel["CFTID"] = (o == null) || (o is DBNull) ? null : o.ToString();
                        o = impactor["_X_COORD"];
                        parcel["Lon"] = (o == null) || (o is DBNull) ? double.NaN : (double)o;
                        o = impactor["_Y_COORD"];
                        parcel["Lat"] = (o == null) || (o is DBNull) ? double.NaN : (double)o;
                       // o = impactor["GEOMETRY_BIN"];
                        //SqlGeometry sg = (o == null) || (o is DBNull) ? SqlGeometry.Null : (SqlGeometry)o;
                        //parcel["GEOMETRY"] = o is DBNull ? null : sg.STAsBinary().Value;
                        parcel["GEOMETRY"] = impactor["GEOMETRY_BIN"];
                        parcel["CDS"] = 1;
                        parcel["EndCFTID"] = record["_CFTID"];
                        parcel["EndGeometry"] = jnct;
                        parcel["Dist2End"] = dist2end;
                        parcel["ParcelStName"] = impactor["SITE_STREET_NAME"];
                        parcel["ParcelStMode"] = impactor["SITE_MODE"];
                        parcel["StName"] = rdname;
                        parcel["OnDiffSt"] = 0;
                        parcel["CalcAcreage"] = area;
                        parcel["ParcelNumOnSt"] = 0;
                        parcel["LandUse"] = landuse;
                        parcel["APN"] = impactor["APN"];
                        o = impactor["APN"];                        
                        //o = impactor["CAL_ACREAGE"];
                        //double area = o is DBNull ? 0 : (double)o;
                        //o = impactor["LANDUSE_CATEGORY"];
                        //string cat = o is DBNull ? null : (string)o;
                        //if (area < 3 /* && landuse == "RESIDENTIAL"*/)
                        candidates.Add(parcel);
                    }
                    impactors.Remove(impactor);
                    i--;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("GetCDS at subject cftid="+record["_CFTID"]+", impactor cftid="+bad["_CFTID"] +": "+ e.Message + "," + e.StackTrace);
            }
            impactors.Clear();
            buffer2 = BufferCutByCornerLine(rd, buffer2, cornerLine);
            if (buffer.STArea() * 3 < buffer2.STArea())
            {
                buffer2 = buffer;
            }
            //buffer=buffer.STUnion(buffer2.STDifference(buffer.STBuffer(0.00003))); //deal with road longer than network problem
            
            int k = 0;
            bool found2 = false;
            SqlGeometry realbuf = new SqlGeometry(), realbuf2=new SqlGeometry();
            double minDist = double.MaxValue;
            for (int i = 0; i < buffer2.STNumGeometries(); i++)
            {
                SqlGeometry sg = buffer2.STGeometryN(i + 1);
                if (sg.STDistance(jnct)<0.00002)
                {
                    found2 = true;
                    if (k++ == 0)
                        realbuf = sg;
                    else
                        realbuf = realbuf.STUnion(sg);
                }
                else //if road is weirdly aligned, find the closest one
                {
                    if (!found2)
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
            if (!found2) //if road is weirdly aligned, use the closest one as buffer
                realbuf = realbuf2;
            realbuf = realbuf.STDifference(inter.STBuffer(0.0001));
            //SqlGeometry interbuf = rd.STStartPoint().STBuffer(0.0005)
            //    .STUnion(rd.STEndPoint().STBuffer(0.0005))
            //    .STDifference(jnct.STBuffer(0.00055)),
            //    extra = buffer2.STDifference(realbuf.STBuffer(0.00001)).STDifference(interbuf);
            
            //for (int i = 0; i < extra.STNumGeometries(); i++)
            //{
            //    SqlGeometry sg = extra.STGeometryN(i + 1);
            //    if (sg.STDistance(realbuf) < 0.00002)
            //    {
            //        realbuf = realbuf.STUnion(sg);
            //        break;
            //    }
            //}
            int cnt = 0;
            foreach (DataRecord parcel in candidates)
            {
                //if ((string)parcel["LOCATIONID"] == "US_53_033_2817550060")
                //{
                //    Console.WriteLine();
                //}  
                //byte[] b = (byte[])parcel["GEOMETRY"];
                SqlGeometry sg = parcel["GEOMETRY"] as SqlGeometry;
                string stname = parcel["ParcelStName"] as string, stmode = parcel["ParcelStMode"] as string,
                    addr = String.IsNullOrEmpty(stname) ? null : (String.IsNullOrEmpty(stmode) ? stname : stname + " " + stmode);
                
                bool onDiffSt=false;
                if (rdname != null && addr != null)
                {
                    if (!String.IsNullOrEmpty(rdname) && !String.IsNullOrEmpty(rdname.Trim())
                        && !String.IsNullOrEmpty(addr) && !String.IsNullOrEmpty(addr.Trim()))
                    {
                        rdname = rdname.Trim().ToLower();
                        addr = addr.Trim().ToLower();
                        if (rdname.Contains(addr))
                        {
                            cnt++;
                        }
                        else
                            onDiffSt = true;
                    }
                }
                //SqlGeometry sg = SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(b), 4269);
                //if (sg.STDistance(realbuf) < 0.00002 && !onDiffSt)
                if (sg.STDistance(realbuf) < 0.00002)
                {
                    //string locationid=parcel["LOCATIONID"] as string;
                    //if (locationid.Equals("US_53_033_2894700120"))
                    //{
                    //    Console.WriteLine("");
                    //}                    
                    if (onDiffSt) parcel["OnDiffSt"] = 1;
                    parcels.Add(parcel);
                }
            }
            candidates.Clear();
            
            
            realbuf = SqlGeometry.Null;
            realbuf2 = SqlGeometry.Null;
            foreach (DataRecord dr in parcels)
            {
                dr["ParcelNumOnSt"] = cnt;
            }
            if (cds != null && (bool)cds)
                return parcels;
           
            //if (cnt < 4)
            //    return null;
            return parcels;            

        }
        //private SqlGeometry ExtendCornerLine(SqlGeometry cornerln, SqlGeometry inter, double r)
        //{
        //    int SRID = cornerln.STSrid.Value;
        //    Vector jnct = GeoUtils.Point2Vector(inter);

        //}
        //private SqlGeometry ExtendLine(Vector line, Vector jnct, double minlen, double len, int SRID)
        //{

        //    if (line.Length > minlen)
        //        return GeoUtils.Points2LineString(new List<Vector> { jnct, jnct + line }, SRID);
        //    else
        //    {
        //        line.Normalize();
        //        return GeoUtils.Points2LineString(new List<Vector> { jnct, jnct + line * len }, SRID);
        //    }
        //}
        //private double Dist2Intersection(SqlGeometry inter, SqlGeometry rd, SqlGeometry parcel)
        //{ 
            
        //}
        private SqlGeometry BufferCutByCornerLine(SqlGeometry rd, SqlGeometry buffer, SqlGeometry cornerln)
        {
            SqlGeometry toreturn = new SqlGeometry();
            if (cornerln == null)
                return buffer;
            buffer = buffer.STDifference(cornerln.STBuffer(0.00001));            
            int k = 0;
            for (int i = 0; i < buffer.STNumGeometries(); i++)
            {
                SqlGeometry sg = buffer.STGeometryN(i + 1);
                if (sg.STIntersects(rd))
                {
                    if (k++ == 0)
                    {
                        toreturn = sg;
                    }
                    else
                    {
                        toreturn = toreturn.STUnion(sg);
                    }
                }
            }
            return toreturn;
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
