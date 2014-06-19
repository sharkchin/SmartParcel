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
    class GetNeighborhoodId : IGeometryAlgorithm
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
            facts.Add("MacroNeighborhoodName", "NA");
            facts.Add("MacroNeighborhoodId", -9999.0);
            facts.Add("MicroNeighborhoodName", "NA");
            facts.Add("MicroNeighborhoodId", -9999.0);
            facts.Add("NeighborhoodName", "NA");
            facts.Add("NeighborhoodId", -9999.0);
            o = record[MasterProgramConstants.GEOMETRY_BIN];
            if (o is DBNull)
                return null;
            //otherwise
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
            bool macro = false, micro = false, neigh = false;
            try
            {
                if (impactors.Count > 0)
                {
                    for (int i = 0; i < impactors.Count; i++)
                    {
                        if (macro && micro && neigh) break;
                        AbstractRecord impactor = impactors.ElementAt(i);
                        SqlGeometry imp = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];
                        string type=impactor["NBR_TYPE"] as string;
                        if (cent.STIntersects(imp))
                        {
                            switch (type)
                            { 
                                case "M":
                                    macro = true;
                                    facts["MacroNeighborhoodName"] = impactor["NEIGHBORHD"];
                                    facts["MacroNeighborhoodId"] = impactor["NID"];
                                    break;
                                case "S":
                                    micro = true;
                                    facts["MicroNeighborhoodName"] = impactor["NEIGHBORHD"];
                                    facts["MicroNeighborhoodId"] = impactor["NID"];
                                    break;
                                case "N":
                                    neigh = true;
                                    facts["NeighborhoodName"] = impactor["NEIGHBORHD"];
                                    facts["NeighborhoodId"] = impactor["NID"];
                                    break;                                
                            }                            
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
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
