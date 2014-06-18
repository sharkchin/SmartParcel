using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;
using System.IO;

namespace DMP.MasterProgram.Utils.Elevation
{
    public class ElevationProcessor
    {
        private  string sqlPolygonString = "POLYGON(({0},{1}, {2}, {3}, {4}))";
       // private string ShapeFilePath = "D:\\Data Path\\Elevation Data\\Final\\Elevation";
        private string dataFilePath =null;// "D:\\Data Path\\Elevation Data\\Final\\";


        public ElevationProcessor(string dataFilePath)
        {
           // this.ShapeFilePath = shapeFilePath;
            this.dataFilePath = dataFilePath;
        }
        #region Public Functions
        public SqlGeometry GetElevation(SqlGeometry subjGeometry, double lessThan)
        {

            SqlGeometry intersectedGeom = null; ;
            try
            {
                string ShapeFilePath = System.IO.Path.Combine(dataFilePath, MasterProgramConstants.ELEVATION_SHAPE_FILE_NAME);
                List<string> intersectingFiles = ShapeFileQueryEngine.GetRecords(subjGeometry, ShapeFilePath);
                

                SlopeCalculator slopeCalculator = new SlopeCalculator();

                for (int i = 0; i < intersectingFiles.Count; i++)
                {
                    double xllCorner = 0;
                    double yllCorner = 0;
                    double cellSize = 0;
                    float[,] intersectingGrid = GetIntersectingGrid(subjGeometry,System.IO.Path.Combine(dataFilePath,intersectingFiles[i]), ref xllCorner, ref yllCorner, ref cellSize);
                    slopeCalculator.CalculateSlopeOf3x3Grid(intersectingGrid, xllCorner, yllCorner, cellSize, ref intersectedGeom, lessThan);
                    //slopeCalculator.CalculateSlopeAtCenter(intersectingGrid, xllCorner, yllCorner, cellSize, ref intersectedGeom, lessThan);

                }


            }
            catch (Exception ex)
            {
                throw ex;
            }

            return intersectedGeom;

        }

        #endregion

