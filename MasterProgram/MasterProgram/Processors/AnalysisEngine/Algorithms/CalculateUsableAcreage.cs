/**
 * This Algorithm calculate the usable acreage of the Subject Geometry.
 * The record should contain some "Usable Geometry" under the attribute named "TASK_CALCULATED".
 * If Usable Geometry contains multiple geometries,the largest geometry  out of them is used to calculate the 
 * Usable Acreage.

 *
 */

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
    public class CalculateUsableAcreage : IGeometryAlgorithm
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private List<AbstractRecord> impactors;
        private Dictionary<String, String> parameters;

        public object ProcessRecord(AbstractRecord record, bool isSubByTask)
        {
            double usableAcreage = 0;
            double usableArea = 0;

            try
            {
                SqlGeometry subjGeom = (SqlGeometry)record[MasterProgramConstants.GEOMETRY_BIN];
                SqlGeometry impactorGeom = (SqlGeometry)record[MasterProgramConstants.TASK_CALCULATED];
                double subjArea = subjGeom.STArea().Value;

                if (impactorGeom != null)
                {
                     for (int t = 1; t <= impactorGeom.STNumGeometries().Value; t++)
                    {
                        SqlGeometry childGeom = impactorGeom.STGeometryN(t);
                        childGeom = childGeom.STIntersection(subjGeom);
                        double childGeomArea = childGeom.STArea().Value;

                        if (childGeomArea > usableArea)
                        {
                            usableArea = childGeomArea;
                        }

                    }

                     usableAcreage = (usableArea / subjArea) * 100;

                }
                else
                {
                    usableAcreage = 0;
                }
            }
            catch(Exception  ex)
            {
                logger.Error("Error at CalculateUsableAcreage:ProcessRecord:- Error while calculationg usable Acreage "+ex.Message);
                throw new ApplicationException("Error while calculationg usable Acreage "+ex.Message);
            }
            return usableAcreage;
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
