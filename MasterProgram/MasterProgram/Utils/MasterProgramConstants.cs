using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DMP.MasterProgram.Utils
{
    public static class MasterProgramConstants
    {

        //Geometry Columns
        public const string CFTID = "_CFTID";
        public const string GEOMETRY_BIN = "GEOMETRY_BIN";
        public const string AE_GEOMETRY = "AE_GEOMETRY";
        public const string GEO_TYPE = "_GEO_TYPE";
        public const string LON = "LON";
        public const string LAT = "LAT";
        public const string _X_COORD = "_X_COORD";
        public const string _Y_COORD = "_Y_COORD";

        //Parameters Name from XML
        public const string IS_IMPACTORS_ON_DEMAND = "IsImpactorsOnDemand";
        public const string PROCESSING_TILE_SIZE = "ProcessingTileSize";
        public const string PREFER_GPU = "PreferGPU";
        public const string OUTPUT_BATCH_SIZE = "OutputBatchSize";
        public const string USE_CUSTOM_GEOMETRY = "UseCustomGeometry";
        public const string NUMBER_OF_THREADS = "NumberOfThreads";

        public const string SUBJECT_SET = "SubjectSet";
        public const string TASK_CALCULATED = "TaskCalculated";

        public const int SRID = 4269;  // spatial reference ID


        //Algorithm Intersct Methods
        public const string GEOMETRY_INTERSECT_GEOMETRY = "GeometryIntersectGeometry";
        public const string CENTROID_INTERSECT_GEOMETRY = "CentroidIntersectGeometry";
        public const string BUFFER = "buffer";
        public const string BUFFFER_UNIT = "bufferUnit";

        public const string INTERSECT_METHOD = "intersectMethod";

        //Elevation
       // public const string SHAPE_FILE_PATH = "ShapeFilePath";
       // public const string DATA_FILE_PATH = "DataFilePath";
        public const string ELEVATION_DATA_PATH = "ElevationDataPath";       
        public const string SLOPE_UPPER_LIMIT = "slopeUpperLimit";
        public const string ELEVATION_SHAPE_FILE_NAME = "ElevationShapeFile";
        public const string ELEVATION_RAWDATA_FILENAME = "ElevationRawData";

    }
}