        #region Private Functions
        private float[,] GetIntersectingGrid1(SqlGeometry geometry, string filePath, ref double xStart, ref double yStart, ref double cSize)
        {
            SqlGeometry subjEnvelope = geometry.STEnvelope();
            float[,] elevationPoints = null;

            string headerFile =  System.IO.Path.Combine(filePath , MasterProgramConstants.ELEVATION_RAWDATA_FILENAME);
            headerFile = headerFile + ".hdr";
            int rows = 0;
            int columns = 0;
            double xllCorner = 0;
            double yllCorner = 0;
            double cellSize = 0;
            double NODATA = -9999;
            bool msbfirst = true;

            //reading the header file
            try
            {
                StreamReader hdrReader = new StreamReader(headerFile);
                string hdrString = null;
                while ((hdrString = hdrReader.ReadLine()) != null)
                {
                    int startIndex = hdrString.IndexOf(" ");
                    int endIndex = hdrString.LastIndexOf(" ");
                    hdrString = hdrString.Substring(0, startIndex) + hdrString.Substring(endIndex);
                    String[] lineTemp = hdrString.Split(' ');
                    if (lineTemp[0].Trim().Equals("nrows"))
                    {
                        rows = int.Parse(lineTemp[1]);
                    }
                    else if (lineTemp[0].Trim().Equals("ncols"))
                    {
                        columns = int.Parse(lineTemp[1]);

                    }
                    else if (lineTemp[0].Trim().Equals("xllcorner"))
                    {
                        xllCorner = Double.Parse(lineTemp[1]);
                    }
                    else if (lineTemp[0].Trim().Equals("yllcorner"))
                    {
                        yllCorner = Double.Parse(lineTemp[1]);
                    }
                    else if (lineTemp[0].Trim().Equals("cellsize"))
                    {
                        cellSize = Double.Parse(lineTemp[1]);
                    }
                    else if (lineTemp[0].Trim().Equals("NODATA_value"))
                    {
                        NODATA = Double.Parse(lineTemp[1]);
                    }
                    else if (lineTemp[0].Trim().Equals("byteorder"))
                    {
                        msbfirst = lineTemp[1].Trim().Equals("MSBFIRST");
                    }
                }//while 

                StringBuilder builder = new StringBuilder();

                //create a geometry of the boundary of square
                Object[] objArray = new Object[5];
                objArray[0] = xllCorner + " " + yllCorner;
                objArray[1] = (xllCorner + cellSize * rows) + " " + yllCorner;
                objArray[2] = (xllCorner + cellSize * rows) + " " + (yllCorner + cellSize * rows);
                objArray[3] = xllCorner + " " + (yllCorner + cellSize * rows);
                objArray[4] = xllCorner + " " + yllCorner;

                builder.AppendFormat(sqlPolygonString, objArray);
                SqlGeometry filePoly = SqlGeometry.STGeomFromText(new SqlChars(builder.ToString()), 4269);

                //find the part of square that intersected with subject
                SqlGeometry intersectedGeometry = subjEnvelope.STIntersection(filePoly);
                SqlGeometry intersectedEnvelope = intersectedGeometry.STEnvelope();

                double xMin = intersectedEnvelope.STPointN(1).STX.Value;
                double yMin = intersectedEnvelope.STPointN(1).STY.Value;

                double xMax = intersectedEnvelope.STPointN(3).STX.Value;
                double yMax = intersectedEnvelope.STPointN(3).STY.Value;

                double xMinDiff = (xMin - xllCorner);
                int xStartingPoint = Convert.ToInt32(xMinDiff / cellSize);

                double xMaxDiff = (xMax - xllCorner);
                int xEndingPoint = Convert.ToInt32(xMaxDiff / cellSize);


                double yMinDiff = (yMin - yllCorner);
                int yStartingPoint = Convert.ToInt32(yMinDiff / cellSize);

                double yMaxDiff = (yMax - yllCorner);
                int yEndingPoint = Convert.ToInt32(yMaxDiff / cellSize);



                //fill the references
                xStart = xllCorner + xStartingPoint * cellSize;
                yStart = yllCorner + yStartingPoint * cellSize;
                cSize = cellSize;



                string fltFile = System.IO.Path.Combine(filePath, MasterProgramConstants.ELEVATION_RAWDATA_FILENAME);
                fltFile = fltFile + ".flt";

                // elevationPoints = new float[xEndingPoint - xStartingPoint + 2, yEndingPoint - yStartingPoint  + 2];
                elevationPoints = new float[yEndingPoint - yStartingPoint + 2, xEndingPoint - xStartingPoint + 2];


                //to keep track of elements of final2-d array
                int eX = 0;
                int eY = 0;
                int startingEY = 0;

                

                if (xStartingPoint == 0)
                {
                    startingEY = 1;
                }
                else
                {
                    xStartingPoint--;
                }

                if (xEndingPoint == columns)
                {


                }
                else
                {
                    xEndingPoint++;

                }

                if (yStartingPoint == 0)
                {
                    eX++;

                }
                else
                {
                    yStartingPoint--;

                }

                if (yEndingPoint == rows)
                {

                }
                else
                {
                    yEndingPoint++;
                }



                using (BinaryReader fileReader = new BinaryReader(File.Open(fltFile, FileMode.Open, FileAccess.Read, FileShare.Read), Encoding.BigEndianUnicode))
                {
                    for (int r = yStartingPoint; r < yEndingPoint; r++)
                    {
                        fileReader.BaseStream.Position = ((yStartingPoint + eX) * rows + xStartingPoint) * 4;
                        eY = startingEY;

                        for (int c = xStartingPoint; c < xEndingPoint; c++)
                        {
                            if (msbfirst)
                            {
                                byte[] bytes = fileReader.ReadBytes(4);
                                Array.Reverse(bytes);
                                float data = BitConverter.ToSingle(bytes, 0);
                                if (data == -9999)
                                    elevationPoints[eX, eY] = 0;
                                else
                                    elevationPoints[eX, eY] = data;

                            }
                            else
                            {
                                float data = fileReader.ReadSingle();
                                if (data == -9999)
                                    elevationPoints[eX, eY] = 0;
                                else
                                    elevationPoints[eX, eY] = data;
                            }

                            eY++;

                        }

                        eX++;

                    }



                }
            }
            catch (Exception ex)
            {
                throw ex;
            }


            return elevationPoints;
        }

