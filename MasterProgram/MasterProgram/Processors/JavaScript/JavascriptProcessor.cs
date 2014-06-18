/**
 * CONFIDENTIAL AND PROPRIETARY
 * This document is confidential and contains proprietary information.
 * Neither this document nor any of the information contained herein
 * may be reproduced or disclosed to any person under any circumstances
 * without the express written consent of Digital Map Products.
 *
 * Copyright:    Copyright (c) 2012
 * Company:      Digital Map Products
 * @author       Chris
 * @version      1.0
 *
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dmp.Neptune.Data;
using Noesis.Javascript;
//using ServiceStack.Text;
using Dmp.Neptune.Collections;
using DMP.MasterProgram.Utils;
using DMP.MasterProgram.Processors;
using DMP.MasterProgram.Utils.Caching;
using DMP.MasterProgram.Utils.Parsers;
using DMP.MasterProgram.ProcessorMetadata;

namespace DMP.MasterProgram.Processors.JavaScript
{
    public class JavascriptProcessor : IRecordProcessor
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private JavascriptContext v8;
        private Object jsLock = new Object();
        private JavascriptProcessorMetadata metadata;

        /// <summary>
        /// Constructs an instance of the JavaScript record processor. Th
        /// </summary>
        /// <param name="jsFile">Disk path to the JavaScript to load</param>
        /// <param name="parameters">An dictionary of parameters that will be passed in to the JavaScript (or null)</param>
        private void Initialize()
        {
            String jsFile = this.metadata.ExpressionScript;
            IDictionary<String, String> parameters = this.metadata.Parameters;
            String script = File.ReadAllText(jsFile);

            try
            {
                //Create our JavaScript object
                v8 = new JavascriptContext();
                v8.Run(script);

                //Setup our JavaScript parameters
                if (parameters != null)
                {
                    v8.SetParameter("_parameters", parameters);
                }
                else
                {
                    v8.Run("_parameters = new Object();");
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error in loading JavaScript", ex);
                throw new ApplicationException("Error in loading JavaScript: " + ex);
            }
        }

        /// <summary>
        /// Runs a record through the processRecord JavaScript function.
        /// </summary>
        /// <param name="record">Input record object</param>
        /// <returns>The mutated record or null to remove this record from the stream</returns>
        public override AbstractRecord ProcessRecord(AbstractRecord record)
        {
            //No record in = no record out
            if (record == null)
            {
                return null;
            }

            //Handles breaking up our AbstractRecord object into something that can be easily
            //serailized into JSON
            IDictionary<String, Object> recToSerialize = ConvertToInternalDict(record);

            try
            {
                Object outputRec = null;
                IDictionary<String, Object> newRecord = null;

                //Although V8 has it's own thread locking we lock here to ensure that none of our
                //variables get overwritten
                lock (jsLock)
                {
                    v8.SetParameter("_record", recToSerialize);
                    outputRec =  v8.Run("processRecord(_record);");
                    
                }

                //Validate that the JavaScript actually outputted a record-like JSON object
                if (outputRec is IDictionary<String, Object>)
                {
                    newRecord = (IDictionary<string, object>)outputRec;
                }
                //If we didn't output JSON did we atleast return nothing?
                else if (outputRec == null)
                {
                    return null;
                }
                else
                {
                    throw new InvalidOperationException("Only an associative array or null can be returned from processRecord method");
                }

                //Go through all the fields in this record that will need to be converted back into an AbstractRecord
                AbstractRecord newDataRecord = new DataRecord(new String[] { });
                String[] keys = newRecord.Keys.ToArray();
                foreach (String key in keys)
                {
                    //Convert object arrays to AbstractRecord arrays
                    if (newRecord[key] is Object[])
                    {
                        newDataRecord[key] = ConvertToRecordArray((Object[])newRecord[key]);
                    }
                    //Convert integers to doubles
                    else if (newRecord[key] is int)
                    {
                        newDataRecord[key] = Convert.ToDouble(newRecord[key]);
                    }
                    //Convert everything to DMP Date Time so that the time part is stored/displayed
                    else if (newRecord[key] is DateTime)
                    {
                        newDataRecord[key] = new DmpDateTime((DateTime)newRecord[key]);
                    }
                    //Otherwise leave the type as is
                    else
                    {
                        newDataRecord[key] = newRecord[key];
                    }
                }
                return newDataRecord;
            }
            catch (Exception ex)
            {
                //If something breaks throw it back
                //logger.Error("Error in JavaScript processor (" + JsonSerializer.SerializeToString(recToSerialize) + ")", ex);
                //throw new ApplicationException("Error in JavaScript processor (" + JsonSerializer.SerializeToString(recToSerialize) + "): " + ex.Message);
                throw new ApplicationException("Error in JavaScript processor " + ex.Message);
            }
        }

        /// <summary>
        /// Handles converting any JSON Object arrays back to AbstractRecord arrays
        /// </summary>
        private AbstractRecord[] ConvertToRecordArray(Object[] objArray)
        {
            List<AbstractRecord> newRecords = new List<AbstractRecord>();

            //Go through all the records
            for (int i = 0; i < objArray.Length; i++)
            {
                //New record object
                AbstractRecord newDataRec = new DataRecord(new String[] { });

                //Verify that we don't have some funky data structure - if we do we'll ignore this
                if (objArray[i] is IDictionary<String, Object>)
                {
                    //Now go through all the fields in this record
                    IDictionary<String, Object> newRecord = (IDictionary<String, Object>)objArray[i];
                    String[] keys = newRecord.Keys.ToArray();
                    foreach (String key in keys)
                    {
                        //Convert object arrays to AbstractRecord arrays
                        if (newRecord[key] is Object[])
                        {
                            newDataRec[key] = ConvertToRecordArray((Object[])newRecord[key]);
                        }
                        //Convert integers to doubles
                        else if (newRecord[key] is int)
                        {
                            newDataRec[key] = Convert.ToDouble(newRecord[key]);
                        }
                        //Convert everything to DMP Date Time so that the time part is stored/displayed
                        else if (newRecord[key] is DateTime)
                        {
                            newDataRec[key] = new DmpDateTime((DateTime)newRecord[key]);
                        }
                        //Otherwise leave the type as is
                        else
                        {
                            newDataRec[key] = newRecord[key];
                        }
                    }
                }

                //If we actually added something to this record then add it to the list
                if (newDataRec.Fields.Length > 0)
                {
                    newRecords.Add(newDataRec);
                }
            }

            return newRecords.ToArray();
        }

        /// <summary>
        /// Takes care of converting our AbstractRecord objects to something that can be easily serialized into JSON
        /// </summary>
        private IDictionary<String, Object> ConvertToInternalDict(AbstractRecord rec)
        {
            //Get the internal dictionary for our record then clone it
            IDictionary<String, Object> recDict = rec.GetInternalDictionary();
            IDictionary<String, Object> newRec = new Dictionary<String, Object>(recDict);

            //Go through our record columns for anything that needs to be converted
            foreach (string recKey in recDict.Keys)
            {
                //Convert AbstractRecord arrays (links) to Dictionary arrays
                if (recDict[recKey] is AbstractRecord[])
                {
                    AbstractRecord[] recArray = (AbstractRecord[])recDict[recKey];
                    List<IDictionary<String, Object>> recList = new List<IDictionary<String,Object>>();
                    for (int i = 0; i < recArray.Length; i++)
                    {
                        recList.Add(ConvertToInternalDict(recArray[i]));
                    }
                    newRec[recKey] = recList.ToArray();
                }
                //Convert DmpDateTime to the standard C# Date/Time so it will end up as Date in JSON
                else if (recDict[recKey] is DmpDateTime)
                {
                    newRec[recKey] = ((DmpDateTime)recDict[recKey]).DateTime;
                }
            }

            return newRec;
        }

        /// <summary>
        /// initialize the Jacascript Processor.
        /// </summary>
        /// <param name="xmlDOM">XML Dom</param>
        public override void InitializeMetaData(string xmlDOM)
        {
            if (xmlDOM == null || String.IsNullOrEmpty(xmlDOM))
            {
                logger.Error("Error while initializing Javascript Processor: XMLDom containing the metadata is null or empty");
                throw new ApplicationException("Error while initializing Javascript Processor:XMLDom containing the metadata is null or empty");
            }

            try
            {
                GenerateMetaData(xmlDOM);
                Initialize();
            }
            catch (Exception e)
            {
                logger.Error("Error while initializing Javascript Processor", e);
                throw new ApplicationException("Error while initializing Javascript Processor: "+e.Message);
            }
        }

        /// <summary>
        /// calls the Javascript parser to parse the XML File and create the Javascript Metadata object
        /// </summary>
        /// <param name="xmlString">XML Configuration</param>
        public void GenerateMetaData(string xmlString)
        {
            try
            {
                JavascriptParser parser = new JavascriptParser(xmlString);
                this.metadata = parser.Parse();
            }
            catch (Exception e)
            {
                logger.Error("Error while generating metadata for Javascript Processor", e);
                throw new ApplicationException("Error while generating metadata for Javascript Processor: " + e.Message);

            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cache"></param>
        public override void SetImpactorCacheMap(Dictionary<string, DataSet> cache)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="impactors"></param>
        public override void SetImpactors(Dictionary<string, List<AbstractRecord>> impactors)
        {

        }

        public override void Dispose()
        {
            try
            {                
              //  v8.Dispose();
            }
            catch
            {
            }
        }
    }
}



