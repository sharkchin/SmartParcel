using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dmp.Neptune.Collections;
using log4net;
using MonoGIS.NetTopologySuite.Geometries;
using MonoGIS.NetTopologySuite.IO;
using DMP.MasterProgram.Utils;
using Microsoft.SqlServer.Types;

namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class GeometryIntersectionAndArea : IGeometryAlgorithm
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private List<AbstractRecord> impactors;
        private Dictionary<String, String> parameters;

        public object ProcessRecord(AbstractRecord record, bool isSubByTask)
        {
            double area = 0;
            string intersectMethod = null;
            if (parameters != null)
            {
                parameters.TryGetValue(MasterProgramConstants.INTERSECT_METHOD, out intersectMethod);
            }
            if (intersectMethod == null)
            {
                intersectMethod = MasterProgramConstants.GEOMETRY_INTERSECT_GEOMETRY;//default value
            }

            SqlGeometry subjectGeom = (SqlGeometry)record[MasterProgramConstants.GEOMETRY_BIN];
            for (int i = 0; i < impactors.Count; i++)
            {
                AbstractRecord impactor = impactors.ElementAt(i);
                SqlGeometry intersectedGeom = null;
                
                SqlGeometry impactorGeom = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];

                if (intersectMethod.Equals(MasterProgramConstants.GEOMETRY_INTERSECT_GEOMETRY))
                {
                    if(subjectGeom.STIntersects(impactorGeom))
                    {
                        intersectedGeom = subjectGeom.STIntersection(impactorGeom);
                        area = area + intersectedGeom.STArea().Value;                 
                    }
                }
                else if (intersectMethod.Equals(MasterProgramConstants.CENTROID_INTERSECT_GEOMETRY))
                {
                    if(subjectGeom.STCentroid().STIntersects(impactorGeom))
                    {
                        intersectedGeom = subjectGeom.STIntersection(impactorGeom);
                        area = area + intersectedGeom.STArea().Value;
                    }
                     
                }

            }
            return area;
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
