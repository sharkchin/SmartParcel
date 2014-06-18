/*
 * "Important Points"
 * tileX = 3 = 011 2
 * tileY = 5 = 101 2
 * quadkey = 100111 2 = 213 4 = “213”
 * 
 * The length of a quadkey (the number of digits) equals the level of detail of the corresponding tile.
 * 
 * The quadkey of any tile starts with the quadkey of its parent tile.
 * 
 * At the lowest level of detail (Level 1), the map is 512 x 512 pixels. At each successive level of detail,
 * the map width and height grow by a factor of 2: Level 2 is 1024 x 1024 pixels, Level 3 is 2048 x 2048 pixels,
 * Level 4 is 4096 x 4096 pixels
 * 
 * Each tile is 256 x 256 pixels.
 * 
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoGIS.NetTopologySuite.Geometries;
using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;

namespace DMP.MasterProgram.Utils.Chunking
{
    class TileSystem
    {

        private const int TILE_WIDTH = 256;
        private const double dRadiansPerDegree = Math.PI / 180.0;
        private const double dDegreesPerRadian = 180.0 / Math.PI;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private static Point MercToGeo(double x, double y)
        {
            double lon = x - 180.0;
            y = (y - 180.0) * dRadiansPerDegree;                  //remove offset, -> radians

            double lat = Math.Atan(Math.Sinh(y));

            lat *= dDegreesPerRadian;

           
            return new Point(lon, lat, 0);
           
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="bsKey"></param>
        /// <param name="returnMerc"></param>
        /// <returns></returns>
        public static Envelope GetTileExtents(string bsKey)
        {

            double tileSize = 360.0;            //whole kitten caboodle

            double xll = 0.0;
            double yll = 0.0;

            double xur, yur;

            int len = bsKey.Length;

            for (int i = 0; i < len; i++)
            {
                char ch = bsKey[i];
                tileSize *= 0.5;
                if ((ch & 1) != 0)
                {
                    xll += tileSize;
                }
                ////0 is high, 1 is low
                if ((ch & 2) == 0)
                {
                    yll += tileSize;
                }

            }//For i

            xur = xll + tileSize;
            yur = yll + tileSize;

            Point pt1 = null;
            Point pt2 = null;

           
             pt1 = MercToGeo(xll, yll);
             pt2 = MercToGeo(xur, yur);
           


            return new Envelope(pt1.X, pt2.X, pt1.Y, pt2.Y);


        }

    }
}
    

