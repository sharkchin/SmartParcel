using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dmp.Neptune.Collections;
using log4net;
using log4net.Config;
using DMP.MasterProgram.Utils;
using Microsoft.SqlServer.Types;
using System.Windows;
using DMP.MasterProgram.Utils.AnalysisEngineUtil;


namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    public class DistanceToAirport : IGeometryAlgorithm
    {
        public const int SRID = 4269;
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private List<AbstractRecord> impactors;
        private Dictionary<String, String> parameters;


        public object ProcessRecord(AbstractRecord record, bool isSubByTask)
        {
            double dist = double.MaxValue;
            object o = record[MasterProgramConstants.GEOMETRY_BIN];

            if (o is DBNull)
                return null;

            //otherwise
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
                        SqlGeometry imp = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];
                        Vector impvec = new Vector((double)impactor[MasterProgramConstants.LON], (double)impactor[MasterProgramConstants.LAT]);
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
                    return dist * AlgorithmUtil.MetersPerDegree(((double)closeimp["LON"] + pcvec.X) / 2, ((double)closeimp["LAT"] + pcvec.Y) / 2, SRID);
                }
            
            }
            catch (Exception e)
            {
                throw new ApplicationException("Error while performing Distance to Airport : " + e.Message);
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
