/**
 * 
 * 
 * 
 */ 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using DMP.MasterProgram.ProcessorMetadata;
using log4net;
using log4net.Config;

namespace DMP.MasterProgram.Utils.Parsers
{
    class AnalysisEngineParser
    {
        private XDocument doc;
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="xmlDOM">XML DOM</param>
        public AnalysisEngineParser(string xmlDOM)
        {
            if (xmlDOM == null || String.IsNullOrEmpty(xmlDOM))
            {
                logger.Error("Error in the AnalysisEngineParser constructor: XMLDom containing the metadata is null or empty");
                throw new ApplicationException("Error in the AnalysisEngineParser constructor:XMLDom containing the metadata is null or empty");
            }

            try
            {
                //parse the xml string
                doc = XDocument.Parse(xmlDOM);
            }
            catch (Exception e)
            {
                logger.Error("Error while initializing XDocument  in Analysis Engine Parser ", e);
                throw new ApplicationException("initializing XDocument  in Analysis Engine Parser: "+e.Message);
            }
        }

        /// <summary>
        /// parse Configuration XML and create AnalysisEngine Metadata object.
        /// </summary>
        /// <returns> Analysis Engine Metadata object</returns>
        public AnalysisEngineMetadata Parse()
        {

            AnalysisEngineMetadata metadata = new AnalysisEngineMetadata();
            try
            {
                //read the Base attributes
                foreach (XElement element in doc.Element("Processor").Element("RecordAttributes").Element("BaseAttributes").Elements("RecordAttribute"))
                {
                    AnalysisEngineMetadata.AEAttribute att = new AnalysisEngineMetadata.AEAttribute();
                    att.TaskName = null;
                    att.IsBaseAttribute = true;
                    metadata.AttributeMap.Add(element.Attribute("name").Value, att);
                }

                //read the "to process" attributes
                foreach (XElement element in doc.Element("Processor").Element("RecordAttributes").Element("ToProcessAttributes").Elements("RecordAttribute"))
                {
                    AnalysisEngineMetadata.AEAttribute att = new AnalysisEngineMetadata.AEAttribute();
                    att.TaskName = element.Attribute("task").Value;
                    att.IsBaseAttribute = false;
                    metadata.AttributeMap.Add(element.Attribute("name").Value, att);
                }

                //read the tasks
                foreach (XElement element in doc.Element("Processor").Element("Tasks").Elements("Task"))
                {
                    AnalysisEngineMetadata.AETask task = new AnalysisEngineMetadata.AETask();
                    task.TaskClass = element.Attribute("class").Value;
                    bool isSubject = true;
                    foreach (XElement input in element.Elements("Input"))
                    {
                        if (isSubject)
                        {
                            task.SubjectName = input.Value;
                            isSubject = false;
                        }
                        else
                        {
                            task.ImpactorName = input.Value;
                        }
                    }
                    metadata.TaskMap.Add(element.Attribute("name").Value, task);
                }

                //read the parameters
                foreach (XElement element in doc.Element("Processor").Element("Parameters").Elements("Parameter"))
                {
                    //BDE-41
                   /* if((element.Attribute("name").Value).Equals(MasterProgramConstants.IS_IMPACTORS_ON_DEMAND))
                    {
                        metadata.IsImpactorOnDemand =Convert.ToBoolean(element.Attribute("value").Value);
                    }
                    else */if ((element.Attribute("name").Value).Equals(MasterProgramConstants.PROCESSING_TILE_SIZE))
                    {
                        metadata.ProcessingTileSize = Convert.ToInt16(element.Attribute("value").Value);
                    }
                    else if ((element.Attribute("name").Value).Equals(MasterProgramConstants.PREFER_GPU))
                    {
                        metadata.PreferGPU = Convert.ToBoolean(element.Attribute("value").Value);
                    }
                   
                }
            }
            catch (Exception e)
            {

                logger.Error("Error in parsing Analysis Engine xml Configuration file : ", e);
                throw new ApplicationException("Error in parsing Analysis engine xml Configuration file : "+ e.Message);
            }

            return metadata;
        }
    }
}
