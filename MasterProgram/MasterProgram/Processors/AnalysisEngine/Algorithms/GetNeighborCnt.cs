using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using DMP.MasterProgram.Processors.AnalysisEngine.Algorithms;
using DMP.MasterProgram.Processors.AnalysisEngine.Geometries;
using Dmp.Neptune.Collections;
using Dmp.Neptune.Data;
using Dmp.Neptune.Utils.ShapeFile;
using DMP.MasterProgram.Utils;
using Dmp.Neptune.DatabaseManager;
using log4net;
using log4net.Config;
using Microsoft.SqlServer.Types;
using System.Windows;


namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class GetNeighborCnt : IGeometryAlgorithm
    {
        public const int SRID = 4269;
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
            SqlGeometry parcel = (SqlGeometry)o;
            string parcelid = (string)record["DMPID"];
            o = record["BufferCutByRoad"];
            if (o == null || o is DBNull)
                return null;
            SqlGeometry buf = (SqlGeometry)o;
            List<AbstractRecord> candidates = new List<AbstractRecord>();
            for (int i = 0; i < impactors.Count; i++)
            {
                AbstractRecord impactor = impactors.ElementAt(i);
                SqlGeometry imp = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];
                string impactorid = (string)impactor["DMPID"]; ;
                if (parcelid == impactorid)
                    continue;
                if (imp.Filter(buf))
                {
                    buf = buf.STDifference(imp.Reduce(0.00003).STBuffer(0.00001));
                    candidates.Add(impactor);
                }
            }
            impactors = null;
            int k = 0;
            SqlGeometry realbuf = new SqlGeometry();
            //bool found = false;
            for (int i = 0; i < buf.STNumGeometries(); i++)
            {
                if (buf.STGeometryN(i + 1).Filter(parcel))
                {
                    if (k++ == 0)
                        realbuf = buf.STGeometryN(i + 1);
                    else
                        realbuf = realbuf.STUnion(buf.STGeometryN(i + 1));
                }
            }
            int cnt = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                AbstractRecord impactor = candidates.ElementAt(i);
                SqlGeometry imp = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];
                if (imp.STDistance(realbuf) < 0.00002)
                    cnt++;
            }
            return cnt;
        }
        private SqlGeography GetGeographyFromGeometry(SqlGeometry geom)
        {
            if (geom == null) return null;

            try
            {
                return SqlGeography.STGeomFromWKB(geom.STAsBinary(), SRID);
            }
            catch (Exception)
            {
                // A common reason for an exception being thrown is invalid ring orientation, 
                // so attempt to fix it. The technique used is described at
                // http://blogs.msdn.com/edkatibah/archive/2008/08/19/working-with-invalid-data-and-the-sql-server-2008-geography-data-type-part-1b.aspx

                return SqlGeography.STGeomFromWKB(
                    geom.MakeValid().Reduce(.000001).STUnion(geom.STStartPoint()).STAsBinary(), SRID);
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
