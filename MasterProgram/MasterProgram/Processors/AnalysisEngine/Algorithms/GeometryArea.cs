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
    public class GeometryArea : IGeometryAlgorithm
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private List<AbstractRecord> impactors;
        private Dictionary<String, String> parameters;

        public object ProcessRecord(AbstractRecord record, bool isSubByTask)
        {
            SqlGeometry subjectGeom = null;
            if (isSubByTask)
            {
                subjectGeom = (SqlGeometry)record[MasterProgramConstants.TASK_CALCULATED];
            }
            else
            {
                subjectGeom = (SqlGeometry)record[MasterProgramConstants.GEOMETRY_BIN];
            }

            return subjectGeom.STArea();
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
