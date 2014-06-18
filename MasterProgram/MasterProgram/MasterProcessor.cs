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
using DMP.MasterProgram.ProcessorMetadata;
using DMP.MasterProgram.Utils.Parsers;
using DMP.MasterProgram.Utils;
using DMP.MasterProgram.Processors;
using DMP.MasterProgram.Processors.JavaScript;
using DMP.MasterProgram.Processors.AnalysisEngine;
using Dmp.Neptune.Collections;
using DMP.MasterProgram.Utils.DatabaseManager;
using System.Threading;
using log4net;
using log4net.Config;
using DMP.MasterProgram.Utils.Caching;
using DMP.MasterProgram.Processors.OutputProcessor;



namespace DMP.MasterProgram
{
    public delegate void OnRecordRetrieved(AbstractRecord record, Object lockObj, ManualResetEvent manualResetEvent, ExceptionHandler exceptionHandler);
    //public delegate void OnRecordRetrieved(AbstractRecord record, Object lockObj, ExceptionHandler exceptionHandler);
    class ThreadCounter
    {
        public int NumThreadsCreated { get; set; }
        public int NumThreadsFinished { get; set; }
        public bool LastThreadFinished { get; set; }
    }

    public class ExceptionHandler
    {
        public Exception Exception { get; set; }
        public bool IsExceptionPresent { get; set; }
    }   
  
    public class MasterProcessor
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        
        private MasterProgramMetadata metaData=null;
        private List<IRecordProcessor> processorList=null;
        //contains the cache for each impactor.
        private Dictionary<string, DataSet> impactorCacheMap = null;
        //contains list of impactors recor for each impactor data set
        private Dictionary<string, List<AbstractRecord>> impactorsMap = null;
        private bool toOutput = false;
        private OutputWriter outputWriter = null;
        private bool isInitialized = false;
        private int accountId;
        private int groupId;
        private int userId;
        private string versionId;


        public string ActiveVersionId { get; set; }

        public int NumOfThreads { get; set; }

        // TODO: other parameters such as view in time, showhistory, and what have you.

        public MasterProcessor(string xmlDOM)
        {
            Initialize(xmlDOM);
        }

        /// <summary>
        /// initializes the Master Program
        /// </summary>
        /// <param name="xmlDOM">xml containing teh metadata defination for Master Program</param>
        private void Initialize(string xmlDOM)
        {
            if (isInitialized) return;
            try
            {
                //create the Master Program metadata object by reading the configuration xml
                GenerateMetaData(xmlDOM);


                //BDE-41 : Impactor On Demand Selective
                //if impactors are on demand,create cache for each Impactor Data Set
               /* if (this.metaData.IsImpactorOnDemand)
                {
                    //initializing the ImpactorCacheMap with key as DataSet Name
                    this.impactorCacheMap = new Dictionary<string, DataSet>();
                    Dictionary<string, MasterProgramMetadata.GeometryBuffer> impactorOnDemandBuffers = this.metaData.Subject.ImpactorOnDemandBuffers;

                    foreach (KeyValuePair<String, MasterProgramMetadata.InputDataSet> input in metaData.Impactors)
                    {
                        string dataSetName = input.Key;
                        MasterProgramMetadata.InputDataSet inputDataSet = input.Value;

                        DataSet dataSet = new DataSet(inputDataSet,metaData.UseCustomGeometry);
                        dataSet.MinZoomLevel = inputDataSet.MinZoomLevel;
                        dataSet.MaxZoomLevel = inputDataSet.MaxZoomLevel;

                        if(impactorOnDemandBuffers.ContainsKey(dataSetName))
                        {
                            MasterProgramMetadata.GeometryBuffer buffer;
                            impactorOnDemandBuffers.TryGetValue(dataSetName,out buffer);
                            dataSet.SubjectBuffer = buffer.BufferValue;
                            dataSet.SubjectBufferUnit = buffer.BufferUnit;
                        }

                        impactorCacheMap.Add(dataSetName, dataSet);
                    }
                }
                else
                {
                    //fetch all the impactors
                    this.impactorsMap = RequestImpactors();
                }*/

                InitializeImpactors();
               
                //BDE-41 ends

                //create and initialize all the Processors mentioned in the XML

                Dictionary<string,string> processors = metaData.Processors;   
                this.processorList = new List<IRecordProcessor>();

                MasterProgramParser parser = new MasterProgramParser(xmlDOM);

                foreach(KeyValuePair<string,string> processor in processors)
                {
                    string className = processor.Key;
                    string xmlString = processor.Value;
                    //create an instance of Processor
                    IRecordProcessor proc = (IRecordProcessor)Activator.CreateInstance(Type.GetType(className));
                    if (proc.isOutputProcessor)
                    {
                        toOutput = true;
                        outputWriter =(OutputWriter) proc;
                        outputWriter.InitializeMetaData(xmlString);                       
                        continue;
                    }
                    //intialize metadata
                    proc.InitializeMetaData(xmlString);
                    proc.SetImpactorCacheMap(this.impactorCacheMap);
                    proc.SetImpactors(this.impactorsMap);
                    processorList.Add(proc);               
                }

            }
            catch (Exception e)
            {
                logger.Error("MasterProcessor.Initialize: Error while initializing Master Program", e);
                throw new ApplicationException("Error while initializing Master Program: "+e.Message);
            }
            isInitialized = true;
        }

