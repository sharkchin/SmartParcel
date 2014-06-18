using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;
using Dmp.Neptune.Query;
using Dmp.Neptune.Utils.ShapeFile;
using MonoGIS.NetTopologySuite.Geometries;
using MonoGIS.NetTopologySuite.IO;

namespace DMP.MasterProgram.Utils.Elevation
{
    public class ShapeFileQueryEngine
    {

        #region Public Functions

        public static List<string> GetRecords(SqlGeometry geometry, string fileName)
        {
            List<string> filesPath = new List<string>();

            DmpShpReader psSHP = null;
            DmpDbfReader hDbf = null;

            if (geometry == null)
                throw new ApplicationException("ShapeFileQueryEngine.GetRecords : Geometry should not be null");

            WKBReader wkbReader = new WKBReader();
            Geometry monoGeometry = wkbReader.Read(geometry.STAsBinary().Value);

            try
            {
                psSHP = new DmpShpReader(fileName);
                hDbf = new DmpDbfReader(fileName);

                ShapeTreeResultHandler resultHandler;
                resultHandler = new ShapeTreeResultHandler(true, IntersectionType.GEOMETRY, false, false, 4);

                MapTree.DmpSearchDiskTree(psSHP, fileName, monoGeometry, resultHandler);

                List<MapTree.DqxData> results = resultHandler.Result;

                for (int i = 0; i < results.Count; i++)
                {
                    string filePath = (string)hDbf.GetValue(results[i].id, 0);
                    filesPath.Add(filePath);

                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (hDbf != null) hDbf.Close();
                psSHP.Close();
            }

            return filesPath;
        }

        #endregion
    }
}
