using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DMP.MasterProgram.Processors.AnalysisEngine.Algorithms;
using DMP.MasterProgram.Processors.AnalysisEngine.Geometries;
using Dmp.Neptune.Collections;
using DMP.MasterProgram.Utils;
using Dmp.Neptune.Utils.ShapeFile;
using log4net;
using log4net.Config;
using MonoGIS.NetTopologySuite.Geometries;
using MonoGIS.NetTopologySuite.IO;
using Microsoft.SqlServer.Types;
using System.Windows;
using System.Diagnostics;



namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class PolygonCutByRd : IGeometryAlgorithm
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
            object o = record["GEOMETRY_BIN"];
            if (o is DBNull)
                return null;
            //otherwise
            SqlGeometry poly = (SqlGeometry)o;
            try
            {
                poly = GeoUtils.Reduce2NumPoints(poly, 1000);
            }
            catch (Exception e)
            {
                Console.WriteLine("the ocean piece id=" + record["id"] + " is too complicate to process:"+e.StackTrace);
                return null;
            }
            SqlGeometry buffer = poly.STBuffer(0.0005);            

            for (int i = 0; i < impactors.Count; i++)
            {
                AbstractRecord impactor = impactors.ElementAt(i);
                o = impactor[MasterProgramConstants.GEOMETRY_BIN];
                if (o is DBNull)
                    continue;
                SqlGeometry imp = (SqlGeometry)o;
                if(imp.Filter(buffer))
                {
                    try
                    {
                        buffer = buffer.STDifference(GeoUtils.Reduce2NumPoints(imp, 1000).STBuffer(0.0001));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("the road with cftid=" + impactor["_CFTID"] + " is too complicate to process:"+e.StackTrace);
                        return null;
                    }
                }
            }
            
            return buffer;
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
