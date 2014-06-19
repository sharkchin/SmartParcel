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
    class CalcAcreage : IGeometryAlgorithm
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
        /// 
        /// </summary>
        /// <param name="record">Subject Record</param>
        /// <returns>List of Impactors intersect with Subject Record</returns>
        public object ProcessRecord(AbstractRecord record, bool isSubByTask)
        {
            object o = record[MasterProgramConstants.GEOMETRY_BIN];
            if (o is DBNull)
                return null;
            SqlGeometry parcel = (SqlGeometry)o, cent = new SqlGeometry();
            if (!parcel.STIsValid())
            {
                parcel=parcel.MakeValid();
            }
            try
            {                
                SqlGeometry ev = parcel.STEnvelope();
                double lat = double.NaN, lon = double.NaN;
                if (record.Fields.Contains("lat") && record.Fields.Contains("lon"))
                {
                    o = record["lat"];
                    lat = o is DBNull ? double.NaN : (double)o;

                    o = record["lon"];
                    lon = o is DBNull ? double.NaN : (double)o;
                }
                if (double.IsNaN(lat) || double.IsNaN(lon))
                {
                    if (!parcel.STIsValid())
                        parcel = parcel.MakeValid();
                    if (parcel.STGeometryType() != "Polygon" && parcel.STGeometryType() != "MultiPolygon")
                        cent = GetCenterOfPoints(parcel);
                    else
                        cent = parcel.STCentroid();
                    lat = cent.STY.Value; lon = cent.STX.Value;
                }
                double ratio = GeoUtils.MetersPerDegree(lon, lat, SRID);
                double area = parcel.STArea().Value * ratio * ratio * 0.000247105;
                return area;
            }
            catch (Exception e)
            { 
                Console.WriteLine();
                throw new Exception("something woring within CalcAcreage:" + e.Message);
            }

            
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
            if (geom.STIsValid())
            {
                return SqlGeography.STGeomFromWKB(geom.STAsBinary(), SRID);
            }
            else
            {
                return SqlGeography.STGeomFromWKB(
                    geom.MakeValid().Reduce(.000001).STUnion(geom.STStartPoint()).MakeValid().STAsBinary(), SRID);
            }

            //try
            //{
            //    return SqlGeography.STGeomFromWKB(geom.STAsBinary(), SRID);
            //}
            //catch (Exception)
            //{
            //    // A common reason for an exception being thrown is invalid ring orientation, 
            //    // so attempt to fix it. The technique used is described at
            //    // http://blogs.msdn.com/edkatibah/archive/2008/08/19/working-with-invalid-data-and-the-sql-server-2008-geography-data-type-part-1b.aspx

            //    return SqlGeography.STGeomFromWKB(
            //        geom.MakeValid().Reduce(.000001).STUnion(geom.STStartPoint()).MakeValid().STAsBinary(), SRID);
            //}
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
