using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DMP.MasterProgram.Processors;
using log4net;
using log4net.Config;
using Dmp.Neptune.Collections;
using DMP.MasterProgram.Utils;

namespace DMP.MasterProgram
{
    class RecordProcessorThread
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private List<IRecordProcessor> processorList;
        public AbstractRecord record;
        public AbstractRecord outputRecord;
        public bool result = true;


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="processorList">List of all Record Processor</param>
        /// <param name="record">Subject Record</param>
        public RecordProcessorThread(List<IRecordProcessor> processorList, AbstractRecord record)
        {
            this.processorList = processorList;
            this.record = record;
            this.outputRecord = record;
        }
       
        /// <summary>
        /// process record by going through a list of Processors and callinng each Processor
        /// </summary>
        public void ProcessRecord()
        {
            try
            {
                for (int i = 0; i < processorList.Count; i++)
                {
                    IRecordProcessor processor = processorList[i];
                    if (outputRecord != null)
                    {
                        outputRecord = processor.ProcessRecord(outputRecord);
                    }
                    else
                    {
                        return ;
                    }
                }
            }
            catch(Exception e)
            {
                logger.Error("RecordProcessorThread.ProcessRecord : Error while processing record", e);
                result = false;
                throw new ApplicationException("Error while processing record: "+e.Message);
            }

        }
    }
}
