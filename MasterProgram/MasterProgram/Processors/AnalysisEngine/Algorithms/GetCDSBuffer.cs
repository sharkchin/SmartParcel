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
//using MonoGIS.NetTopologySuite.Geometries;
//using MonoGIS.NetTopologySuite.IO;
using Microsoft.SqlServer.Types;
using System.Windows;
using Dmp.Neptune.Utils.ShapeFile;
using System.IO;
using DMPGeometryLibrary;
using System.Diagnostics;



namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class GetCDSBuffer : IGeometryAlgorithm
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        //private static string logfile = GetValidFileName(@"./GetCDSBufferlog.txt");
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
            //object o = record[MasterProgramConstants.GEOMETRY_BIN];
            //SqlGeometry jnct = (o == null)||(o is DBNull)? SqlGeometry.Null : (SqlGeometry)o;  
            DataRecord toreturn = new DataRecord(new string[] { });
            string stname = string.Empty;
            SqlGeometry jnct = record[MasterProgramConstants.GEOMETRY_BIN] as SqlGeometry;
            
            //if (jnct == SqlGeometry.Null)
            if(jnct==null || jnct==SqlGeometry.Null)
                return null;
            //double r = 0.00026;
            double rr=0.001;
            SqlGeometry cdsRd = new SqlGeometry();
            //bool trueCDS = false;
            int k = 0;
            bool cds = false;
            SqlGeometry nearbyRds = new SqlGeometry(), inter=new SqlGeometry();
            if (impactors.Count > 1000)
                Console.WriteLine("GetCDSBuffer: warning!!!!!!!! impactors.count=" + impactors.Count);
            double rdLen = 0;
            for (int i = 0; i < impactors.Count; i++)
            {
                AbstractRecord impactor = impactors.ElementAt(i);                
                SqlGeometry imp = impactor[MasterProgramConstants.GEOMETRY_BIN] as SqlGeometry;
                if (imp == null || imp==SqlGeometry.Null)
                    continue;
                
                int? fow = impactor["FOW"] as int?;                                      
                
                if (imp.STDistance(jnct)<0.000001)
                {
                    if (fow == 22)
                    {
                        cds = true;
                    }
                    stname = impactor["NAME"] as string;
                    int? backrd = impactor["BACKRD"] as int?;
                    if (stname == null && fow!=22)
                        return null;                    
                    if ((String.IsNullOrEmpty(stname) || String.IsNullOrEmpty(stname.Trim())) && !cds)
                        return null;                    
                    if (isAlley(stname))
                        return null;
                    if (backrd.Equals(1))
                        return null;
                    
                    if (k == 1 && !cds)  //not cul de sac junction
                        return null;
                    else
                    {                        
                        if (k++ == 0)
                            cdsRd = imp;
                        else
                        {
                            cdsRd = cdsRd.STUnion(imp);
                            rdLen += imp.STLength().Value;
                            if (rdLen > 0.015 && !cds) //if road lengh > 0.5 mile and not marked as cds by tomtom
                            {
                                k = 0;  //force it to be null
                                break;
                            }
                        }
                        impactors.RemoveAt(i);
                        i--;
                    }
                }
            }
            if (k == 0)
                return null;
            else
            {
                try
                {
                    SqlGeometry cdsRdFull = ConnectLines(impactors, cdsRd, ref nearbyRds, ref inter),
                        cornerLine=new SqlGeometry();
                       // cornerLine = GetCornerLines(cdsRd, nearbyRds),
                       // buffer = GeoUtils.Reduce2NumPoints(cdsRdFull,1000).STBuffer(r);
                    impactors.Clear();
                    if (nearbyRds == SqlGeometry.Null || nearbyRds.STIsEmpty())
                    {
                        toreturn["rd"] = cdsRdFull;
                        toreturn["intersection"] = inter;
                        toreturn["corner_centerln"] = null;
                        toreturn["rdname"] = stname;
                        toreturn["cds"] = cds;
                        return toreturn;
                    }
                    else
                    {
                        try
                        {
                            cornerLine = GetCornerLines(cdsRdFull, nearbyRds, inter, rr);
                            //nearbyRds = GeoUtils.Reduce2NumPoints(nearbyRds.STIntersection(buffer), 1000);
                        }
                        catch (Exception e)
                        {
                            //o = record["ID"];
                            //double id = o is DBNull ? -1 : (double)o;
                            Console.WriteLine("Exception wehn GetCDSBuffer:" + e.StackTrace);
                            double? id = record["ID"] as double?;
                            string latlon = jnct.STStartPoint().STY.Value.ToString() + ", " + jnct.STStartPoint().STX.Value.ToString();
                            //Write2Log(logfile, "subject1, " + id + ", " + latlon);
                            nearbyRds = null;
                            return null;
                        }
                        #region deprecated
                        // buffer = buffer.STDifference(nearbyRds.STBuffer(0.0001));
                       // SqlGeometry realbuf = new SqlGeometry();
                       // k = 0;
                       // for (int i = 0; i < buffer.STNumGeometries(); i++)
                       // {
                       //     SqlGeometry sg = buffer.STGeometryN(i + 1);
                        //    if (sg.Filter(jnct.STBuffer(0.00001)))
                       //     {
                       //         if (k++ == 0)
                       //             realbuf = sg;
                       //         else
                       //             realbuf = realbuf.STUnion(sg);
                        //    }
                       // }
                       // if (realbuf.STIsEmpty())
                        //    return null;
                        ////detect the end is round or pointed
                        //SqlGeometry endcut = realbuf.STIntersection(jnct.STBuffer(0.0003)),
                        //    end = SqlGeometry.Null;
                        //for (int i = 0; i < endcut.STNumGeometries(); i++)
                        //{
                        //    if (endcut.STGeometryN(i + 1).STIntersects(jnct.STBuffer(0.00001)))
                        //    {
                        //        end = endcut.STGeometryN(i + 1);
                        //        break;
                        //    }
                        //}
                        //if (end.IsNull)
                        //    return null;
                        //SqlGeometry endring = end.STExteriorRing();
                        //if (endring == null || endring.IsNull)
                        //    Console.Write("");
                        //if (endring.STLength() / Math.Sqrt(end.STArea().Value) > 6)
                        //    return null;
                        #endregion

                        cdsRd = SqlGeometry.Null;
                        nearbyRds = SqlGeometry.Null;
                        //buffer = SqlGeometry.Null;
                        toreturn["rd"] = cdsRdFull;
                        toreturn["intersection"] = inter;
                        toreturn["corner_centerln"] = cornerLine;
                        toreturn["rdname"] = stname;
                        toreturn["cds"] = cds;
                        //return new SqlGeometry[2] { cdsRdFull, realbuf };
                        return toreturn;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("catch:"+e.Message);
                    return null;
                }
            }
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
        private bool isAlley(string name)
        {
            if (name == null)
                return false;
            name = name.Trim();
            if (name.EndsWith("Aly"))
                return true;
            else
                return false;
        }
        private SqlGeometry GetCornerLines(SqlGeometry rd, SqlGeometry nearbyRd, SqlGeometry inter, double minLen)
        {
            if (rd == null || nearbyRd == null) return null;
            int SRID = rd.STSrid.Value;
            SqlGeometry toreturn = new SqlGeometry();
            SqlGeometry pt1 = rd.STStartPoint(), pt2 = rd.STEndPoint();
            //    , inter=pt1;
            //if (pt1.STDistance(nearbyRd) > pt2.STDistance(nearbyRd))
            //{
            //    inter = pt2;
            //}
            double r = minLen, rr = 0.00026;  //radius to find nearby roads
            double maxArea = Math.PI * rr * rr * 150 / 360;  //if angle > 150 degree, it's not a corner lot
            SqlGeometry interbuf=inter.STBuffer(rr), interbufsmall=interbuf, nearbyRdCrop=nearbyRd.STIntersection(interbufsmall);
            SqlGeometry interbuf2=new SqlGeometry();
            interbufsmall=interbufsmall.STDifference(nearbyRdCrop.STBuffer(0.00001));
            int k=0;
            for (int i = 0; i < interbufsmall.STNumGeometries(); i++)
			{
			    SqlGeometry sg=interbufsmall.STGeometryN(i+1);
                if(sg.STIntersects(rd))
                {
                    if(k++==0)
                    {
                        interbuf2=sg;
                    }
                    else
                    {
                        if(sg.STIntersection(rd.STBuffer(0.00001)).STArea().Value>interbuf2.STIntersection(rd.STBuffer(0.00001)).STArea().Value)
                            interbuf2=sg;
                    }
                }
			}
            interbuf=interbuf2.STDifference(rd.STBuffer(0.00001));
            Vector jnctvec = Point2Vector(inter);
            SqlGeometry mask = new SqlGeometry();
            k = 0;
            for (int i = 0; i < interbuf.STNumGeometries(); i++)
            {
                SqlGeometry sg = interbuf.STGeometryN(i + 1);                                                    
                //if (sg.STArea() < maxArea)
                {
                    SqlGeometry cent = sg.STCentroid();
                    if (!cent.IsNull)
                    {
                        Vector centvec = Point2Vector(cent),
                            dirvec = centvec - jnctvec;
                        SqlGeometry centln = ExtendLine(dirvec, jnctvec, r, r, SRID).MakeValid();
                        if (k++ == 0)
                            mask = centln;
                        else
                            mask = mask.STUnion(centln);
                    }
                }
            }
            if (mask.STIsEmpty())
                return null;
            return mask;
        }
        private SqlGeometry ExtendLine(Vector line, Vector jnct, double minlen, double len, int SRID)
        {

            if (line.Length > minlen)
                return GeoUtils.Points2LineString(new List<Vector> { jnct, jnct + line },SRID);
            else
            {
                line.Normalize();
                return GeoUtils.Points2LineString(new List<Vector> { jnct, jnct + line * len }, SRID);
            }
        }
        //From all nearby roads, try to find connecting roads until it hits an intersection
        private SqlGeometry ConnectLines(List<AbstractRecord> impactors, SqlGeometry rd, ref SqlGeometry nearbyRds, ref SqlGeometry intersection)
        {
            double r = 0.00026;
            if (impactors.Count == 0)
                return rd;
            else
            {
                int k = 0;
                SqlGeometry rd2 = rd;
                bool cds = false;
                for (int i = 0; i < impactors.Count; i++)
                {
                    AbstractRecord impactor = impactors.ElementAt(i);
                    SqlGeometry imp = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];
                    object o = impactor["FOW"];
                    int fow = o == null ? 22 : (int)o;
                    
                    if (imp.STDistance(rd)<0.000001)
                    {
                        if (fow == 22)
                            cds = true;
                        
                        rd2 = rd2.STUnion(imp);
                        SqlGeometry imp_extend=new SqlGeometry();
                        if (imp.STStartPoint().STDistance(rd) < 0.00001)
                            imp_extend = ExtendLine(imp, imp.STStartPoint(), r * 1.1, r * 1.1);
                        else
                            imp_extend = ExtendLine(imp, imp.STEndPoint(), r * 1.1, r * 1.1);

                        if (k++ == 0)
                        {
                            intersection = imp.STBuffer(0.00001).STIntersection(rd.STBuffer(0.00001)).STCentroid();
                            nearbyRds = imp_extend;
                        }
                        else
                            nearbyRds = nearbyRds.STUnion(imp_extend);
                        impactors.RemoveAt(i);
                        i--;                        
                    }
                }
                if (k == 0)
                    return rd;
                else if (k >= 2 && !cds)
                    return rd;
                else
                {
                    return ConnectLines(impactors, rd2, ref nearbyRds, ref intersection);
                }
            }
        }
        private SqlGeometry ExtendLine(SqlGeometry line, SqlGeometry jnct, double minlen, double len)
        {
            Vector deadend = new Vector(jnct.STX.Value, jnct.STY.Value),
                linestart = new Vector(line.STStartPoint().STX.Value, line.STStartPoint().STY.Value),
                lineend = new Vector(line.STEndPoint().STX.Value, line.STEndPoint().STY.Value),
                openend = new Vector();
            string end;
            if (linestart == deadend)
            {
                openend = lineend;
                end = "end";
            }
            else
            {
                openend = linestart;
                end = "start";
            }
            Vector dir = openend - deadend;
            //dir = dir / dir.Length;
            if (dir.Length > minlen)
                return line;
            else
                return AddPoint2Line(line, openend + dir * (len - (dir.Length)) / dir.Length, end);
        }
        private SqlGeometry AddPoint2Line(SqlGeometry line, Vector p, string end)
        {
            string str = line.ToString();
            string oldvalue, newvalue;
            if (end == "start")
            {
                oldvalue = "(";
                newvalue = "(" + p.X.ToString() + " " + p.Y.ToString() + ", ";
            }
            else if (end == "end")
            {
                oldvalue = ")";
                newvalue = ", " + p.X.ToString() + " " + p.Y.ToString() + ")";
            }
            else
                throw new SystemException("end specification should be either 'start' or 'end'!!");
            return SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(str.Replace(oldvalue, newvalue)), 4269);
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
        private static Vector Point2Vector(SqlGeometry point)
        {
            return new Vector(point.STX.Value, point.STY.Value);
        }
    }
}
