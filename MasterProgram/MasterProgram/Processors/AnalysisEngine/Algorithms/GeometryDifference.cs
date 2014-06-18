using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dmp.Neptune.Collections;
using log4net;
using log4net.Config;
using DMP.MasterProgram.Utils;
using Microsoft.SqlServer.Types;

namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    public class GeometryDifference : IGeometryAlgorithm
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private List<AbstractRecord> impactors;
        private Dictionary<String, String> parameters;

        public object ProcessRecord(AbstractRecord record, bool isSubByTask)
        {
            SqlGeometry subjectGeom = null;
            try
            {
                if (isSubByTask)
                {
                    subjectGeom = (SqlGeometry)record[MasterProgramConstants.TASK_CALCULATED];
                }
                else
                {
                    subjectGeom = (SqlGeometry)record[MasterProgramConstants.GEOMETRY_BIN];
                }

                for (int i = 0; i < impactors.Count; i++)
                {
                    AbstractRecord impactor = impactors.ElementAt(i);

                    SqlGeometry impactorGeom = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];
                    if (subjectGeom.STIntersects(impactorGeom))
                    {
                        subjectGeom = subjectGeom.STDifference(impactorGeom);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error while calculating geometry Difference between subject and impactor : " + ex.Message);
            }
               


            return subjectGeom;

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
