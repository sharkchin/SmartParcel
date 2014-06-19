using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DMP.MasterProgram.Processors.AnalysisEngine.Algorithms;
using DMP.MasterProgram.Processors.AnalysisEngine.Geometries;
using Dmp.Neptune.Collections;
using DMP.MasterProgram.Utils;
using Dmp.Neptune.Utils.ShapeFile;
using log4net;
using log4net.Config;
using MonoGIS.NetTopologySuite.Geometries;
using MonoGIS.NetTopologySuite.IO;
using Microsoft.SqlServer.Types;
using System.Windows;
using System.Diagnostics;
using System.IO;



namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class GetRoadBuffer : IGeometryAlgorithm
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        //private static string logfile=GetValidFileName(@"./GetRoadBufferlog.txt");
        private static object writerlock = new object();
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
            object o = record["GEOMETRY_BIN"];
            if (o is DBNull)
                return null;
            //otherwise
            SqlGeometry rd = (SqlGeometry)o;
            if (rd.STLength() < 0.0001)
                return null;
            
            SqlGeometry buffer = new SqlGeometry(), rdstartptbuffer = rd.STStartPoint().STBuffer(0.0003), rdendptbuffer = rd.STEndPoint().STBuffer(0.0003);
            try
            {
                buffer = rd.STBuffer(0.0003);
            }
            catch (Exception e)
            {                
                Console.WriteLine("exception during reduce2numpoints in GetRoadBuffer:" + e.Message);
                return null;
            }
            Vector startpt=new Vector(-9999,-9999), endpt=startpt;
            //bool shortRd = false;
            o = record["ID"];
            double subjectID = o is DBNull ? double.NaN : (double)o;
            List<SqlGeometry> startjnctRds = new List<SqlGeometry>(), endjnctRds=new List<SqlGeometry>();
            bool startHasNonMajorRd = false, endHasNonMajorRd = false;
            int k = 0;           
            
            for (int i = 0; i < impactors.Count; i++)
            {
                AbstractRecord impactor = impactors.ElementAt(i);
                o = impactor[MasterProgramConstants.GEOMETRY_BIN];
                if (o is DBNull)
                    continue;
                SqlGeometry imp = (SqlGeometry)o;
                o = impactor["ID"];
                double impID = o is DBNull ? double.NaN : (double)o;
                o = impactor["FRC"];
                int frc = o is DBNull ? 0 : (int)o;
                
                if (subjectID != double.NaN && impID != double.NaN && subjectID == impID)
                    continue;
                if (subjectID == double.NaN && rd.STEquals(imp))                
                    continue;                                
                if (Math.Abs(GeoUtils.AngleOfLines(imp, rd)) < 40 && !imp.STIntersects(rd)) //parallel roads                
                    continue;
                
                if (imp.STNumPoints() > 1000)
                {
                    try
                    {
                        imp = GeoUtils.Reduce2NumPoints(imp, 1000);
                    }
                    catch (Exception e)
                    {                        
                        Console.WriteLine("too many points in rd " + impactor["_CFTID"] + " and reduce failed: " + e.Message);
                    }
                }
                if (imp.Filter(rdstartptbuffer))
                {
                    //buffer.STDifference(imp.STBuffer(0.00005));                                                                                
                    startjnctRds.Add(imp);
                    if (frc > 5)
                        startHasNonMajorRd = true;
                }
                if (imp.Filter(rdendptbuffer))
                {
                    endjnctRds.Add(imp);
                    if (frc > 5)
                        endHasNonMajorRd = true;
                }
                
            }
            List<Vector> processedVectors = new List<Vector>();
            if (startHasNonMajorRd && startjnctRds.Count > 1)
            {
                try
                {
                    buffer = buffer.STDifference(GeoUtils.Reduce2NumPoints(GetIsometricLine(rd, startjnctRds, true, 4269), 1000).STBuffer(0.00001));
                }
                catch (Exception e)
                {
                    //o = record["ID"];
                    //string id = o is DBNull ? "null" : (string)o;                    
                    //string latlon = rd.STStartPoint().STY.Value.ToString() + ", " + rd.STStartPoint().STX.Value.ToString();
                    //Write2Log(logfile, "impactor2, " + id + ", " + latlon);
                    Console.WriteLine("exception during reduce2numpoints in GetRoadBuffer line103:" + e.Message);
                    return null;
                }
                //UnionAndSplit(ref rd, ref startjnctRds, true);
                //foreach (SqlGeometry imp in startjnctRds)
                //{
                //    SqlGeometry isoline = GetIsometricLine(rd, ref startpt, ref endpt, ref shortRd, imp, true, 0.001, 0.0008).STBuffer(0.00002);
                //    if(!isoline.IsNull)
                //        buffer = buffer.STDifference(isoline);
                //}
            }
            
            if (endHasNonMajorRd && endjnctRds.Count > 1)
            {
                try
                {
                    buffer = buffer.STDifference(GeoUtils.Reduce2NumPoints(GetIsometricLine(rd, endjnctRds, false, 4269),1000).STBuffer(0.00001));
                }
                catch (Exception e)
                {
                    //o = record["ID"];
                    //string id = o is DBNull ? "null" : (string)o;
                    //string latlon = rd.STStartPoint().STY.Value.ToString() + ", " + rd.STStartPoint().STX.Value.ToString();
                    //Write2Log(logfile, "impactor2, " + id + ", " + latlon);
                    Console.WriteLine("exception during reduce2numpoints in GetRoadBuffer line125:" + e.Message);
                    return null;
                }
            }
            
            if (buffer.STNumGeometries() == 1)
                return buffer;
            k = 0;
            bool found = false;
            SqlGeometry realbuf = new SqlGeometry(), realbuf2 = new SqlGeometry();
            double minDist = double.MaxValue;
           // Console.WriteLine("region 2 "+TimeSpan.FromMilliseconds(t1.ElapsedMilliseconds));
            
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
            //Console.WriteLine("GetRoadBuffer " + TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds));
            return realbuf;
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
        public SqlGeometry GetIsometricLine(SqlGeometry rd, List<SqlGeometry> imps, bool IntersectAtStart, int SRID)
        {
            
            SqlGeometry jnct=new SqlGeometry();
            if (IntersectAtStart)
                jnct = rd.STStartPoint();
            else
                jnct = rd.STEndPoint();
            SqlGeometry jnctbuffer=jnct.STBuffer(0.00025).STDifference(rd.STBuffer(0.00001))/*, fullrds = rd.STBuffer(0.00001)*/;
           // Stopwatch t1 = Stopwatch.StartNew();
            foreach (SqlGeometry imp in imps)
            {
                //fullrds = fullrds.STUnion(imp.STBuffer(0.00001));
                if (jnctbuffer.Filter(imp))
                    jnctbuffer = jnctbuffer.STDifference(imp.STBuffer(0.00001));
            }
            //Console.WriteLine("getisometricline1 takes " + TimeSpan.FromMilliseconds(t1.ElapsedMilliseconds));
            //t1 = Stopwatch.StartNew();
            //jnctbuffer = jnctbuffer.STDifference(fullrds);
            SqlGeometry isolines = new SqlGeometry();
            int k = 0;
            //Console.WriteLine("getisometricline2 takes " + TimeSpan.FromMilliseconds(t1.ElapsedMilliseconds));
            //t1 = Stopwatch.StartNew();
            for (int i = 0; i < jnctbuffer.STNumGeometries(); i++)
            {
                SqlGeometry cent = jnctbuffer.STGeometryN(i + 1).STCentroid();
                if (cent.IsNull)
                    continue;
                SqlGeometry isoline = GeoUtils.Points2LineString(new List<SqlGeometry>(2) { jnct, cent }, SRID);
                if (k++ == 0)
                    isolines = GeoUtils.ExtendLine(isoline,jnct,0.0008,0.0008);
                else
                    isolines = isolines.STUnion(GeoUtils.ExtendLine(isoline, jnct, 0.0008, 0.0008));
            }
            //Console.WriteLine("getisometricline3 takes " + TimeSpan.FromMilliseconds(t1.ElapsedMilliseconds));
            return isolines;
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
