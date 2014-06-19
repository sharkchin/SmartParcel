using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Dmp.Neptune.Utils.ShapeFile;
using DMP.MasterProgram;
using System.Diagnostics;
using DMP.MasterProgram.Processors;
using Dmp.Neptune.BusinessObjects.Data;
using Dmp.Neptune.Utils;
using System.Text.RegularExpressions;
using Dmp.Neptune.Webservices;
using MonoGIS.NetTopologySuite.Geometries;
using System.Xml;
using Dmp.Auth;

namespace MasterProgram
{
    class Program
    {
        
        static void Main(string[] args)
        {
            Stopwatch timer = Stopwatch.StartNew();
            string taskName = "", datasource = "";
            if (args.Length == 0)
            {
                Console.WriteLine("Restart and specify the datasource or input the datasource name (press enter to exit):");
                taskName = Console.ReadLine();
                if (taskName.Length == 0) return;
                datasource = "my_folder/" + taskName;
            }
            else
            {
                datasource = args[0];
            }
            Dmp.Neptune.BusinessObjects.User user = new Dmp.Neptune.BusinessObjects.User(233184, 100701, 100701);
            Resource res = user.GetResourceByName(datasource);
            if (res == null)
                throw new ApplicationException("Resource not found");
      
            string xmlConfigFile = res.Metadata.AnalysisConfig;
            int index = xmlConfigFile.LastIndexOfAny(new char[] { '/', '\\' });
            string folderName = xmlConfigFile.Substring(0, index);
            string fileName = xmlConfigFile.Substring(index + 1);
            if (FileManagementUtil.IsSystemFileName(fileName))
                throw new ApplicationException("File names that begin with '.' are not allowed.");
            if (fileName.IndexOf("..") >= 0)
                throw new ApplicationException("Hmm...trying to hack the system. Bad. Bad.");
            else if (Regex.IsMatch(fileName, "(^|[\\/])Layers[\\/]", RegexOptions.IgnoreCase))
                throw new ApplicationException("You do not have permission to access the \"Layers\" folder.");
            String physicalFilePath = PathResolver.GetFilePath(user, fileName, folderName, true);
            XDocument xdoc = XDocument.Load(physicalFilePath);
            XElement config = xdoc.Element("MasterProgram");
            if (config.Attribute("lastFinishedCFTID") != null && config.Attribute("lastFinishedCFTID").Value != "0")
            {
                if (xdoc.Element("MasterProgram").Element("DataSets").Elements("DataSet").ElementAt(0).Element("Filter").Element("AttributeCriteria") != null)
                {
                    string attCriteria = xdoc.Element("MasterProgram").Element("DataSets").Elements("DataSet").ElementAt(0).Element("Filter").Element("AttributeCriteria").Value;
                    if (attCriteria.Contains(">="))
                    {
                        int i = attCriteria.IndexOf(">=");
                        int j1 = attCriteria.IndexOf("_CFTID"), j2 = attCriteria.IndexOf("and", j1);
                        if (i < j2)
                            xdoc.Element("MasterProgram").Element("DataSets").Elements("DataSet").ElementAt(0).Element("Filter").Element("AttributeCriteria").Value =
                                attCriteria.Replace(attCriteria.Substring(i + 2, j2 - i - 2), "'" + config.Attribute("lastFinishedCFTID").Value + "' ");
                        else
                            xdoc.Element("MasterProgram").Element("DataSets").Elements("DataSet").ElementAt(0).Element("Filter").Element("AttributeCriteria").Value =
                                attCriteria.Replace(attCriteria.Substring(i + 2), "'" + config.Attribute("lastFinishedCFTID").Value + "' ");
                    }
                    else
                    {
                        xdoc.Element("MasterProgram").Element("DataSets").Elements("DataSet").ElementAt(0).Element("Filter").Element("AttributeCriteria").Value =
                            "_CFTID>='" + config.Attribute("lastFinishedCFTID").Value + "'" + " and " + attCriteria;
                    }
                }
                else
                {
                    xdoc.Element("MasterProgram").Element("DataSets").Elements("DataSet").ElementAt(0).Element("Filter").Add(new XElement("AttributeCriteria", "_CFTID>='"
                        + config.Attribute("lastFinishedCFTID").Value + "'"));
                }
            }
            bool hasOutputWriter = false;
            #region read analysisConfig.xml
            foreach (XElement element in xdoc.Element("MasterProgram").Element("DataSets").Elements("DataSet"))
            {
                Resource resource = user.GetResourceByName(element.Attribute("resource").Value.ToString());
                if (resource == null)
                    throw new ApplicationException("Dataset resource '" + element.Attribute("resource").Value.ToString() + "' not found!'");

                string storageType = resource.StorageType.ToString();

                if ("DOFS".Equals(storageType))
                {
                    string path = PathResolver.Normalize(resource.DataPath, true);
                    string tableName = System.IO.Path.GetFileName(path);
                    //path = GetShapeFileDirectory(path, false);
                    element.Add(new XAttribute("dbResource", path));
                    element.Add(new XAttribute("tableName", tableName));
                    element.Add(new XAttribute("storageType", storageType));
                }
                else if ("DB".Equals(storageType))
                {
                    string databaseName = resource.DatabaseName;
                    string tableName = resource.Viewname;
                    element.Add(new XAttribute("dbResource", databaseName));
                    element.Add(new XAttribute("tableName", tableName));
                    element.Add(new XAttribute("storageType", storageType));
                }

                if (element.Element("Filter") != null)
                {
                    if (element.Element("Filter").Element("InclusionGeometries") != null)
                    {

                        XElement uriElement = element.Element("Filter").Element("InclusionGeometries").Element("Uri");
                        if (uriElement != null && !String.IsNullOrEmpty(uriElement.Value))
                        {
                            string inclusionURI = uriElement.Value;

                            if (user.SessionKey != null)
                            {
                                Geometry[] geos = GeoUtils.GetGeosFromBds(inclusionURI + "&SS_CANDY=" + user.SessionKey.DmpCandy);

                                if (geos != null && geos.Length > 0)
                                {
                                    Geometry geo = null;
                                    if (geos.Length > 1)
                                        geo = new GeometryCollection(geos);
                                    else
                                        geo = geos[0];
                                    uriElement.Remove();
                                    XElement wktElement = new XElement("Wkt");
                                    wktElement.Value = geo.ToText();
                                    element.Element("Filter").Element("InclusionGeometries").Add(wktElement);

                                }
                            }
                        }
                    } // InclusionGeometries

                    if (element.Element("Filter").Element("ExclusionGeometries") != null)
                    {
                        XElement uriElement = element.Element("Filter").Element("ExclusionGeometries").Element("Uri");
                        if (uriElement != null && !String.IsNullOrEmpty(uriElement.Value))
                        {
                            string exclusionURI = uriElement.Value;
                            if (user.SessionKey != null)
                            {
                                Geometry[] geos = GeoUtils.GetGeosFromBds(exclusionURI + "&SS_CANDY=" + user.SessionKey.DmpCandy);

                                if (geos != null && geos.Length > 0)
                                {
                                    Geometry geo = null;
                                    if (geos.Length > 1)
                                        geo = new GeometryCollection(geos);
                                    else
                                        geo = geos[0];

                                    uriElement.Remove();
                                    XElement wktElement = new XElement("Wkt");
                                    wktElement.Value = geo.ToText();
                                    element.Element("Filter").Element("ExclusionGeometries").Add(wktElement);
                                }
                            }
                        }
                    } // ExclusionGeometries
                } // Filter != null
            }
            foreach (XElement element in xdoc.Element("MasterProgram").Element("Processors").Elements("Processor"))
            {
                if ("DMP.MasterProgram.Processors.JavaScript.JavascriptProcessor".Equals(element.Attribute("class").Value))
                {
                    XElement expScriptEle = element.Element("ExpressionScript");
                    if (expScriptEle == null || String.IsNullOrEmpty(expScriptEle.Value))
                        throw new ApplicationException("ExpressionScript not defined!");

                    expScriptEle.Value = PathResolver.GetFilePath(user, expScriptEle.Value, null, true);
                }

                if (Regex.IsMatch(element.Attribute("class").Value, @"DMP\.MasterProgram\.Processors\.OutputProcessor\.[\w\.]+"))
                {
                    if (!hasOutputWriter)
                    {
                        XDocument xdocMetadata = XDocument.Load(new System.IO.StringReader(res.Metadata.GetXmlDoc().InnerXml.ToString()));
                        XElement dataPathEle = xdocMetadata.Element("Layer").Element("DataPath");
                        if (dataPathEle != null)
                        {
                            string dataStoragePath = dataPathEle.Value;
                            dataStoragePath = PathResolver.Normalize(dataStoragePath, true);
                            dataPathEle.Value = dataStoragePath;
                        }

                        hasOutputWriter = true;
                        element.Add(xdocMetadata.Elements());
                    }
                    else
                    {
                        throw new ApplicationException("Cannot have more than one OutputProcessor!");
                    }
                }
            }
            #endregion read analysisConfig

            if (!hasOutputWriter)
                throw new ApplicationException("Analysis Configuration has no output writer! Please define one!");
            XmlDocument responseWriter = new XmlDocument();
            try
            {

                //MasterProcessor mp = new MasterProcessor(xdoc.ToString(),_numThreads, _bucketsize, ConfigurationManager.AppSettings["metricLogPath"]);   

                MasterProcessor mp = new MasterProcessor(xdoc.ToString());

                bool result = mp.ProcessRecords();

                if (result)
                {
                    responseWriter = new XmlDocument();
                    XmlElement responseNode = responseWriter.CreateElement("Response");
                    responseWriter.AppendChild(responseNode);
                    XmlNode resultNode = responseWriter.CreateElement("Results");
                    resultNode.InnerText = "Success";
                    responseNode.AppendChild(resultNode);
                    Console.Write("success:");
                    Console.WriteLine(TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds));
                    Console.ReadKey();
                }
                else
                {
                    responseWriter = new XmlDocument();
                    XmlElement responseNode = responseWriter.CreateElement("Response");
                    responseWriter.AppendChild(responseNode);
                    XmlNode resultNode = responseWriter.CreateElement("Results");
                    resultNode.InnerText = "Failed!";
                    responseNode.AppendChild(resultNode);
                    Console.Write("failed:");
                    Console.WriteLine(TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds));
                    Console.ReadKey();
                }
            }
            catch (Exception exc)
            {
                responseWriter = new XmlDocument();
                XmlElement responseNode = responseWriter.CreateElement("Response");
                responseWriter.AppendChild(responseNode);
                XmlNode resultNode = responseWriter.CreateElement("Results");
                resultNode.InnerText = "Error: " + exc.Message;
                responseNode.AppendChild(resultNode);
            }                      
        }

           
        
    }
}
