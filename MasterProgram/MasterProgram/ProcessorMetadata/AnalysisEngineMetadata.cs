using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DMP.MasterProgram.ProcessorMetadata
{
   

    public class AnalysisEngineMetadata
    {
        /// <summary>
        /// Class to store Analysis Engine Task object
        /// </summary>
        public class AETask
        {

            private string taskClass;
            private string subjectName;
            private string impactorName;

            /// <summary>
            /// Name of the Task Class
            /// </summary>
            public string TaskClass
            {
                get
                {
                    return this.taskClass;
                }
                set
                {
                    this.taskClass = value;
                }
            }

            /// <summary>
            /// Name of the Subject
            /// </summary>
            public string SubjectName
            {
                get
                {
                    return this.subjectName;
                }
                set
                {
                    this.subjectName = value;
                }
            }

            /// <summary>
            /// Name of th impactor
            /// </summary>
            public string ImpactorName
            {
                get
                {
                    return this.impactorName;
                }
                set
                {
                    this.impactorName = value;
                }
            }

        }

        /// <summary>
        /// Class to store Analysis Engine attribute Object
        /// </summary>
        public class AEAttribute
        {
           
            private string taskName;
            private bool isBaseAttribute;

            /// <summary>
            /// Name of the task to calculate this attribute
            /// </summary>
            public string TaskName
            {
                get
                {
                    return this.taskName;

                }
                set
                {
                    this.taskName = value;

                }
            }

            /// <summary>
            /// Is this attribute is baseAttribute.
            /// </summary>
            public bool IsBaseAttribute
            {
                get
                {
                    return this.isBaseAttribute;
                }
                set
                {
                    this.isBaseAttribute = value;
                }
            }

        }

        private Dictionary<String, AETask> taskMap;
        private Dictionary<String, AEAttribute> attributeMap;

        //BDE-41
        //private  bool isImpactorOnDemand;
        private  int processingTileSize;
        private  bool preferGPU;

        /// <summary>
        /// Constructor
        /// </summary>
        public AnalysisEngineMetadata()
        {
            this.taskMap = new Dictionary<string, AETask>();
            this.attributeMap = new Dictionary<string, AEAttribute>();
        }

        /// <summary>
        /// Getter,Setter for map containing Analysis Engine Task objects with key as task name and value as task object
        /// </summary>
        public Dictionary<String, AETask> TaskMap
        {
            get
            {
                return taskMap;
            }
        }

        /// <summary>
        /// Getter,Setter for map containing AEAttribute objects with key as attribute name and value as Attribute object
        /// </summary>
        public Dictionary<String, AEAttribute> AttributeMap
        {
            get
            {
                return attributeMap;
            }
        }

        /// <summary>
        /// true if impactor is on demand ,else false
        /// </summary>
        //BDE-41
        /*
        public bool IsImpactorOnDemand 
        {
            get
            {
                return this.isImpactorOnDemand;
            }
            set
            {
                this.isImpactorOnDemand = value;
            }
        }*/


        /// <summary>
        /// getter,setter for the size of th eprocessing tile
        /// </summary>
        public int ProcessingTileSize
        {
            get
            {
                return this.processingTileSize;
            }
            set
            {
                this.processingTileSize = value;
            }
        }

        /// <summary>
        /// to tell if Analysis Engine has to prefer GPU or CPU
        /// </summary>
        public bool PreferGPU
        {
            get
            {
                return this.preferGPU;
            }
            set
            {
                this.preferGPU = value;
            }
        }
    }
}
