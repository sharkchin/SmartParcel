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



namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class GetCornerLotLines : IGeometryAlgorithm
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        //private static string logfile = GetValidFileName(@"./GetCornerLotLinelog.txt");
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
            object o = record[MasterProgramConstants.GEOMETRY_BIN];
            if (o is DBNull)
                return null;
            //otherwise 
            double r = 0.0003, rr=0.0008;  //radius to find nearby roads
            SqlGeometry jnct = (SqlGeometry)o, jnctbuf = jnct.STBuffer(r), jnctbuf2=jnct.STBuffer(rr);
            o = record["ID"];
            double jnctid = o is DBNull ? -1 : (double)o;
            double maxArea = Math.PI * r * r * 150 / 360;  //if angle > 150 degree, it's not a corner lot
            int k = 0;
            SqlGeometry nearbyRds = new SqlGeometry();
            for (int i = 0; i < impactors.Count; i++)
            {
                AbstractRecord impactor = impactors.ElementAt(i);
                o = impactor[MasterProgramConstants.GEOMETRY_BIN];
                if (o is DBNull)
                    continue;
                //otherwise
                //if ((string)impactor["_cftid"] == "021230032000113032")
                //{
                //    Console.WriteLine("");
                //}
                SqlGeometry imp = (SqlGeometry)o;
                o = impactor["F_JNCTID"];
                double fid = o is DBNull ? -2 : (double)o;
                o = impactor["T_JNCTID"];
                double tid = o is DBNull ? -3 : (double)o;
                o = impactor["NAME"];
                string stname = (o == null) || (o is DBNull) ? "" : (string)o;
                if (isAlley(stname))
                    continue;                
                if (fid==jnctid || tid==jnctid)
                {
                    jnctbuf = jnctbuf.STDifference(ExtendLine(imp, jnct, r * 1.1, r * 1.2).STBuffer(0.00001));
                    if (k++ == 0)
                        nearbyRds = imp;
                    else
                        nearbyRds = nearbyRds.STUnion(imp);                    
                }
                else if(imp.Filter(jnctbuf2)) //save the nearby roads to deal with cross-road center-line-masks
                {                    
                    if (k++ == 0)
                        nearbyRds = imp;
                    else
                        nearbyRds = nearbyRds.STUnion(imp);
                }
            }

            Vector jnctvec = Point2Vector(jnct);
            SqlGeometry mask = new SqlGeometry();
            k = 0;
            for (int i = 0; i < jnctbuf.STNumGeometries(); i++)
            {
                SqlGeometry sg = jnctbuf.STGeometryN(i + 1);                                                    
                if (sg.STArea() < maxArea)
                {
                    SqlGeometry cent = sg.STCentroid();
                    if (!cent.IsNull)
                    {
                        Vector centvec = Point2Vector(cent),
                            dirvec = centvec - jnctvec;
                        SqlGeometry centln = ExtendLine(dirvec, jnctvec, rr, rr).MakeValid();
                        if (k++ == 0)
                            mask = centln;
                        else
                            mask = mask.STUnion(centln);
                    }
                }
            }
            if (mask.STIsEmpty())
                return null;
            else if (nearbyRds.STIsEmpty())
                return mask;
            else //deal with cross-road center-line-masks
            {
                //if (nearbyRds.STNumPoints() > 10000)
                //    Console.WriteLine("stop" + (string)record["_CFTID"]);
                SqlGeometry interpt = nearbyRds.STIntersection(mask);

                if (!interpt.IsNull && interpt.STIsValid() && !interpt.STIsEmpty())
                {
                    //if (interpt.STNumPoints() > 10000) //something wrong
                    //{
                    //    o = record["ID"];
                    //    double id = o is DBNull ? -1 : (double)o;
                    //    string latlon = jnct.STStartPoint().STY.Value.ToString() + ", " + jnct.STStartPoint().STX.Value.ToString();
                    //   // Write2Log(logfile, "subject1, " + id + ", " + latlon);
                    //    return null;
                    //}
                    //else
                        mask = mask.STDifference(interpt.STBuffer(0.00002));
                }
                SqlGeometry realmask = new SqlGeometry();
                k = 0;
                for (int i = 0; i < mask.STNumGeometries(); i++)
                {
                    SqlGeometry sg = mask.STGeometryN(i + 1);
                    if (sg.Filter(jnct.STBuffer(0.00003)))
                    {
                        if (k++ == 0)
                            realmask = sg;
                        else
                            realmask = realmask.STUnion(sg);
                    }
                }
                //Console.WriteLine("GetCornerLotLines:" + TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds));
                return realmask;
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
            name = name.Trim();
            if (name.EndsWith("Aly"))
                return true;
            else
                return false;
        }
        private SqlGeometry ExtendLine(Vector line, Vector jnct, double minlen, double len)
        {

            if (line.Length > minlen)
                return Point2LineGeometry(new Vector[2] { jnct, jnct + line });
            else
            {
                line.Normalize();
                return Point2LineGeometry(new Vector[2] { jnct, jnct + line * len });
            }
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
        private SqlGeometry ExtendLine(SqlGeometry line, SqlGeometry jnct, double minlen, double len)
        {
            line = line.Reduce(0.00001);
            if (line.STNumPoints() == 2 && line.STLength() > minlen)
                return line;
            SqlGeometry env = line.STEnvelope();
            if (env.STPointN(1).STDistance(env.STPointN(2)) > minlen || env.STPointN(2).STDistance(env.STPointN(3)) > minlen)
                return line;     
            SqlGeometry cent=env.STCentroid();
            if(cent.IsNull) //this should not happen, just put it here in case
                cent=line.STBuffer(0.00002).STCentroid();
            Vector deadend = new Vector(jnct.STX.Value, jnct.STY.Value),
                openend = new Vector(cent.STX.Value, cent.STY.Value);                
            Vector dir = openend - deadend;
            dir.Normalize();
            Vector newend = deadend + dir * len;
            
            return GeoUtils.Points2LineString(new List<Vector>(2) { deadend, newend }, line.STSrid.Value);
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
