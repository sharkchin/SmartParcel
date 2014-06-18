using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Types;
using System.Windows;


namespace DMP.MasterProgram.Utils.AnalysisEngineUtil
{
    public class AlgorithmUtil
    {
        public const int SRID = 4269;

        public static double ToMeters(double distance, string unit)
        {
            if (unit.Equals("Meter"))
                return distance;
            else if (unit.Equals("Kilometer"))
                return distance * 1000;
            else if (unit.Equals("Feet"))
                return distance * 0.3048;
            else
                return distance * 1609.344;

        }

        /// <summary>
        /// Convert a Vector (point) into a Point SqlGeometry object
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static SqlGeometry Point2SqlGeometry(Vector point, int SRID)
        {
            System.Data.SqlTypes.SqlChars chars = new System.Data.SqlTypes.SqlChars(String.Format("POINT({0} {1})", point.X, point.Y));
            return SqlGeometry.STGeomFromText(chars, SRID);
        }

        /// <summary>
        /// calculate how many meters in one degree at a point
        /// </summary>
        /// <param name="sg"></param>
        /// <returns></returns>
        /// <remarks>by Alex Feb.2013</remarks>
        public static double MetersPerDegree(double X, double Y, int SRID)
        {
            SqlGeography h1 = Point2SqlGeography(X - 0.5, Y, SRID), h2 = Point2SqlGeography(X + 0.5, Y, SRID),
                v1 = Point2SqlGeography(X, Y - 0.5, SRID), v2 = Point2SqlGeography(X, Y + 0.5, SRID);
            double hr = h1.STDistance(h2).Value, hv = v1.STDistance(v2).Value;
            return (hr + hv) / 2;
        }

        /// <summary>
        /// Convert a Vector (point) into a Point SqlGeometry object
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        /// <remarks>by Alex Dec.2012</remarks>
        public static SqlGeography Point2SqlGeography(Double X, Double Y, int SRID)
        {
            System.Data.SqlTypes.SqlChars chars = new System.Data.SqlTypes.SqlChars(String.Format("POINT({0} {1})", X, Y));
            return SqlGeography.STGeomFromText(chars, SRID);
            
        }

        public static SqlGeography GetGeographyFromGeometry(SqlGeometry geom)
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

    }
}
