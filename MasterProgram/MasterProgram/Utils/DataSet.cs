using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using log4net.Config;
using DMP.MasterProgram.Utils.Chunking;
using DMP.MasterProgram.Utils.Caching;
using Microsoft.SqlServer.Types;
using Dmp.Neptune.Collections;
using MonoGIS.NetTopologySuite.Geometries;
using MonoGIS.NetTopologySuite.IO;
using Dmp.Neptune.DatabaseManager;
using DMP.MasterProgram.ProcessorMetadata;


namespace DMP.MasterProgram.Utils
{
    public class DataSet
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private RecordCache cache;

        private int maxZoomLevel;
        private int minZoomLevel;

        private double subjectBuffer;
        private string subjectBufferUnit;

        /// <summary>
        /// Max Zoom Level
        /// </summary>
        public int MaxZoomLevel 
        {
            get
            {
                return this.maxZoomLevel;
            }
            set
            {
                this.maxZoomLevel = value;
            }
        }

        /// <summary>
        /// Min Zoom level
        /// </summary>
        public int MinZoomLevel 
        {
            get
            {
                return this.minZoomLevel;
            }

            set
            {
                this.minZoomLevel = value;
            }
        }

        public double SubjectBuffer 
        {
            get
            {
                return this.subjectBuffer;
            }
            set
            {
                this.subjectBuffer = value;
            }
        }

        public string SubjectBufferUnit 
        {
            get
            {
                return this.subjectBufferUnit;
            }
            set
            {
                this.subjectBufferUnit = value;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="database"></param>
        /// <param name="tableName"></param>
        /// <param name="storageType"></param>
        /// <param name="fields"></param>
        /// <param name="attributeCriteria"></param>
        //public DataSet(string database, string tableName, string storageType, List<string> fields, string attributeCriteria)
        public DataSet(MasterProgramMetadata.InputDataSet inputDataSet)
        {
            //default values,if these values remain "-1" in future,throw exception.
            maxZoomLevel = -1;
            minZoomLevel = -1;

            cache = new RecordCache(inputDataSet);
        }

        /// <summary>
        /// return all the records intersected with given geometry 
        /// </summary>
        /// <param name="geom"></param>
        /// <param name="maxZoomLevel"></param>
        /// <param name="minZoomLevel"></param>
        /// <returns></returns>
        public List<AbstractRecord> getByGeom(SqlGeometry geom, int maxZoomLevel, int minZoomLevel)
        {
            List<AbstractRecord> ouputList = null;
            //if zoomLevel is -1 ,use the zoom Level set in the dataSet
            if (maxZoomLevel == -1)
                maxZoomLevel = this.maxZoomLevel;

            if (minZoomLevel == -1)
                minZoomLevel = this.minZoomLevel;

            double distanceInMeter = 0;

            if (subjectBuffer > 0)
            {
                distanceInMeter = ToMeters(subjectBuffer, subjectBufferUnit);
            }

            bool increaseBuffer = false;


            try
            {
                while (true)
                {
                    if (distanceInMeter > 0)
                    {
                        
                        /*SqlGeography geog = MSSpatialDBManager.GetGeographyFromGeometry(geom);

                        // to do: temporary fix until we find a better buffering routine/library 29 sept 11 ~MR
                        // weed to 100 vertices after 2 miles
                        if (distanceInMeter < 3218.688)
                        {
                            geog = geog.STBuffer(distanceInMeter);
                        }
                        else
                        {
                            for (int a = 32; geog.STNumPoints() > 110; a *= 2)
                            {
                                geog = geog.Reduce(a);
                            }
                            geog = geog.STBuffer(distanceInMeter);
                        }
                        geom = MSSpatialDBManager.GetGeometryFromGeography(geog);*/
                        geom = geom.STCentroid().STBuffer(distanceInMeter * .00001);
                    }

                    CFTIdGenerator cftIdGenerator = new CFTIdGenerator(geom, maxZoomLevel, minZoomLevel);
                    List<string> cftIdList = null;

                    cftIdList = cftIdGenerator.GetCFTIdList();
                    ouputList = getByCFTIds(cftIdList);


                    if (ouputList.Count > 0 || !increaseBuffer)
                    {
                        return ouputList;
                    }

                    distanceInMeter = distanceInMeter + 1000;
                    cftIdGenerator = null;
                    cftIdList = null;
                    //return getByCFTIds(cftIdList);
                }
            }
            catch(Exception e)
            {
                throw e;
            }

        }

        /// <summary>
        /// return all the records with the given CFTId's
        /// </summary>
        /// <param name="cftIdList"></param>
        /// <returns></returns>
        public List<AbstractRecord> getByCFTIds(List<string> cftIdList)
        {
            List<AbstractRecord> recordList = new List<AbstractRecord>();
            try
            {
                for(int j = 0;j<cftIdList.Count;j++)
                {
                    string cftId = cftIdList[j]; 
                    List<AbstractRecord> records = null;
                    records = cache.FindByCFTID(cftId);
                    if (records != null && records.Count > 0)
                    {
                        recordList.AddRange(records);
                    }
                }
            }
            catch(Exception e)
            {
                throw e;
            }

            return recordList;
        }

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

        
    }
}