        /// <summary>
        /// calls the MasterProgramm parser to parse the XML File and create the Master Program Metadata object
        /// </summary>
        /// <param name="xmlDOM">xmlDOM</param>
        private void GenerateMetaData(string xmlDOM)
        {
            try
            {
                //parse the file(absolute File Path)
                MasterProgramParser parser = new MasterProgramParser(xmlDOM);
                //parse 
                this.metaData = parser.Parse();
            }
            catch (Exception e)
            {
                logger.Error("MasterProcessor.GenerateMetaData Error while generating metadata for Master Program", e);
                throw new ApplicationException("Error while generating metadata for Master Program : " + e.Message);
            }
        }
    
        /// <summary>
        /// fetch all the impactors data from the database
        /// DEPRECATED
        /// </summary>
        /// <returns>map of impactors with key as impactor data set Name and value as Record List</returns>
        private Dictionary<string, List<AbstractRecord>> RequestImpactors()
        {
            string dataSetName = null;
            List<AbstractRecord> recordList = null;
            MasterProgDBManager dbManager;
            Dictionary<string, List<AbstractRecord>> impactorsMap = new Dictionary<string, List<AbstractRecord>>();

            try
            {
                //for each impactor ,fetch the list of records and stored in a map with key as input data set name
                foreach (KeyValuePair<String, MasterProgramMetadata.InputDataSet> input in this.metaData.Impactors)
                {
                    dataSetName = input.Key;

                    MasterProgramMetadata.InputDataSet impactorDataSet = input.Value;
                    if (impactorDataSet == null)
                    {
                        logger.Error("Error while fetching Impactor data :Impactor DataSets for " + dataSetName + " is not defined in the XML");
                        throw new ApplicationException("Error while fetching Impactor data :Impactor DataSets for " + dataSetName + " is not defined in the XML");
                    }

                    //fetch all the impactor Data
                    if ("DOFS".Equals(impactorDataSet.StorageType))
                    {
                        dbManager = new SQLiteManager();
                        recordList = dbManager.FetchRecords(impactorDataSet);
                        impactorsMap.Add(dataSetName, recordList);
                    }
                    else if ("DB".Equals(impactorDataSet.StorageType))
                    {
                        dbManager = new SQLManager(impactorDataSet.Database);
                        ((SQLManager)dbManager).ActiveVersionId = this.ActiveVersionId;
                        recordList = dbManager.FetchRecords(impactorDataSet);
                        impactorsMap.Add(dataSetName, recordList);
                    }
                    else
                    {
                        logger.Error("Storage Type is different from DOFS or DB");
                        throw new ApplicationException("Storage Type is different from DOFS or DB");
                    }
                }
            
            }
            catch (Exception e)
            {
                logger.Error("Error while fetching Impactor Data for " + dataSetName, e);
                throw new ApplicationException("Error while fetching Impactor Data for " + dataSetName +" : "+ e.Message);
            }

            return impactorsMap;
        }


