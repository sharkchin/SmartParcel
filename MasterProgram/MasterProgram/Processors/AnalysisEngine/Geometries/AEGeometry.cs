/**
 * 
 * 
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoGIS.NetTopologySuite.IO;
using MonoGIS.NetTopologySuite.Geometries;

namespace DMP.MasterProgram.Processors.AnalysisEngine.Geometries
{
    public abstract class AEGeometry
    {
        public AEEnvelope envelope;

        /// <summary>
        /// create Analysis Engine Envelope from GEOS Envelope
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public AEEnvelope createAEEnvelope(Envelope e)
        {
            AEEnvelope envelope = new AEEnvelope();
            envelope.XMax = e.MaxX;
            envelope.XMin = e.MinX;
            envelope.YMax = e.MaxY;
            envelope.YMin = e.MinY;

            return envelope;
        }

        /// <summary>
        /// creates Analysis Geometry from GEOS  Geometry
        /// </summary>
        /// <param name="geometry">GEOS Geometry</param>
        /// <param name="geometryType">Geometry Type</param>
        /// <returns></returns>
        public static List<AEGeometry> createAEGeometry(Geometry geometry, int geometryType)
        {
            List<AEGeometry> geomList = new List<AEGeometry>();
            try
            {
                if (geometryType == 1)
                {
                    Coordinate coordinate = geometry.Coordinate;
                    AEGeometry geom = new AEPoint(coordinate.X, coordinate.Y);

                    Envelope envelope = (Envelope)geometry.EnvelopeInternal;
                    geom.envelope = geom.createAEEnvelope(envelope);

                    coordinate = null;
                    envelope = null;
                    geomList.Add(geom);
                }
                else if (geometryType == 3)
                {
                    if (geometry is Polygon)
                    {

                        Polygon polygon = (Polygon)geometry;
                        if (polygon.InteriorRings.Length > 0)
                        {
                            MultiPolygon decimatedGeo = Dmp.Neptune.Utils.ShapeFile.Decimator.VattiSegmenter.Decimate(geometry, geometry.NumPoints / 4,
                                     Dmp.Neptune.Utils.ShapeFile.Decimator.VattiSegmenter.DEFAULT_FIX_INVALID_GEOS,
                                            Dmp.Neptune.Utils.ShapeFile.Decimator.VattiSegmenter.DEFAULT_TOLERANCE);

                            int numberOfGeometries = decimatedGeo.NumGeometries;
                            for (int k = 0; k < numberOfGeometries; k++)
                            {
                                Geometry childGeom = decimatedGeo.GetGeometryN(k);

                                geomList.Add(createAEGeometry(childGeom));
                            }
                        }
                        else
                        {

                            geomList.Add(createAEGeometry(geometry));
                        }
                    }
                    //There is problem with conversion from sqlite GeometryBin to MONOGisGeometry
                    //Polygon get converted to MultiPolygon with one Polygon inside
                    else if (geometry is MultiPolygon) 
                    {
                        MultiPolygon multiPolygon = (MultiPolygon)geometry;
                        for (int i = 0; i < multiPolygon.NumGeometries; i++)
                        {
                            Polygon polygon = (Polygon)multiPolygon.GetGeometryN(i);
                            if (polygon.InteriorRings.Length > 0)
                            {
                                MultiPolygon decimatedGeo = Dmp.Neptune.Utils.ShapeFile.Decimator.VattiSegmenter.Decimate(geometry, geometry.NumPoints / 4,
                                         Dmp.Neptune.Utils.ShapeFile.Decimator.VattiSegmenter.DEFAULT_FIX_INVALID_GEOS,
                                                Dmp.Neptune.Utils.ShapeFile.Decimator.VattiSegmenter.DEFAULT_TOLERANCE);

                                int numberOfGeometries = decimatedGeo.NumGeometries;
                                for (int k = 0; k < numberOfGeometries; k++)
                                {
                                    Geometry childGeom = decimatedGeo.GetGeometryN(k);

                                    geomList.Add(createAEGeometry(childGeom));
                                }
                            }
                            else
                            {

                                geomList.Add(createAEGeometry(geometry));
                            }
                        }

                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }

            return geomList;

        }

        private static AEGeometry createAEGeometry(Geometry geometry)
        {
             Coordinate[] coordArr = geometry.Coordinates;
             List<double> xCoord = new List<double>();
             List<double> yCoord = new List<double>();

             for (int i = 0; i < coordArr.Length; i++)
             {
                 Coordinate coord = coordArr.ElementAt(i);
                 xCoord.Add(coord.X);
                 yCoord.Add(coord.Y);
             }

             AEGeometry geom = new AEPolygon(xCoord.ToArray(), yCoord.ToArray());

             Envelope envelope = (Envelope)geometry.EnvelopeInternal;
             geom.envelope = geom.createAEEnvelope(envelope);
             coordArr = null;
             envelope = null;
             xCoord = null;
             yCoord = null;

             return geom;                     

        }
    }
}
