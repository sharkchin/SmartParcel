using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DMP.MasterProgram.Processors.AnalysisEngine.Algorithms;
using DMP.MasterProgram.Processors.AnalysisEngine.Geometries;
using Dmp.Neptune.Collections;
using DMP.MasterProgram.Utils;
using log4net;
using log4net.Config;
using MonoGIS.NetTopologySuite.Geometries;
using MonoGIS.NetTopologySuite.IO;
using Microsoft.SqlServer.Types;

namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class GetGolfParcels : IGeometryAlgorithm
    {
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
            List<AbstractRecord> intersectList = new List<AbstractRecord>();
            
            string intersectMethod = null;
            if (parameters != null)
            {
                parameters.TryGetValue(MasterProgramConstants.INTERSECT_METHOD, out intersectMethod);
            }
            if (intersectMethod == null)
            {
                intersectMethod = MasterProgramConstants.GEOMETRY_INTERSECT_GEOMETRY;//default value
            }


            if ((int)record[MasterProgramConstants.GEO_TYPE] == 1)
            {
                //GeometryIntersectsGeometry and CentroidIntersectsGeometry are same

                AEPoint point = (AEPoint)((List<AEGeometry>)record[MasterProgramConstants.AE_GEOMETRY]).ElementAt(0);
                if (point == null)
                {
                    logger.Error("Error while processing record in IntersectAlgorithm :AE Geometry for subject record is not defined");
                }
                for (int i = 0; i < impactors.Count; i++)
                {
                    AbstractRecord impactor = impactors.ElementAt(i);
                    if ((int)impactor[MasterProgramConstants.GEO_TYPE] == 2 || (int)impactor[MasterProgramConstants.GEO_TYPE] == 3 || (int)impactor[MasterProgramConstants.GEO_TYPE] == 6)
                    {
                        List<AEGeometry> geometryList = (List<AEGeometry>)(impactor[MasterProgramConstants.AE_GEOMETRY]);
                        for (int k = 0; k < geometryList.Count; k++)
                        {
                            AEPolygon polygon = (AEPolygon)geometryList.ElementAt(k);
                            bool result = IsPointInPolygon(point, polygon);
                            if (result)
                            {
                                DataRecord parcel = new DataRecord(new string[] { });
                                parcel["DMPID"] = impactor["DMPID"];
                                parcel["DMPID0"] = impactor["_DMP_ID"];
                                parcel["LOCATIONID"] = impactor["LOCATION_ID"];
                                parcel["APN"] = impactor["APN"];
                                parcel["CFTID"] = impactor["_CFTID"];
                                parcel["Lon"] = impactor["_Y_COORD"];
                                parcel["Lat"] = impactor["_X_COORD"];
                                parcel["GEOMETRY"] = impactor["GEOMETRY_BIN"];
                                parcel["CourseID"] = record["CourseID"];
                                parcel["LandUse"] = impactor["LANDUSE_CATEGORY"];
                                intersectList.Add(parcel);
                                break;
                            }
                        }
                    }
                    else
                    {
                        logger.Error("Error in IntersectAlgorithm: Geometry Type of Impactor is not correct");
                        throw new ApplicationException("Error in IntersectAlgorithm: Geometry Type of Impactor is not correct");

                    }
                }
            }                        
            else
            {
                logger.Error("Error in IntersectAlgorithm: Geometry Type of subject is not correct");
               // throw new ApplicationException("Error in IntersectAlgorithm: Geometry Type of subject is not correct");
            }

            return intersectList;
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
