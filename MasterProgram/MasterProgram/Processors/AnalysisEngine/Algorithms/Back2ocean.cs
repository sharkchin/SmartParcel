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



namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class Back2ocean : IGeometryAlgorithm
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static HashSet<string> blacklist = new HashSet<string> { "100660192_138334838" };
        private List<AbstractRecord> impactors;
        private Dictionary<String, String> parameters;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="record">Subject Record</param>
        /// <returns>List of Impactors intersect with Subject Record</returns>
        public object ProcessRecord(AbstractRecord record, bool isSubByTask)
        {
            //Stopwatch timer = Stopwatch.StartNew();
            object o = record["GEOMETRY_BIN"];
            if (o is DBNull)
                return null;
            //otherwise
            SqlGeometry poly = (SqlGeometry)o;
            double polyarea = poly.STArea().Value;
            o = record["PolygonCutByRd"];
            if (o == null)
                return null;
            SqlGeometry buffer = (SqlGeometry)o;
            
            
            List<AbstractRecord> candidates = new List<AbstractRecord>(), parcels = new List<AbstractRecord>();            
            for (int i = 0; i < impactors.Count; i++)
            {
                AbstractRecord impactor = impactors.ElementAt(i);
                //if(!(impactor["location_id"] is DBNull) && (string)impactor["location_id"]=="US_06_059_052-150-01")
                //{
                //    Console.WriteLine();
                //}
                o = impactor[MasterProgramConstants.GEOMETRY_BIN];
                if (o is DBNull)
                    continue;
                SqlGeometry imp = (SqlGeometry)o;
                o = impactor["_DMP_ID"];
                string dmpid = o is DBNull ? null : o.ToString();
                if (dmpid != null && blacklist.Contains(dmpid))
                {
                    imp = SqlGeometry.Null;
                    return null;
                }
                if (imp.STNumPoints() > 100000)
                {
                    try
                    {
                        imp = GeoUtils.Reduce2NumPoints(imp, 1000);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("too many points (" + imp.STNumPoints() + ") in parcel _cftid=" + impactor["_CFTID"]+":"+e.StackTrace);
                        imp = SqlGeometry.Null;
                        if (dmpid != null)
                        {
                            blacklist.Add(dmpid);
                            Console.Write("blacklist changes to: ");
                            foreach (string id in blacklist)
                                Console.Write(id + ", ");
                        }
                        Console.WriteLine();
                        return null;
                    }
                }
                if (buffer.Filter(imp))
                {                    
                    o = impactor["LANDUSE_CATEGORY"];
                    string code = o is DBNull ? null : (string)o;                    
                    o = impactor["SITE_ADDR"];
                    string addr = o is DBNull ? null : (string)o;                    
                    if (imp.STNumPoints() > 1000)
                    {
                        try
                        {
                            imp = GeoUtils.Reduce2NumPoints(imp, 1000);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("too many points (" + imp.STNumPoints() + ") in parcel _cftid=" + impactor["_CFTID"]+":"+e.StackTrace);
                            imp = SqlGeometry.Null;
                            return null;
                        }
                    }
                    SqlGeometry impbuf = imp.STBuffer(0.00005);
                    o = impactor["CAL_ACREAGE"];
                    double area = o is DBNull ? 0 : (double)o;                    
                   
                    if (code == "RESIDENTIAL")
                    {                        
                        buffer = buffer.STDifference(impbuf);                        
                    }
                    if (code == "PUBLIC")
                    {
                        continue;
                    }
                    else
                    {
                        if (imp.STIntersection(poly).STArea().Value / polyarea < 0.8)
                            buffer = buffer.STDifference(impbuf);
                    }

                    DataRecord parcel = new DataRecord(new string[] { "DMPID", "LOCATIONID", "Lon", "Lat", "GEOMETRY", "Buffer", "B2Poly" });
                    o = impactor["_DMP_ID"];
                    parcel["DMPID"] = o is DBNull ? null : o.ToString();
                    o = impactor["LOCATION_ID"];
                    parcel["LOCATIONID"] = o is DBNull ? null : o.ToString();
                    o = impactor["_X_COORD"];
                    parcel["Lon"] = o is DBNull ? double.NaN : (double)o;
                    o = impactor["_Y_COORD"];
                    parcel["Lat"] = o is DBNull ? double.NaN : (double)o;
                    o = impactor["GEOMETRY_BIN"];
                    SqlGeometry sg = o is DBNull ? SqlGeometry.Null : (SqlGeometry)o;
                    parcel["GEOMETRY"] = o is DBNull ? null : sg.STAsBinary().Value;
                    parcel["Buffer"] = impbuf.STAsBinary().Value;
                    //parcel["buffer"] = imp.STAsBinary().Value;
                    parcel["B2Poly"] = 1;


                    if (area < 3 && code == "RESIDENTIAL")
                        candidates.Add(parcel);
                }
            }
            //buffer = buffer2;
            int k = 0;
            bool found = false;
            SqlGeometry realbuf = new SqlGeometry(), realbuf2 = new SqlGeometry();
            double minDist = double.MaxValue;
            for (int i = 0; i < buffer.STNumGeometries(); i++)
            {
                SqlGeometry sg = buffer.STGeometryN(i + 1);
                if (sg.Filter(poly))
                {
                    found = true;
                    if (k++ == 0)
                        realbuf = sg;
                    else
                        realbuf = realbuf.STUnion(sg);
                }
                else //if subject is weirdly aligned (subject on parcel), find the closest one
                {
                    if (!found)
                    {
                        double dist = sg.STDistance(poly).Value;
                        if (dist < minDist)
                        {
                            minDist = dist;
                            realbuf2 = sg;
                        }
                    }
                }
            }
            
            if (!found) //if subject is weirdly aligned, use the closest one as buffer
                realbuf = realbuf2;
            realbuf = realbuf.STUnion(poly);
            foreach (DataRecord parcel in candidates)
            {                
                byte[] b = (byte[])parcel["Buffer"];
                
                SqlGeometry sg = SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(b), 4269);
                if (sg.STDistance(realbuf) < 0.00002)
                {
                    
                    parcels.Add(parcel);
                }
            }
            //Console.WriteLine("timer=" + TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds));
            return parcels;
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
