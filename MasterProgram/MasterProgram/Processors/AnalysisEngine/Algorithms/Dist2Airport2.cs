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
    class Dist2Airport2 : IGeometryAlgorithm
    {
        public const int SRID = 4269;
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private List<AbstractRecord> impactors;
        private Dictionary<String, String> parameters;

        /// <summary>
        /// to test if point is in the polygon
        /// </summary>
        /// <param name="x">X cordinate of Point</param>
        /// <param name="y">Y cordinate of Point</param>
        /// <param name="polyX">array of X cordinates of Polygon</param>
        /// <param name="polyY">array of Y cordinates of Polygon</param>
        /// <param name="polySides">number of polygon sides</param>
        /// <returns>true/false</returns>
        private bool IsPointInPolygon(double x, double y, double[] polyX, double[] polyY, int polySides)
        {

            int i, j = polySides - 1;
            bool oddNodes = false;
            try
            {
                for (i = 0; i < polySides; i++)
                {
                    if ((((polyY[i] <= y) && (y < polyY[j])) ||
                      ((polyY[j] <= y) && (y < polyY[i]))) &&
                      (x < (polyX[j] - polyX[i]) * (y - polyY[i]) / (polyY[j] - polyY[i]) + polyX[i]))
                        oddNodes = !oddNodes;

                    j = i;
                }
            }
            catch (Exception e)
            {
                logger.Error("Error while testing point Intesrscts polygon ", e);
                throw new ApplicationException("Error while testing point Intesrscts polygon: " + e.Message);
            }

            return oddNodes;
        }

        /// <summary>
        /// to test if point is in the polygon
        /// </summary>
        /// <param name="point">Point Geometry</param>
        /// <param name="polygons">Polygon Geometry</param>
        /// <returns>true/false</returns>
        private bool IsPointInPolygon(AEPoint point, AEPolygon polygons)
        {
            bool res = false;
            try
            {
                res = IsPointInPolygon(point.XCordinate, point.YCordinate, polygons.XCordinates, polygons.YCordinates, polygons.NumberOfPoints);
            }
            catch (Exception e)
            {
                logger.Error("Error while testing point Intesrscts polygon ", e);
                throw e;
            }
            return res;
        }


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
            SqlGeometry apzone = (SqlGeometry)o;
            o=record["id"];
            string apID=o is DBNull?"N/A":(string)o;
            o=record["lat"];
            if(o is DBNull)
                return null;
            double lat=(double)o;
            o=record["lon"];
            if(o is DBNull)
                return null;
            double lon=(double)o;
            SqlGeography ap=GeoUtils.Point2SqlGeography(lon, lat, 4269);
            List<AbstractRecord> parcels = new List<AbstractRecord>();
            for (int i = 0; i < impactors.Count; i++)
            {
                AbstractRecord impactor = impactors.ElementAt(i);
                o = impactor[MasterProgramConstants.GEOMETRY_BIN];
                if (o is DBNull)
                    continue;
                SqlGeometry imp = (SqlGeometry)o;
                o = impactor["CAL_ACREAGE"];
                double area = o is DBNull ? 0 : (double)o;
                o = impactor["LANDUSE_CATEGORY"];
                string code = o is DBNull ? null : (string)o;
                if (area < 3 && code == "RESIDENTIAL" && apzone.Filter(imp))
                {
                    DataRecord parcel = new DataRecord(new string[] { "DMPID", "LOCATIONID", "Lon", "Lat", "GEOMETRY", "AirportID", "D2A" });
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
                    SqlGeography pc = GeoUtils.Point2SqlGeography((double)parcel["Lon"], (double)parcel["Lat"], 4269);
                    parcel["GEOMETRY"] = o is DBNull ? null : sg.STAsBinary().Value;
                    parcel["AirportID"] = apID;
                    parcel["D2A"] = pc.STDistance(ap);                                        
                    parcels.Add(parcel);
                }
            }
            return parcels;
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
    }
}
