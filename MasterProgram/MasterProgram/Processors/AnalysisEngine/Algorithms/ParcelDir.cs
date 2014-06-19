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
    class ParcelDir : IGeometryAlgorithm
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
            //Console.WriteLine(record["dmpid"]);
            object o = record[MasterProgramConstants.GEOMETRY_BIN];
            if (o is DBNull)
                return null;
            SqlGeometry parcel = (SqlGeometry)o, cent = new SqlGeometry();            
            object p = record["_X_COORD"], q = record["_Y_COORD"];            
            Vector pcvec;
            if (p is DBNull || q is DBNull)
            {
                cent = parcel.STEnvelope().STBuffer(0.00002).STCentroid();
                pcvec = new Vector(cent.STX.Value, cent.STY.Value);
            }
            else
            {
                pcvec = new Vector((double)p, (double)q);
                cent = GeoUtils.Point2SqlGeometry(pcvec, SRID);
            }
            o = record["SITE_STREET_NAME"];
            List<AbstractRecord> candidates = new List<AbstractRecord>();
            bool foundstreet = false;
            int k = 0;
            if (!(o is DBNull))  //has street name to reference
            {
                string stname = (string)o; stname = stname.ToLower(); string[] stnames = stname.Split(' ');
                o = record["SITE_MODE"];
                string stmode = o is DBNull ? "" : (string)o; stmode = stmode.ToLower();
                for (int i = 0; i < impactors.Count; i++)
                {
                    AbstractRecord impactor = impactors.ElementAt(i);
                    o = impactor["NAME"];                    
                    if (o is DBNull)
                        continue;
                    string name = (string)o; name = name.ToLower();
                    bool match = false;
                    if (stnames.Length == 1)
                    {
                        if (name.Contains(stname))
                            match = true;
                    }
                    else if(stnames.Length > 1)
                    {
                        match = true;
                        foreach (string st in stnames)
                        {
                            if (!name.Contains(st))
                            {
                                match = false;
                                break;
                            }
                        }
                    }
                    if (match)
                    {
                        if (stmode!="" && name.Contains(stmode))
                        {
                            if (k++ == 0)  //first occurence would remove all the weak candidates
                                candidates.Clear();
                            foundstreet = true;
                            candidates.Add(impactor);
                        }
                        else
                        {
                            if (k == 0)  //no strong candidates yet
                            {
                                foundstreet = true;
                                candidates.Add(impactor);
                            }
                        }                        
                    }                    
                }
            }
            if (foundstreet)
                impactors = new List<AbstractRecord>(candidates);
            candidates.Clear();
            double dist = double.MaxValue;
            int s = 0;
            k = 0;
            SqlGeometry closestrd = new SqlGeometry();
            if (impactors.Count == 0)
                return "NA";
            if (impactors.Count > 1)
            {
                for (int i = 0; i < impactors.Count; i++)
                {
                    AbstractRecord impactor = impactors.ElementAt(i);
                    SqlGeometry imp = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];
                    //Vector impvec = new Vector((double)impactor["_X_COORD"], (double)impactor["_Y_COORD"]);                    
                    double disti = imp.STDistance(parcel).Value;
                    if (dist - disti > 0.00002)
                    {
                        closestrd = imp;
                        k = 1;
                        dist = disti;
                        s = i;
                    }
                    else if (Math.Abs(dist - disti) <= 0.00002)
                    {
                        if (k++ == 0)
                            closestrd = imp;
                        else
                            closestrd = closestrd.STUnion(imp);
                    }
                }
                if (dist == double.MaxValue)
                    return "NA";
            }
            else
            {
                AbstractRecord impactor = impactors.ElementAt(0);
                closestrd = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];
            }
            if (closestrd == SqlGeometry.Null)
                return "NA";
           // AbstractRecord closeimp = impactors.ElementAt(s);
            //SqlGeometry g = (SqlGeometry)closeimp[MasterProgramConstants.GEOMETRY_BIN],
            SqlGeometry g=closestrd, centbuf = cent.STBuffer(cent.STDistance(g).Value + 0.00002), crosslines = centbuf.STIntersection(g);
            k = 0;
            Vector dirvec=new Vector();
            for (int i = 0; i < crosslines.STNumGeometries(); i++)
            {
                SqlGeometry cl = crosslines.STGeometryN(i + 1);
                Vector p1 = Point2Vector(cl.STStartPoint()), p2 = Point2Vector(cl.STEndPoint()),
                    centp = (p1 + p2) / 2, dirveci = centp - pcvec;
                dirveci.Normalize();
                if (k++ == 0)
                    dirvec = dirveci;
                else
                {
                    dirvec = (dirvec + dirveci) / 2;
                    dirvec.Normalize();
                }
            }
            return Angle2Dir(Vector.AngleBetween(new Vector(1, 0), dirvec));
                                                            
        }
        private string Angle2Dir(double angle)
        {
            int chaincode = (int)Math.Round(angle / 45);
            switch (chaincode)
            { 
                case 0:
                    return "E";
                case 1:
                    return "NE";
                case 2:
                    return "N";
                case 3:
                    return "NW";
                case 4:
                case -4:
                    return "W";
                case -3:
                    return "SW";
                case -2:
                    return "S";
                case -1:
                    return "SE";
                default:
                    return "NA";
            }
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
