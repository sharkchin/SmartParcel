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

namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class GetMRBuffer : IGeometryAlgorithm
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
            SqlGeometry rd = (SqlGeometry)record[MasterProgramConstants.GEOMETRY_BIN],
                buf = rd.STBuffer(0.001);
            for (int i = 0; i < impactors.Count; i++)
            {
                AbstractRecord impactor = impactors.ElementAt(i);
                SqlGeometry imp = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];
                if(buf.Filter(imp))
                {                
                    buf = buf.STDifference(imp.STBuffer(0.00001));
                }
            }
            for (int i = 0; i < buf.STNumGeometries(); i++)
            {
                if (!buf.STGeometryN(i + 1).Filter(rd))
                    buf = buf.STDifference(buf.STGeometryN(i + 1));
            }
            //return ClearLineString(buf).STAsBinary().Value;
            return buf.STAsBinary().Value;
        }
        private SqlGeometry ClearLineString(SqlGeometry toprocess)
        {
            SqlGeometry toreturn = toprocess;
            int i = 0;
            //for (int i = 0; i < toprocess.STNumGeometries(); i++)
            while(toreturn.STGeometryType()=="GeometryCollection")
            {
                i += 1;
                SqlGeometry sg=toprocess.STGeometryN(i);
                if (sg.STGeometryType() != "Polygon" && sg.STGeometryType() != "MultiPolygon")
                    toreturn = toreturn.STDifference(sg);
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
