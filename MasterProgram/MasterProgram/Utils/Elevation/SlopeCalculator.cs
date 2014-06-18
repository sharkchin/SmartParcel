using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;

namespace DMP.MasterProgram.Utils.Elevation
{
    public class SlopeCalculator
    {


        private string sqlPolygonString = "POLYGON(({0},{1}, {2}, {3}, {4}))";

        #region Public Functions
        public void CalculateSlopeAtCenter(float[,] points, double xllCorner, double yllCorner, double cellSize, ref SqlGeometry outputGeom, double lessThanSlope)
        {
            StringBuilder builder = new StringBuilder();
            Object[] objArray = new Object[5];
            float[,] points3x3 = new float[3, 3];

            try
            {

                for (int r = 1; r < points.GetLength(0) - 1; r++)
                {

                    for (int c = 1; c < points.GetLength(1) - 1; c++)
                    {
                        points3x3[1, 1] = points[r, c];
                        points3x3[1, 0] = points[r, c - 1];
                        points3x3[1, 2] = points[r, c + 1];
                        points3x3[2, 0] = points[r + 1, c - 1];
                        points3x3[2, 1] = points[r + 1, c];
                        points3x3[2, 2] = points[r + 1, c + 1];
                        points3x3[0, 0] = points[r - 1, c - 1];
                        points3x3[0, 1] = points[r - 1, c];
                        points3x3[0, 2] = points[r - 1, c + 1];

                        //string slopeCateg = CalculateSlopeCategory(points3x3);
                        double slope = CalculateSlope(points3x3);

                        if (slope < lessThanSlope)
                        {
                            // string slopeCateg = CalculateSlopeCategory(slope);

                            double x1 = (xllCorner + (c * cellSize) - (cellSize / 2));
                            double y1 = (yllCorner + (r * cellSize) - (cellSize / 2));

                            double x2 = (xllCorner + (c * cellSize) + (cellSize / 2));

                            double y2 = (yllCorner + (r * cellSize) + (cellSize / 2));

                            objArray[0] = x1 + " " + y1;
                            objArray[1] = x2 + " " + y1;
                            objArray[2] = x2 + " " + y2;
                            objArray[3] = x1 + " " + y2;
                            objArray[4] = x1 + " " + y1;

                            builder.AppendFormat(sqlPolygonString, objArray);
                            SqlGeometry geometry = SqlGeometry.STGeomFromText(new SqlChars(builder.ToString()), 4269);
                            builder.Length = 0;
                            builder.Capacity = 0;
                            


                            if (outputGeom == null)
                            {
                                outputGeom = geometry;
                            }
                            else
                            {
                                outputGeom = outputGeom.STUnion(geometry);
                            }

                        }



                    }//for loop columns
                }//for loop rows

            }
            catch (Exception ex)
            {
                throw ex;
            }


        }

