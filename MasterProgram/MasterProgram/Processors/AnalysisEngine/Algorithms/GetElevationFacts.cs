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
using System.Configuration;

namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class GetElevationFacts : IGeometryAlgorithm
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
            facts.Add("CentElev", -9999);
            facts.Add("MaxElev", -9999);
            facts.Add("MinElev", -9999);
            o = record[MasterProgramConstants.GEOMETRY_BIN];
            if (o is DBNull)
                return facts;
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
            double centElev=GetElev(lat, lon);
            facts["CentElev"] = centElev;
            //SqlGeometry box = parcel.STEnvelope();
            //double ulon = box.STPointN(1).STX.Value, ulat = box.STPointN(1).STY.Value, llon=0, llat=0;
            //if (box.STPointN(2).STX.Value == ulon)
            //{
            //    if (box.STPointN(2).STY.Value > ulat)
            //    {
            //        llat = ulat;
            //        ulat = box.STPointN(2).STY.Value;
            //    }
            //    else
            //    {
            //        llat = box.STPointN(2).STY.Value;
            //    }
            //}
            //else
            //{
            //    if (box.STPointN(2).STX.Value > ulon)
            //    {
            //        llon = ulon;
            //        ulon = box.STPointN(2).STX.Value;
            //    }
            //    else
            //    {
            //        llon = box.STPointN(2).STX.Value;
            //    }
            //}
            //double maxDist = 0.0005;
            //List<double> maxmin = GetMaxMinElev(parcel, maxDist);
                    //List<SqlGeometry> pts = GetSamplePoints(parcel, 0.0005);
                    //double maxElev = double.MinValue, minElev = double.MaxValue;
                    //foreach (SqlGeometry pt in pts)
                    //{
                    //    double elev = GetElev(pt.STY.Value, pt.STX.Value);
                    //    if (elev >= maxElev) maxElev = elev;
                    //    if (elev <= minElev) minElev = elev;
                    //}
            //facts["MaxElev"]= Math.Max(maxmin[0],centElev);
            //facts["MinElev"]= Math.Min(maxmin[1],centElev);
            return facts;
        }
        private List<double> GetMaxMinElev(SqlGeometry geo, double maxDist)
        {
            List<double> maxmin=new List<double>{-1,-1};
            if (geo.IsNull || !geo.STIsValid())
                return maxmin;
            double max0=double.MinValue, max1=double.MinValue, min0=double.MaxValue, min1=double.MaxValue;
            SqlGeometry a0 = SqlGeometry.Null, a1 = SqlGeometry.Null, i0 = SqlGeometry.Null, i1 = SqlGeometry.Null;
            int n = geo.STNumPoints().Value;
            //if (n > 1000)
            //{
            //    //Console.WriteLine("a lot of points");
            //    geo = GeoUtils.Reduce2NumPoints(geo, 1000);
            //}
            n = geo.STNumPoints().Value;
            int step=Math.Max(1,(int)(n/100));
            for (int i = 0; i < n; i=i+step)
            {
                double elev = GetElev(geo.STPointN(i + 1).STY.Value, geo.STPointN(i + 1).STX.Value);
                if (elev > max0)
                {
                    max1 = max0;
                    a1 = a0;
                    max0 = elev;
                    a0 = geo.STPointN(i + 1);
                }
                else if (elev > max1)
                {
                    max1 = elev;
                    a1 = geo.STPointN(i + 1);
                }
                if (elev < min0)
                {
                    min1 = min0;
                    i1 = i0;
                    min0 = elev;
                    i0 = geo.STPointN(i + 1);
                }
                else if (elev < min1)
                {
                    min1 = elev;
                    i1 = geo.STPointN(i + 1);
                }
            }
            Vector a0v = Point2Vector(a0), a1v = Point2Vector(a1), i0v = Point2Vector(i0), i1v = Point2Vector(i1);
            maxmin[0]=GetMaxElev(a0v, a1v, max0, max1, maxDist);
            maxmin[1]=GetMinElev(i0v, i1v, min0, min1, maxDist);
            return maxmin;
        }
        private double GetMaxElev(Vector a0, Vector a1, double max0, double max1, double maxDist)
        {
            if ((a0 - a1).Length < maxDist)
                return max0;
            Vector cent = (a0 + a1) / 2;
            double elev = GetElev(cent.Y, cent.X);
            if (elev > max0)
            {
                return GetMaxElev(cent, a0, elev, max0, maxDist);
            }
            else
            {
                return GetMaxElev(a0, cent, max0, elev, maxDist);
            }            
        }
        private double GetMinElev(Vector i0, Vector i1, double min0, double min1, double maxDist)
        {
            if ((i0 - i1).Length < maxDist)
                return min0;
            Vector cent = (i0 + i1) / 2;
            double elev = GetElev(cent.Y, cent.X);
            if (elev < min0)
            {
                return GetMaxElev(cent, i0, elev, min0, maxDist);
            }
            else
            {
                return GetMaxElev(i0, cent, min0, elev, maxDist);
            }
        }
        private List<SqlGeometry> GetSamplePoints(SqlGeometry geo, double maxDist)
        {
            int SRID=geo.STSrid.Value;
            List<SqlGeometry> ret = new List<SqlGeometry>();
            if (geo.IsNull || !geo.STIsValid())
                return ret;
            SqlGeometry box = geo.STEnvelope();
            Vector pt1 = Point2Vector(box.STPointN(1)), pt2 = Point2Vector(box.STPointN(2)),
                pt3 = Point2Vector(box.STPointN(3)), pt4 = Point2Vector(box.STPointN(4)),
                s1=pt1-pt2,s2=pt2-pt3,s3=pt3-pt4,s4=pt4-pt1;
            SqlGeometry divLn1=GeoUtils.Points2LineString(new List<Vector> {pt2+s1/2,pt4+s3/2},SRID),
                divLn2=GeoUtils.Points2LineString(new List<Vector> {pt3+s2/2,pt1+s4/2},SRID);
            double side1 = s1.Length, side2 = s2.Length;
            if (side1 < maxDist && side2 < maxDist)
            {
                ret.Add(geo.STCentroid());
                return ret;
            }
            if(side1<maxDist)
            {
                SqlGeometry geo2=geo.STDifference(divLn2.STBuffer(0.00001));
                for(int i=0;i<geo2.STNumGeometries();i++)
                {
                    ret.AddRange(GetSamplePoints(geo2.STGeometryN(i+1),maxDist));
                }
                return ret;
            }
            if(side2<maxDist)
            {
                SqlGeometry geo2=geo.STDifference(divLn1.STBuffer(0.00001));
                for(int i=0;i<geo2.STNumGeometries();i++)
                {
                    ret.AddRange(GetSamplePoints(geo2.STGeometryN(i+1),maxDist));
                }
                return ret;
            }
            SqlGeometry geo3=geo.STDifference(divLn1.STBuffer(0.00001));
            geo3=geo3.STDifference(divLn2.STBuffer(0.00001));
            for(int i=0;i<geo3.STNumGeometries();i++)
            {
                ret.AddRange(GetSamplePoints(geo3.STGeometryN(i+1),maxDist));
            }
            return ret;
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
            string folderdir = ConfigurationManager.AppSettings["ElevDataPath"] + foldername;

            //Debug.WriteLine(foldername);
            string hdrfile = folderdir + "\\float" + foldername + "_13.hdr";
            if (!File.Exists(hdrfile))
                return -9999;
            int ncols = -9999, nrows = -9999;
            double xll = -9999, yll = -9999, cellsize = -9999;
            using (StreamReader reader = new StreamReader(File.Open(hdrfile, FileMode.Open, FileAccess.Read, FileShare.Read)))
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
