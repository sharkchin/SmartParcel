using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using log4net.Config;
using Dmp.Neptune.Utils.ShapeFile;
using MonoGIS.NetTopologySuite.Geometries;
using MonoGIS.NetTopologySuite.IO;
using Dmp.Neptune.Index;
using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;


namespace DMP.MasterProgram.Utils.Chunking
{
    class CFTIdGenerator
    {
        public const int DATABOUNDS_MAX_ZOOM = 21;
        public const int DATABOUNDS_MIN_ZOOM = 1;
        private static int _stepDown = -1;
        private int _leaveLevel = -1;
        private int _databoundsMinZoom = DATABOUNDS_MIN_ZOOM;
        private int _databoundsMaxZoom = DATABOUNDS_MAX_ZOOM;



        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private List<string> cftIdList = new List<string>();
        private string CFTId;
        object geom;

        private List<string> currentNodes = new List<string>();
        private List<string> excludeNodes = new List<string>();
        private List<string> visitedNodes = new List<string>();
        private List<string> visitedParentNodes = new List<string>();

      
        /// <summary>
        /// Constructor
        /// </summary>
        public CFTIdGenerator(SqlGeometry geometry, int maxZoomLevel, int minZoomLevel)
        {
            if (maxZoomLevel == -1 )
                throw new ApplicationException("MaxZoomLevel is not set");

            if (minZoomLevel == -1 )
                throw new ApplicationException("MinZoomLevel is not set");

            // hasn't been initialized, so initialize it
            if (_stepDown == -1)
            {
               // string sStepUp = DmpConfig.GetSetting("CFTID_STEP_DOWN");
               // if (!int.TryParse(sStepUp, out _stepDown)) // default to 2
                    _stepDown = 2;

                if (_stepDown < 0)
                {
                    if (logger.IsWarnEnabled)
                        logger.Warn("Warning: CFTID_STEP_DOWN setting not applied. Value must be a non-negative integer. Defaulting to 2.");
                    _stepDown = 2;
                }
            }

            _databoundsMinZoom = minZoomLevel;
            if (minZoomLevel < DATABOUNDS_MIN_ZOOM)
                throw new SystemException("Min data bounds cannot be less than " + DATABOUNDS_MIN_ZOOM);

            _databoundsMaxZoom = maxZoomLevel;
            if (maxZoomLevel > DATABOUNDS_MAX_ZOOM)
                throw new SystemException("Min data bounds cannot be more than " + DATABOUNDS_MAX_ZOOM);                 




            this.geom = geometry;
            CftIndex index = new CftIndex();
            WKTReader wktReader = new WKTReader();
            this.CFTId = index.GetIndex(wktReader.Read(geometry.STAsText().ToSqlString().Value));
            wktReader = null;
        }
    
        /// <summary>
        /// return the list of CFTId's covering the given geometry
        /// </summary>
        /// <returns></returns>
        public List<string> GetCFTIdList()
        {
            GenerateCFTIds(this.CFTId, this.geom);
            GenerateCFTIdList();
            //Clean
            geom = null; ;

            currentNodes.Clear();
            excludeNodes.Clear();
            visitedNodes.Clear();
            visitedParentNodes.Clear();

            return this.cftIdList;
           
        }

