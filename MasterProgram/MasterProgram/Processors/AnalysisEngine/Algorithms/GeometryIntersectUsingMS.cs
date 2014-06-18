using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dmp.Neptune.Collections;
using log4net;
using log4net.Config;
using DMP.MasterProgram.Utils;
using Microsoft.SqlServer.Types;
using DMP.MasterProgram.Utils.AnalysisEngineUtil;

namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class GeometryIntersectUsingMS : IGeometryAlgorithm
    {

        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private List<AbstractRecord> impactors;
        private Dictionary<String, String> parameters;

        public object ProcessRecord(AbstractRecord record, bool isSubByTask)
        {
            List<AbstractRecord> intersectList = new List<AbstractRecord>();
            string intersectMethod = null;
            double buffer = 0;
            string bufferS = null;
            string bufferUnit = null;
            try
            {
                if (parameters != null)
                {
                    parameters.TryGetValue(MasterProgramConstants.INTERSECT_METHOD, out intersectMethod);
                    parameters.TryGetValue(MasterProgramConstants.BUFFER, out bufferS);
                    parameters.TryGetValue(MasterProgramConstants.BUFFFER_UNIT, out bufferUnit);
                }
                if (intersectMethod == null)
                {
                    intersectMethod = MasterProgramConstants.GEOMETRY_INTERSECT_GEOMETRY;//default value
                }

                SqlGeometry subjectGeom = (SqlGeometry)record[MasterProgramConstants.GEOMETRY_BIN];

                if (bufferS != null)
                {
                    buffer = Convert.ToDouble(bufferS);
                    double distanceInMeter = AlgorithmUtil.ToMeters(buffer, bufferUnit);

                    double centroidToMaxPoint = CalculateCentroidToMaxPointDis(subjectGeom);

                    subjectGeom = subjectGeom.STCentroid().STBuffer((distanceInMeter * .00001)+centroidToMaxPoint);

                }


                for (int i = 0; i < impactors.Count; i++)
                {
                    AbstractRecord impactor = impactors.ElementAt(i);

                    SqlGeometry impactorGeom = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];

                    if (intersectMethod.Equals(MasterProgramConstants.GEOMETRY_INTERSECT_GEOMETRY))
                    {
                        if (subjectGeom.STIntersects(impactorGeom))
                        {
                            intersectList.Add(impactor);
                            break;
                        }
                    }
                    else if (intersectMethod.Equals(MasterProgramConstants.CENTROID_INTERSECT_GEOMETRY))
                    {
                        if (subjectGeom.STCentroid().STIntersects(impactorGeom))
                        {
                            intersectList.Add(impactor);
                            break;
                        }                       
                    }

                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error while performing intersection between subject and impactor : " + ex.Message);
            }
             return intersectList.ToArray();
        }

        //Write this
        private double CalculateCentroidToMaxPointDis(SqlGeometry subjectGeom)
        {
            double maxDist = 0;
            SqlGeometry envelope = subjectGeom.STEnvelope();
            SqlGeometry centroid = subjectGeom.STCentroid();

            double xMin = envelope.STPointN(1).STX.Value;
            double yMin = envelope.STPointN(1).STY.Value;

            double xMax = envelope.STPointN(3).STX.Value;
            double yMax = envelope.STPointN(3).STY.Value;




            return maxDist;

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
