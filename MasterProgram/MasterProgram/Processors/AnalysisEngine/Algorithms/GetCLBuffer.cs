using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DMP.MasterProgram.Processors.AnalysisEngine.Algorithms;
using DMP.MasterProgram.Processors.AnalysisEngine.Geometries;
using Dmp.Neptune.Collections;
using DMP.MasterProgram.Utils;
using log4net;
using log4net.Config;
using MonoGIS.NetTopologySuite.Geometries;
using MonoGIS.NetTopologySuite.IO;
using Microsoft.SqlServer.Types;
using System.Windows;



namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class GetCLBuffer : IGeometryAlgorithm
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
            object o = record[MasterProgramConstants.GEOMETRY_BIN];
            if (o is DBNull)
                return null;
            //otherwise
            SqlGeometry jnct = (SqlGeometry)o,
                buffer = (SqlGeometry)record["PRE_GEOMETRY_BIN"];
            if (buffer == null)
                return null;
            List<AbstractRecord> candidates=new List<AbstractRecord>(), parcels = new List<AbstractRecord>();            
            for (int i = 0; i < impactors.Count; i++)
            {               
                AbstractRecord impactor = impactors.ElementAt(i);
                o = impactor[MasterProgramConstants.GEOMETRY_BIN];
                if (o is DBNull)
                    continue;
                //otherwise
                SqlGeometry imp = (SqlGeometry)o;
                if (buffer.Filter(imp))
                {
                    try
                    {
                        buffer = buffer.STDifference(imp);
                        DataRecord parcel = new DataRecord(new string[] { "LocID", "Lon", "Lat", "GEOMETRY", "CL" });
                        o = impactor["_DMP_ID"];
                        parcel["DMPID"] = o is DBNull ? null : o.ToString();
                        o = impactor["LOCATION_ID"];
                        parcel["LOCATIONID"] = o is DBNull ? null : o.ToString();
                        o = impactor["_X_COORD"];
                        parcel["Lon"] = o is DBNull ? double.NaN : (double)o;
                        o = impactor["_Y_COORD"];
                        parcel["Lat"] = o is DBNull ? double.NaN : (double)o;
                        o = impactor["GEOMETRY_BIN"];
                        SqlGeometry sg = o is DBNull ? SqlGeometry.Null : (SqlGeometry)o;
                        parcel["GEOMETRY"] = o is DBNull ? null : sg.STAsBinary().Value;
                        parcel["CL"] = 1;
                        o = impactor["CAL_ACREAGE"];
                        double area = o is DBNull ? 0 : (double)o;
                        o = impactor["LANDUSE_CATEGORY"];
                        string code = o is DBNull ? null : (string)o;
                        if (area < 3 && code == "RESIDENTIAL")
                            candidates.Add(parcel);
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("something wrong in GetCLBuffer:"+e.ToString());
                    }
                }
            }
            int k = 0;
            SqlGeometry realbuf = new SqlGeometry();
            for (int i = 0; i < buffer.STNumGeometries(); i++)
            {
                SqlGeometry sg = buffer.STGeometryN(i + 1);
                if (sg.Filter(jnct.STBuffer(0.00001)))
                {
                    if (k++ == 0)
                        realbuf = sg;
                    else
                        realbuf = realbuf.STUnion(sg);
                }
            }
            if (realbuf == SqlGeometry.Null)
                return null;
            //otherwise    
            foreach (DataRecord parcel in candidates)
            {
                byte[] b = (byte[])parcel["GEOMETRY_BIN"];
                SqlGeometry sg = SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(b), 4269);
                if (sg.STDistance(realbuf) < 0.00001)
                {
                    parcels.Add(parcel);
                }
            }
            return parcels;
           
            
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