        /// <summary>
        /// 
        /// </summary>
        private void GenerateCFTIds(string cftId,object geometry)
        {
           
            List<string> siblings = null;

            // gotta respect the data bounds max level
            if (cftId.Length > _databoundsMaxZoom)
                cftId = cftId.Substring(0, _databoundsMaxZoom);

            if (_leaveLevel == -1)
            {
                //_leaveLevel = node.Length;
                _leaveLevel = Math.Min(cftId.Length + _stepDown, _databoundsMaxZoom);
            }


            ProcessNode(cftId, geometry);
            siblings = GenerateSiblings(cftId);

            //check all siblings that needs to process(i.e intersect with input geometry)
            foreach (string s in siblings)
            {
                ProcessNode(s,geometry);
            }
           
        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="geom1"></param>
        /// <returns></returns>
        private bool IsIntersect(string node, object geom1)
        {
            SqlGeometry geom2 = (SqlGeometry)GetSpatial(node);
            return (bool)geom2.STIntersects((SqlGeometry)geom1);               
            
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private bool ProcessNode(string node, object geometry)
        {
            if (visitedNodes.Contains(node))
            {
                return false;
            }
            if (node.Length < _leaveLevel)//this.minZoomLevel)
            {

                if (!IsIntersect(node, geometry))
                {
                    return false;
                }
                List<string> children = GetChildren(node);
                List<string> includedChildren = new List<string>();
                List<string> excludedChildren = new List<string>();
                foreach (string child in children)
                {
                    if (ProcessNode(child, geometry))
                    {
                        includedChildren.Add(child);
                    }
                    else
                    {
                        excludedChildren.Add(child);
                    }
                }
                if (includedChildren.Count >= excludedChildren.Count)
                {
                    foreach (string child in excludedChildren)
                    {
                        if (visitedNodes.Contains(child))
                        {
                            excludeNodes.Add(child);
                        }
                        else
                        {
                            visitedNodes.Add(child);
                        }
                    }
                    currentNodes.Add(node);
                    visitedNodes.Add(node);
                    RemoveChildren(node);
                    return true;
                }
                return false;
            }
            else
            {
                if (node.Length >_leaveLevel) //this.minZoomLevel)
                {
                    node = node.Substring(0, _leaveLevel);//this.minZoomLevel);
                }
                return AddNode(node,geometry);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="CFTId"></param>
        /// <returns></returns>
        private List<string> GenerateSiblings(string CFTId)
        {
            if (CFTId == null)
                throw new ApplicationException("CFT id cannot be null.");

            if (CFTId.Trim().Length == 0)
                return null;

            List<string> siblings = new List<string>();

            int tileX, tileY, zoom;
            GeoUtils.QuadKeyToTileXY(CFTId, out tileX, out tileY, out zoom);

            int maxTile = (int)Math.Pow(2, zoom) - 1;

            for (int i = -1; i < 2; i++)
            {
                for (int j = -1; j < 2; j++)
                {
                    int sTileX = tileX + i;
                    int sTileY = tileY + j;

                    // ignore myself and invalid tiles
                    if ((i == 0 && j == 0) || sTileX < 0 || sTileY < 0 || sTileX > maxTile || sTileY > maxTile)
                        continue;

                    siblings.Add(GeoUtils.TileXYToQuadKey(sTileX, sTileY, zoom));
                }
            }

            return siblings;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private object GetSpatial(string id)
        {
            if (id == null)
                return null;

            // TO-DO change to VETileStrategy
            Envelope box = TileSystem.GetTileExtents(id);

            Polygon p = new Polygon(new LinearRing(new Coordinate[] 
            {
                new Coordinate(box.MinX, box.MinY),
                new Coordinate(box.MaxX, box.MinY),
                new Coordinate(box.MaxX, box.MaxY),
                new Coordinate(box.MinX, box.MaxY),
                new Coordinate(box.MinX, box.MinY)
            }));

            box = p.EnvelopeInternal;

            double halfWidth = box.Width / 2;
            double halfHeight = box.Height / 2;

            double newMinX = box.MinX - halfWidth;
            double newMinY = box.MinY - halfHeight;
            double newMaxX = box.MaxX + halfWidth;
            double newMaxY = box.MaxY + halfHeight;

            // clamp - if i don't, these numbers will wrap around
            if (newMinX < -180) newMinX = -180;
            if (newMinY < -90) newMinY = -90;
            if (newMaxX > 180) newMaxX = 180;
            if (newMaxY > 90) newMaxY = 90;

            p = new Polygon(new LinearRing(new Coordinate[]
            {
                new Coordinate(newMinX, newMinY),
                new Coordinate(newMaxX, newMinY),
                new Coordinate(newMaxX, newMaxY),
                new Coordinate(newMinX, newMaxY),
                new Coordinate(newMinX, newMinY)
            }));

           
            return SqlGeometry.STGeomFromText(new SqlChars(p.ToText()), 4269);
           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private List<string> GetChildren(string id)
        {
            if (id == null)
                throw new SystemException("Tile id cannot be null.");

            List<string> children = new List<string>();
            children.Add(id + "0");
            children.Add(id + "1");
            children.Add(id + "2");
            children.Add(id + "3");

            return children;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        private void RemoveChildren(string node)
        {

            if (node.Length >= _leaveLevel)
            {
                return;
            }
            List<string> children = GetChildren(node);

            bool nodeContainsAllChildren = true;
            foreach (string child in children)
            {
                if (!currentNodes.Contains(child))
                    nodeContainsAllChildren = false;
                
            }

            if (nodeContainsAllChildren)
            {
                foreach (string child in children)
                {
                    currentNodes.Remove(child);
                }
            }
            else
            {
                currentNodes.Remove(node);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private bool AddNode(string node,object geometry)
        {
            if (visitedNodes.Contains(node)) return false;

           
            if (!IsIntersect(node,geometry))
            {
                return false;
            }
           
            currentNodes.Add(node);
            visitedNodes.Add(node);
            return true;
        }


        private void GenerateCFTIdList()
        {
            if (currentNodes == null || currentNodes.Count == 0)
            {
                return ;
            }
           
            List<string> _parentNodes = new List<string>();
            // add a like condition to each tile in the ring
            foreach (string node in currentNodes)
            {
                cftIdList.Add(node + "%");
               // if (!_isPointDataSet) // don't bother checking parents
                    AddParents(node, ref _parentNodes);               
            }

            // add parent Nodes
            if (_parentNodes.Count > 0)
            {
                
                foreach (string parent in _parentNodes)
                {
                    cftIdList.Add(parent);
                }
            }
        }

        /// <summary>
        /// recursively add all ancestors of this node
        /// </summary>
        /// <param name="node">index ID</param>
        private void AddParents(string node, ref List<string> parentNodes)
        {
            // don't go any higher in the tree
            if (node.Length <= _databoundsMinZoom) return;

            List<string> parents = GetParents(node);
            if (parents == null || parents.Count == 0) return;
            foreach (string p in parents)
            {
                if (visitedParentNodes.Contains(p))
                {
                    continue;
                }
                visitedParentNodes.Add(p);
                parentNodes.Add(p);
                AddParents(p, ref parentNodes);
            }
        }

         /// <summary>
        /// Get all the immediate parents
        /// </summary>
        /// <param name="id">spatial index</param>        
        /// <returns>if root node, return null, otherwise return list of indices</returns>
        public List<string> GetParents(string id)
        {
            if (id == null)
                throw new SystemException("Tile id cannot be null.");

            if (id.Length <= 1 )//|| id.Length <= this.maxZoomLevel)
                return null;

            // >= 2
            List<string> parents = new List<string>();
            parents.Add(id.Substring(0, id.Length - 1));
            
            List<string> idSiblings = GenerateSiblings(id);
            for (int i = 0; i < idSiblings.Count; i++)
            {
                string siblingParent = idSiblings[i].Substring(0, idSiblings[i].Length - 1);
                if (!parents.Contains(siblingParent))                
                    parents.Add(siblingParent);                
            }

            return parents;
        }
    }
}
