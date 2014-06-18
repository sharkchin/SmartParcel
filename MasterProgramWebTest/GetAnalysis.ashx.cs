using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Dmp.Neptune.Webservices;
using DMP.MasterProgram;
using System.Xml.Linq;
using Dmp.Neptune.BusinessObjects.Data;
using Dmp.Neptune.Utils;
using System.Text.RegularExpressions;
using System.Xml;
using System.Diagnostics;
using DMP.MasterProgram.Processors;
using MonoGIS.NetTopologySuite.Geometries;
using Dmp.Neptune.Utils.ShapeFile;
using Dmp.Neptune.TransactionEngine;
using Dmp.Neptune.Jobs;



namespace MasterProgramWebTest
{
    /// <summary>
    /// Summary description for MasterProgramHandler
    /// </summary>
    public class MasterProgramHandler : VariableOutputPage
    {

        protected override void ValidateInput(InputValidator validator)
        {
            validator.Add("datasource", true, 256, null, "Invalid Data Source  Specified", "The Data Source containing the Master Program XML defination");
            validator.Add("isInteractive", true, 256, null, "Is Interactive", "Is Interactive");
            validator.Add("email", false, 256, InputValidator.EMAIL, "Invalid email address specified.",
            "E-mail to use to send the notifications to.");
            base.ValidateInput(validator);

        }
        protected override DocumentationGenerator SetupDocumentation(InputValidator validator)
        {
            string desc = "";
            string output = "success/failure";
            string title = Request.AppRelativeCurrentExecutionFilePath.Substring(Request.AppRelativeCurrentExecutionFilePath.LastIndexOf('/') + 1);
            title = title.Replace(".aspx", "");
            return new DocumentationGenerator(title, desc, validator.Parameters, output);
        }

        public override void DoPageLoad(Dmp.Neptune.BusinessObjects.User user, object sender, EventArgs e)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            string datasource = GetRequestParam("datasource");
            string activeVersionId = GetRequestParam("activeVersionId");
            bool isInteractive = Convert.ToBoolean(GetRequestParam("isInteractive"));
            string email = GetRequestParam("email");
            string analysisConfigXML = GetRequestParam("analysisConfig");


            Resource res = user.GetResourceByName(datasource);
            if (res == null)
                throw new ApplicationException("Resource not found");

            int groupId = user.GroupID;
            int userId = user.UserID;
            int accountId = user.AccountID;

            TEVersion version = new TEVersion(user, 0, "Analysis", null);
            string versionId = version.VersionID;

            XDocument xdoc = null;
            if (analysisConfigXML != null)
            {
                xdoc = XDocument.Parse(analysisConfigXML);
            }
            else
            {
                string folderName, fileName;
                string xmlConfigFile = res.Metadata.AnalysisConfig;//GetRequestParam("Definition");

                int index = xmlConfigFile.LastIndexOfAny(new char[] { '/', '\\' });

                folderName = xmlConfigFile.Substring(0, index);
                fileName = xmlConfigFile.Substring(index + 1);

                if (FileManagementUtil.IsSystemFileName(fileName))
                    throw new ApplicationException("File names that begin with '.' are not allowed.");

                if (fileName.IndexOf("..") >= 0)
                    throw new ApplicationException("Hmm...trying to hack the system. Bad. Bad.");
                else if (Regex.IsMatch(fileName, "(^|[\\/])Layers[\\/]", RegexOptions.IgnoreCase))
                    throw new ApplicationException("You do not have permission to access the \"Layers\" folder.");

                String physicalFilePath = PathResolver.GetFilePath(user, fileName, folderName, this.Staging);
                xdoc =  XDocument.Load(physicalFilePath);
            }           


            bool hasOutputWriter = false;
            //Resolve the Data Base resource,Table Name and Storage type for the Input Data Sets
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
                    }

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
                    }
                    

                }
 
            }


            foreach (XElement element in xdoc.Element("MasterProgram").Element("Processors").Elements("Processor"))
            {
                if ("DMP.MasterProgram.Processors.JavaScript.JavascriptProcessor".Equals(element.Attribute("class").Value))
                {
                    XElement expScriptEle = element.Element("ExpressionScript");
                    if (expScriptEle == null || String.IsNullOrEmpty(expScriptEle.Value))
                        throw new ApplicationException("ExpressionScript not defined!");

                    expScriptEle.Value = PathResolver.GetFilePath(user, expScriptEle.Value, null, this.Staging);
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

            if (!hasOutputWriter)
                throw new ApplicationException("Analysis Configuration has no output writer! Please define one!");

            // default it to plain text 
            string mimeType = "text/xml";
            ResponseBuilder rb = new ResponseBuilder(Request);

            if (rb.OutputType.Equals("json", StringComparison.CurrentCultureIgnoreCase))
                mimeType = rb.MimeType;

            Response.ContentType = mimeType;

            XmlDocument responseWriter = new XmlDocument();


            try
            {

                if (!isInteractive)
                {
                    GetAnalysisModel model = new GetAnalysisModel();

                    model.InputXml = xdoc.ToString();
                    model.DataSource = datasource;

                    model.ComputerName = Environment.MachineName;
                    model.RecipientEmail = email;

                    model.CreationTime = DateTime.Now;

                    model.ProcessId = Guid.NewGuid().ToString();
                    model.AccountName = user.Account.Name;
                    model.AccountId = user.Account.AccountID;
                    model.GroupId = user.GroupID;
                    model.UserId = user.UserID;
                    model.ActiveVersionId = activeVersionId;

                    model.VersionId = version.VersionID;

                    DataLoadUtils.AddJobInQ(DataLoadUtils.QUEUE_NAME, typeof(GetAnalysisJob), model);

                    WriteSuccess(string.Format("An email will be sent to {0} after the process {1} is completed.",
                        email, model.ProcessId), String.Format(DMPJob.PublicTemp + "/DataLoadOutput/{0}/status.xml", model.ProcessId));

                   
                }
                else
                {
                    MasterProcessor mp = new MasterProcessor(xdoc.ToString());
                    mp.ActiveVersionId = activeVersionId;
                    bool result = mp.ProcessRecords(null, accountId, groupId, userId, versionId);


                    if (result)
                    {
                        responseWriter = new XmlDocument();
                        XmlElement responseNode = responseWriter.CreateElement("Response");
                        responseWriter.AppendChild(responseNode);

                        XmlNode resultNode = responseWriter.CreateElement("Results");

                        resultNode.InnerText = "Success";
                        responseNode.AppendChild(resultNode);


                    }
                    else
                    {

                        responseWriter = new XmlDocument();
                        XmlElement responseNode = responseWriter.CreateElement("Response");

                        responseWriter.AppendChild(responseNode);

                        XmlNode resultNode = responseWriter.CreateElement("Results");

                        resultNode.InnerText = "Failed!";
                        responseNode.AppendChild(resultNode);

                    }
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

            // build response for xml
            if (mimeType.ToLower().Contains("/xml"))//;, StringComparison.CurrentCultureIgnoreCase))
            {
                Response.Write(rb.GetResponse(responseWriter));
            }
            else
            {
                Response.Write(rb.GetResponse(responseWriter));
            }

            timer.Stop();

            System.Threading.ThreadPool.SetMaxThreads(1000, 1000);
            long t = timer.ElapsedMilliseconds;

            
            

        }
       
    }
}