        //BDE-41
        private void InitializeImpactors()
        {

            this.impactorCacheMap = new Dictionary<string, DataSet>();
            Dictionary<string, MasterProgramMetadata.GeometryBuffer> impactorOnDemandBuffers = this.metaData.Subject.ImpactorOnDemandBuffers;
            MasterProgDBManager dbManager;
            this.impactorsMap = new Dictionary<string, List<AbstractRecord>>();
            foreach (KeyValuePair<String, MasterProgramMetadata.InputDataSet> input in metaData.Impactors)
            {

                string dataSetName = input.Key;
                MasterProgramMetadata.InputDataSet inputDataSet = input.Value;
                bool isOnDemand = inputDataSet.IsOnDemand;

                if (isOnDemand)
                {
                    DataSet dataSet = new DataSet(inputDataSet);
                    dataSet.MinZoomLevel = inputDataSet.MinZoomLevel;
                    dataSet.MaxZoomLevel = inputDataSet.MaxZoomLevel;

                    if (impactorOnDemandBuffers.ContainsKey(dataSetName))
                    {
                        MasterProgramMetadata.GeometryBuffer buffer;
                        impactorOnDemandBuffers.TryGetValue(dataSetName, out buffer);
                        dataSet.SubjectBuffer = buffer.BufferValue;
                        dataSet.SubjectBufferUnit = buffer.BufferUnit;
                    }

                    impactorCacheMap.Add(dataSetName, dataSet);

                }
                else
                {
                    List<AbstractRecord> recordList = null;
                    if (inputDataSet == null)
                    {
                        logger.Error("Error while fetching Impactor data :Impactor DataSets for " + dataSetName + " is not defined in the XML");
                        throw new ApplicationException("Error while fetching Impactor data :Impactor DataSets for " + dataSetName + " is not defined in the XML");
                    }

                    //fetch all the impactor Data
                    if ("DOFS".Equals(inputDataSet.StorageType))
                    {
                        dbManager = new SQLiteManager();
                        recordList = dbManager.FetchRecords(inputDataSet);
                        impactorsMap.Add(dataSetName, recordList);
                    }
                    else if ("DB".Equals(inputDataSet.StorageType))
                    {
                        dbManager = new SQLManager(inputDataSet.Database);
                        ((SQLManager)dbManager).ActiveVersionId = this.ActiveVersionId;
                        recordList = dbManager.FetchRecords(inputDataSet);
                        impactorsMap.Add(dataSetName, recordList);
                    }
                    else
                    {
                        logger.Error("Storage Type is different from DOFS or DB");
                        throw new ApplicationException("Storage Type is different from DOFS or DB");
                    }

                }

            }

        }
        //BDE-41 ends

        public bool ProcessRecords()
        {
            return ProcessRecords(null, -1, -1, -1, null);
        }

        /// <summary>
        /// Entry Point to the Master Program
        /// </summary>
        /// <param name="xmlDOM">xml containing the metadata of the MasterProgram</param>
        public bool ProcessRecords(string xmlDOM)
        {
            return ProcessRecords(xmlDOM, -1, -1, -1, null);
        }