        private float[,] GetIntersectingGrid(SqlGeometry geometry, string filePath, ref double xStart, ref double yStart, ref double cSize)
        {
            SqlGeometry subjEnvelope = geometry.STEnvelope();
            float[,] elevationPoints = null;

            string headerFile = System.IO.Path.Combine(filePath, MasterProgramConstants.ELEVATION_RAWDATA_FILENAME);
            headerFile = headerFile + ".hdr";
            int rows = 0;
            int columns = 0;
            double xllCorner = 0;
            double yllCorner = 0;
            double cellSize = 0;
            double NODATA = -9999;
            bool msbfirst = true;

            //reading the header file
            try
            {
                StreamReader hdrReader = new StreamReader(headerFile);
                string hdrString = null;
                while ((hdrString = hdrReader.ReadLine()) != null)
                {
                    int startIndex = hdrString.IndexOf(" ");
                    int endIndex = hdrString.LastIndexOf(" ");
                    hdrString = hdrString.Substring(0, startIndex) + hdrString.Substring(endIndex);
                    String[] lineTemp = hdrString.Split(' ');
                    if (lineTemp[0].Trim().Equals("nrows"))
                    {
                        rows = int.Parse(lineTemp[1]);
                    }
                    else if (lineTemp[0].Trim().Equals("ncols"))
                    {
                        columns = int.Parse(lineTemp[1]);

                    }
                    else if (lineTemp[0].Trim().Equals("xllcorner"))
                    {
                        xllCorner = Double.Parse(lineTemp[1]);
                    }
                    else if (lineTemp[0].Trim().Equals("yllcorner"))
                    {
                        yllCorner = Double.Parse(lineTemp[1]);
                    }
                    else if (lineTemp[0].Trim().Equals("cellsize"))
                    {
                        cellSize = Double.Parse(lineTemp[1]);
                    }
                    else if (lineTemp[0].Trim().Equals("NODATA_value"))
                    {
                        NODATA = Double.Parse(lineTemp[1]);
                    }
                    else if (lineTemp[0].Trim().Equals("byteorder"))
                    {
                        msbfirst = lineTemp[1].Trim().Equals("MSBFIRST");
                    }
                }//while 

                StringBuilder builder = new StringBuilder();

                //create a geometry of the boundary of square
                Object[] objArray = new Object[5];
                objArray[0] = xllCorner + " " + yllCorner;
                objArray[1] = (xllCorner + cellSize * rows) + " " + yllCorner;
                objArray[2] = (xllCorner + cellSize * rows) + " " + (yllCorner + cellSize * rows);
                objArray[3] = xllCorner + " " + (yllCorner + cellSize * rows);
                objArray[4] = xllCorner + " " + yllCorner;

                builder.AppendFormat(sqlPolygonString, objArray);
                SqlGeometry filePoly = SqlGeometry.STGeomFromText(new SqlChars(builder.ToString()), 4269);

                //find the part of square that intersected with subject
                SqlGeometry intersectedGeometry = subjEnvelope.STIntersection(filePoly);
                SqlGeometry intersectedEnvelope = intersectedGeometry.STEnvelope();

                double xMin = intersectedEnvelope.STPointN(1).STX.Value;
                double yMin = intersectedEnvelope.STPointN(1).STY.Value;

                double xMax = intersectedEnvelope.STPointN(3).STX.Value;
                double yMax = intersectedEnvelope.STPointN(3).STY.Value;

                double xMinDiff = xMin - xllCorner; 
                int xStartingPoint = Convert.ToInt32(xMinDiff / cellSize);

                double xMaxDiff = xMax - xllCorner; ; 
                int xEndingPoint = Convert.ToInt32(xMaxDiff / cellSize);


                double yMinDiff = (yllCorner + (rows*cellSize)) - yMin;
                int yEndingPoint = Convert.ToInt32(yMinDiff / cellSize);

                double yMaxDiff = (yllCorner + (rows * cellSize)) - yMax; ;
                int yStartingPoint = Convert.ToInt32(yMaxDiff / cellSize);

                double sD = yMax - yllCorner;
                int s = Convert.ToInt32(sD / cellSize);



                //fill the references
                xStart = xllCorner + xStartingPoint * cellSize;
                //change this
                yStart = yllCorner + s * cellSize;
                cSize = cellSize;



                string fltFile = System.IO.Path.Combine(filePath, MasterProgramConstants.ELEVATION_RAWDATA_FILENAME);
                fltFile = fltFile + ".flt";

                // elevationPoints = new float[xEndingPoint - xStartingPoint + 2, yEndingPoint - yStartingPoint  + 2];
                elevationPoints = new float[yEndingPoint - yStartingPoint + 2, xEndingPoint - xStartingPoint + 2];


                //to keep track of elements of final2-d array
                int eX = 0;
                int eY = 0;
                int startingEY = 0;



                if (xStartingPoint == 0)
                {
                    startingEY = 1;
                }
                else
                {
                    xStartingPoint--;
                }

                if (xEndingPoint == columns)
                {


                }
                else
                {
                    xEndingPoint++;

                }

                if (yStartingPoint == 0)
                {
                    eX++;

                }
                else
                {
                    yStartingPoint--;

                }

                if (yEndingPoint == rows)
                {

                }
                else
                {
                    yEndingPoint++;
                }


                


                using (BinaryReader fileReader = new BinaryReader(File.Open(fltFile, FileMode.Open, FileAccess.Read, FileShare.Read), Encoding.BigEndianUnicode))
                {
                    for (int r = yStartingPoint; r < yEndingPoint; r++)
                    {
                        fileReader.BaseStream.Position =(((yStartingPoint + eX) * rows + xStartingPoint) * 4);
                        eY = startingEY;

                        for (int c = xStartingPoint; c < xEndingPoint; c++)
                        {
                            if (msbfirst)
                            {
                                byte[] bytes = fileReader.ReadBytes(4);
                                Array.Reverse(bytes);
                                float data = BitConverter.ToSingle(bytes, 0);
                                if (data == -9999)
                                    elevationPoints[eX, eY] = 0;
                                else
                                    elevationPoints[eX, eY] = data;

                            }
                            else
                            {
                                float data = fileReader.ReadSingle();
                                if (data == -9999)
                                    elevationPoints[eX, eY] = 0;
                                else
                                    elevationPoints[eX, eY] = data;
                            }

                            eY++;

                        }

                        eX++;

                    }



                }
            }
            catch (Exception ex)
            {
                throw ex;
            }


            return elevationPoints;
        }


        #endregion
    }
}
