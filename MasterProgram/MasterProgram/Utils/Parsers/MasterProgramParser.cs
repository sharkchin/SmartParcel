/**
 * 
 * 
 * 
 * 
 */
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
    public class MasterProgramParser 
    {
        
        private XDocument doc;
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="xmlDOM">XML DOM</param>
        public MasterProgramParser(string xmlDOM)
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
                logger.Error("Error while initializing XDocument  in MasterProgramParser ",e);
                throw new ApplicationException("Error while initializing XDocument  in MasterProgramParser: "+e.Message);
            }
        }


        /// <summary>
        /// parse Configuration XML and create MasterProgram Metadata object.
        /// </summary>
        /// <returns>Master Program Metadata object</returns>
        public MasterProgramMetadata Parse()
        {
            MasterProgramMetadata metadata = new MasterProgramMetadata();

            try
            {
                //read the processor classes
                foreach (XElement processor in doc.Element("MasterProgram").Element("Processors").Elements("Processor"))
                {
                    metadata.Processors.Add((string)processor.Attribute("class").Value,processor.ToString());

                }
       
                //read the DataSet
                foreach (XElement dataSet in doc.Element("MasterProgram").Element("DataSets").Elements("DataSet"))
                {
                    MasterProgramMetadata.InputDataSet inputDataSet = new MasterProgramMetadata.InputDataSet();
    
                    inputDataSet.TableName = dataSet.Attribute("tableName").Value;
                    inputDataSet.StorageType = dataSet.Attribute("storageType").Value;
                    if ("DOFS".Equals(inputDataSet.StorageType))
                    {
                        string dbName = dataSet.Attribute("dbResource").Value;
                        ShapeVersioner sv = new ShapeVersioner(dbName);
                        dbName = sv.GetCurrentVersionFolder();
                        dbName = dbName + "/" + inputDataSet.TableName + ".s3db";
                        inputDataSet.Database = dbName;
                    }
                    else
                    {
                        inputDataSet.Database = dataSet.Attribute("dbResource").Value;
                    }
                    

                    
                    if (dataSet.Attribute("baseResource") != null)
                    {

                        inputDataSet.IsBaseResource = Convert.ToBoolean(dataSet.Attribute("baseResource").Value);
                    }

                    //BDE-41 : Impactor On Demand Selective
                    if (dataSet.Attribute("onDemand") != null)
                    {
                        inputDataSet.IsOnDemand = Convert.ToBoolean(dataSet.Attribute("onDemand").Value);

                    }

                    
                   
                    if (dataSet.Element("Filter")!=null)
                    {
                        if (dataSet.Element("Filter").Element("AttributeCriteria") != null)
                        {
                            inputDataSet.AttributeCriteria = dataSet.Element("Filter").Element("AttributeCriteria").Value;
                        }

                        //if filter contain Inclusion Geometries
                        if (dataSet.Element("Filter").Element("InclusionGeometries") != null)
                        {
                            XElement inclusionGeometry = dataSet.Element("Filter").Element("InclusionGeometries");
                            if (inclusionGeometry.Element("Wkt") != null)
                            {
                                inputDataSet.InclusionWKT = inclusionGeometry.Element("Wkt").Value;
                            }

                        }

                        //if filter contain exclusion Geometries
                        if (dataSet.Element("Filter").Element("ExclusionGeometries") != null)
                        {
                            XElement exclusionGeometry = dataSet.Element("Filter").Element("ExclusionGeometries");
                            if (exclusionGeometry.Element("Wkt") != null)
                            {
                                inputDataSet.ExclusionWKT = exclusionGeometry.Element("Wkt").Value;
                            }

                        }

                    }
                    inputDataSet.Fields = new List<string>();

                    foreach (XElement field in dataSet.Element("Fields").Elements("Field"))
                    {
                        inputDataSet.Fields.Add(field.Value);

                    }

                    if (!inputDataSet.IsBaseResource)
                    {
                        XElement tileSize = dataSet.Element("ProcessingTileSize");
                        if (tileSize != null)
                        {
                            if(tileSize.Attribute("minZoomLevel")!=null)
                            {
                                inputDataSet.MinZoomLevel = Convert.ToInt32(tileSize.Attribute("minZoomLevel").Value);
                            }

                            if (tileSize.Attribute("maxZoomLevel") != null)
                            {
                                inputDataSet.MaxZoomLevel = Convert.ToInt32(tileSize.Attribute("maxZoomLevel").Value);
                            }
                        }

                    }


                    //If its a baseRsource(Subject)
                    if (inputDataSet.IsBaseResource)
                    {
                        inputDataSet.ImpactorOnDemandBuffers = new Dictionary<string, MasterProgramMetadata.GeometryBuffer>();
                        if (dataSet.Element("SearchToleranceOnImpactors") != null)
                        {
                            foreach (XElement buffer in dataSet.Element("SearchToleranceOnImpactors").Elements("SearchToleranceOnImpactor"))
                            {
                                MasterProgramMetadata.GeometryBuffer b = new MasterProgramMetadata.GeometryBuffer();

                                b.BufferUnit = buffer.Attribute("unit").Value;
                                b.BufferValue = Convert.ToDouble(buffer.Value);

                                inputDataSet.ImpactorOnDemandBuffers.Add(buffer.Attribute("dataSet").Value, b);
                            }
                        }

                        metadata.Subject = inputDataSet;
                    }
                        //else impactor
                    else
                    {
                        metadata.Impactors.Add(dataSet.Attribute("name").Value, inputDataSet);
                    }
                }


                //read the parameters
                if (doc.Element("MasterProgram").Element("Parameters") != null)
                {
                    foreach (XElement element in doc.Element("MasterProgram").Element("Parameters").Elements("Parameter"))
                    {
                        //BDE-41
                        /*if ((element.Attribute("name").Value).Equals(MasterProgramConstants.IS_IMPACTORS_ON_DEMAND))
                        {
                            metadata.IsImpactorOnDemand = Convert.ToBoolean(element.Attribute("value").Value);
                        }
                        else */if ((element.Attribute("name").Value).Equals(MasterProgramConstants.OUTPUT_BATCH_SIZE))
                        {
                            metadata.OutputBatchSize = Convert.ToInt32(element.Attribute("value").Value);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error("Error in parsing Master Program xml Configuration file : ", e);
                throw new ApplicationException("Error in parsing Master Program xml Configuration file : "+e.Message);
            }   

            return metadata;
        }

    }
}
