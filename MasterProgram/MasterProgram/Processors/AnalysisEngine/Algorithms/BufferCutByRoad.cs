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
    class BufferCutByRoad : IGeometryAlgorithm
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
            SqlGeometry parcel = (SqlGeometry)o,
                //buf = GeoUtils.BufferByEdge(parcel, 0.0003, 0.00003);              
                buf = SqlGeometry.Null;
            try
            {
                buf = parcel.Reduce(0.00003).STBuffer(0.0003);
                for (int i = 0; i < impactors.Count; i++)
                {
                    AbstractRecord impactor = impactors.ElementAt(i);
                    o = impactor[MasterProgramConstants.GEOMETRY_BIN];
                    if (o is DBNull)
                        continue;
                    SqlGeometry imp = (SqlGeometry)o;
                    //Console.WriteLine("BufferCutByRoad:"+record["locationid"] + ", " + impactor["id"]);
                    if (imp.Filter(buf))
                    {
                        //if (imp.STNumPoints() > 100)
                        //    imp = imp.STIntersection(buf);
                        buf = buf.STDifference(imp.STBuffer(0.00001));
                    }
                    imp = SqlGeometry.Null;
                }

                int k = 0;
                SqlGeometry realbuf = new SqlGeometry();
                //bool found = false;
                for (int i = 0; i < buf.STNumGeometries(); i++)
                {
                    if (buf.STGeometryN(i + 1).Filter(parcel))
                    {
                        if (k++ == 0)
                            realbuf = buf.STGeometryN(i + 1);
                        else
                            realbuf = realbuf.STUnion(buf.STGeometryN(i + 1));
                    }
                }
                return realbuf;
            }
            catch (Exception e)
            {
                Console.WriteLine("exception in BufferCutByRoad:"+e.StackTrace);
                return null;
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
