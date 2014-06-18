using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using log4net;
using log4net.Config;
using DMP.MasterProgram.ProcessorMetadata;
using Dmp.Neptune.Webservices;

namespace DMP.MasterProgram.Utils.Parsers
{
    class JavascriptParser
    {
        private XDocument doc;
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="xmlDOM">XML DOM</param>
        public JavascriptParser(string xmlDOM)
        {
            if (xmlDOM == null || String.IsNullOrEmpty(xmlDOM))
            {
                logger.Error("Error in the JavascriptParser constructor: XMLDom containing the metadata is null or empty");
                throw new ApplicationException("Error in the JavascriptParser constructor:XMLDom containing the metadata is null or empty");
            }

            try
            {
                //parse the xml string
                doc = XDocument.Parse(xmlDOM);
            }
            catch (Exception e)
            {
                logger.Error("Error while initializing XDocument  in JavascriptParser ", e);
                throw new ApplicationException("Error while initializing XDocument  in JavascriptParser: "+e.Message);
            }
        }

        /// <summary>
        /// parse Configuration XML and create AnalysisEngine Metadata object.
        /// </summary>
        /// <returns> Analysis Engine Metadata object</returns>
        public JavascriptProcessorMetadata Parse()
        {

            JavascriptProcessorMetadata metadata = new JavascriptProcessorMetadata();
            try
            {
                //read the expression script
                string expressionScriptPath = doc.Element("Processor").Element("ExpressionScript").Value;
                metadata.ExpressionScript = PathResolver.Normalize(expressionScriptPath, true); 

                //read the parameters
                if (doc.Element("Processor").Element("Parameters") != null)
                {
                    metadata.Parameters = new Dictionary<string, string>();
                    foreach (XElement element in doc.Element("Processor").Element("Parameters").Elements("Parameter"))
                    {
                        metadata.Parameters.Add(element.Attribute("name").Value, element.Attribute("value").Value);

                    }
                }
            }
            catch (Exception e)
            {

                logger.Error("Error in parsing Javascript  xml Configuration file : ", e);
                throw new ApplicationException("Error in parsing Javascript xml Configuration file : "+ e.Message);
            }

            return metadata;
        }
    }
}
