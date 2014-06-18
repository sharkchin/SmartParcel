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
using System.Configuration;
using System.IO;
using System.Threading;
using System.Text;


public partial class GetAnalysis : VariableOutputPage
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
            xdoc = XDocument.Load(physicalFilePath);
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
                    string bufferUnit = null;
                    string bufferS = null;
                    double buffer = 0.0;
                    double distanceInMeter = 0.0;

                    XElement incElement = element.Element("Filter").Element("InclusionGeometries");
                    if (incElement.Attribute("BufferValue") != null && incElement.Attribute("BufferUnit") != null)
                    {
                        bufferS = incElement.Attribute("BufferValue").Value;
                        bufferUnit = incElement.Attribute("BufferUnit").Value;
                    }


                    if (bufferS != null)
                    {
                        buffer = Convert.ToDouble(bufferS);
                        distanceInMeter = ToMeters(buffer, bufferUnit);

                    }



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

                                geo = geo.Buffer(distanceInMeter * .00001);

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

        int timeOutInSeconds = 60;

       /* if(xdoc.Element("MasterProgram").Element("Parameters") != null)
        {
            foreach (XElement element in xdoc.Element("MasterProgram").Element("Parameters").Elements("Parameter"))
            {
                if ((element.Attribute("name").Value).Equals("TimeOut"))
                {
                    timeOutInSeconds = Convert.ToInt32(element.Attribute("value").Value);                   
                }
            }

        }*/

        string timeOutInSecS = ConfigurationManager.AppSettings["GET_ANALYSIS_TIMEOUT"];

        if (timeOutInSecS != null)
        {
            timeOutInSeconds = Convert.ToInt32(timeOutInSecS);
        }

        string nOfThreadsS = ConfigurationManager.AppSettings["GET_ANALYSIS_THREADS_COUNT"];
        int nOfThreads = 2;

        if (nOfThreadsS != null)
        {
            nOfThreads = Convert.ToInt32(nOfThreadsS);
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

                CreateStatusFile(model);

                //DataLoadUtils.AddJobInQ(DataLoadUtils.QUEUE_NAME, typeof(GetAnalysisJob), model);               

                 ThreadPool.QueueUserWorkItem(
                                (state) =>
                                {
                                    string filePath = null;
                                    try
                                    {
                                        filePath = CreateStatusFile(model);
                                        MasterProcessor mp = new MasterProcessor(xdoc.ToString());
                                        mp.NumOfThreads = nOfThreads;
                                        mp.ActiveVersionId = activeVersionId;
                                      /*  mp.ProcessRecords(null, accountId, groupId, userId, versionId);
                                        UpdateSuccesssStatusFile(filePath, model.DataSource);

                                        SendSuccessEmail("Get Analysis", 0, null, model);*/

                                       Thread threadToKill = null;
                                        Action wrappedAction = () =>
                                        {
                                            threadToKill = Thread.CurrentThread;
                                            mp.ProcessRecords(null, accountId, groupId, userId, versionId);
                                            UpdateSuccesssStatusFile(filePath, model.DataSource);

                                            SendSuccessEmail("Get Analysis", 0, null, model);
                                        };

                                        IAsyncResult result = wrappedAction.BeginInvoke(null, null);
                                        if (result.AsyncWaitHandle.WaitOne(timeOutInSeconds*1000))
                                        {
                                            wrappedAction.EndInvoke(result);
                                        }
                                        else
                                        {
                                            threadToKill.Abort();
                                            UpdateErrorStatusFile(filePath, "Get Analysis taking longer than "+timeOutInSeconds+" seconds");
                                        }
                                    }
                                    catch (SystemException ex)
                                    {
                                        SendErrorEmail("Get Analysis", model.ProcessId, new Exception("Internal system error contact support"), model);

                                        //writing errror xml into status.xml
                                        UpdateErrorStatusFile(filePath, "Internal system error contact support");

                                    }
                                    catch (Exception ex)
                                    {
                                        string message = SendErrorEmail("Get Analysis", model.ProcessId, ex,model);
                                        //update error status file
                                        UpdateErrorStatusFile(filePath, message);

                                    }         
                                 
                                }, null);


                WriteSuccess(string.Format("An email will be sent to {0} after the process {1} is completed.",
                    email, model.ProcessId), String.Format(DMPJob.PublicTemp + "/DataLoadOutput/{0}/status.xml", model.ProcessId));


            }
            else
            {
                MasterProcessor mp = new MasterProcessor(xdoc.ToString());
                mp.ActiveVersionId = activeVersionId;
                mp.NumOfThreads = nOfThreads;
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


        if (isInteractive)
        {
            // build response for xml
            if (mimeType.ToLower().Contains("/xml"))//;, StringComparison.CurrentCultureIgnoreCase))
            {
                Response.Write(rb.GetResponse(responseWriter));
            }
            else
            {
                Response.Write(rb.GetResponse(responseWriter));
            }
        }

        timer.Stop();
        long t = timer.ElapsedMilliseconds;
    }

    private string CreateStatusFile(GetAnalysisModel mModel)
    {
        //write
        bool staging = true;
        //create processing.txt

        // Create a new file name. This example generates
        // a random string.
        string newFileName = "status.xml";

        string folderPath = PathResolver.Normalize(ConfigurationManager.AppSettings.Get("PublicDirectory") + "/DataLoadOutput/" + mModel.ProcessId + "/", staging);
        //Combine the new file name with the path
        string filePath = System.IO.Path.Combine(folderPath, newFileName);
        //overwrite the files.
        if (!System.IO.Directory.Exists(folderPath))
            System.IO.Directory.CreateDirectory(folderPath);
        if (!System.IO.File.Exists(filePath))
        {
            FileStream fs = System.IO.File.Create(filePath);
            fs.Close();
        }

        // Write processing xml into status.xml
        string processingText = "<Response>";
        processingText += "<Success status=\"Processing\" message=\"Job is still processing.\" />";
        processingText += "</Response>";
        System.IO.File.WriteAllText(filePath, processingText);

        return filePath;
    }
        

    private static double ToMeters(double distance, string unit)
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

    protected void UpdateSuccesssStatusFile(String filePath, String msg)
    {
        string successText = "<Response>";
        successText += "<Success status=\"Success\" />";
        successText += "<Results><![CDATA[Get Analysis on " + msg + " successfully done]]></Results>";
        successText += "</Response>";
        System.IO.File.WriteAllText(filePath, successText);
    }

    protected void UpdateErrorStatusFile(String filePath, String msg)
    {
        string successText = "<Response>";
        successText += "<Success status=\"Error\" />";
        successText += "<Results><![CDATA[" + msg + "]]></Results>";
        successText += "</Response>";
        System.IO.File.WriteAllText(filePath, successText);
    }

    /// <summary>
    /// Send a success email to recipient.
    /// </summary>
    /// <param name="requestType">Type of request.</param>
    /// <param name="numRecordLoaded">Not applicable.</param>
    /// <param name="statusData">Job status description.</param>
    protected  void SendSuccessEmail(string requestType, int numRecordLoaded, string statusData, GetAnalysisModel mModel)
    {
        string subject = string.Format("{0} request {1} succeeded.", requestType, mModel.ProcessId);
        StringBuilder message = new StringBuilder();
        message.AppendFormat("Analysis of {0} layer successfully done.\r\n", mModel.DataSource);

        if (!string.IsNullOrEmpty(statusData))
        {
            message.AppendLine();
            message.AppendLine(" -------------- Job Status --------------");
            message.AppendLine();
            message.AppendLine(statusData);
        }

        message.AppendLine();
        message.AppendLine();

        SendJobStatusEmail("Get Analysis Service", subject, message.ToString(), null, mModel);
    }

    protected void SendJobStatusEmail(String emailFrom, string subject, String message, String attachmentPath, GetAnalysisModel dataModel )
    {
        try
        {
           
            EmailUtils.SendMail(dataModel.SmtpServer,
                    dataModel.SmtpPort,
                    dataModel.RecipientEmail,
                    dataModel.DontReplyEmailAddress,
                    emailFrom,
                    subject,
                    message,
                    attachmentPath);

           
        }
        catch (Exception ex)
        {
           // mLogger.Error("Failed to send Email", ex);
        }
    }//SendJo

    /// <summary>
    /// Send a failure email full of errors.
    /// </summary>
    /// <param name="requestType">Type of request.</param>
    /// <param name="procID">The process ID.</param>
    /// <param name="ex">The Exception raised.</param>
    protected  string SendErrorEmail(string requestType, string procID, Exception ex, GetAnalysisModel mModel)
    {


        String subject = requestType + " Loader request " + mModel.ProcessId + " failed.";
        StringBuilder message = new StringBuilder();
        message.AppendLine("Failed to complete analysis of {0} layer " + mModel.DataSource + " due to following error:");
        message.AppendLine();
        message.AppendLine(ex.Message);
        message.AppendLine();


        if (ex.InnerException != null) message.Append(ex.InnerException.Message);
        message.AppendLine();
        message.AppendLine();

        SendJobStatusEmail("Get Analysis Service", subject, message.ToString(), null, mModel);
        return message.ToString();
    }



    
}
