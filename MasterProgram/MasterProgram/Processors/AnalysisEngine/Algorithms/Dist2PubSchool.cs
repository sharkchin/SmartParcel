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
    class Dist2PubSchool : IGeometryAlgorithm
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
            facts.Add("Dist2ElementarySchool", -9999);
            facts.Add("Dist2MiddleSchool", -9999);
            facts.Add("Dist2HighSchool", -9999);
            double diste = double.MaxValue, distm = double.MaxValue, disth = double.MaxValue;
            o = record[MasterProgramConstants.GEOMETRY_BIN];
            if (o is DBNull)
                return facts;
            SqlGeometry parcel = (SqlGeometry)o, cent = new SqlGeometry();
            
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

            double METER_PER_DEGREE = GeoUtils.MetersPerDegree(lon, lat, cent.STSrid.Value);
            bool founde = false, foundm = false, foundh = false;
            if ((int)facts["ElementarySchoolAccuracy"] == 2)
            {                
                for (int i = 0; i < impactors.Count; i++)
                {
                    AbstractRecord impactor = impactors.ElementAt(i);
                    SqlGeometry sg=impactor[MasterProgramConstants.GEOMETRY_BIN] as SqlGeometry;
                    string schoolid=facts["ElementarySchoolId"] as string;
                    if (schoolid==(string)impactor["NCES_SCHID"])
                    {
                        founde = true;
                        facts["ElementarySchoolName"] = impactor["NAME"];
                        facts["SchoolDistrictName"] = impactor["DISTRICT_NAME"];
                        facts["Dist2ElementarySchool"] = sg.STDistance(cent).Value*METER_PER_DEGREE;                        
                    }
                }                
            }
            if ((int)facts["MiddleSchoolAccuracy"] == 2)
            {
                for (int i = 0; i < impactors.Count; i++)
                {
                    AbstractRecord impactor = impactors.ElementAt(i);
                    SqlGeometry sg = impactor[MasterProgramConstants.GEOMETRY_BIN] as SqlGeometry;
                    string schoolid = facts["MiddleSchoolId"] as string;
                    if (schoolid == (string)impactor["NCES_SCHID"])
                    {
                        foundm = true;
                        facts["MiddleSchoolName"] = impactor["NAME"];
                        facts["SchoolDistrictName"] = impactor["DISTRICT_NAME"];
                        facts["Dist2MiddleSchool"] = sg.STDistance(cent).Value * METER_PER_DEGREE;
                    }
                }
            }
            if ((int)facts["HighSchoolAccuracy"] == 2)
            {
                for (int i = 0; i < impactors.Count; i++)
                {
                    AbstractRecord impactor = impactors.ElementAt(i);
                    SqlGeometry sg = impactor[MasterProgramConstants.GEOMETRY_BIN] as SqlGeometry;
                    string schoolid = facts["HighSchoolId"] as string;
                    if (schoolid == (string)impactor["NCES_SCHID"])
                    {
                        foundh = true;
                        facts["HighSchoolName"] = impactor["NAME"];
                        facts["SchoolDistrictName"] = impactor["DISTRICT_NAME"];
                        facts["Dist2HighSchool"] = sg.STDistance(cent).Value * METER_PER_DEGREE;
                    }
                }
            }
            if ((int)facts["ElementarySchoolAccuracy"] == 1 || (int)facts["MiddleSchoolAccuracy"] == 1 || (int)facts["HighSchoolAccuracy"] == 1)
            {
                for (int i = 0; i < impactors.Count; i++)
                {
                    AbstractRecord impactor = impactors.ElementAt(i);
                    SqlGeometry sg = impactor[MasterProgramConstants.GEOMETRY_BIN] as SqlGeometry;
                    string schooldistrictid = facts["SchoolDistrictId"] as string;
                    if (schooldistrictid!=(string)impactor["NCES_DISID"])
                    {
                        impactors.RemoveAt(i);
                    }
                }
            }

            int se = 0, sm = 0, sh = 0;
            try
            {
                if (impactors.Count > 0)
                {
                    for (int i = 0; i < impactors.Count; i++)
                    {
                        AbstractRecord impactor = impactors.ElementAt(i);                        
                        Vector impvec = new Vector((double)impactor["_X_COORD"], (double)impactor["_Y_COORD"]);
                        string schtype = impactor["LEVEL_CODE"] as string;
                        double disti = (impvec - pcvec).Length;
                        if (!founde && schtype!=null && schtype.ToLower().Contains("p") && diste > disti)
                        {
                            diste = disti;
                            se = i;
                        }
                        if (!foundm && schtype != null && schtype.ToLower().Contains("m") && distm > disti)
                        {
                            distm = disti;
                            sm = i;
                        }
                        if (!foundh && schtype != null && schtype.ToLower().Contains("h") && disth > disti)
                        {
                            disth = disti;
                            sh = i;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            if (!founde && diste != double.MaxValue)
            {
                AbstractRecord closeimp = impactors.ElementAt(se);
                if ((int)facts["ElementarySchoolAccuracy"] == -1)
                    facts["ElementarySchoolAccuracy"] = 0;
                if (!founde)
                {
                    facts["ElementarySchoolName"] = closeimp["NAME"];
                    facts["ElementarySchoolId"] = closeimp["NCES_SCHID"];
                    facts["SchoolDistrictName"] = closeimp["DISTRICT_NAME"];
                    facts["SchoolDistrictId"] = closeimp["NCES_DISID"];
                    facts["Dist2ElementarySchool"] = cent.STDistance((SqlGeometry)closeimp[MasterProgramConstants.GEOMETRY_BIN]).Value * METER_PER_DEGREE;
                }
            }
            if (!foundm && distm != double.MaxValue)
            {
                AbstractRecord closeimp = impactors.ElementAt(sm);
                if ((int)facts["MiddleSchoolAccuracy"] == -1)
                    facts["MiddleSchoolAccuracy"] = 0;
                if (!foundm)
                {
                    facts["MiddleSchoolName"] = closeimp["NAME"];
                    facts["MiddleSchoolId"] = closeimp["NCES_SCHID"];
                    facts["SchoolDistrictName"] = closeimp["DISTRICT_NAME"];
                    facts["SchoolDistrictId"] = closeimp["NCES_DISID"];
                    facts["Dist2MiddleSchool"] = cent.STDistance((SqlGeometry)closeimp[MasterProgramConstants.GEOMETRY_BIN]).Value * METER_PER_DEGREE;
                }
            }
            if (!foundh && disth != double.MaxValue)
            {
                AbstractRecord closeimp = impactors.ElementAt(sh);
                if ((int)facts["HighSchoolAccuracy"] == -1)
                    facts["HighSchoolAccuracy"] = 0;
                if (!foundh)
                {
                    facts["HighSchoolName"] = closeimp["NAME"];
                    facts["HighSchoolId"] = closeimp["NCES_SCHID"];
                    facts["SchoolDistrictName"] = closeimp["DISTRICT_NAME"];
                    facts["SchoolDistrictId"] = closeimp["NCES_DISID"];
                    facts["Dist2HighSchool"] = cent.STDistance((SqlGeometry)closeimp[MasterProgramConstants.GEOMETRY_BIN]).Value * METER_PER_DEGREE;
                }
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
