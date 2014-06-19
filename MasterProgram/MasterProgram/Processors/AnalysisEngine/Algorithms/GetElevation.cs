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
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;


namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class GetElevation : IGeometryAlgorithm
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
            object o = record[MasterProgramConstants.GEOMETRY_BIN];
            if (o is DBNull)
                return null;
            SqlGeometry parcel = (SqlGeometry)o, cent = new SqlGeometry();
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

            double elev = GetElev(lat, lon);
            return elev;
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
        private static double GetElev(double lat, double lon)
        {
            string foldername = (lat > 0 ? "n" + Math.Ceiling(lat).ToString("00") : "s" + Math.Ceiling(lat * -1).ToString("00"))
                + (lon > 0 ? "e" + Math.Ceiling(lon).ToString("000") : "w" + Math.Ceiling(lon * -1).ToString("000"));
            string folderdir = @"\\dmpdpu3\d$\Data\USGS\NED 13\float\" + foldername;
            
            //Debug.WriteLine(foldername);
            string hdrfile = folderdir + "\\float" + foldername + "_13.hdr";
            if (!File.Exists(hdrfile))
                return -9999;
            int ncols = -9999, nrows = -9999;
            double xll = -9999, yll = -9999, cellsize = -9999;
            using (StreamReader reader = new StreamReader(File.Open(hdrfile,FileMode.Open,FileAccess.Read,FileShare.Read)))
            {
                string str = "";
                while ((str = reader.ReadLine()) != null)
                {
                    string[] rowstr = Regex.Split(str, @"\s+");
                    switch (rowstr[0])
                    {
                        case "ncols":
                            ncols = int.Parse(rowstr[1]);
                            break;
                        case "nrows":
                            nrows = int.Parse(rowstr[1]);
                            break;
                        case "xllcorner":
                            xll = double.Parse(rowstr[1]);
                            break;
                        case "yllcorner":
                            yll = double.Parse(rowstr[1]);
                            break;
                        case "cellsize":
                            cellsize = double.Parse(rowstr[1]);
                            break;
                        default:
                            break;
                    }

                }
            }
            string fltfile = folderdir + "\\float" + foldername + "_13.flt";
            if (!File.Exists(hdrfile))
                return -9999;
            int index = latlon2index(lat, lon, xll, yll, nrows, ncols, cellsize);
            //Debug.WriteLine(index);
            double elev = -9999;

            using (BinaryReader reader = new BinaryReader(File.Open(fltfile, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                int length = (int)reader.BaseStream.Length;
                reader.BaseStream.Seek(index * sizeof(Single), SeekOrigin.Begin);
                elev = reader.ReadSingle();
            }
            return elev;
        }
        private static int latlon2index(double lat, double lon, double xll, double yll, int nrows, int ncols, double cellsize)
        {
            int x = (int)Math.Round((lon - xll) / cellsize), y = (int)Math.Round((lat - yll) / cellsize),
                row = nrows - y - 1, col = x;
            if (row < 0)
                row = 0;
            if (row > nrows - 1)
                row = nrows - 1;
            if (col < 0)
                col = 0;
            if (col > ncols - 1)
                col = ncols - 1;
            return ncols * row + col;
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
