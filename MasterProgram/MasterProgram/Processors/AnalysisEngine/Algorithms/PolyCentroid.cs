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


namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class PolyCentroid : IGeometryAlgorithm
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
                return new Dictionary<string, object> { { "CentX", double.NaN }, { "CentY", double.NaN } };

            SqlGeometry parcel = (SqlGeometry)o;            
            //return new Dictionary<string, object> { { "CentX", ev.STX.Value }, { "CentY", ev.STY.Value } };
            return parcel.STCentroid();
        }
        private SqlGeography GetGeographyFromGeometry(SqlGeometry geom)
        {
            if (geom == null) return null;
            if (geom.STIsValid())
            {
                return SqlGeography.STGeomFromWKB(geom.STAsBinary(), SRID);
            }
            else
            {
                return SqlGeography.STGeomFromWKB(
                    geom.MakeValid().Reduce(.000001).STUnion(geom.STStartPoint()).MakeValid().STAsBinary(), SRID);
            }

            //try
            //{
            //    return SqlGeography.STGeomFromWKB(geom.STAsBinary(), SRID);
            //}
            //catch (Exception)
            //{
            //    // A common reason for an exception being thrown is invalid ring orientation, 
            //    // so attempt to fix it. The technique used is described at
            //    // http://blogs.msdn.com/edkatibah/archive/2008/08/19/working-with-invalid-data-and-the-sql-server-2008-geography-data-type-part-1b.aspx

            //    return SqlGeography.STGeomFromWKB(
            //        geom.MakeValid().Reduce(.000001).STUnion(geom.STStartPoint()).MakeValid().STAsBinary(), SRID);
            //}
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
