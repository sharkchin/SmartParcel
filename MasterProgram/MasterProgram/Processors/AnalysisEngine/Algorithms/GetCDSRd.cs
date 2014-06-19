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
    class GetCDSRd : IGeometryAlgorithm
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
            SqlGeometry jnct = o is DBNull?SqlGeometry.Null:(SqlGeometry)o;
            if (jnct == SqlGeometry.Null)
                return null;
            SqlGeometry cdsRd = new SqlGeometry();
            int k = 0;
            bool cds = false;
            for (int i = 0; i < impactors.Count; i++)
            {
                AbstractRecord impactor = impactors.ElementAt(i);
                o = impactor[MasterProgramConstants.GEOMETRY_BIN];
                if (o is DBNull)
                    continue;
                //else
                SqlGeometry imp = (SqlGeometry)o;
                o = impactor["FOW"];
                int fow = o == null ? 22 : (int)o;
                
                if (imp.Filter(jnct.STBuffer(0.00001)))
                {
                    if (fow == 22)
                        cds = true;
                    if (k == 2 && !cds)  //not cul de sac junction
                        return null;
                    else
                    {
                        if (k++ == 0)
                            cdsRd = imp;
                        else
                            cdsRd = cdsRd.STUnion(imp);
                        impactors.RemoveAt(i);
                        i--;
                    }
                }
            }
            if (k == 0)
                return null;
            else
            {                
                return ConnectLines(impactors, cdsRd);
            }
        }
        private SqlGeometry ConnectLines(List<AbstractRecord> impactors, SqlGeometry rd)
        {
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
                    
                    if (imp.Filter(rd.STBuffer(0.00001)))
                    {
                        if (fow == 22)
                            cds = true;
                        if (k++ == 1 && !cds)
                            return rd;
                        else
                        {
                            rd2 = rd2.STUnion(imp);
                            impactors.RemoveAt(i);
                            i--;
                        }
                    }
                }
                if (k == 0)
                    return rd;
                else
                {                    
                    return ConnectLines(impactors, rd2);
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
    }
}
