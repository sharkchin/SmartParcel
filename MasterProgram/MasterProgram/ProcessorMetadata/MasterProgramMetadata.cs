using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DMP.MasterProgram.ProcessorMetadata
{

    public class MasterProgramMetadata
    {
        public class GeometryBuffer
        {
            private string bufferUnit;
            private double bufferValue;

            public string BufferUnit 
            { 
                get
                {
                    return this.bufferUnit;
                }
                set
                {
                    this.bufferUnit = value;
                }
            }

            public double BufferValue 
            { 
                get
                {
                    return this.bufferValue;
                }
                set
                {
                    this.bufferValue = value;
                }
            }
        }

        public class InputDataSet
        {
            private string database;
            private List<string> fields;
            private string attributeCriteria;
            private string tableName;
            private bool isBaseResource;
            private string storageType;
            private string inclusionWKT;
            private string exclusionWKT;
            private int minZoomLevel;
            private int maxZoomLevel;
            private Dictionary<string, GeometryBuffer> impactorOnDemandBuffer;
            //BDE-41 : Impactor On Demand Selective
            private bool isOnDemand = false;

            public bool IsOnDemand
            {
                get
                {
                    return this.isOnDemand;
                }
                set
                {
                    this.isOnDemand = value;
                }
            }
            
           

           
            public int MinZoomLevel 
            {
                get
                {
                    return this.minZoomLevel;

                }

                set
                {
                    this.minZoomLevel = value;
                }
            }

            public int MaxZoomLevel
            {
                get
                {
                    return this.maxZoomLevel;
                }

                set
                {
                    this.maxZoomLevel = value;
                }


            }
      
           
            public string InclusionWKT 
            {
                get
                {
                    return this.inclusionWKT;
                }
                set
                {
                    this.inclusionWKT = value;
                }
            }

           
            public string ExclusionWKT 
            {
                get
                {
                    return this.exclusionWKT;
                }
                set
                {
                    this.exclusionWKT = value;
                }
            }

            /// <summary>
            /// SQLite Data Base
            /// </summary>
            public string Database
            {
                get
                {
                    return this.database;

                }
                set
                {
                    this.database = value;
                }
            }


            /// <summary>
            /// List of Record attributes
            /// </summary>
            public List<string> Fields
            {
                get
                {
                    return this.fields;
                }
                set
                {
                    this.fields = value;
                }
            }

            /// <summary>
            /// filter
            /// </summary>
            public string AttributeCriteria
            {
                get
                {
                    return this.attributeCriteria;

                }
                set
                {
                    this.attributeCriteria = value;

                }
            }

            /// <summary>
            /// Table Name
            /// </summary>
            public string TableName 
            {
                get
                {
                    return this.tableName;

                }
                set
                {
                    this.tableName = value;
                }
            }

            /// <summary>
            /// 
            /// </summary>
            public bool IsBaseResource
            {
                get
                {
                    return this.isBaseResource;
                }
                set
                {
                    this.isBaseResource = value;
                }
            }

            public string StorageType
            {
                get
                {
                    return this.storageType;
                }

                set
                {
                    this.storageType = value;
                }
            }


             public Dictionary<string, GeometryBuffer> ImpactorOnDemandBuffers
             {
                 get
                 {
                     return this.impactorOnDemandBuffer;
                 }
                 set
                 {
                     this.impactorOnDemandBuffer = value;
                 }
             }

        }

       
        private Dictionary<string,string> processors;
        private Dictionary<string, InputDataSet> impactors;
        private InputDataSet subject;
        private int outputBatchSize = 5000;
         
        public MasterProgramMetadata()
        {
            this.processors = new Dictionary<string, string>();
            this.impactors = new Dictionary<string, InputDataSet>();

        }

        /// <summary>
        /// map of Processors with key as Processor Class Name and value as XMLDom related to it
        /// </summary>
        public Dictionary<string, string> Processors
        {
            get
            {
                return processors;

            }
        }

        /// <summary>
        /// map of Impactors key as Impactor Name , Value as InputDataSet object
        /// </summary>
        public Dictionary<string, InputDataSet> Impactors 
        {
            get
            {
                return this.impactors;
            }
        }



        /// <summary>
        /// InputDataSet of the subject
        /// </summary>
        public InputDataSet Subject
        {
            get
            {
                return this.subject;
            }
            set
            {
                this.subject = value;
            }
        }

        public int OutputBatchSize 
        {
            get
            {
                return this.outputBatchSize;
            }
            set
            {
                this.outputBatchSize = value;
            }
        }


    }
}