        public void CalculateSlopeOf3x3Grid(float[,] points, double xllCorner, double yllCorner, double cellSize, ref SqlGeometry outputGeom, double lessThanSlope)
        {
            StringBuilder builder = new StringBuilder();
            Object[] objArray = new Object[5];
            float[,] points3x3 = new float[3, 3];

            try
            {

              /*  for (int r = 1; r < points.GetLength(0) - 1; r = r + 3)
                {

                    for (int c = 1; c < points.GetLength(1) - 1; c = c + 3)
                    {
                        points3x3[1, 1] = points[r, c];
                        points3x3[1, 0] = points[r, c - 1];
                        points3x3[1, 2] = points[r, c + 1];
                        points3x3[2, 0] = points[r + 1, c - 1];
                        points3x3[2, 1] = points[r + 1, c];
                        points3x3[2, 2] = points[r + 1, c + 1];
                        points3x3[0, 0] = points[r - 1, c - 1];
                        points3x3[0, 1] = points[r - 1, c];
                        points3x3[0, 2] = points[r - 1, c + 1];

                        //string slopeCateg = CalculateSlopeCategory(points3x3);
                        double slope = CalculateSlope(points3x3);
                        if (slope < lessThanSlope)
                        {

                            double x1 = (xllCorner + ((c - 1) * cellSize - (cellSize / 2)));
                            double y1 = (yllCorner + ((r - 1) * cellSize) - (cellSize / 2));

                            double x2 = (xllCorner + ((c + 1) * cellSize) + (cellSize / 2));

                            double y2 = (yllCorner + ((r + 1) * cellSize) + (cellSize / 2));

                            objArray[0] = x1 + " " + y1;
                            objArray[1] = x2 + " " + y1;
                            objArray[2] = x2 + " " + y2;
                            objArray[3] = x1 + " " + y2;
                            objArray[4] = x1 + " " + y1;

                            builder.AppendFormat(sqlPolygonString, objArray);
                            SqlGeometry geometry = SqlGeometry.STGeomFromText(new SqlChars(builder.ToString()), 4269);
                            builder.Length = 0;
                            builder.Capacity = 0;
                            


                            if (outputGeom == null)
                            {
                                outputGeom = geometry;
                            }
                            else
                            {
                                outputGeom = outputGeom.STUnion(geometry);

                            }


                        }



                    }//for loop columns
                }//for loop rows*/
                for (int r = 1; r < points.GetLength(0) - 1; r = r + 3)
                {

                    for (int c = 1; c < points.GetLength(1) - 1; c = c + 3)
                    {
                        points3x3[1, 1] = points[r, c];
                        points3x3[1, 0] = points[r, c - 1];
                        points3x3[1, 2] = points[r, c + 1];
                        points3x3[2, 0] = points[r - 1, c - 1];
                        points3x3[2, 1] = points[r - 1, c];
                        points3x3[2, 2] = points[r - 1, c + 1];
                        points3x3[0, 0] = points[r + 1, c - 1];
                        points3x3[0, 1] = points[r + 1, c];
                        points3x3[0, 2] = points[r + 1, c + 1];

                        //string slopeCateg = CalculateSlopeCategory(points3x3);
                        double slope = CalculateSlope(points3x3);
                        if (slope < lessThanSlope)
                        {

                            double x1 = (xllCorner + ((c - 1) * cellSize - (cellSize / 2)));
                            double y1 = (yllCorner + ((r - 1) * cellSize) - (cellSize / 2));

                            double x2 = (xllCorner + ((c + 1) * cellSize) + (cellSize / 2));

                            double y2 = (yllCorner - ((r + 1) * cellSize) + (cellSize / 2));

                            objArray[0] = x1 + " " + y1;
                            objArray[1] = x2 + " " + y1;
                            objArray[2] = x2 + " " + y2;
                            objArray[3] = x1 + " " + y2;
                            objArray[4] = x1 + " " + y1;

                            builder.AppendFormat(sqlPolygonString, objArray);
                            SqlGeometry geometry = SqlGeometry.STGeomFromText(new SqlChars(builder.ToString()), 4269);
                            builder.Length = 0;
                            builder.Capacity = 0;



                            if (outputGeom == null)
                            {
                                outputGeom = geometry;
                            }
                            else
                            {
                                outputGeom = outputGeom.STUnion(geometry);

                            }


                        }



                    }//for loop columns
                }//for loop rows
            }
            catch (Exception ex)
            {
                throw ex;
            }


        }
        #endregion

        #region Private Functions

        private string CalculateSlopeCategory(float[,] pointMatrix)
        {
            string slopeCateg = null;
            double slope = CalculateSlope(pointMatrix);

            slopeCateg = CalculateSlopeCategory(slope);

            return slopeCateg;
        }

        private string CalculateSlopeCategory(double slope)
        {
            string slopeCateg = null;

            /*  if (slope % 5 != 0)
            {
                int slopeLow =((int) slope / 5 )*5+ 1;
                int slopeHigh = slopeLow + 4;
                slopeCateg = slopeLow + "-" + slopeHigh;
            }
            else
            {
                int slopeLow = ((int)slope / 5) * 5 - 4;
                int slopeHigh = (int)slope ;
                slopeCateg = slopeLow + "-" + slopeHigh;

            }*/


            double slopeLow = Math.Floor(slope);
            double slopeHigh = slopeLow + 1;
            slopeCateg = slopeLow + "-" + slopeHigh;

            return slopeCateg;
        }

        private double CalculateSlope(float[,] pointMatrix)
        {
            float slope = 0;
            float cellSizeInM = 10;

            try
            {

                float e1 = pointMatrix[2, 0];
                float e2 = pointMatrix[2, 1];
                float e3 = pointMatrix[2, 2];
                float e4 = pointMatrix[1, 0];
                float e5 = pointMatrix[1, 2];
                float e6 = pointMatrix[0, 0];
                float e7 = pointMatrix[0, 1];
                float e8 = pointMatrix[0, 2];


                float nX = (e1 + 2 * e4 + e6) - (e3 + 2 * e5 + e8);
                float nY = (e6 + 2 * e7 + e8) - (e1 + 2 * e2 + e3);

                slope = (float)Math.Sqrt(nX * nX + nY * nY) / (8*cellSizeInM);
                slope = slope * 100;
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return slope;

        }

        #endregion
    }
}
