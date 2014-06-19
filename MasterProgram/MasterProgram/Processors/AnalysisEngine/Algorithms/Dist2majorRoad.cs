using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using DMP.MasterProgram.Processors.AnalysisEngine.Algorithms;
using DMP.MasterProgram.Processors.AnalysisEngine.Geometries;
using Dmp.Neptune.Collections;
using Dmp.Neptune.Data;
using Dmp.Neptune.Utils.ShapeFile;
using DMP.MasterProgram.Utils;
using Dmp.Neptune.DatabaseManager;
using log4net;
using log4net.Config;
using Microsoft.SqlServer.Types;
using System.Windows;


namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class Dist2majorRoad : IGeometryAlgorithm
    {
        public const int SRID = 4269;
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
            object o = null;
            Dictionary<string, object> facts = new Dictionary<string, object>();
            if (record.Fields.Contains("FACTS"))
            {
                o = record["FACTS"];
                if (o is DBNull || o == null)
                    return null;
                facts = (Dictionary<string, object>)o;
            }
            facts.Add("Dist2majorRoad", -9999);
            facts.Add("MajorRoadCFTID", "NA");
            facts.Add("MajorRoadName", "NA");
            facts.Add("RoadCFTID", "NA");
            facts.Add("RoadName", "NA");
            facts.Add("RoadAccuracy",-1);
            facts.Add("RoadClass",-1);
            facts.Add("RoadSpeedCat",-1);
            double dist = double.MaxValue, dist2mr = double.MaxValue;
            o = record[MasterProgramConstants.GEOMETRY_BIN];
            if (o is DBNull)
                return facts;
            SqlGeometry parcel = (SqlGeometry)o, cent = new SqlGeometry();
            string stname = record["SITE_STREET_NAME"] as string, stmode = record["SITE_MODE"] as string;
            bool NoStName = (stname == null);
            if (!NoStName) 
            {
                stname = stname.Trim().ToLower();
                if (stmode != null) stmode = stmode.Trim().ToLower();
            }
            //double px = 0, py = 0;
            //, cent=new SqlGeometry();
            double lat = double.NaN, lon = double.NaN;
            Vector pcvec = new Vector();
            if (record.Fields.Contains("lat") && record.Fields.Contains("lon"))
            {
                o = record["lat"];
                lat = o is DBNull ? double.NaN : (double)o;
                o = record["lon"];
                lon = o is DBNull ? double.NaN : (double)o;
                pcvec = new Vector(lon, lat);
                cent = GeoUtils.Point2SqlGeometry(pcvec, SRID);
            }
            //try
            //{
            if (double.IsNaN(lat) || double.IsNaN(lon))
            {
                if (!parcel.STIsValid())
                    parcel = parcel.MakeValid();
                if (parcel.STGeometryType() != "Polygon" && parcel.STGeometryType() != "MultiPolygon")
                    cent = GetCenterOfPoints(parcel);
                else
                    cent = parcel.STCentroid();
                lat = cent.STY.Value; lon = cent.STX.Value;
                pcvec = new Vector(lon, lat);
            }
            double METER_PER_DEGREE = GeoUtils.MetersPerDegree(lon, lat, SRID);
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine(e);
            //}

            //SqlGeometry closeimp = new SqlGeometry();
            int s = -1, t = -1, accu=-1;

            //double minDist = double.MaxValue;
            try
            {
                if (impactors.Count > 0)
                {
                    for (int i = 0; i < impactors.Count; i++)
                    {
                        AbstractRecord impactor = impactors.ElementAt(i);
                        Vector impvec = new Vector((double)impactor["_X_COORD"], (double)impactor["_Y_COORD"]);
                        int? frc=impactor["FRC"] as int?;
                        double disti=double.MaxValue;
                        if(frc!=null && (int)frc<6)
                        {
                            disti = (impvec - pcvec).Length;
                            if (dist2mr > disti)
                            {
                                dist2mr = disti;
                                t = i;
                            }
                        }

                        int accuracy=-1;
                        if (NoStName && accuracy!=1)
                        {                            
                            accuracy=0;
                        }
                        else
                        {
                            string fullStName = impactor["NAME"] as string;
                            if (fullStName == null) continue;
                            fullStName = fullStName.Trim().ToLower();
                            if (fullStName.Contains(stname))
                            {
                                if (stmode == null)
                                    accuracy = 1;
                                else if (fullStName.Contains(stmode))
                                    accuracy = 1;
                            }
                        }
                        if(accuracy>=0)
                        {   
                            if(disti==double.MaxValue)
                                disti = (impvec - pcvec).Length;
                            if (dist > disti)
                            {
                                dist = disti;
                                s = i;
                                accu = accuracy;
                            }
                        }
                        
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Dist2majorRoad:"+e.Message+","+e.StackTrace);
            }
            if (t != -1)
            {
                AbstractRecord closeimp = impactors.ElementAt(t);
                dist = cent.STDistance((SqlGeometry)closeimp[MasterProgramConstants.GEOMETRY_BIN]).Value;
                facts["Dist2majorRoad"] = dist * METER_PER_DEGREE;
                facts["MajorRoadCFTID"] = closeimp["_CFTID"];
                facts["MajorRoadName"] = closeimp["NAME"];
            }
            if (s != -1)
            {
                AbstractRecord closeimp = impactors.ElementAt(s);
                facts["RoadCFTID"] = closeimp["_CFTID"];
                facts["RoadName"] = closeimp["NAME"];
                facts["RoadAccuracy"] = accu;
                facts["RoadClass"] = closeimp["FRC"];
                facts["RoadSpeedCat"] = closeimp["SPEEDCAT"];
            }
            return facts;
        }
        private SqlGeometry GetCenterOfPoints(SqlGeometry geo)
        {
            int n = geo.STNumPoints().Value;
            Vector accum = new Vector(0, 0);

            for (int i = 0; i < n; i++)
            {
                accum += Point2Vector(geo.STPointN(i + 1));
            }

            return GeoUtils.Point2SqlGeometry(accum / n, geo.STSrid.Value);
        }
        private SqlGeography GetGeographyFromGeometry(SqlGeometry geom)
        {
            if (geom == null) return null;

            try
            {
                return SqlGeography.STGeomFromWKB(geom.STAsBinary(), SRID);
            }
            catch (Exception)
            {
                // A common reason for an exception being thrown is invalid ring orientation, 
                // so attempt to fix it. The technique used is described at
                // http://blogs.msdn.com/edkatibah/archive/2008/08/19/working-with-invalid-data-and-the-sql-server-2008-geography-data-type-part-1b.aspx

                return SqlGeography.STGeomFromWKB(
                    geom.MakeValid().Reduce(.000001).STUnion(geom.STStartPoint()).STAsBinary(), SRID);
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
        private static Vector Point2Vector(SqlGeometry point)
        {
            return new Vector(point.STX.Value, point.STY.Value);
        }
    }
}
