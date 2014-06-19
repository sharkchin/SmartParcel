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
using System.Diagnostics;



namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class Back2polygon : IGeometryAlgorithm
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
            SqlGeometry subject = record["GEOMETRY_BIN"] as SqlGeometry;
            if (subject==null)
                return null;
            //otherwise                        
            double polyarea=subject.STArea().Value;
            SqlGeometry buffer = subject.STBuffer(0.0005);            
            List<AbstractRecord> candidates = new List<AbstractRecord>(), parcels = new List<AbstractRecord>();
            
            for (int i = 0; i < impactors.Count; i++)
            {
                AbstractRecord impactor = impactors.ElementAt(i);                
                SqlGeometry imp = impactor[MasterProgramConstants.GEOMETRY_BIN] as SqlGeometry;
                if (imp==null)
                    continue;                
                
                if (buffer.Filter(imp))
                {
                    int np = imp.STNumPoints().Value;                                        
                    double? area0 = impactor["CAL_ACREAGE"] as double?;
                    double area = 0;
                    if (area0 != null)
                        area = (double)area0;
                    if (area < 0.00001 && np < 1000)
                        imp = imp.STBuffer(0.00001);
                    string code = impactor["LANDUSE_CATEGORY"] as string;                    
                    if (code == "RESIDENTIAL")
                    {                        
                        buffer = buffer.STDifference(imp);                        
                    }
                    else
                    {
                        if (imp.STIntersection(subject).STArea().Value / polyarea < 0.8)
                            buffer = buffer.STDifference(imp);
                    }                    
                    DataRecord parcel = new DataRecord(new string[] { "DMPID", "LOCATIONID", "Lon", "Lat", "GEOMETRY", "B2Poly" });                    
                    parcel["DMPID"] = impactor["_DMP_ID"];                    
                    parcel["LOCATIONID"] = impactor["LOCATION_ID"];                    
                    parcel["Lon"] = impactor["_X_COORD"];                    
                    parcel["Lat"] = impactor["_Y_COORD"];                    
                    parcel["GEOMETRY"] = impactor["GEOMETRY_BIN"];
                    parcel["B2Poly"] = 1;                   
                    
                    if (area < 3 && code == "RESIDENTIAL")
                        candidates.Add(parcel);                   
                }
            }
            
            int k = 0;
            bool found = false;
            SqlGeometry realbuf = new SqlGeometry(), realbuf2 = new SqlGeometry();
            double minDist = double.MaxValue;
            for (int i = 0; i < buffer.STNumGeometries(); i++)
            {
                SqlGeometry sg = buffer.STGeometryN(i + 1);
                if (sg.Filter(subject))
                {
                    found = true;
                    if (k++ == 0)
                        realbuf = sg;
                    else
                        realbuf = realbuf.STUnion(sg);
                }
                else //if subject is weirdly aligned (subject on parcel), find the closest one
                {
                    if (!found)
                    {
                        double dist = sg.STDistance(subject).Value;
                        if (dist < minDist)
                        {
                            minDist = dist;
                            realbuf2 = sg;
                        }
                    }
                }
            }
            
            if (!found) //if subject is weirdly aligned, use the closest one as buffer
                realbuf = realbuf2;
            foreach (DataRecord parcel in candidates)
            {                
                SqlGeometry sg = parcel["GEOMETRY"] as SqlGeometry;
                if (sg == null)
                    continue;
                if (sg.STDistance(realbuf) < 0.00002)
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
