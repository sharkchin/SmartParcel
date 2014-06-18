using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DMP.MasterProgram.Processors.OutputProcessor;
using Dmp.Neptune.Collections;
using DMP.MasterProgram.Utils.Caching;
using log4net;
using log4net.Config;
using DMP.MasterProgram.Utils.Parsers;
using DMP.MasterProgram.ProcessorMetadata;
using DMP.MasterProgram.Utils.DatabaseManager;
using DMP.MasterProgram.Utils;
using Dmp.Neptune.DataLoader;

namespace DMP.MasterProgram.Processors.OutputProcessor
{
    class OutputWriter :IRecordProcessor
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private OutputProcessorMetadata metadata;
        private int accountId;
        private int groupId;
        private int userId;
        private string versionId;

        private DataLoaderUtils dlu = null;

        public int AccountId 
        {
            get
            {
                return this.accountId;
            }
            set
            {
                this.accountId = value;
            }
        }

        public int GroupId 
        {
            get
            {
                return this.groupId;
            }
            set
            {
                this.groupId = value;
            }
        }

        public int UserId 
        {
            get
            {
                return this.userId;
            }
            set
            {
                this.userId = value;
            }
        }

        public string VersionId 
        {
            get
            {
                return this.versionId;
            }
            set
            {
                this.versionId = value;
            }
        }

        public OutputWriter()
        {
            this.isOutputProcessor = true;
        }

        /// <summary>
        /// Interface Method.Not used here
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public override AbstractRecord ProcessRecord(AbstractRecord record)
        {
            return null;
        }

        /// <summary>
        /// This method store the result into the database
        /// </summary>
        /// <param name="recordsList">List of Records</param>
        /// <returns>true/false based on output is succesful or not</returns>
        public  bool ProcessRecords(List<AbstractRecord> recordsList)
        {
            MasterProgDBManager dbManager = null;
            bool result = false;

            if ("DOFS".Equals(metadata.StorageType))
            {
                dbManager = new SQLiteManager();
               
            }
            else if("DB".Equals(metadata.StorageType))
            {
                dbManager = new SQLManager(metadata.Database);
                    
            }

            try
            {

                if ("DOFS".Equals(metadata.StorageType))
                {
                    if (!string.IsNullOrEmpty(metadata.DataPath))
                    {
                        result = dbManager.PopulateRecords(recordsList, metadata.Database, metadata.TableName, metadata.Attributes, metadata.IndexedFields, false);
                        Dmp.Neptune.Utils.ShapeFile.ShapeVersioner sv = new Dmp.Neptune.Utils.ShapeFile.ShapeVersioner(metadata.DataPath);
                        Dmp.Neptune.Utils.DataPublisher.CreatePublishStatusFile(System.IO.Directory.GetParent(metadata.DataPath).ToString(), DateTime.Now);
                        metadata.LayerMetadataXml.Save(System.IO.Path.Combine(metadata.DataPath, metadata.TableName + ".xml"));
                    }

                }
                else if ("DB".Equals(metadata.StorageType))
                {
                    if (dlu == null)
                    {
                        if (accountId == -1 || groupId == -1 || userId == -1 || versionId == null)
                            throw new Exception("Account Information is not correct.Please provide correct user info to populate data into SQL table");
                        dlu = new DataLoaderUtils(accountId, groupId, userId, versionId);
                    }
                    result = dbManager.PopulateRecords(recordsList, metadata.Database, metadata.TableName, metadata.Attributes, dlu);
                }

            }
            catch (Exception e)
            {
                logger.Error("Error while populating records into the database", e);
                throw e;
            }
            finally
            {
                dbManager = null;
            }

            return result;
        }



        /// <summary>
        /// initialize the OutputWriter Processor.
        /// </summary>
        /// <param name="xmlString">xml Configuration</param>
        public override  void InitializeMetaData(string xmlDOM)
        {
            if (xmlDOM == null || String.IsNullOrEmpty(xmlDOM))
            {
                logger.Error("Error while initializing OutputWriter Processor: XMLDom containing the metadata is null or empty");
                throw new ApplicationException("Error while initializing OutputWriter Processor:XMLDom containing the metadata is null or empty");
            }

            try
            {
                GenerateMetaData(xmlDOM);
                
            }
            catch (Exception e)
            {
                logger.Error("Error while initializing OutputWriter Processor", e);
                throw new ApplicationException("Error while initializing OutputWriter Processor: " + e.Message);
            }
        }

        /// <summary>
        /// calls the Analysis Engine parser to parse the XML File and create the Output Processor Metadata object
        /// </summary>
        /// <param name="xmlString">xml Configuration string</param>
        public  void GenerateMetaData(string xmlString)
        {
            try
            {
                OutputProcessorParser parser = new OutputProcessorParser(xmlString);
                this.metadata = parser.Parse();
            }
            catch (Exception e)
            {
                logger.Error("Error while generating metadata for Analysis Engine", e);
                throw new ApplicationException("Error while generating metadata for Analysis Engine: " + e.Message);
            }

        }

        /// <summary>
        /// Interface Method,Do Nothing here
        /// </summary>
        /// <param name="cache"></param>
        public override void SetImpactorCacheMap(Dictionary<string, DataSet> cache)
        {

        }

        /// <summary>
        /// Interface Method,Do Nothing here
        /// </summary>
        /// <param name="impactors"></param>
        public override void SetImpactors(Dictionary<string, List<AbstractRecord>> impactors)
        {

        }
        public override void Dispose()
        {            
        }
    }
}
