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



namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class PreGetCLBuffer : IGeometryAlgorithm
    {
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
            //otherwise 
            double r = 0.0005;
            SqlGeometry jnct = (SqlGeometry)o,
                jnctbuf = jnct.STBuffer(r);            
            double maxArea = Math.PI * r * r * 150 / 360;
            for (int i = 0; i < impactors.Count; i++)
            {
                AbstractRecord impactor = impactors.ElementAt(i);
                o = impactor[MasterProgramConstants.GEOMETRY_BIN];
                if (o is DBNull)
                    continue;
                //otherwise
                SqlGeometry imp = (SqlGeometry)o;
                if (imp.Filter(jnct.STBuffer(0.00001)))
                {                                                                              
                    jnctbuf = jnctbuf.STDifference(ExtendLine(imp, jnct, r*1.1, r*1.2).STBuffer(0.00001));                    
                }
            }
            
            Vector jnctvec = new Vector(jnct.STX.Value, jnct.STY.Value);
            SqlGeometry buffer = new SqlGeometry();
            int k = 0;
            for (int i = 0; i < jnctbuf.STNumGeometries(); i++)
            {
                SqlGeometry sg = jnctbuf.STGeometryN(i + 1);                
                if (sg.STArea() < maxArea)
                {
                    Vector centvec = new Vector(sg.STCentroid().STX.Value, sg.STCentroid().STY.Value),
                        dirvec = centvec - jnctvec;
                    SqlGeometry centln = ExtendLine(dirvec, jnctvec, r, r);                                       
                    if (k++ == 0)
                        buffer = centln;
                    else
                        buffer = buffer.STUnion(centln);
                }
            }
            return buffer;
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
                return AddPoint2Line(line,openend+dir*(len-(dir.Length))/dir.Length,end);
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
    }
}