        public bool ProcessRecords(string xmlDOM,int accountId, int groupId, int userId, string versionId)
        {
            bool result = false;

            this.accountId = accountId;
            this.groupId = groupId;
            this.userId = userId;
            this.versionId = versionId;

            this.outputWriter.AccountId = accountId;
            this.outputWriter.GroupId = groupId;
            this.outputWriter.UserId = userId;
            this.outputWriter.VersionId = versionId;

            if (!isInitialized)
            {
                if (xmlDOM == null || String.IsNullOrEmpty(xmlDOM))
                {
                    logger.Error("GenerateMetaData.ProcessRecords : Error in MasterProcessor.processRecords: XMLDom containing the metadata for MasterProgram is null or empty");
                    throw new ApplicationException("XMLDom containing the metadata for MasterProgram is null or empty");
                }
            }


            try
            {
                //Initialize the MasterProgram.
                Initialize(xmlDOM);

                ThreadCounter counter = new ThreadCounter();
                counter.NumThreadsCreated = 0;
                counter.NumThreadsFinished = 0;
                counter.LastThreadFinished = false;

                Semaphore semaphore = new Semaphore(NumOfThreads,NumOfThreads);

                result = ProcessRecords(GetRecordRetrievedCallback(counter, semaphore));
            }
            catch (ThreadAbortException ex)
            {
                

            }
            catch (Exception e)
            {
                logger.Error("GenerateMetaData.ProcessRecords : Error in the Master Program while performing analysis", e);
                Console.Write(e.Message);
                throw new ApplicationException("Error in the Master Program while performing analysis: " + e.Message + " Trace: " + e);

            }
            finally
            {
                foreach (var proc in processorList)                                    
                  proc.Dispose();                
            }
            return result;

        }

        private bool ProcessRecords(OnRecordRetrieved recordRetrievedCallback)
        {
            bool result = true;
            List<AbstractRecord> records = null;
            try
            {
                MasterProgramMetadata.InputDataSet subjectDataSet = metaData.Subject;


                if (subjectDataSet == null)
                {
                    logger.Error("GenerateMetaData.ProcessRecords: Subject DataSets is not defined in the XML");
                    throw new ApplicationException("Subject DataSet is not defined in the XML");
                }

                if ("DOFS".Equals(subjectDataSet.StorageType))
                {
                    SQLiteManager dbManager = new SQLiteManager();
                    //fetch all the subject Data
                    records = dbManager.FetchAndProcessRecords(subjectDataSet, recordRetrievedCallback);
                   
                }
                else if ("DB".Equals(subjectDataSet.StorageType))
                {
                    SQLManager dbManager = new SQLManager(subjectDataSet.Database);
                    dbManager.ActiveVersionId = this.ActiveVersionId;
                    records = dbManager.FetchAndProcessRecords(subjectDataSet, recordRetrievedCallback);
                    
                }
                else
                {
                    logger.Error("GenerateMetaData.ProcessRecords: Storage Type is different from DOFS or DB");
                    throw new ApplicationException("Storage Type for subject data is different from DOFS or DB");
                }


            }
            catch (Exception e)
            {
                logger.Error(e.Message);
                throw e;
            }

            return result;

        }

