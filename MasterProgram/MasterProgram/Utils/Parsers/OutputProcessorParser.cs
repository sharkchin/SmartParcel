using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using log4net;
using log4net.Config;
using DMP.MasterProgram.ProcessorMetadata;
using Dmp.Neptune.Utils.ShapeFile;

namespace DMP.MasterProgram.Utils.Parsers
{
    class OutputProcessorParser
    {
        private XDocument doc;
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


         /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="xmlDOM">XML DOM</param>
        public OutputProcessorParser(string xmlDOM)
        {
            if (xmlDOM == null || String.IsNullOrEmpty(xmlDOM))
            {
                logger.Error("Error in the MasterProgramParser constructor: XMLDom containing the metadata is null or empty");
                throw new ApplicationException("Error in the MasterProgramParser constructor:XMLDom containing the metadata is null or empty");
            }

            //load the xml file
            try
            {
                doc = XDocument.Parse(xmlDOM);
            }
            catch(Exception e)
            {
                logger.Error("Error while initializing XDocument  in OutputProcessorParser ",e);
                throw new ApplicationException("Error while initializing XDocument  in OutputProcessorParser: "+e.Message);
            }
        }

        public OutputProcessorMetadata Parse()
        {
            OutputProcessorMetadata metadata = new OutputProcessorMetadata();

            try
            {
                metadata.LayerMetadataXml = doc.Element("Processor").Element("Layer");
                metadata.StorageType = doc.Element("Processor").Element("Layer").Attribute("StorageType").Value;
                
                //read the  attributes
                foreach (XElement element in doc.Element("Processor").Element("Layer").Element("Schema").Element("ElementType").Elements("AttributeType"))
                {
                    metadata.Attributes.Add(element.Attribute("name").Value, element.Element("Datatype").Attribute("type").Value);
                }

                if ("DOFS".Equals(metadata.StorageType))
                {
                    if (doc.Element("Processor").Element("Layer").Element("DataPath") != null)
                    {
                        string dataPath = doc.Element("Processor").Element("Layer").Element("DataPath").Value;
                        string tableName = System.IO.Path.GetFileName(dataPath);
                        ShapeVersioner sv = new ShapeVersioner(dataPath);
                        dataPath = sv.CreateNewVersionFolder();
                        sv.WriteVersionPendingFile();
                        metadata.Database = dataPath + "/" + tableName + ".s3db";
                        metadata.TableName = tableName;
                        metadata.DataPath = dataPath;
                    }
                }
                else if ("DB".Equals(metadata.StorageType))
                {

                    metadata.TableName = doc.Element("Processor").Element("Layer").Element("TableName").Value;
                    metadata.Database = doc.Element("Processor").Element("Layer").Element("DatabaseName").Value;
                }

                List<string> indexedFields = new List<string>();
                if (doc.Element("Processor").Element("Layer").Element("IndexedFields") != null)
                {
                    indexedFields.Add(doc.Element("Processor").Element("Layer").Element("IndexedFields").Value);
                    metadata.IndexedFields = indexedFields.ToArray();
                }

            }
            catch(Exception e)
            {
                logger.Error("Error in parsing OutputProcessor xml Configuration  : ", e);
                throw new ApplicationException("Error in parsing OutputProcessor xml Configuration  : "+e.Message);

            }


            return metadata;
        }

        private string GetShapeFileDirectory(string shapeDataRootPath, bool createNewVersion)
        {
            string dbfName = shapeDataRootPath;

            // version that resource!
            ShapeVersioner sv = new ShapeVersioner(dbfName);
            if (createNewVersion)
                dbfName = sv.CreateNewVersionFolder();
            else
                dbfName = sv.GetCurrentVersionFolder();

            return dbfName;

        }
    }
}
