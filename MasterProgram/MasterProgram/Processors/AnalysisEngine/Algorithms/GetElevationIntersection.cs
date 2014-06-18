using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dmp.Neptune.Collections;
using log4net;
using log4net.Config;
using DMP.MasterProgram.Utils;
using Microsoft.SqlServer.Types;
using DMP.MasterProgram.Utils.Elevation;

namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    public class GetElevationIntersection : IGeometryAlgorithm
    {

        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private List<AbstractRecord> impactors;
        private Dictionary<String, String> parameters;

        public object ProcessRecord(AbstractRecord record, bool isSubByTask)
        {
            SqlGeometry subjectGeom = null;
            SqlGeometry resultGeometry = null;
            if (isSubByTask)
            {
               subjectGeom = (SqlGeometry)record[MasterProgramConstants.TASK_CALCULATED];
            }
            else
            {
                subjectGeom = (SqlGeometry)record[MasterProgramConstants.GEOMETRY_BIN];
            }
           // string shapeFilePath = null;
            string dataFilePath = null;
            string slopeLessThan = null;

            if (subjectGeom == null)
            {
                throw new ApplicationException("GetElevation:ProcessRecord : Subject Geometry is null");
            }

            try
            {
                if (parameters != null)
                {
                    //parameters.TryGetValue(MasterProgramConstants.SHAPE_FILE_PATH, out shapeFilePath);
                   // parameters.TryGetValue(MasterProgramConstants.DATA_FILE_PATH, out dataFilePath);
                    parameters.TryGetValue(MasterProgramConstants.ELEVATION_DATA_PATH, out dataFilePath);
                    parameters.TryGetValue(MasterProgramConstants.SLOPE_UPPER_LIMIT, out slopeLessThan);
                }
                //dataFilePath = "D:\\Data Path\\Elevation Data\\Final";
                if ( dataFilePath == null)
                {
                    throw new ApplicationException("GetElevation:ProcessRecord : File Path  is null");
                }

                
                ElevationProcessor elevationProcessor = new ElevationProcessor(dataFilePath);

                double slopeLessThanD = 0;
                if (slopeLessThan != null)
                {
                    slopeLessThanD = Convert.ToDouble(slopeLessThan);
                }

                resultGeometry =  elevationProcessor.GetElevation(subjectGeom, slopeLessThanD);
                if (resultGeometry != null && subjectGeom!=null)
                {
                    resultGeometry = resultGeometry.STIntersection(subjectGeom);
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error while calculating Elevation :"+ex.Message);
            }



            return resultGeometry;

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
