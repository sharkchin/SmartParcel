using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using DMP.MasterProgram.Processors.AnalysisEngine.Algorithms;
using Dmp.Neptune.Collections;
using Dmp.Neptune.Data;
using Dmp.Neptune.Utils.ShapeFile;
using DMP.MasterProgram.Utils;
using Dmp.Neptune.DatabaseManager;
using log4net;
using log4net.Config;
using Microsoft.SqlServer.Types;
using System.Windows;
using DMP.MasterProgram.Utils.AnalysisEngineUtil;



namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    public class GeometryDistance : IGeometryAlgorithm
    {
        public const int SRID = 4269;
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private List<AbstractRecord> impactors;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="record">Subject Record</param>
        /// <returns>List of Impactors intersect with Subject Record</returns>
        public object ProcessRecord1(AbstractRecord record, bool isSubByTask)
        {
            double dist = double.MaxValue;
              
            SqlGeometry parcel = (SqlGeometry)record[MasterProgramConstants.GEOMETRY_BIN];
            SqlGeography p1 = SqlGeography.STGeomFromWKB(parcel.STStartPoint().STAsBinary(), SRID);

            if (parcel == null)
            {
                logger.Error("Error while processing record in IntersectAlgorithm :AE Geometry for subject record is not defined");
            }
            SqlGeometry imp = new SqlGeometry();
            if (impactors.Count > 0)
            {
                for (int i = 0; i < impactors.Count; i++)
                {
                    AbstractRecord impactor = impactors.ElementAt(i);
                    imp = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];
                    if (!parcel.IsNull && !imp.IsNull)
                        dist = Math.Min(dist, parcel.STStartPoint().STDistance(imp).Value);
                }

                SqlGeography p2 = SqlGeography.STGeomFromWKB(imp.STStartPoint().STAsBinary(), SRID);
                double ratio = p1.STDistance(p2).Value / parcel.STStartPoint().STDistance(imp.STStartPoint()).Value;
                dist = dist * ratio;
            }
            return dist;
        }

        public object ProcessRecord(AbstractRecord record, bool isSubByTask)
        {
            double dist = double.MaxValue;
            object o = record[MasterProgramConstants.GEOMETRY_BIN];
            if (o is DBNull)
                return null;
            SqlGeometry parcel = (SqlGeometry)o, cent = new SqlGeometry();
            Vector pcvec;
            try
            {
                object p = record[MasterProgramConstants._X_COORD], q = record[MasterProgramConstants._Y_COORD];
                if (p is DBNull || q is DBNull)
                {
                    cent = parcel.STEnvelope().STBuffer(0.00002).STCentroid();
                    pcvec = new Vector(cent.STX.Value, cent.STY.Value);
                }
                else
                {
                    pcvec = new Vector((double)p, (double)q);
                    cent = AlgorithmUtil.Point2SqlGeometry(pcvec, SRID);
                }

                int s = 0;
                if (impactors.Count > 0)
                {
                    for (int i = 0; i < impactors.Count; i++)
                    {
                        AbstractRecord impactor = impactors.ElementAt(i);
                        Vector impvec = new Vector((double)impactor[MasterProgramConstants._X_COORD], (double)impactor[MasterProgramConstants._Y_COORD]);
                        double disti = (impvec - pcvec).Length;
                        if (dist > disti)
                        {
                            dist = disti;
                            s = i;
                        }
                    }

                }
                if (dist == double.MaxValue)
                    return -9999;
                else
                {
                    AbstractRecord closeimp = impactors.ElementAt(s);
                    SqlGeometry closeImpGeom = (SqlGeometry)closeimp[MasterProgramConstants.GEOMETRY_BIN];
                    dist = cent.STDistance(closeImpGeom).Value;
                    return dist * AlgorithmUtil.MetersPerDegree(((double)closeimp[MasterProgramConstants._X_COORD] + pcvec.X) / 2, ((double)closeimp[MasterProgramConstants._Y_COORD] + pcvec.Y) / 2, SRID);
                }
           
            
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error while calculating shortest distance between subject and impactor : " + ex.Message);
            }
           
        }


        /// <summary>
        /// set the Impactor List
        /// </summary>
        /// <param name="impactors">impactor List</param>
        public void InitializeImpactors( List<AbstractRecord> impactors)
        {
            this.impactors = impactors;

        }

        /// <summary>
        /// set the parameter List
        /// </summary>
        /// <param name="parameters"></param>
        public void InitializeParameters(Dictionary<String, String> parameters)
        {
            //this.parameters = parameters;
        }

       

    }
}

