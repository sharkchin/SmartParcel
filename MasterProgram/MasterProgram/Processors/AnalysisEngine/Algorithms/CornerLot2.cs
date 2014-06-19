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
    class CornerLot2 : IGeometryAlgorithm
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        //private static string logfile = GetValidFileName(@"./CornerLot2log.txt");
        //private static object writerlock = new object();
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
            List<AbstractRecord> candidates = new List<AbstractRecord>(), parcels = new List<AbstractRecord>();
            object o = record[MasterProgramConstants.GEOMETRY_BIN];
            if (o is DBNull)
                return null;
            //otherwise
            SqlGeometry rd = (SqlGeometry)o, buffer=new SqlGeometry();
            int k = 0;
            int np = rd.STNumPoints().Value;
            if (np <= 3)
                return null;
            //otherwise
            for (int i = 1; i < rd.STNumPoints()-1; i++)
            {
                double r = 0.0003;
                SqlGeometry pt1=rd.STPointN(i), pt2 = rd.STPointN(i + 1), pt3=rd.STPointN(i+2);
                Vector pt2vec=Point2Vector(pt2),
                    rd1 = Point2Vector(pt1) - pt2vec,
                    rd2 = Point2Vector(pt3) - pt2vec;
                double angle=Vector.AngleBetween(rd1,rd2);
                if (Math.Abs(angle) < 150)
                { 
                    rd1.Normalize();
                    rd2.Normalize();
                    Vector centpt = ((pt2vec + rd1) + (pt2vec + rd2)) / 2, centln = centpt - pt2vec;
                    SqlGeometry mask = ExtendLine(centln, pt2vec, r * 1.1, r * 1.2);
                    if (k++ == 0) //first time
                        buffer = mask;
                    else
                        buffer = buffer.STUnion(mask);
                }
            }
            if (k == 0)
                return null;
            //otherwise

            for (int i = 0; i < impactors.Count; i++)
            {
                AbstractRecord impactor = impactors.ElementAt(i);
                o = impactor["LANDUSE_CATEGORY"];
                string landuse = o is DBNull ? null : (string)o;
                if (landuse == "TRANSPORT" || landuse == "AGRICULTURAL")
                    continue;
                o = impactor["CAL_ACREAGE"];
                double area = o is DBNull ? 0 : (double)o;
                o = impactor[MasterProgramConstants.GEOMETRY_BIN];
                if (o is DBNull)
                    continue;
                //otherwise
                SqlGeometry imp = (SqlGeometry)o;
                //o = impactor["_DMP_ID"];
                //string dmpid = o is DBNull ? null : o.ToString();
                string dmpid = impactor["_DMP_ID"] as string;
                if (imp.Filter(buffer))
                {
                    buffer = buffer.STDifference(imp);
                    imp = SqlGeometry.Null;
                    DataRecord parcel = new DataRecord(new string[] { "DMPID", "LOCATIONID", "CFTID", "Lon", "Lat", "GEOMETRY", "CL" });
                    //o = impactor["_DMP_ID"];
                    //parcel["DMPID"] = o is DBNull ? null : o.ToString();
                    parcel["DMPID"] = impactor["_DMP_ID"] as string;
                    //o = impactor["LOCATION_ID"];
                    parcel["LOCATIONID"] = impactor["LOCATION_ID"] as string;
                    //o = impactor["_CFTID"];
                    parcel["CFTID"] = impactor["_CFTID"] as string;
                    //o = impactor["_X_COORD"];                    
                    //parcel["Lon"] = o is DBNull ? double.NaN : (double)o;
                    parcel["Lon"] = impactor["_X_COORD"];
                    //o = impactor["_Y_COORD"];
                    parcel["Lat"] = impactor["_Y_COORD"];
                    //o = impactor["GEOMETRY_BIN"];
                    //SqlGeometry sg = o is DBNull ? SqlGeometry.Null : (SqlGeometry)o;
                    //parcel["GEOMETRY"] = o is DBNull ? null : sg.STAsBinary().Value;
                    parcel["GEOMETRY"] = impactor["GEOMETRY_BIN"];
                    parcel["CL"] = 1;
                                        
                    //if (area < 3 && landuse == "RESIDENTIAL")
                    candidates.Add(parcel);
                }
            }            
            k = 0;
            SqlGeometry realbuf = new SqlGeometry();
            for (int i = 0; i < buffer.STNumGeometries(); i++)
            {
                SqlGeometry sg = buffer.STGeometryN(i + 1);
                if (sg.Filter(rd))
                {
                    if (k++ == 0)
                        realbuf = sg;
                    else
                        realbuf = realbuf.STUnion(sg);
                }
            }
            if (realbuf.STIsEmpty())
            {
                //o = record["ID"];
                //double id = o is DBNull ? -1 : (double)o;
                //string latlon = rd.STStartPoint().STY.Value.ToString() + ", " + rd.STStartPoint().STX.Value.ToString();
                //Write2Log(logfile, "impactor1, " + id + ", " + latlon + ", ");          
                return null;
            }
            //otherwise    
            foreach (DataRecord parcel in candidates)
            {
                //byte[] b = (byte[])parcel["GEOMETRY"];
                //SqlGeometry sg = SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(b), 4269);
                o = parcel["GEOMETRY"];
                if (o is DBNull)
                    continue;
                SqlGeometry sg = (SqlGeometry)o;
                if (sg.STDistance(realbuf) < 0.00001)
                {
                    parcels.Add(parcel);
                }
            }
            return parcels;


        }
        //public void Write2Log(string logfile, string msg)
        //{
        //    lock (writerlock)
        //    {
        //        using (StreamWriter writer = new StreamWriter(logfile, true))
        //        {
        //            writer.WriteLine(DateTime.Now + ", " + msg);
        //        }
        //    }
        //}
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