        private OnRecordRetrieved GetRecordRetrievedCallback(ThreadCounter counter, Semaphore semaphore)
        {
            int numRecordsProcessed = 0;

            int batchSize = this.metaData.OutputBatchSize;
            
            bool result = false;

            Queue<RecordProcessorThread> threadList = new Queue<RecordProcessorThread>();
            List<AbstractRecord> outputRecordsList = new List<AbstractRecord>(this.metaData.OutputBatchSize);


            return (record, lockObj, manualResetEvent, exceptionHandler) =>
                {
                      if (record != null)
                        {
                            semaphore.WaitOne();
                            counter.NumThreadsCreated++;
                            RecordProcessorThread recordProc = new RecordProcessorThread(processorList, record);

                            ThreadPool.QueueUserWorkItem(
                                (state) =>
                                {
                                   

                                    try
                                    {
                                        recordProc.ProcessRecord();
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.Error("MasterProcessor:GetRecordRetrievedCallback:-Error while processing record");
                                        exceptionHandler.IsExceptionPresent = true;
                                        exceptionHandler.Exception = ex;
                                    }
                                    finally
                                    {
                                        semaphore.Release();
                                        lock (lockObj)
                                        {
                                            manualResetEvent.Set();

                                            numRecordsProcessed++;
                                            counter.NumThreadsFinished++;
                                            threadList.Enqueue(recordProc);
                                            if (numRecordsProcessed == batchSize)
                                            {
                                                while (threadList.Count != 0)
                                                {
                                                    RecordProcessorThread outputRecordProc = threadList.Dequeue();

                                                    //Not output the record if it is null
                                                    if (outputRecordProc.outputRecord != null && outputRecordProc.result)
                                                    {
                                                        outputRecordsList.Add(outputRecordProc.outputRecord);
                                                    }

                                                    outputRecordProc.outputRecord = null;
                                                    outputRecordProc.record = null;
                                                    outputRecordProc = null;


                                                }

                                                //if the OuputProcessor is mentioned in the XML File to output result into the database
                                                if (this.toOutput)
                                                {
                                                    try
                                                    {
                                                        if (outputRecordsList.Count > 0)
                                                        {
                                                            result = outputWriter.ProcessRecords(outputRecordsList);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        exceptionHandler.IsExceptionPresent = true;
                                                        exceptionHandler.Exception = ex;
                                                    }
                                                }
                                                outputRecordsList = null;
                                                outputRecordsList = new List<AbstractRecord>(this.metaData.OutputBatchSize);

                                                numRecordsProcessed = 0;
                                            }



                                        } // lock
                                    } // finally



                                    if (counter.NumThreadsFinished == counter.NumThreadsCreated &&
                                        counter.LastThreadFinished)
                                    {
                                        while (threadList.Count != 0)
                                        {
                                            RecordProcessorThread outputRecordProc = threadList.Dequeue();

                                            //Not output the record if it is null
                                            if (outputRecordProc.outputRecord != null && outputRecordProc.result)
                                            {
                                                outputRecordsList.Add(outputRecordProc.outputRecord);
                                            }

                                            outputRecordProc.outputRecord = null;
                                            outputRecordProc.record = null;
                                            outputRecordProc = null;
                                        }

                                        //if the OuputProcessor is mentioned in the XML File to output result into the database
                                        if (this.toOutput)
                                        {
                                            try
                                            {
                                                if (outputRecordsList.Count > 0)
                                                {
                                                    result = outputWriter.ProcessRecords(outputRecordsList);
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                exceptionHandler.IsExceptionPresent = true;
                                                exceptionHandler.Exception = ex;
                                            }
                                        }

                                        outputRecordsList = null;
                                        outputRecordsList = new List<AbstractRecord>(this.metaData.OutputBatchSize);

                                        lock (lockObj)
                                            Monitor.Pulse(lockObj);


                                    }


                                }, null);

                        } // record != null
                        else
                        {
                            lock (lockObj)
                            {
                                counter.LastThreadFinished = true;
                                if (counter.NumThreadsFinished == counter.NumThreadsCreated &&
                                            counter.LastThreadFinished)
                                {

                                    while (threadList.Count != 0)
                                    {
                                        RecordProcessorThread outputRecordProc = threadList.Dequeue();

                                        //Not output the record if it is null
                                        if (outputRecordProc.outputRecord != null && outputRecordProc.result)
                                        {
                                            outputRecordsList.Add(outputRecordProc.outputRecord);
                                        }

                                        outputRecordProc.outputRecord = null;
                                        outputRecordProc.record = null;
                                        outputRecordProc = null;
                                    }

                                    //if the OuputProcessor is mentioned in the XML File to output result into the database
                                    if (this.toOutput)
                                    {
                                        try
                                        {
                                            if (outputRecordsList.Count > 0)
                                            {
                                                result = outputWriter.ProcessRecords(outputRecordsList);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            exceptionHandler.IsExceptionPresent = true;
                                            exceptionHandler.Exception = ex;
                                        }
                                    }

                                    outputRecordsList = null;
                                    outputRecordsList = new List<AbstractRecord>(this.metaData.OutputBatchSize);

                                    Monitor.Pulse(lockObj);
                                }

                            } // lock
                        } // record == null
                    


                }; // delegate;
        }


    }
}
