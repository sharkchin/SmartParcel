/**
 * 
 * 
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DMP.MasterProgram.Processors;
using Dmp.Neptune.Collections;
using DMP.MasterProgram.ProcessorMetadata;
using DMP.MasterProgram.Utils.Parsers;
using log4net;
using log4net.Config;
using DMP.MasterProgram.Utils.Caching;
using DMP.MasterProgram.Utils;
using DMP.MasterProgram.Utils.Chunking;
using MonoGIS.NetTopologySuite.Geometries;
using DMP.MasterProgram.Processors.AnalysisEngine.Algorithms;
using System.Diagnostics;
using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;

namespace DMP.MasterProgram.Processors.AnalysisEngine
{
    class AnalysisEngine : IRecordProcessor
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private AnalysisEngineMetadata metadata;
        private Dictionary<string, DataSet> cache;
        private Dictionary<string, List<AbstractRecord>> impactors;


        /// <summary>
        /// Process the task
        /// </summary>
        /// <param name="record">Subject record</param>
        /// <param name="task">Analysis Engine Task object </param>
        /// <param name="attributeName">Name of th eattribute to be calculated</param>
        /// <returns></returns>
        private AbstractRecord ProcessTask(AbstractRecord record, AnalysisEngineMetadata.AETask task, string attributeName)
        {
            string taskClass = task.TaskClass;
            List<AbstractRecord> impactorList = null;
            Dictionary<String, String> parameterMap = new Dictionary<String, String>();

            try
            {
                //if record doesnot contain Geometry object don't process it
                if (record[MasterProgramConstants.AE_GEOMETRY] == null)
                {
                   // return null;
                }
                string[] splitClass = taskClass.Split('?');
                IGeometryAlgorithm algorithm = (IGeometryAlgorithm)Activator.CreateInstance(Type.GetType(splitClass[0]));
                if (splitClass.Length > 1)
                {
                    string[] parameters = splitClass[1].Split('&');
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        string[] parameter = parameters[i].Split('=');
                        parameterMap.Add(parameter[0], parameter[1]);


                    }
                }
                algorithm.InitializeParameters(parameterMap);

                object output = null;
                string impactorName = task.ImpactorName;
                string subjectName = task.SubjectName;
                //if subject is calculated from other task
                bool isSubByTask = false;

                //if subject is calculated from other task
                if (metadata.TaskMap.ContainsKey(subjectName))
                {
                    AnalysisEngineMetadata.AETask aet1;
                    metadata.TaskMap.TryGetValue(subjectName, out aet1);
                    ProcessTask(record, aet1, subjectName);
                    record[MasterProgramConstants.TASK_CALCULATED] = record[subjectName];
                    record[subjectName] = null;
                    isSubByTask = true;

                }

                
                if(string.IsNullOrEmpty(impactorName))
                {
                    //do nothing
                }
                    //if impactors are calculated from other task
                else if (metadata.TaskMap.ContainsKey(impactorName))
                {
                    AnalysisEngineMetadata.AETask aet1;
                    metadata.TaskMap.TryGetValue(impactorName, out aet1);
                    record = ProcessTask(record, task, impactorName);
                    impactorList = (List<AbstractRecord>)record[impactorName];
                    record[impactorName] = null;
                }
                //BDE-41
                //if key is present in the cache,get the records from cache or DB
               /* else if (this.metadata.IsImpactorOnDemand)
                {
                    if (this.cache.ContainsKey(impactorName))
                    {
                        impactorList = RequestImpactors(record, impactorName);

                    }
                }*/
                else if (this.cache.ContainsKey(impactorName))
                {
                    impactorList = RequestImpactors(record, impactorName);

                }//BDE-41 ends
                //if impactor is not on demand ,get the impactors already fetched.
                else if (this.impactors.ContainsKey(impactorName))
                {
                    this.impactors.TryGetValue(impactorName, out  impactorList);
                }

                algorithm.InitializeImpactors(impactorList);
                output = algorithm.ProcessRecord(record, isSubByTask);

                algorithm = null;
                impactorList = null;

                if(!string.IsNullOrEmpty(impactorName))
                {
                    record[impactorName] = null;
                
                }
                record[attributeName] = output;
            }
            catch (Exception e)
            {
                logger.Error("Error while performing task :" + taskClass + " to calculate " + attributeName, e);
                throw new ApplicationException("Error while performing task :" + taskClass + " to calculate " + attributeName +": " + e.Message);
            }
            return record;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="record">Subject Record</param>
        /// <param name="impactorName">Name of the impactor</param>
        /// <returns></returns>
        private List<AbstractRecord> RequestImpactors(AbstractRecord record, string impactorName)
        {
            string CFTId = (string)record[MasterProgramConstants.CFTID];

            SqlGeometry geometry = (SqlGeometry)record[MasterProgramConstants.GEOMETRY_BIN];
         
            List<AbstractRecord> impactors =  null;

            try
            {
                DataSet dataSet;
                this.cache.TryGetValue(impactorName, out dataSet);
     
                impactors = dataSet.getByGeom(geometry, -1, -1);

              
            }
            catch(Exception e)
            {
                logger.Error("Error while fetching impactor " + impactorName, e);
                throw new ApplicationException("Error while fetching impactor " + impactorName + ": " + e.Message);
            }

            return impactors;

        }

        /// <summary>
        /// calculate the attribute
        /// </summary>
        /// <param name="record">subject record</param>
        /// <param name="task">Analysis Engine Task object</param>
        /// <param name="attributeName">Name of the Attribute</param>
        /// <returns></returns>
        private AbstractRecord ProcessAttribute(AbstractRecord record, AnalysisEngineMetadata.AETask task, string attributeName)
        {
            return ProcessTask(record, task, attributeName);
        }

        /// <summary>
        /// Process the subject record
        /// </summary>
        /// <param name="record">Subject Record</param>
        /// <returns>processed record/null</returns>
        public override AbstractRecord ProcessRecord(AbstractRecord record)
        {
            if (record == null)
                return null;

            //create an outputRecord
            AbstractRecord outputRecord = new DataRecord(new String[] { });
            try
            {

                //iterate through all the record attributes and calculate those need to be calculated
                foreach (KeyValuePair<String, AnalysisEngineMetadata.AEAttribute> attribute in metadata.AttributeMap)
                {
                    string attributeName = attribute.Key;
                    AnalysisEngineMetadata.AEAttribute aea = attribute.Value;

                    if (aea.IsBaseAttribute)
                    {
                        outputRecord[attributeName] = record[attributeName];
                    }
                    else
                    {
                        string taskName = aea.TaskName;
                        AnalysisEngineMetadata.AETask aet;
                        metadata.TaskMap.TryGetValue(taskName, out aet);
                        //process the record
                        record = ProcessAttribute(record, aet, attributeName);
                        //copy the calculated attribute value into the output record 
                        outputRecord[attributeName] = record[attributeName];

                    }

                }
                record = null;
            }
            catch (Exception e)
            {
                logger.Error("Error while processing subject record in Analysis Engine", e);
                throw new ApplicationException("Error while processing subject record in Analysis Engine: "+ e.Message);
            }

            return outputRecord;

        }

        /// <summary>
        /// Set the Cache Map
        /// </summary>
        /// <param name="cache">cache Map containing ImpactorCache for each Impactor </param>
        public override void SetImpactorCacheMap(Dictionary<string, DataSet> cache)
        {
            this.cache = cache;
        }


        /// <summary>
        /// initialize the Analysis Engine Processor.
        /// </summary>
        /// <param name="xmlString">xml Configuration</param>
        public override void InitializeMetaData(string xmlDOM)
        {
            if (xmlDOM == null || String.IsNullOrEmpty(xmlDOM))
            {
                logger.Error("Error while initializing Analysis Engine Processor: XMLDom containing the metadata is null or empty");
                throw new ApplicationException("Error while initializing Analysis Engine Processor:XMLDom containing the metadata is null or empty");
            }

            try
            {
                GenerateMetaData(xmlDOM);
            }
            catch (Exception e)
            {
                logger.Error("Error while initializing Analysis Engine Processor", e);
                throw new ApplicationException("Error while initializing Analysis Engine Processor: "+ e.Message); 
            }

        }


        /// <summary>
        /// calls the Analysis Engine parser to parse the XML File and create the Analysis Engine Metadata object
        /// </summary>
        /// <param name="xmlString">xml Configuration string</param>
        public void GenerateMetaData(string xmlString)
        {
            try
            {
                AnalysisEngineParser parser = new AnalysisEngineParser(xmlString);
                this.metadata = parser.Parse();
            }
            catch (Exception e)
            {
                logger.Error("Error while generating metadata for Analysis Engine", e);
                throw new ApplicationException("Error while generating metadata for Analysis Engine: " + e.Message);
            }

        }

        /// <summary>
        /// set the Impactor Map
        /// </summary>
        /// <param name="impactors">map containing key as Impactor Name and value as list of Impactor Records</param>
        public override void SetImpactors(Dictionary<string, List<AbstractRecord>> impactors)
        {
            this.impactors = impactors;
        }

        public override void Dispose()
        {
            
        }
    }
}
