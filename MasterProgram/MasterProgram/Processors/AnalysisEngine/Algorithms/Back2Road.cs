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
using System.Windows;



namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    class Back2Road : IGeometryAlgorithm
    {
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
            object o = record["FRC"];
            int frc = o is DBNull ? int.MaxValue : (int)o;
            if (frc < 6)
            {
                #region get back to major road
                SqlGeometry rd = (SqlGeometry)record["GEOMETRY_BIN"], buffer = new SqlGeometry();
                List<AbstractRecord> candidates = new List<AbstractRecord>(), parcels = new List<AbstractRecord>();
                if (rd != null)
                {
                    buffer = rd.STBuffer(0.001);
                    for (int i = 0; i < impactors.Count; i++)
                    {
                        AbstractRecord impactor = impactors.ElementAt(i);
                        SqlGeometry imp = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];
                        if (buffer.Filter(imp))
                        {
                            try
                            {
                                buffer = buffer.STDifference(imp);
                                //DataRecord parcel = new DataRecord(new string[] { "ID","LocID","Lon","Lat","GEOMETRY","CDS"});
                                DataRecord parcel = new DataRecord(new string[] { "LocID", "Lon", "Lat", "GEOMETRY", "CL", "CDS", "B2MR" });
                                o = impactor["_DMP_ID"];
                                parcel["DMPID"] = o is DBNull ? null : o.ToString();
                                o = impactor["LOCATION_ID"];
                                parcel["LOCATIONID"] = o is DBNull ? null : o.ToString();
                                o = impactor["_X_COORD"];
                                parcel["Lon"] = o is DBNull ? double.NaN : (double)o;
                                o = impactor["_Y_COORD"];
                                parcel["Lat"] = o is DBNull ? double.NaN : (double)o;
                                o = impactor["GEOMETRY_BIN"];
                                SqlGeometry sg = o is DBNull ? SqlGeometry.Null : (SqlGeometry)o;
                                parcel["GEOMETRY"] = o is DBNull ? null : sg.STAsBinary().Value;
                                parcel["CL"] = 0;
                                parcel["CDS"] = 0;
                                parcel["B2MR"] = 1;
                                candidates.Add(parcel);
                            }
                            catch
                            {
                                Console.WriteLine("catch");
                            }
                        }
                    }
                    int k = 0;
                    bool found = false;
                    SqlGeometry realbuf = new SqlGeometry(), realbuf2 = new SqlGeometry();
                    double minDist = double.MaxValue;
                    for (int i = 0; i < buffer.STNumGeometries(); i++)
                    {
                        SqlGeometry sg = buffer.STGeometryN(i + 1);
                        if (sg.Filter(rd))
                        {
                            found = true;
                            if (k++ == 0)
                                realbuf = sg;
                            else
                                realbuf = realbuf.STUnion(sg);
                        }
                        else //if road is weirdly aligned, find the closest one
                        {
                            double dist = sg.STDistance(rd).Value;
                            if (dist < minDist)
                            {
                                minDist = dist;
                                realbuf2 = sg;
                            }
                        }
                    }
                    if (!found) //if road is weirdly aligned, use the closest one as buffer
                        realbuf = realbuf2;
                    foreach (DataRecord parcel in candidates)
                    {
                        byte[] b = (byte[])parcel["GEOMETRY"];
                        SqlGeometry sg = SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(b), 4269);
                        if (sg.STDistance(realbuf) < 0.00003)
                        {
                            parcels.Add(parcel);
                        }
                    }
                    return parcels;
                }
                else
                    return null;
                #endregion get back to major road
            }
            else if (frc == 7)
            {
                #region get cul de sac
                SqlGeometry rd = (SqlGeometry)record["RD_GEOMETRY"], buffer = new SqlGeometry();
                List<AbstractRecord> candidates = new List<AbstractRecord>(), parcels = new List<AbstractRecord>();
                if (rd != null)
                {
                    buffer = rd.STBuffer(0.0003);
                    for (int i = 0; i < impactors.Count; i++)
                    {
                        AbstractRecord impactor = impactors.ElementAt(i);
                        SqlGeometry imp = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];
                        if (buffer.Filter(imp))
                        {
                            try
                            {
                                buffer = buffer.STDifference(imp);
                                //DataRecord parcel = new DataRecord(new string[] { "ID","LocID","Lon","Lat","GEOMETRY","CDS"});
                                DataRecord parcel = new DataRecord(new string[] { "LocID", "Lon", "Lat", "GEOMETRY", "CL", "CDS", "B2MR" });
                                o = impactor["_DMP_ID"];
                                parcel["DMPID"] = o is DBNull ? null : o.ToString();
                                o = impactor["LOCATION_ID"];
                                parcel["LOCATIONID"] = o is DBNull ? null : o.ToString();
                                o = impactor["_X_COORD"];
                                parcel["Lon"] = o is DBNull ? double.NaN : (double)o;
                                o = impactor["_Y_COORD"];
                                parcel["Lat"] = o is DBNull ? double.NaN : (double)o;
                                o = impactor["GEOMETRY_BIN"];
                                SqlGeometry sg = o is DBNull ? SqlGeometry.Null : (SqlGeometry)o;
                                parcel["GEOMETRY"] = o is DBNull ? null : sg.STAsBinary().Value;
                                parcel["CL"] = 0;
                                parcel["CDS"] = 1;
                                parcel["B2MR"] = 0;
                                o = impactor["CAL_ACREAGE"];
                                double area = o == null ? 0 : (double)o;
                                if (area < 3)
                                    candidates.Add(parcel);
                            }
                            catch
                            {
                                Console.WriteLine("catch");
                            }
                        }
                    }
                    int k = 0;
                    bool found = false;
                    SqlGeometry realbuf = new SqlGeometry(), realbuf2 = new SqlGeometry();
                    double minDist = double.MaxValue;
                    for (int i = 0; i < buffer.STNumGeometries(); i++)
                    {
                        SqlGeometry sg = buffer.STGeometryN(i + 1);
                        if (sg.Filter(rd))
                        {
                            found = true;
                            if (k++ == 0)
                                realbuf = sg;
                            else
                                realbuf = realbuf.STUnion(sg);
                        }
                        else //if road is weirdly aligned, find the closest one
                        {
                            double dist = sg.STDistance(rd).Value;
                            if (dist < minDist)
                            {
                                minDist = dist;
                                realbuf2 = sg;
                            }
                        }
                    }
                    if (!found) //if road is weirdly aligned, use the closest one as buffer
                        realbuf = realbuf2;
                    foreach (DataRecord parcel in candidates)
                    {
                        byte[] b = (byte[])parcel["GEOMETRY"];
                        SqlGeometry sg = SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(b), 4269);
                        if (sg.STDistance(realbuf) < 0.00001)
                        {
                            parcels.Add(parcel);
                        }
                    }
                }
                #endregion get cul de sac

                #region get corner lot
                SqlGeometry jnct = (SqlGeometry)record[MasterProgramConstants.GEOMETRY_BIN],
                    clbuffer = (SqlGeometry)record["CL_BUFFER"];
                //SqlGeometry cls = new SqlGeometry();
                List<AbstractRecord> clcandidates = new List<AbstractRecord>(), clparcels = new List<AbstractRecord>();
                if (buffer != null)
                {
                    for (int i = 0; i < impactors.Count; i++)
                    {
                        AbstractRecord impactor = impactors.ElementAt(i);
                        SqlGeometry imp = (SqlGeometry)impactor[MasterProgramConstants.GEOMETRY_BIN];
                        if (buffer.Filter(imp))
                        {
                            buffer = buffer.STDifference(imp);
                            DataRecord parcel = new DataRecord(new string[] { "LocID", "Lon", "Lat", "GEOMETRY","CL", "CDS","B2MR" });
                            o = impactor["_DMP_ID"];
                            parcel["DMPID"] = o is DBNull ? null : o.ToString();
                            o = impactor["LOCATION_ID"];
                            parcel["LOCATIONID"] = o is DBNull ? null : o.ToString();
                            o = impactor["_X_COORD"];
                            parcel["Lon"] = o is DBNull ? double.NaN : (double)o;
                            o = impactor["_Y_COORD"];
                            parcel["Lat"] = o is DBNull ? double.NaN : (double)o;
                            o = impactor["GEOMETRY_BIN"];
                            SqlGeometry sg = o is DBNull ? SqlGeometry.Null : (SqlGeometry)o;
                            parcel["GEOMETRY"] = o is DBNull ? null : sg.STAsBinary().Value;
                            parcel["CL"] = 0;
                            parcel["CDS"] = 1;
                            parcel["B2MR"] = 0;
                            o = impactor["CAL_ACREAGE"];
                            double area = o is DBNull ? 0 : (double)o;
                            if (area < 3)
                                clcandidates.Add(parcel);
                        }
                    }
                    int k = 0;
                    SqlGeometry realbuf = new SqlGeometry();
                    for (int i = 0; i < buffer.STNumGeometries(); i++)
                    {
                        SqlGeometry sg = buffer.STGeometryN(i + 1);
                        if (sg.Filter(jnct.STBuffer(0.00001)))
                        {
                            if (k++ == 0)
                                realbuf = sg;
                            else
                                realbuf = realbuf.STUnion(sg);
                        }
                    }


                    foreach (DataRecord parcel in candidates)
                    {
                        byte[] b = (byte[])parcel["GEOMETRY_BIN"];
                        SqlGeometry sg = SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(b), 4269);
                        if (sg.STDistance(realbuf) < 0.00003)
                        {
                            clparcels.Add(parcel);
                        }
                    }

                }
                #endregion get corner lot
                return parcels;
            }
            else
                return null;
                

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
